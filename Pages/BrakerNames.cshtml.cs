using FX5u_Web_HMI_App.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FX5u_Web_HMI_App.Pages
{
    public class BrakerNamesModel : PageModel
    {
        private readonly ILogger<BrakerNamesModel> _logger;
        private readonly ISLMPService _slmpService;
        private readonly LogDbContext _db;

        #region Bound Properties
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
        #endregion

        private const int WORDS_PER_NAME = 10; // 10 words = 20 ASCII bytes
        private const string BASE_D = "D4000";  // page 1 base
        private static readonly Dictionary<string, string> _stringAddressMap = new();

        static BrakerNamesModel()
        {
            // BrakerName1..20 -> D4000..D4199 (10 words each)
            for (int i = 0; i < 20; i++)
                _stringAddressMap.Add($"BrakerName{i + 1}", $"D{4000 + (i * WORDS_PER_NAME)}");
        }

        public BrakerNamesModel(ILogger<BrakerNamesModel> logger, ISLMPService slmpService, LogDbContext db)
        {
            _logger = logger;
            _slmpService = slmpService;
            _db = db;
        }

        public async Task OnGet()
        {
            _slmpService.SetHeartbeatValue(20);
            await UpdateModelValuesAsync();
        }

        // ---------- English (PLC) ----------
        public class WriteStringRequest
        {
            public string Name { get; set; } = "";
            public string Value { get; set; } = "";
        }

        public async Task<JsonResult> OnGetReadRegisters()
        {
            await UpdateModelValuesAsync();

            var dict = GetType().GetProperties()
                .Where(p => p.IsDefined(typeof(BindPropertyAttribute), false))
                .ToDictionary(
                    p => char.ToLowerInvariant(p.Name[0]) + p.Name.Substring(1),
                    p => p.GetValue(this)
                );

            return new JsonResult(dict);
        }

        [ValidateAntiForgeryToken]
        public async Task<JsonResult> OnPostWriteString([FromBody] WriteStringRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.Name))
                return new JsonResult(new { status = "Error", message = "Request data is missing." });

            try
            {
                if (!_stringAddressMap.TryGetValue(request.Name, out var address))
                    throw new KeyNotFoundException($"Writable register '{request.Name}' not found.");

                var val = request.Value ?? string.Empty;
                if (val.Length > 20)
                    return new JsonResult(new { status = "Error", message = "Max 20 characters." });

                // 🔴 IMPORTANT: write full 10 words (20 bytes), zero-padded
                var ok = await WriteFixedAsciiAsync(address, val);
                if (!ok) throw new Exception("PLC write failed.");

                // keep your NameTranslations upsert if you added it
                await UpsertTranslationAsync(en: val, gu: null);

                return new JsonResult(new { status = "Success", message = $"Wrote '{val}' to {request.Name}." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to write string: {RegisterName}", request?.Name);
                return new JsonResult(new { status = "Error", message = ex.Message });
            }
        }


        // ---------- Gujarati (DB Ids 1..20) ----------
        public JsonResult OnGetGetGujarati()
        {
            var values = Enumerable.Range(1, 20)
                .Select(i => new
                {
                    Key = $"BrakerName{i}",
                    Val = _db.LocaleBreakerNames
                             .Where(x => x.Id == i && x.Lang == "gu")
                             .Select(x => x.Text)
                             .FirstOrDefault() ?? string.Empty
                })
                .ToDictionary(x => x.Key, x => x.Val);

            return new JsonResult(new { values });
        }

        public class SetGujaratiRequest
        {
            public string Name { get; set; } = "";
            public string Value { get; set; } = "";
        }

        [ValidateAntiForgeryToken]
        public async Task<JsonResult> OnPostSetGujarati([FromBody] SetGujaratiRequest req)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(req?.Name) || !req.Name.StartsWith("BrakerName"))
                    return new JsonResult(new { status = "Error", message = "Invalid name" });

                if ((req.Value ?? "").Length > 20)
                    return new JsonResult(new { status = "Error", message = "Max 20 characters" });

                int idx = int.Parse(req.Name.Replace("BrakerName", "")); // 1..20
                var row = _db.LocaleBreakerNames.SingleOrDefault(x => x.Id == idx && x.Lang == "gu");
                if (row == null)
                    _db.LocaleBreakerNames.Add(new LocaleBreakerName { Id = idx, Lang = "gu", Text = req.Value ?? string.Empty });
                else
                    row.Text = req.Value ?? string.Empty;

                await _db.SaveChangesAsync();

                // 🔁 Auto-maintain NameTranslations: look up current English from PLC, map En→Gu
                var read = await _slmpService.ReadInt16BlockAsync($"D{4000 + (idx - 1) * WORDS_PER_NAME}", WORDS_PER_NAME);
                if (read.IsSuccess)
                {
                    var en = DecodeAscii(read.Content);
                    await UpsertTranslationAsync(en: en, gu: req.Value ?? "");
                }

                return new JsonResult(new { status = "Success" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save Gujarati text for {Name}", req?.Name);
                return new JsonResult(new { status = "Error", message = ex.Message });
            }
        }

        // ---------- Helpers ----------
        private async Task UpdateModelValuesAsync()
        {
            try
            {
                var read = await _slmpService.ReadInt16BlockAsync(BASE_D, WORDS_PER_NAME * 20);
                if (!read.IsSuccess)
                    throw new Exception("Failed to read breaker names (D4000) from PLC: " + read.Message);

                var words = read.Content;

                for (int i = 0; i < 20; i++)
                {
                    var prop = GetType().GetProperty($"BrakerName{i + 1}");
                    if (prop is null) continue;

                    var slice = new ArraySegment<short>(words, i * WORDS_PER_NAME, WORDS_PER_NAME);
                    string value = DecodeAscii(slice);
                    prop.SetValue(this, value);
                }

                ConnectionStatus = "Connected";
                ErrorMessage = string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UpdateModelValuesAsync failed");
                ConnectionStatus = "Disconnected";
                ErrorMessage = ex.Message;
            }
        }

        private static string DecodeAscii(IReadOnlyList<short> words)
        {
            byte[] bytes = new byte[words.Count * 2];
            for (int j = 0; j < words.Count; j++)
            {
                ushort w = (ushort)words[j];
                bytes[j * 2 + 0] = (byte)(w & 0xFF);
                bytes[j * 2 + 1] = (byte)((w >> 8) & 0xFF);
            }
            return Encoding.ASCII.GetString(bytes).TrimEnd('\0', ' ');
        }

        private static string Normalize(string s) => (s ?? "").Trim();

        // Auto-maintain English→Gujarati dictionary
        private async Task UpsertTranslationAsync(string en, string? gu)
        {
            en = Normalize(en);
            gu = Normalize(gu ?? "");
            if (string.IsNullOrEmpty(en)) return;

            var row = await _db.NameTranslations.SingleOrDefaultAsync(t => t.En == en);
            if (row == null)
            {
                _db.NameTranslations.Add(new NameTranslation { En = en, Gu = gu ?? "" });
            }
            else
            {
                if (!string.IsNullOrEmpty(gu)) row.Gu = gu;
            }
            await _db.SaveChangesAsync();
        }
        private static short[] PackAsciiFixed(string value, int words = 10)
        {
            var bytes = new byte[words * 2]; // 20 bytes
            var src = System.Text.Encoding.ASCII.GetBytes(value ?? string.Empty);

            var len = Math.Min(src.Length, bytes.Length);
            Array.Copy(src, 0, bytes, 0, len); // rest stays 0x00

            // pack little-endian => low byte, then high byte per word
            var outWords = new short[words];
            for (int i = 0; i < words; i++)
            {
                int b0 = bytes[i * 2 + 0];
                int b1 = bytes[i * 2 + 1];
                outWords[i] = (short)((b1 << 8) | b0);
            }
            return outWords;
        }

        // Writes a full, zero-padded 10-word string block into D area
        private async Task<bool> WriteFixedAsciiAsync(string dAddress, string value)
        {
            // Clear the entire 10-word block (zero out old characters)
            var zeros = new short[10];
            var clear = await _slmpService.WriteStringAsync(dAddress, new string('\0', 20));
            if (!clear.IsSuccess) return false;

            // Now write the new string (up to 20 chars)
            var wr = await _slmpService.WriteStringAsync(dAddress, value ?? string.Empty);
            return wr.IsSuccess;
        }

    }
    public class WriteStringRequest
    {
        public string Name { get; set; } = "";
        public string Value { get; set; } = "";
    }

}
