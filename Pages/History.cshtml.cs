using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System;
using Microsoft.EntityFrameworkCore;
using FX5u_Web_HMI_App.Data;
using System.Text.Json;

namespace FX5u_Web_HMI_App.Pages
{
    public class HistoryModel : PageModel
    {
        private readonly ILogger<HistoryModel> _logger;
        private readonly LogDbContext _context;

        // Chart data
        public List<DataLog> Logs { get; set; }
        public string ChartDataLabelsLocal { get; set; }    // IST strings for the chart
        public string ChartDataValues_Torque { get; set; }
        public string ChartDataValues_Position { get; set; }
        public string ChartDataValues_RPM { get; set; }
        public string ChartDataValues_BrakerNo { get; set; }

        // Internal UTC window used for querying (not bound in the UI)
        public DateTime StartDateUtc { get; set; }
        public DateTime EndDateUtc { get; set; }

        // UI filter fields (always shown/edited in LOCAL time)
        [BindProperty(SupportsGet = true)] public DateTime DisplayStart { get; set; }
        [BindProperty(SupportsGet = true)] public DateTime DisplayEnd { get; set; }

        // Client offset (minutes to add Local => UTC)
        [BindProperty(SupportsGet = true)] public int ClientOffsetMinutes { get; set; }

        // Quick ranges
        [BindProperty(SupportsGet = true)] public string Range { get; set; }

        // Paging (kept if you re-add later)
        [BindProperty(SupportsGet = true)] public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 100;
        public int TotalPages { get; set; }
        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < TotalPages;

        public HistoryModel(ILogger<HistoryModel> logger, LogDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public async Task OnGetAsync()
        {
            // 1) Decide the UTC window and the LOCAL fields to render
            if (!string.IsNullOrEmpty(Range))
            {
                // Quick ranges: compute in UTC, render LOCAL
                EndDateUtc = DateTime.UtcNow;
                StartDateUtc = Range switch
                {
                    "1h" => EndDateUtc.AddHours(-1),
                    "24h" => EndDateUtc.AddHours(-24),
                    "7d" => EndDateUtc.AddDays(-7),
                    _ => EndDateUtc.AddHours(-24)
                };
                var ist = GetIndiaTimeZone();
                DisplayStart = TimeZoneInfo.ConvertTimeFromUtc(StartDateUtc, ist);
                DisplayEnd = TimeZoneInfo.ConvertTimeFromUtc(EndDateUtc, ist);
            }
            else if (DisplayStart == DateTime.MinValue || DisplayEnd == DateTime.MinValue)
            {
                // First load: default last 24h — render LOCAL, compute UTC for query
                var ist = GetIndiaTimeZone();
                var nowIst = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, ist);
                DisplayEnd = nowIst;
                DisplayStart = nowIst.AddHours(-24);

                // Convert local (IST) to UTC for querying
                StartDateUtc = DisplayStart.AddMinutes(ClientOffsetMinutes);
                EndDateUtc = DisplayEnd.AddMinutes(ClientOffsetMinutes);
                StartDateUtc = DateTime.SpecifyKind(StartDateUtc, DateTimeKind.Utc);
                EndDateUtc = DateTime.SpecifyKind(EndDateUtc, DateTimeKind.Utc);
            }
            else
            {
                // Manual filter submit: Display* are LOCAL; convert to UTC using offset
                StartDateUtc = DateTime.SpecifyKind(DisplayStart.AddMinutes(ClientOffsetMinutes), DateTimeKind.Utc);
                EndDateUtc = DateTime.SpecifyKind(DisplayEnd.AddMinutes(ClientOffsetMinutes), DateTimeKind.Utc);
            }

            // 2) Query with UTC window
            var query = _context.DataLogs
                .Where(log => log.Timestamp >= StartDateUtc && log.Timestamp <= EndDateUtc)
                .OrderBy(log => log.Timestamp);

            int totalCount = await query.CountAsync();
            TotalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)PageSize));
            if (CurrentPage < 1) CurrentPage = 1;
            if (CurrentPage > TotalPages) CurrentPage = TotalPages;

            var page = await query
                .Skip((CurrentPage - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            Logs = page;

            // 3) Build IST labels on the server (plain strings so Chart.js won't add GMT)
            var istTz = GetIndiaTimeZone();
            var labelsLocal = page.Select(log =>
            {
                var utc = DateTime.SpecifyKind(log.Timestamp, DateTimeKind.Utc);
                var ist = TimeZoneInfo.ConvertTimeFromUtc(utc, istTz);
                return ist.ToString("yyyy-MM-dd HH:mm:ss");
            }).ToList();

            ChartDataLabelsLocal = JsonSerializer.Serialize(labelsLocal);
            ChartDataValues_Torque = JsonSerializer.Serialize(page.Select(log => log.Torque));
            ChartDataValues_Position = JsonSerializer.Serialize(page.Select(log => log.Position));
            ChartDataValues_RPM = JsonSerializer.Serialize(page.Select(log => log.RPM));
            ChartDataValues_BrakerNo = JsonSerializer.Serialize(page.Select(log => log.BrakerNo));
        }

        private static TimeZoneInfo GetIndiaTimeZone()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata"); }
            catch { return TimeZoneInfo.FindSystemTimeZoneById("India Standard Time"); }
        }
    }
}
