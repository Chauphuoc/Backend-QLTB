using EquipManagementAPI.Data;
using EquipManagementAPI.Models.DTOs;
using EquipManagementAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EquipManagementAPI.Controllers
{
    // Controllers/QualityReportController.cs

    [ApiController]
    [Route("api/quality")]
    [Authorize]
    public class QualityReportController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IQMService _service;
        public QualityReportController(ApplicationDbContext context, IQMService service)
        {
            _context = context;
            _service = service;
        }

        /// <summary>
        /// API #3 — Kiểm – Lỗi – Thoát chuyền theo Tổ
        /// GET /api/quality/team-qc-stats?date=2026-03-24&factoryId=0101
        /// GET /api/quality/team-qc-stats?from=2026-03-01&to=2026-03-24
        /// </summary>
        [HttpGet("team-qc-stats")]
        public async Task<IActionResult> GetTeamQCStats([FromQuery] TeamQCStatsRequest req)
        {
            // Resolve date range
            DateOnly dateFrom, dateTo;

            if (!string.IsNullOrEmpty(req.Date))
            {
                dateFrom = dateTo = DateOnly.Parse(req.Date);
            }
            else if (!string.IsNullOrEmpty(req.From) && !string.IsNullOrEmpty(req.To))
            {
                dateFrom = DateOnly.Parse(req.From);
                dateTo = DateOnly.Parse(req.To);
            }
            else
            {
                dateFrom = dateTo = DateOnly.FromDateTime(DateTime.Today);
            }

            var result = await _service.GetTeamQCStatsAsync(dateFrom, dateTo, req.FactoryId, req.OperationType);
            return Ok(result);
        }

        [HttpGet("defect-summary")]
        public async Task<ActionResult<IEnumerable<DefectSummaryItemDto>>> GetDefectSummary(
        [FromQuery] string? date = null,
        [FromQuery] string? from = null,
        [FromQuery] string? to = null,
        [FromQuery] string? factoryId = null,
        [FromQuery] string? teamId = null,
        [FromQuery] string? operationType = null)
        {
            var (dateFrom, dateTo) = ResolveRange(date, from, to);
            var result = await _service.GetDefectSummaryAsync(dateFrom, dateTo, factoryId, teamId, operationType);
            return Ok(result);
        }

        // shared helper — move to base controller if needed
        private static (DateTime dateFrom, DateTime dateTo) ResolveRange(
            string? date, string? from, string? to)
        {
            if (!string.IsNullOrEmpty(date))
            {
                var d = DateTime.Parse(date);
                return (d, d);
            }
            return (DateTime.Parse(from!), DateTime.Parse(to!));
        }
        // GET /api/quality/team-hourly-qc?teamId=&date=&from=&to=&factoryId=
        [HttpGet("team-hourly-qc")]
        public async Task<ActionResult<IEnumerable<TeamHourlyQCItemDto>>> GetTeamHourlyQC(
            [FromQuery] string teamId,
            [FromQuery] string? date = null,
            [FromQuery] string? from = null,
            [FromQuery] string? to = null,
            [FromQuery] string? factoryId = null,
            [FromQuery] string? operationType = null)
        {
            if (string.IsNullOrEmpty(teamId))
                return BadRequest("teamId is required");

            var (dateFrom, dateTo) = ResolveRange(date, from, to);
            var result = await _service.GetTeamHourlyQCAsync(teamId, dateFrom, dateTo, factoryId, operationType);
            return Ok(result);
        }
        // GET /api/quality/team-defect-breakdown?teamId=&date=&from=&to=&factoryId=
        [HttpGet("team-defect-breakdown")]
        public async Task<ActionResult<IEnumerable<TeamDefectBreakdownItemDto>>> GetTeamDefectBreakdown(
            [FromQuery] string teamId,
            [FromQuery] string? date = null,
            [FromQuery] string? from = null,
            [FromQuery] string? to = null,
            [FromQuery] string? factoryId = null,
            [FromQuery] string? operationType = null)
        {
            if (string.IsNullOrEmpty(teamId))
                return BadRequest("teamId is required");

            var (dateFrom, dateTo) = ResolveRange(date, from, to);
            var result = await _service.GetTeamDefectBreakdownAsync(teamId, dateFrom, dateTo, factoryId, operationType);
            return Ok(result);
        }

        /// <summary>
        /// GET /api/quality/hourly-defect-trend
        /// Diễn biến kiểm/lỗi theo từng khung giờ — toàn nhà máy hoặc lọc theo tổ.
        /// Query: date | (from + to), factoryId?, teamId?, operationType?
        /// </summary>
        [HttpGet("hourly-defect-trend")]
        public async Task<ActionResult<IEnumerable<HourlyDefectTrendItemDto>>> GetHourlyDefectTrend(
            [FromQuery] string? date = null,
            [FromQuery] string? from = null,
            [FromQuery] string? to = null,
            [FromQuery] string? factoryId = null,
            [FromQuery] string? teamId = null,
            [FromQuery] string? operationType = null)
        {
            var (dateFrom, dateTo) = ResolveRange(date, from, to);
            var result = await _service.GetHourlyDefectTrendAsync(dateFrom, dateTo, factoryId, teamId, operationType);
            return Ok(result);
        }
    }
}
