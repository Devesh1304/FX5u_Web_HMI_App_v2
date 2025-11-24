using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FX5u_Web_HMI_App.Data;              // LogDbContext, NameTranslation, LocaleBreakerNames
using Microsoft.EntityFrameworkCore;

namespace FX5u_Web_HMI_App.Pages
{
    public class BrakerSelect2Model : PageModel
    {
        private readonly ILogger<BrakerSelect2Model> _logger;
        private readonly ISLMPService _slmpService;
        private readonly LogDbContext _db;

        public BrakerSelect2Model(ILogger<BrakerSelect2Model> logger, ISLMPService slmpService, LogDbContext db)
        {
            _logger = logger;
            _slmpService = slmpService;
            _db = db;
        }

        #region Bound props
        [BindProperty] public string ConnectionStatus { get; set; } = "Disconnected";
        [BindProperty] public string ErrorMessage { get; set; } = string.Empty;

        [BindProperty] public string BrakerName1 { get; set; }
        [BindProperty] public string BrakerName2 { get; set; }
        [BindProperty] public string BrakerName3 { get; set; }
        [BindProperty] public string BrakerName4 { get; set; }
        [BindProperty] public string BrakerName5 { get; set; }
        [BindProperty] public string BrakerName6 { get; set; }
        [BindProperty] public string BrakerName7 { get; set; }
        [BindProperty] public string BrakerName8 { get; set; }
        [BindProperty] public string BrakerName9 { get; set; }
        [BindProperty] public string BrakerName10 { get; set; }
        [BindProperty] public string BrakerName11 { get; set; }
        [BindProperty] public string BrakerName12 { get; set; }
        [BindProperty] public string BrakerName13 { get; set; }
        [BindProperty] public string BrakerName14 { get; set; }
        [BindProperty] public string BrakerName15 { get; set; }
        [BindProperty] public string BrakerName16 { get; set; }
        [BindProperty] public string BrakerName17 { get; set; }
        [BindProperty] public string BrakerName18 { get; set; }
        [BindProperty] public string BrakerName19 { get; set; }
        [BindProperty] public string BrakerName20 { get; set; }

        [BindProperty] public int BrakerSelect1 { get; set; }
        [BindProperty] public int BrakerSelect2 { get; set; }
        [BindProperty] public int D1001 { get; set; }
        [BindProperty] public int ButtonStatusBits1_10 { get; set; }
        [BindProperty] public int ButtonStatusBits11_20 { get; set; }
        #endregion

        private static readonly Dictionary<string, string> _writableInt16Map = new()
        {
            { nameof(BrakerSelect1), "D4403" },
            { nameof(BrakerSelect2), "D4404" }
        };

        public async Task OnGet()
        {
            _slmpService.SetHeartbeatValue(23);
            await UpdateModelValuesAsync();
        }

        public async Task<JsonResult> OnGetReadRegisters()
        {
            await UpdateModelValuesAsync();
            var props = GetType().GetProperties()
                .Where(p => p.IsDefined(typeof(BindPropertyAttribute), false))
                .ToDictionary(p => char.ToLowerInvariant(p.Name[0]) + p.Name.Substring(1), p => p.GetValue(this));
            return new JsonResult(props);
        }

        // ===== English grid from PLC (D4200..D4390) =====
        public async Task<JsonResult> OnGetReadBreakerGrid()
        {
            try
            {
                var names = await Read20NamesEnAsync();
                return new JsonResult(new { success = true, names });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ReadBreakerGrid (page2) failed");
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }

        // ===== Gujarati grid via NameTranslations; fallback to LocaleBreakerNames (Id 21..40) =====
        public async Task<JsonResult> OnGetReadBreakerGridLocalized(string lang = "gu")
        {
            try
            {
                var en = await Read20NamesEnAsync();

                if (!string.Equals(lang, "gu", StringComparison.OrdinalIgnoreCase))
                    return new JsonResult(new { success = true, names = en });

                // Seed any missing EN keys into NameTranslations
                var toSeed = en.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
                if (toSeed.Count > 0)
                {
                    var existing = await _db.NameTranslations
                        .Where(t => toSeed.Contains(t.En))
                        .Select(t => t.En).ToListAsync();

                    var newOnes = toSeed.Except(existing);
                    foreach (var key in newOnes)
                        _db.NameTranslations.Add(new NameTranslation { En = key, Gu = "" });
                    if (newOnes.Any()) await _db.SaveChangesAsync();
                }

                // Map EN->GU
                var maps = await _db.NameTranslations
                                    .Where(t => toSeed.Contains(t.En))
                                    .ToListAsync();
                var dict = maps.ToDictionary(t => t.En, t => t.Gu ?? string.Empty, StringComparer.Ordinal);
                var outNames = en.Select(s => dict.TryGetValue(s ?? string.Empty, out var gu) ? gu : string.Empty).ToArray();

                // Positional fallback to LocaleBreakerNames (21..40 for this page)
                if (outNames.Any(x => string.IsNullOrWhiteSpace(x)))
                {
                    var guFallback = await _db.LocaleBreakerNames
                        .Where(x => x.Lang == "gu" && x.Id >= 21 && x.Id <= 40)
                        .OrderBy(x => x.Id)
                        .Select(x => x.Text)
                        .ToListAsync();

                    while (guFallback.Count < 20) guFallback.Add(string.Empty);

                    for (int i = 0; i < 20; i++)
                        if (string.IsNullOrWhiteSpace(outNames[i]) && !string.IsNullOrWhiteSpace(guFallback[i]))
                            outNames[i] = guFallback[i];
                }

                // Final fallback to English
                for (int i = 0; i < 20; i++)
                    if (string.IsNullOrWhiteSpace(outNames[i])) outNames[i] = en[i] ?? string.Empty;

                return new JsonResult(new { success = true, names = outNames });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ReadBreakerGridLocalized (page2) failed");
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }

        // ===== Selection/write logic (bits 2/3 in D1001) =====
        public async Task<JsonResult> OnPostSelectBreaker([FromBody] BreakerSelectionRequest request)
        {
            if (request.BreakerNumber < 1 || request.BreakerNumber > 20)
                return new JsonResult(new { status = "Error", message = "Invalid breaker number." });

            try
            {
                var d1001 = await _slmpService.ReadInt16Async("D1001");
                if (!d1001.IsSuccess) throw new Exception("Failed to read D1001.");
                short d1001v = d1001.Content;

                if (request.BreakerNumber <= 10)
                {
                    d1001v ^= (1 << 2);
                    var wrSel = await _slmpService.WriteAsync("D4403", (short)request.BreakerNumber);
                    if (!wrSel.IsSuccess) throw new Exception(wrSel.Message);
                }
                else
                {
                    d1001v ^= (1 << 3);
                    var wrSel = await _slmpService.WriteAsync("D4404", (short)(request.BreakerNumber - 10));
                    if (!wrSel.IsSuccess) throw new Exception(wrSel.Message);
                }

                var wr = await _slmpService.WriteAsync("D1001", d1001v);
                if (!wr.IsSuccess) throw new Exception(wr.Message);

                return new JsonResult(new { status = "Success", message = $"Selected Breaker {request.BreakerNumber} and toggled bit." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SelectBreaker (page2) failed for {BreakerNumber}", request.BreakerNumber);
                return new JsonResult(new { status = "Error", message = ex.Message });
            }
        }

        public async Task<JsonResult> OnPostWriteRegister([FromBody] WriteRequest request)
        {
            if (string.IsNullOrEmpty(request?.RegisterName) || string.IsNullOrEmpty(request.Value))
                return new JsonResult(new { status = "Error", message = "Request data is missing." });

            try
            {
                if (_writableInt16Map.TryGetValue(request.RegisterName, out var addr))
                {
                    if (!short.TryParse(request.Value, out var val16))
                        throw new FormatException("Value is not a valid 16-bit integer.");
                    var res = await _slmpService.WriteAsync(addr, val16);
                    if (!res.IsSuccess) throw new Exception(res.Message);
                    return new JsonResult(new { status = "Success", message = $"Wrote {request.Value} to {request.RegisterName}." });
                }
                throw new KeyNotFoundException($"Writable register '{request.RegisterName}' not found.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WriteRegister (page2) failed: {RegisterName}", request?.RegisterName);
                return new JsonResult(new { status = "Error", message = ex.Message });
            }
        }

        private async Task UpdateModelValuesAsync()
        {
            try
            {
                // Names + selections + status bits for buttons
                var namesTask = _slmpService.ReadInt16BlockAsync("D4200", 200);
                var sel1Task = _slmpService.ReadInt16Async("D4403");
                var sel2Task = _slmpService.ReadInt16Async("D4404");
                var bitsTask = _slmpService.ReadBoolAsync("M221", 20);
                var d1001Task = _slmpService.ReadInt16Async("D1001");

                await Task.WhenAll(namesTask, sel1Task, sel2Task, bitsTask, d1001Task);

                var namesRes = namesTask.Result;
                var s1 = sel1Task.Result;
                var s2 = sel2Task.Result;
                var bitsRes = bitsTask.Result;
                var d1001Res = d1001Task.Result;

                if (!namesRes.IsSuccess || !bitsRes.IsSuccess || !d1001Res.IsSuccess)
                    throw new Exception("Failed to read data from PLC.");

                BrakerSelect1 = s1.IsSuccess ? s1.Content : 0;
                BrakerSelect2 = s2.IsSuccess ? s2.Content : 0;
                D1001 = d1001Res.Content;

                // Parse 20 strings (10 words each)
                var data = namesRes.Content;
                for (int i = 0; i < 20; i++)
                {
                    byte[] bytes = new byte[20];
                    for (int w = 0; w < 10; w++)
                    {
                        var wb = BitConverter.GetBytes(data[i * 10 + w]);
                        bytes[w * 2] = wb[0];
                        bytes[w * 2 + 1] = wb[1];
                    }
                    string value = Encoding.ASCII.GetString(bytes).TrimEnd('\0');
                    GetType().GetProperty($"BrakerName{i + 1}")?.SetValue(this, value);
                }

                // Pack bits for button colors
                var bits = bitsRes.Content;
                ButtonStatusBits1_10 = 0;
                ButtonStatusBits11_20 = 0;
                for (int i = 0; i < 20; i++)
                {
                    if (bits[i])
                    {
                        if (i < 10) ButtonStatusBits1_10 |= (1 << i);
                        else ButtonStatusBits11_20 |= (1 << (i - 10));
                    }
                }

                ConnectionStatus = "Connected";
                ErrorMessage = string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UpdateModelValuesAsync (page2) failed.");
                ConnectionStatus = "Error";
                ErrorMessage = ex.Message;
            }
        }

        // Helper: read 20 EN names from D4200..D4390
        private async Task<string[]> Read20NamesEnAsync()
        {
            var block = await _slmpService.ReadInt16BlockAsync("D4200", 200);
            if (!block.IsSuccess) throw new Exception(block.Message);

            var names = new string[20];
            for (int i = 0; i < 20; i++)
            {
                byte[] bytes = new byte[20];
                for (int w = 0; w < 10; w++)
                {
                    var wb = BitConverter.GetBytes(block.Content[i * 10 + w]);
                    bytes[w * 2] = wb[0];
                    bytes[w * 2 + 1] = wb[1];
                }
                names[i] = Encoding.ASCII.GetString(bytes).TrimEnd('\0');
            }
            return names;
        }
    }



}
