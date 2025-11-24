using FX5u_Web_HMI_App.Hubs;
using FX5u_Web_HMI_App.Pages;
using FX5u_Web_HMI_App.Data; // 1. Ensure this 'using' statement is present
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic; // 2. Add this for using List
using System.Linq; // 3. Add this for using .Any()
using System.Threading;
using System.Threading.Tasks;

namespace FX5u_Web_HMI_App.BackgroundServices
{
    public class PlcMonitorService : BackgroundService
    {
        private readonly ILogger<PlcMonitorService> _logger;
        private readonly ISLMPService _slmpService;
        private readonly IHubContext<PlcHub> _hubContext;
        private readonly IServiceProvider _serviceProvider;
        // State variables to track the last known values
        private int _lastNavigationValue = 0;
        private int _lastAlarmCode = 0;
       // private DateTime _lastLogTime = DateTime.MinValue;

        // --- THIS IS THE CORRECTED CONSTRUCTOR ---
        public PlcMonitorService(ILogger<PlcMonitorService> logger,
                                 ISLMPService slmpService,
                                 IHubContext<PlcHub> hubContext,
                                 IServiceProvider serviceProvider) // 1. Add the service here
        {
            _logger = logger;
            _slmpService = slmpService;
            _hubContext = hubContext;
            _serviceProvider = serviceProvider; // 2. Assign the service here
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("PLC Monitor Service is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // --- 1. HANDLE PAGE NAVIGATION LOGIC ---
                    var navResult = await _slmpService.ReadInt16Async("D1"); // Read navigation register
                    if (navResult.IsSuccess)
                    {
                        int currentNavValue = navResult.Content;
                        if (currentNavValue != _lastNavigationValue)
                        {
                            _lastNavigationValue = currentNavValue; // Update state
                            // Check if the value matches the condition to navigate
                            if (currentNavValue == 11)
                            {
                                _logger.LogInformation("Navigation trigger detected! Sending command.");
                                await _hubContext.Clients.All.SendAsync("NavigateToPage", "/PowerMainScreen");
                            }
                        }
                    }

                    // --- 2. HANDLE ALARM POP-UP LOGIC ---
                    var alarmResult = await _slmpService.ReadInt16Async("D102"); // Read alarm register
                    if (alarmResult.IsSuccess)
                    {
                        int currentAlarmCode = alarmResult.Content;
                        if (currentAlarmCode != _lastAlarmCode)
                        {
                            _lastAlarmCode = currentAlarmCode; // Update state
                            _logger.LogInformation($"PLC Alarm code changed to {currentAlarmCode}. Sending update.");
                            // Send the new code to all browsers. The JS will handle the rest.
                            await _hubContext.Clients.All.SendAsync("UpdateAlarmState", currentAlarmCode);
                        }
                    }
                    var logTriggerResult = await _slmpService.ReadBoolAsync("M160", 1);

                    // We must check if the read succeeded AND if the *first item*
                    // in the returned list is 'true'.
                    if (logTriggerResult.IsSuccess && logTriggerResult.Content != null && logTriggerResult.Content.FirstOrDefault() == true)
                    {
                        _logger.LogInformation("M160 data log trigger detected. Logging data...");

                        // Call your existing function
                        await LogPlcDataAsync();

                        _logger.LogInformation("Resetting M160 trigger bit.");

                        // Reset the bit. We must pass an *array* of bools to write.
                        var resetResult = await _slmpService.WriteBoolAsync("M160", false);

                        if (!resetResult.IsSuccess)
                        {
                            _logger.LogWarning("Failed to reset M160 trigger bit. It may re-trigger on the next cycle.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred in PLC Monitor Service.");
                }

                // Wait before the next check
                await Task.Delay(500, stoppingToken);
            }
        }

        private async Task LogPlcDataAsync()
        {
            try
            {
                // We must create a "scope" to get a DbContext in a background service
                using (var scope = _serviceProvider.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<LogDbContext>();

                    // 1. Read all required 32-bit values in parallel
                    var torqueTask = _slmpService.ReadInt32Async("D1982");
                    var positionTask = _slmpService.ReadInt32Async("D1984");
                    var rpmTask = _slmpService.ReadInt32Async("D1980");
                    var brakerNoTask = _slmpService.ReadInt32Async("D1988");
                    var nameTask = _slmpService.ReadStringAsync("D4450", 20);

                    await Task.WhenAll(torqueTask, positionTask, rpmTask, brakerNoTask, nameTask);

                    // 3. Get all results

                    var torqueResult = await torqueTask;
                    var positionResult = await positionTask;
                    var rpmResult = await rpmTask;
                    var brakerNoResult = await brakerNoTask;
                    var nameResult = await nameTask;
                    // 2. Check if all essential reads were successful
                    if (!torqueResult.IsSuccess || !positionResult.IsSuccess || !rpmResult.IsSuccess || !brakerNoResult.IsSuccess)
                    {
                        _logger.LogWarning("Failed to read one or more values for logging.");
                        return;
                    }

                    // 3. Get the Breaker Number and find its name
                    string brakerName = "---"; // Default name
                    if (nameResult.IsSuccess)
                    {
                        brakerName = nameResult.Content.TrimEnd('\0');
                    }
                    else
                    {
                        _logger.LogWarning("Failed to read Breaker Description from D4450.");
                    }

                    // 4. Create the single log entry with all data
                    var newLog = new DataLog
                    {
                        Timestamp = DateTime.UtcNow,
                        Torque = torqueResult.IsSuccess ? torqueResult.Content : 0,
                        Position = positionResult.IsSuccess ? positionResult.Content  : 0, // Assuming decimal format
                        RPM = rpmResult.IsSuccess ? rpmResult.Content : 0,
                        BrakerNo = brakerNoResult.IsSuccess ? brakerNoResult.Content : 0,
                        BreakerDescription = brakerName // Use the string from D4450
                    };

                    // 5. Add and save the new row
                    await dbContext.DataLogs.AddAsync(newLog);
                    await dbContext.SaveChangesAsync();
                    _logger.LogInformation("Successfully logged data snapshot to database.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log PLC data to database.");
            }
        }
    }
 }
