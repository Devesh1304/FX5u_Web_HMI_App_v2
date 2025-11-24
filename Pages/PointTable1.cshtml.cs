using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FX5u_Web_HMI_App.Pages
{
    public class PointTable1Model : PageModel
    {
        private readonly ILogger<PointTable1Model> _logger;
        private readonly ISLMPService _slmpService;

        #region Properties (All 42 data points)
        [BindProperty] public string ConnectionStatus { get; set; } = "Disconnected";
        [BindProperty] public string ErrorMessage { get; set; } = string.Empty;

        // Writable Properties
        [BindProperty] public int PositionWrite1 { get; set; }
        [BindProperty] public int PositionWrite2 { get; set; }
        [BindProperty] public int PositionWrite3 { get; set; }
        [BindProperty] public int SpeedWrite1 { get; set; }
        [BindProperty] public int SpeedWrite2 { get; set; }
        [BindProperty] public int SpeedWrite3 { get; set; }
        [BindProperty] public int AccWrite1 { get; set; }
        [BindProperty] public int AccWrite2 { get; set; }
        [BindProperty] public int AccWrite3 { get; set; }
        [BindProperty] public int DeccWrite1 { get; set; }
        [BindProperty] public int DeccWrite2 { get; set; }
        [BindProperty] public int DeccWrite3 { get; set; }
        [BindProperty] public int DwellTimeWrite1 { get; set; }
        [BindProperty] public int DwellTimeWrite2 { get; set; }
        [BindProperty] public int DwellTimeWrite3 { get; set; }
        [BindProperty] public int AuxWrite1 { get; set; }
        [BindProperty] public int AuxWrite2 { get; set; }
        [BindProperty] public int AuxWrite3 { get; set; }
        [BindProperty] public int MWrite1 { get; set; }
        [BindProperty] public int MWrite2 { get; set; }
        [BindProperty] public int MWrite3 { get; set; }

        // Read-Only Properties
        [BindProperty] public int PositionRead1 { get; set; }
        [BindProperty] public int PositionRead2 { get; set; }
        [BindProperty] public int PositionRead3 { get; set; }
        [BindProperty] public int SpeedRead1 { get; set; }
        [BindProperty] public int SpeedRead2 { get; set; }
        [BindProperty] public int SpeedRead3 { get; set; }
        [BindProperty] public int AccRead1 { get; set; }
        [BindProperty] public int AccRead2 { get; set; }
        [BindProperty] public int AccRead3 { get; set; }
        [BindProperty] public int DeccRead1 { get; set; }
        [BindProperty] public int DeccRead2 { get; set; }
        [BindProperty] public int DeccRead3 { get; set; }
        [BindProperty] public int DwellTimeRead1 { get; set; }
        [BindProperty] public int DwellTimeRead2 { get; set; }
        [BindProperty] public int DwellTimeRead3 { get; set; }
        [BindProperty] public int AuxRead1 { get; set; }
        [BindProperty] public int AuxRead2 { get; set; }
        [BindProperty] public int AuxRead3 { get; set; }
        [BindProperty] public int MRead1 { get; set; }
        [BindProperty] public int MRead2 { get; set; }
        [BindProperty] public int MRead3 { get; set; }
        [BindProperty] public int MaxForwardTorque { get; set; }
        [BindProperty] public int MaxReverseTorque { get; set; }
        [BindProperty] public int PointControlRegister { get; set; }
        [BindProperty] public int SetTolerance { get; set; }


        #endregion

        // --- Address Maps for Writing (Change these addresses to match your PLC) ---
        private static readonly Dictionary<string, string> _writableInt32Map = new()
        {
            { nameof(PositionWrite1), "D3701" },
            { nameof(PositionWrite2), "D3719" },
            { nameof(PositionWrite3), "D3737" },
        };
        private static readonly Dictionary<string, string> _writableInt16Map = new()
        {
            { nameof(SpeedWrite1), "D3703" }, { nameof(AccWrite1), "D3704" }, { nameof(DeccWrite1), "D3705" }, { nameof(DwellTimeWrite1), "D3706" }, { nameof(AuxWrite1), "D3707" }, { nameof(MWrite1), "D3708" },
            { nameof(SpeedWrite2), "D3721" }, { nameof(AccWrite2), "D3722" }, { nameof(DeccWrite2), "D3723" }, { nameof(DwellTimeWrite2), "D3724" }, { nameof(AuxWrite2), "D3725" }, { nameof(MWrite2), "D3726" },
            { nameof(SpeedWrite3), "D3739" }, { nameof(AccWrite3), "D3740" }, { nameof(DeccWrite3), "D3741" }, { nameof(DwellTimeWrite3), "D3742" }, { nameof(AuxWrite3), "D3743" }, { nameof(MWrite3), "D3744" },
            { nameof(MaxForwardTorque), "D3994" },
            { nameof(MaxReverseTorque), "D3998" },
            { nameof(PointControlRegister), "D90" },
            { nameof(SetTolerance), "D3990" }
            

        };

        private static readonly Dictionary<string, string> _readOnlyInt32Map = new()
        {
            { nameof(PositionRead1), "D1801" },
            { nameof(PositionRead2), "D1810" },
            { nameof(PositionRead3), "D1819" },
        };
        private static readonly Dictionary<string, string> _readOnlyInt16Map = new()
        {
            { nameof(SpeedRead1), "D1803" }, { nameof(AccRead1), "D1804" }, { nameof(DeccRead1), "D1805" }, { nameof(DwellTimeRead1), "D1806" }, { nameof(AuxRead1), "D1807" }, { nameof(MRead1), "D1808" },
            { nameof(SpeedRead2), "D1812" }, { nameof(AccRead2), "D1813" }, { nameof(DeccRead2), "D1814" }, { nameof(DwellTimeRead2), "D1815" }, { nameof(AuxRead2), "D1816" }, { nameof(MRead2), "D1817" },
            { nameof(SpeedRead3), "D1821" }, { nameof(AccRead3), "D1822" }, { nameof(DeccRead3), "D1823" }, { nameof(DwellTimeRead3), "D1824" }, { nameof(AuxRead3), "D1825" }, { nameof(MRead3), "D1826" },

        };

        public PointTable1Model(ILogger<PointTable1Model> logger, ISLMPService slmpService)
        {
            _logger = logger;
            _slmpService = slmpService;
        }

        public async Task OnGet()
        {
            _slmpService.SetHeartbeatValue(1);
            await UpdateModelValuesAsync();
        }

        public async Task<JsonResult> OnGetReadRegisters()
        {
            await UpdateModelValuesAsync();
            var properties = this.GetType().GetProperties()
                .Where(p => p.IsDefined(typeof(BindPropertyAttribute), false))
                .ToDictionary(p => char.ToLowerInvariant(p.Name[0]) + p.Name.Substring(1), p => p.GetValue(this));
            return new JsonResult(properties);
        }
        public async Task<JsonResult> OnPostWriteRegister([FromBody] WriteRequest request)
        {
            if (string.IsNullOrEmpty(request.RegisterName) || string.IsNullOrEmpty(request.Value))
                return new JsonResult(new { status = "Error", message = "Request data is missing." });
            try
            {
                if (_writableInt32Map.TryGetValue(request.RegisterName, out var address32))
                {
                    if (int.TryParse(request.Value, out int intValue32))
                    {
                        var result = await _slmpService.WriteInt32Async(address32, intValue32);
                        if (!result.IsSuccess) throw new Exception(result.Message);
                    }
                    else throw new FormatException("Value is not a valid 32-bit integer.");
                }
                else if (_writableInt16Map.TryGetValue(request.RegisterName, out var address16))
                {
                    if (short.TryParse(request.Value, out short intValue16))
                    {
                        var result = await _slmpService.WriteAsync(address16, intValue16);
                        if (!result.IsSuccess) throw new Exception(result.Message);
                    }
                    else throw new FormatException("Value is not a valid 16-bit integer.");
                }
                else
                {
                    throw new KeyNotFoundException($"Writable register '{request.RegisterName}' not found.");
                }
                return new JsonResult(new { status = "Success", message = $"Wrote {request.Value} to {request.RegisterName}." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to write to register: {RegisterName}", request.RegisterName);
                return new JsonResult(new { status = "Error", message = ex.Message });
            }
        }

        // --- THIS METHOD HAS BEEN SIMPLIFIED ---
        private async Task UpdateModelValuesAsync()
        {
            try
            {
                // Read all 32-bit writable values
                foreach (var entry in _writableInt32Map)
                {
                    this.GetType().GetProperty(entry.Key)?.SetValue(this, (await _slmpService.ReadInt32Async(entry.Value)).Content);
                }
                // Read all 16-bit writable values
                foreach (var entry in _writableInt16Map)
                {
                    this.GetType().GetProperty(entry.Key)?.SetValue(this, (int)(await _slmpService.ReadInt16Async(entry.Value)).Content);
                }

                // Read all 32-bit read-only values
                foreach (var entry in _readOnlyInt32Map)
                {
                    this.GetType().GetProperty(entry.Key)?.SetValue(this, (await _slmpService.ReadInt32Async(entry.Value)).Content);
                }
                // Read all 16-bit read-only values
                foreach (var entry in _readOnlyInt16Map)
                {
                    this.GetType().GetProperty(entry.Key)?.SetValue(this, (int)(await _slmpService.ReadInt16Async(entry.Value)).Content);
                }
               
                ConnectionStatus = "Connected";
                ErrorMessage = string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred during SLMP update.");
                ConnectionStatus = "Error";
                ErrorMessage = ex.Message;
            }
        }
    

    }
}