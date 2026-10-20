using EquipManagementAPI.Data;
using EquipManagementAPI.Exceptions;
using EquipManagementAPI.Helpers;
using EquipManagementAPI.Models;
using EquipManagementAPI.Models.DTOs;
using EquipManagementAPI.Services;
using FirebaseAdmin.Messaging;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace EquipManagementAPI.Controllers
{
    [Route("api/production")]
    [ApiController]
    

    public class ProductionAPI : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IQMService  _service;
        private readonly IProductionPlanService _productionPlanService;
        public ProductionAPI(ApplicationDbContext context, IQMService service,IProductionPlanService productionPlanService)
        {
            _context = context;
            _service = service;
            _productionPlanService = productionPlanService;
        }
        //API 1 — Kế hoạch & thực tế thoát chuyền theo tổ
        [HttpGet("team-plan-summary")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<TeamPlanSummaryDTO>> ThongtinKehoachVaThoatChuyen([FromQuery] DateOnly? date, [FromQuery] DateOnly? from, [FromQuery]  DateOnly? to, [FromQuery] string factoryId)
        {
            try
            {
                
                // Validate: phải có date, hoặc có cả from & to
                if (date == null && (from == null || to == null))
                return BadRequest(new
                {
                    success = false,
                    message = "Phải truyền 'date' hoặc cả 'from' và 'to'.",
                });
                DateOnly dateFrom = date ?? from!.Value;
                DateOnly dateTo = date ?? to!.Value;
                var result = await _service.BaoCaoSLKeHoachVaThoatChuyen(date,from,to,factoryId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Có lỗi xảy ra",
                    detail = ex.Message
                });
            }

        }
        //Báo cáo doanh thu
        [HttpGet("revenue-by-month")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
       
        public async Task<ActionResult<IEnumerable<TeamDailyOutputDto>>> BaoCaoDoanhThu(
    [FromQuery] int month,
    [FromQuery] int year,
    [FromQuery] string? factoryId = null,
    [FromQuery] string? teamId = null)
        {
            if (month < 1 || month > 12 || year < 2000)
                return BadRequest("month (1-12) hoặc year không hợp lệ.");

            var daysInMonth = DateTime.DaysInMonth(year, month);
            var startDate = new DateTime(year, month, 1);
            var endDate = new DateTime(year, month, daysInMonth);
            var monthStart = startDate;

            bool hasTeamFilter = !string.IsNullOrWhiteSpace(teamId) && teamId != "all";

            // ── 1. Lấy dữ liệu thực hiện ─────────────────────────────────────
            var qBase = _context.qualityManagementSessions
                .Where(q => q.InspectionDate >= startDate
                         && q.InspectionDate <= endDate
                         && q.OperationType == "1")
                .AsNoTracking();

            if (!string.IsNullOrWhiteSpace(factoryId) && factoryId != "all")
                qBase = qBase.Where(q => q.WorkCenterCode == factoryId);
            if (hasTeamFilter)
                qBase = qBase.Where(q => q.WorkShiftCode.ToString() == teamId);

            // Bước 1a: dữ liệu thô của qualityManagementSessions
            var rawSessions = await qBase
                .Select(q => new
                {
                    q.WorkCenterCode,
                    q.WorkShiftCode,
                    q.InspectionDate,
                    q.ItemNo,
                    q.PassOutputQuantity
                })
                .ToListAsync();

            // Bước 1b: danh sách ItemNo cần tra đơn giá
            var itemNos = rawSessions.Select(x => x.ItemNo).Distinct().ToList();

            // Bước X: tỷ giá Local Exchange, theo ngày đầu tháng gần nhất
            var localExchangeRate = await _context.CurrencyExchangeRates
                .Where(e => e.StartingDate <= monthStart)
                .OrderByDescending(e => e.StartingDate)
                .Select(e => e.LocalExchange)
                .FirstOrDefaultAsync();

            if (localExchangeRate == 0) localExchangeRate = 1m;

            // Bước 1c: lấy đơn giá từ SalesOrderLineFactoryPlans theo ItemNo (+ WorkShiftCode nếu có filter)
            var priceQuery = _context.SalesOrderLineFactoryPlans
                .Where(s => itemNos.Contains(s.ItemNo));

            if (hasTeamFilter)
                priceQuery = priceQuery.Where(s => s.WorkShiftCode == teamId);

            var priceList = await priceQuery
                .AsNoTracking()
                .Select(s => new { s.RowID, s.ItemNo, s.WorkShiftCode, s.CmCost, s.CmCostPlus })
                .ToListAsync();

            // Bước 1d: group theo (ItemNo, WorkShiftCode), lấy RowID lớn nhất (mới nhất)
            var priceByItem = priceList
                .GroupBy(x => new { x.ItemNo, x.WorkShiftCode })
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(x => x.RowID).First()
                );

            // ── 2. Kế hoạch — Sản lượng kế hoạch từ mes.factory_plans + mes.factory_plan_lines ──
            var planQuery =
                from line in _context.MesFactoryPlanLines
                join plan in _context.MesFactoryPlans on line.FactoryPlanId equals plan.Id
                where line.WorkDate >= startDate && line.WorkDate <= endDate && plan.IsActive == true
                select new { plan.FactoryId, plan.LineNumber, plan.StyleId, plan.ColorId, line.WorkDate, line.PlannedQty };

            if (!string.IsNullOrWhiteSpace(factoryId) && factoryId != "all")
                planQuery = planQuery.Where(x => x.FactoryId == factoryId);
            if (hasTeamFilter)
                planQuery = planQuery.Where(x => x.LineNumber == teamId);

            var rawPlans = await planQuery.ToListAsync();

            // ── 3. Giá CM theo Style (giống ComputeRevenueDetailsAsync) ─────────
            var planStyleIds = rawPlans.Select(p => p.StyleId).Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();

            var planCmPricesRaw = await _context.StyleCmPrice
                .Where(p => planStyleIds.Contains(p.StyleId)
                         && p.IsActive
                         && p.EffectiveDate <= endDate
                         && (p.ExpiredDate == null || p.ExpiredDate > startDate))
                .Include(p => p.StyleCmSplits)
                .AsNoTracking()
                .ToListAsync();

            var planCmPriceByStyle = planCmPricesRaw
                .GroupBy(p => p.StyleId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.EffectiveDate).First());

            var planOperationStyleIds = planCmPriceByStyle.Values.Where(p => p.IsOperation).Select(p => p.StyleId).Distinct().ToList();
            var planLineNumbers = rawPlans.Select(p => p.LineNumber).Where(l => !string.IsNullOrEmpty(l)).Distinct().ToList();
            var planOperationByTeamStyle = planOperationStyleIds.Count == 0
                ? new Dictionary<(string StyleId, string LineNumber), string>()
                : (await _context.MesFactoryPlans
                    .Where(p => planOperationStyleIds.Contains(p.StyleId)
                             && p.LineNumber != null && planLineNumbers.Contains(p.LineNumber)
                             && p.IsActive && p.OperationCode != null)
                    .Select(p => new { p.StyleId, p.LineNumber, p.OperationCode, p.CreatedAt })
                    .ToListAsync())
                    .GroupBy(p => (StyleId: p.StyleId, LineNumber: p.LineNumber!))
                    .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.CreatedAt).First().OperationCode!);

            // Trả về đơn giá CM (USD) cho 1 cặp (Style, Tổ) — 0 nếu chưa có giá hoặc chưa xác định công đoạn
            decimal ResolvePlanUnitPrice(string? styleId, string? lineNumber)
            {
                if (string.IsNullOrEmpty(styleId) || !planCmPriceByStyle.TryGetValue(styleId, out var cmPrice))
                    return 0m;

                if (!cmPrice.IsOperation)
                    return cmPrice.CmPrice;

                if (!string.IsNullOrEmpty(lineNumber)
                    && planOperationByTeamStyle.TryGetValue((styleId, lineNumber), out var operationCode))
                {
                    var split = cmPrice.StyleCmSplits.FirstOrDefault(s => s.OperationCode == operationCode);
                    if (split != null)
                        return split.SplitPrice;
                }

                return 0m;
            }

            // ── 4. Gộp thực hiện + đơn giá + tỷ giá, tính doanh thu (in-memory) ─
            var actualByDay = rawSessions
                .Select(q =>
                {
                    priceByItem.TryGetValue(
                        new { ItemNo = q.ItemNo, WorkShiftCode = q.WorkShiftCode.ToString() },
                        out var price);

                    var doanhThu = price == null
                        ? 0m
                        : ((price.CmCost ?? 0) + (price.CmCostPlus ?? 0))
                          * localExchangeRate
                          * (decimal)q.PassOutputQuantity;

                    return new
                    {
                        Day = q.InspectionDate.Day,
                        q.PassOutputQuantity,
                        DoanhThu = doanhThu
                    };
                })
                .GroupBy(x => x.Day)
                .ToDictionary(
                    g => g.Key,
                    g => new
                    {
                        ThucHien = g.Sum(x => x.PassOutputQuantity),
                        DoanhThu = g.Sum(x => x.DoanhThu)
                    });

            // Bước 4c: DT Kế hoạch theo ngày = Sản lượng kế hoạch (PlannedQty) * giá CM, quy đổi VND theo tỷ giá
            var planByDay = rawPlans
                .Select(p => new
                {
                    Day = p.WorkDate.Day,
                    DoanhThu = p.PlannedQty * ResolvePlanUnitPrice(p.StyleId, p.LineNumber) * localExchangeRate
                })
                .GroupBy(x => x.Day)
                .ToDictionary(
                    g => g.Key,
                    g => new { DoanhThuKH = g.Sum(x => x.DoanhThu) });

            // ── 5. Gộp kết quả theo từng ngày ───────────────────────────────────
            var result = Enumerable.Range(1, daysInMonth).Select(day =>
            {
                var actual = actualByDay.GetValueOrDefault(day);
                var plan = planByDay.GetValueOrDefault(day);

                var dtThucHien = actual?.DoanhThu ?? 0;
                var dtKeHoach = plan?.DoanhThuKH ?? 0;

                return new TeamDailyRevenueDto
                {
                    Day = day,
                    DoanhThuKeHoach = dtKeHoach,
                    DoanhThuThucTe = dtThucHien,
                    PctHoanThanh = dtKeHoach > 0
                        ? Math.Round((double)(dtThucHien / dtKeHoach * 100), 2)
                        : 0
                };
            });

            return Ok(result);
        }

        [HttpGet("hourly-output")]
        [ProducesResponseType(typeof(List<HourlyOutputDTO>), 200)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<List<HourlyOutputDTO>>> SanLuongTheoGio(
        [FromQuery] string teamId,
        [FromQuery] DateOnly? date,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] string? factoryId)
        {
            if (string.IsNullOrEmpty(teamId))
                return BadRequest(new { success = false, message = "Phải truyền 'teamId'." });

            if (date == null && (from == null || to == null))
                return BadRequest(new { success = false, message = "Phải truyền 'date' hoặc cả 'from' và 'to'." });

            try
            {
                var result = await _service.SanLuongThoatChuyenTheoGio(teamId, date, from, to, factoryId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = "Có lỗi xảy ra", detail = ex.Message });
            }
        }

        // Route: GET /api/production/daily-output
        [HttpGet("daily-output")]
        public async Task<IActionResult> GetDailyOutput(
            [FromQuery] string dateFrom,
            [FromQuery] string dateTo,
            [FromQuery] string? factoryId = null)
        {
            if (string.IsNullOrEmpty(dateFrom) || string.IsNullOrEmpty(dateTo))
                return BadRequest("Thiếu tham số ngày.");
            var result = await _service.GetDailyOutputAsync(dateFrom, dateTo, factoryId);
            return Ok(result);
        }
        [HttpGet("output-by-style")]
        public async Task<IActionResult> GetOutputByStyle(
    [FromQuery] string dateFrom,
    [FromQuery] string dateTo,
    [FromQuery] string? factoryId = null)
        {
            if (string.IsNullOrEmpty(dateFrom) || string.IsNullOrEmpty(dateTo))
                return BadRequest("Thiếu tham số ngày.");

            var result = await _service.GetOutputByStyleAsync(dateFrom, dateTo, factoryId);
            return Ok(result);
        }

        // GET /api/production/detail-by-po-style-color-size
        [HttpGet("detail-by-po-style-color-size")]
        public async Task<ActionResult<IEnumerable<ProductionDetailDto>>> GetDetailByPoStyleColorSize(
            [FromQuery] string dateFrom,
            [FromQuery] string dateTo,
            [FromQuery] string? factoryId = null)
        {
            if (!DateOnly.TryParse(dateFrom, out var from) || !DateOnly.TryParse(dateTo, out var to))
                return BadRequest("dateFrom / dateTo không hợp lệ (yyyy-MM-dd).");

            // ── Query chính (QC sessions) — chỉ lấy Endline (OperationType = 1) ──────
            var query = _context.qualityManagementSessions
                .Where(q => DateOnly.FromDateTime(q.InspectionDate) >= from
                         && DateOnly.FromDateTime(q.InspectionDate) <= to
                         && q.OperationType == "1" && q.Status==1);

            if (!string.IsNullOrWhiteSpace(factoryId) && factoryId != "all")
                query = query.Where(q => q.WorkCenterCode == factoryId);

            var rows = await query.Join(
                _context.locationXSDs,
                q => q.WorkShiftCode,
                ws => ws.Code,
                (q, ws) => new { Session = q, WorkShiftName = ws.Name }
            )
            .GroupBy(x => new
            {
                x.Session.WorkShiftCode,   // giữ lại để lookup kế hoạch
                x.WorkShiftName,
                x.Session.ItemNo,          // giữ lại để lookup kế hoạch
                x.Session.ItemName,
                x.Session.PoName,
                x.Session.ColorNo,
                x.Session.Size,
            })
            .Select(g => new
            {
                WorkShiftCode  = g.Key.WorkShiftCode,
                ItemNo         = g.Key.ItemNo,
                ToMay          = g.Key.WorkShiftName,
                Style          = g.Key.ItemName,
                Po             = g.Key.PoName,
                ColorNo        = g.Key.ColorNo,
                Size           = g.Key.Size,
                SlKiem         = g.Sum(x => x.Session.CheckedQuantity),
                SlThoatChuyen  = g.Sum(x => x.Session.PassOutputQuantity),
            })
            .OrderByDescending(r => r.SlThoatChuyen)
            .ToListAsync();

            // ── Kế hoạch từ ProductionPlans: lọc theo ngày + nhà máy, load về memory, lấy Max per (Tổ, Style) ──
            var planFromDt = from.ToDateTime(TimeOnly.MinValue);
            var planToDt   = to.ToDateTime(TimeOnly.MaxValue);

            //var allPlans = await _context.ProductionPlans
            //    .Where(p => p.ActualStartingDate.HasValue
            //             && p.ActualStartingDate.Value >= planFromDt
            //             && p.ActualStartingDate.Value <= planToDt
            //             && (!string.IsNullOrWhiteSpace(factoryId) && factoryId != "all"
            //                    ? p.WorkCenterCode == factoryId
            //                    : true))
            //    .Select(p => new { p.WorkShiftCode, p.ItemNo, p.TargetQuantityPerDay })
            //    .ToListAsync();

            //// In-memory: Max(TargetQuantityPerDay) theo (Tổ, Style)
            //var planDict = allPlans
            //    .GroupBy(p => (p.WorkShiftCode, p.ItemNo))
            //    .ToDictionary(g => g.Key, g => g.Max(x => x.TargetQuantityPerDay) ?? 0);
            // ── Kế hoạch từ mes.factory_plans + mes.factory_plan_lines ──

            var planQuery =
                from line in _context.MesFactoryPlanLines
                join plan in _context.MesFactoryPlans on line.FactoryPlanId equals plan.Id
                where line.WorkDate >= planFromDt
                   && line.WorkDate <= planToDt && plan.IsActive==true
                select new { plan.FactoryId, plan.LineNumber, plan.StyleId, plan.ColorId, line.PlannedQty };

            if (!string.IsNullOrWhiteSpace(factoryId) && factoryId != "all")
                planQuery = planQuery.Where(x => x.FactoryId == factoryId);

            var allPlans = await planQuery.ToListAsync();

            // In-memory: Sum(PlannedQty) theo (LineNumber, StyleId) — không cần khử trùng
            var planDict = allPlans
                .GroupBy(p => (p.LineNumber, p.StyleId, p.ColorId))
                .ToDictionary(g => g.Key, g => g.Sum(x => x.PlannedQty));

            var result = rows.Select(r =>
            {
                var slKeHoach = planDict.TryGetValue((r.WorkShiftCode, r.ItemNo, r.ColorNo), out var kh) ? kh : 0;
                return new ProductionDetailDto
                {
                    ToMay = r.ToMay,
                    Style = r.Style,
                    Po = r.Po,
                    ColorNo = r.ColorNo,
                    Size = r.Size,
                    SlKiem = r.SlKiem,
                    SlKeHoach = slKeHoach,
                    SlThoatChuyen = r.SlThoatChuyen,
                    PctHoanThanh = slKeHoach > 0
                                        ? Math.Round((double)r.SlThoatChuyen / slKeHoach * 100, 2)
                                        : 0,
                };
            });

            return Ok(result);
        }

        // GET /api/production/plan-detail-by-style-color
        // Trả về chi tiết kế hoạch theo Tổ-Style-Màu, bao gồm cả hàng chưa có dữ liệu kiểm (slKiem=0, slThoatChuyen=0)
        [HttpGet("plan-detail-by-style-color")]
        public async Task<ActionResult<IEnumerable<ProductionDetailDto>>> GetPlanDetailByStyleColor(
            [FromQuery] string dateFrom,
            [FromQuery] string dateTo,
            [FromQuery] string? factoryId = null)
        {
            if (!DateOnly.TryParse(dateFrom, out var from) || !DateOnly.TryParse(dateTo, out var to))
                return BadRequest("dateFrom / dateTo không hợp lệ (yyyy-MM-dd).");

            var planFromDt = from.ToDateTime(TimeOnly.MinValue);
            var planToDt   = to.ToDateTime(TimeOnly.MaxValue);

            // ── 1. Lookup tên tổ từ locationXSDs (Code → Name) — giống endpoint detail-by-po-style-color-size ──
            var wsNameLookup = await _context.locationXSDs
                .ToDictionaryAsync(ws => ws.Code, ws => ws.Name ?? ws.Code, StringComparer.OrdinalIgnoreCase);

            // ── 2. Lấy tất cả plan rows trong khoảng ngày, group theo (Tổ, Style, Màu, Size, PO) ──
            //var planQuery = _context.ProductionPlans
            //    .Where(p => p.ActualStartingDate.HasValue
            //             && p.ActualStartingDate.Value >= planFromDt
            //             && p.ActualStartingDate.Value <= planToDt);

            //if (!string.IsNullOrWhiteSpace(factoryId) && factoryId != "all")
            //    planQuery = planQuery.Where(p => p.WorkCenterCode == factoryId);

            //var planGroups = await planQuery
            //    .GroupBy(p => new
            //    {
            //        p.WorkShiftCode,
            //        p.ItemNo,
            //        p.ColorNo,
            //        p.SizeNo,
            //        p.POName,
            //    })
            //    .Select(g => new
            //    {
            //        g.Key.WorkShiftCode,
            //        g.Key.ItemNo,
            //        g.Key.ColorNo,
            //        g.Key.SizeNo,
            //        g.Key.POName,
            //        SlKeHoach = g.Max(x => x.TargetQuantityPerDay) ?? 0,
            //    })
            //    .ToListAsync();
            // ── 2. Lấy tất cả plan rows trong khoảng ngày, group theo (Tổ, Style, Màu, Size, PO) ──
            var planQuery =
                from line in _context.MesFactoryPlanLines
                join plan in _context.MesFactoryPlans on line.FactoryPlanId equals plan.Id
                where line.WorkDate >= planFromDt && line.WorkDate <= planToDt && plan.IsActive == true
                select new { plan, line };

            if (!string.IsNullOrWhiteSpace(factoryId) && factoryId != "all")
                planQuery = planQuery.Where(x => x.plan.FactoryId == factoryId);

            var planGroups = await planQuery
                .GroupBy(x => new
                {
                    WorkShiftCode = x.plan.LineNumber,
                    ItemNo = x.plan.StyleId,
                    ColorNo = x.plan.ColorId,
                    SizeNo = x.line.SizeId,
                    PoID = x.plan.PoId,
                })
                .Select(g => new
                {
                    g.Key.WorkShiftCode,
                    g.Key.ItemNo,
                    g.Key.ColorNo,
                    g.Key.SizeNo,
                    g.Key.PoID,
                    SlKeHoach = g.Sum(x => x.line.PlannedQty),
                })
                .ToListAsync();

            if (!planGroups.Any())
                return Ok(Enumerable.Empty<ProductionDetailDto>());

            // ── 3. Lookup tên Style (ItemName) từ QC sessions theo ItemNo — giống cách lấy của endpoint QC ──
            var planItemNos = planGroups
                .Select(p => p.ItemNo)
                .Where(x => x != null)
                .Distinct()
                .ToList();

            var itemNameLookup = await _context.qualityManagementSessions
                .Where(q => planItemNos.Contains(q.ItemNo) && q.ItemName != null && q.ItemName != "" && q.Status==1)
                .GroupBy(q => q.ItemNo)
                .Select(g => new { ItemNo = g.Key, ItemName = g.First().ItemName })
                .ToDictionaryAsync(x => x.ItemNo, x => x.ItemName, StringComparer.OrdinalIgnoreCase);

            // ── 4. Lấy dữ liệu QC Endline trong cùng khoảng ngày ──────────────────
            var qcQuery = _context.qualityManagementSessions
                .Where(q => DateOnly.FromDateTime(q.InspectionDate) >= from
                         && DateOnly.FromDateTime(q.InspectionDate) <= to
                         && q.OperationType == "1" && q.Status==1);

            if (!string.IsNullOrWhiteSpace(factoryId) && factoryId != "all")
                qcQuery = qcQuery.Where(q => q.WorkCenterCode == factoryId);

            var qcData = await qcQuery
                .GroupBy(q => new
                {
                    q.WorkShiftCode,
                    q.ItemNo,
                    q.ColorNo,
                    q.Size,
                    q.SalesOrderNo,
                })
                .Select(g => new
                {
                    g.Key.WorkShiftCode,
                    g.Key.ItemNo,
                    g.Key.ColorNo,
                    g.Key.Size,
                    g.Key.SalesOrderNo,
                    SlKiem        = g.Sum(x => x.CheckedQuantity),
                    SlThoatChuyen = g.Sum(x => x.PassOutputQuantity),
                })
                .ToListAsync();

            // ── 5. Build lookup từ QC data theo key (WorkShiftCode||ItemNo||ColorNo||Size||PoName) ──
            var qcDict = qcData.ToDictionary(
                q => $"{q.WorkShiftCode}||{q.ItemNo}||{q.ColorNo}||{q.Size}||{q.SalesOrderNo}",
                q => (SlKiem: q.SlKiem, SlThoatChuyen: q.SlThoatChuyen),
                StringComparer.OrdinalIgnoreCase
            );

            // ── 6. Gộp: plan baseline + name lookups + QC data ───────────────────
            var result = planGroups
                .Select(p =>
                {
                    var lookupKey = $"{p.WorkShiftCode}||{p.ItemNo}||{p.ColorNo}||{p.SizeNo}||{p.PoID}";
                    var hasQc     = qcDict.TryGetValue(lookupKey, out var qc);
                    var slKiem        = hasQc ? qc.SlKiem        : 0;
                    var slThoatChuyen = hasQc ? qc.SlThoatChuyen : 0;

                    // Tên tổ: ưu tiên locationXSDs (nhất quán với endpoint detail-by-po-style-color-size)
                    var toMayName = wsNameLookup.GetValueOrDefault(p.WorkShiftCode ?? "", p.WorkShiftCode ?? "");
                    // Tên style: ưu tiên QC sessions lookup (nhất quán), fallback về ItemNo
                    var styleName = itemNameLookup.GetValueOrDefault(p.ItemNo ?? "", p.ItemNo ?? "");

                    return new ProductionDetailDto
                    {
                        ToMay         = toMayName,
                        Style         = styleName,
                        Po            = p.PoID  ?? "",
                        ColorNo       = p.ColorNo ?? "",
                        Size          = p.SizeNo  ?? "",
                        SlKiem        = slKiem,
                        SlKeHoach     = p.SlKeHoach,
                        SlThoatChuyen = slThoatChuyen,
                        PctHoanThanh  = p.SlKeHoach > 0
                                            ? Math.Round((double)slThoatChuyen / p.SlKeHoach * 100, 2)
                                            : 0,
                    };
                })
                .OrderBy(r => r.ToMay)
                .ThenBy(r => r.Style)
                .ThenBy(r => r.ColorNo);

            return Ok(result);
        }

        // GET /api/production/team-daily-output
        // ?month=4&year=2026[&factoryId=F01][&teamId=T01]
        [HttpGet("team-daily-output")]
        public async Task<ActionResult<IEnumerable<TeamDailyOutputDto>>> GetTeamDailyOutput(
            [FromQuery] int month,
            [FromQuery] int year,
            [FromQuery] string? factoryId = null,
            [FromQuery] string? teamId = null)
        {
            if (month < 1 || month > 12 || year < 2000)
                return BadRequest("month (1-12) hoặc year không hợp lệ.");

            var daysInMonth = DateTime.DaysInMonth(year, month);
            var startDate = new DateTime(year, month, 1);
            var endDate = new DateTime(year, month, daysInMonth);

            // ── Query thực hiện (thoát chuyền) — chỉ lấy Endline (OperationType = 1) ──
            var actualQuery = _context.qualityManagementSessions
                .Where(q => q.InspectionDate >= startDate && q.InspectionDate <= endDate
                         && q.OperationType == "1");

            if (!string.IsNullOrWhiteSpace(factoryId) && factoryId != "all")
                actualQuery = actualQuery.Where(q => q.WorkCenterCode == factoryId);
            if (!string.IsNullOrWhiteSpace(teamId) && teamId != "all")
                actualQuery = actualQuery.Where(q => q.WorkShiftCode.ToString() == teamId);

            var actualByDay = await actualQuery
                .GroupBy(q => q.InspectionDate.Day)
                .Select(g => new { Day = g.Key, ThucHien = g.Sum(x => x.PassOutputQuantity) })
                .ToDictionaryAsync(x => x.Day, x => x.ThucHien);

            // ── Query kế hoạch theo ngày ──────────────────────────────────────────
            // Điều chỉnh tên bảng / cột cho đúng schema
            //var planQuery = _context.ProductionPlans
            //    .Where(p => p.ActualStartingDate >= startDate && p.ActualStartingDate <= endDate);

            //if (!string.IsNullOrWhiteSpace(factoryId) && factoryId != "all")
            //    planQuery = planQuery.Where(p => p.WorkCenterCode == factoryId);
            //if (!string.IsNullOrWhiteSpace(teamId) && teamId != "all")
            //    planQuery = planQuery.Where(p => p.WorkShiftCode.ToString() == teamId);

            //// Bước 1 (DB): Max(TargetQuantityPerDay) theo (Ngày, ItemNo)
            //var planIntermediate = await planQuery
            //    .GroupBy(p => new { Day = p.ActualStartingDate.Value.Day, p.ItemNo })
            //    .Select(g => new { g.Key.Day, MaxQty = g.Max(x => x.TargetQuantityPerDay) })
            //    .ToListAsync();

            //// Bước 2 (in-memory): Sum các Max của từng style lại theo ngày
            //var planByDay = planIntermediate
            //    .GroupBy(x => x.Day)
            //    .ToDictionary(g => g.Key, g => (decimal)g.Sum(x => x.MaxQty));

            // ── Query kế hoạch theo ngày từ mes.factory_plans + mes.factory_plan_lines ──
            var planQuery =
                from line in _context.MesFactoryPlanLines
                join plan in _context.MesFactoryPlans on line.FactoryPlanId equals plan.Id
                where line.WorkDate >= startDate && line.WorkDate <= endDate && plan.IsActive==true
                select new { plan.FactoryId, plan.LineNumber, line.WorkDate, line.PlannedQty };

            if (!string.IsNullOrWhiteSpace(factoryId) && factoryId != "all")
                planQuery = planQuery.Where(x => x.FactoryId == factoryId);

            if (!string.IsNullOrWhiteSpace(teamId) && teamId != "all")
                planQuery = planQuery.Where(x => x.LineNumber.ToString() == teamId);

            // Sum(planned_qty) trực tiếp theo ngày — không cần khử trùng, không cần bước Max trung gian
            var planByDay = await planQuery
                .GroupBy(x => x.WorkDate.Day)
                .Select(g => new { Day = g.Key, TotalQty = g.Sum(x => x.PlannedQty) })
                .ToDictionaryAsync(x => x.Day, x => (decimal)x.TotalQty);

            // ── Gộp theo từng ngày trong tháng ───────────────────────────────────
            var result = Enumerable.Range(1, daysInMonth).Select(day =>
            {
                var thuchien = actualByDay.GetValueOrDefault(day, 0);
                var keHoach = planByDay.GetValueOrDefault(day, 0);
                var pct = keHoach > 0
                                    ? Math.Round((double)(thuchien / keHoach * 100), 2)
                                    : 0;
                return new TeamDailyOutputDto
                {
                    Day = day,
                    ThuChiHien = thuchien,
                    KeHoach = (int)keHoach,
                    PctHoanThanh = pct,
                };
            });

            return Ok(result);
        }

        [HttpGet("split-po/detail")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<object>> GetSplitPoDetail(
    [FromQuery] string factoryId, [FromQuery] string styleId,
    [FromQuery] string poId)
        {
            if (string.IsNullOrWhiteSpace(factoryId) || string.IsNullOrWhiteSpace(poId))
            {
                return BadRequest(new { success = false, message = "Thiếu factoryId/poId." });
            }

            var plans = await _context.MesFactoryPlans
                .AsNoTracking()
                .Where(p => p.FactoryId == factoryId && p.StyleId == styleId 
                         && p.PoId == poId
                         && p.IsActive)
                .OrderBy(p => p.ColorId)
                .ThenBy(p => p.LineNumber)
                .ThenBy(p => p.CreatedAt)
                .ToListAsync();

            if (plans.Count == 0)
            {
                return BadRequest(new { success = false, message = "Không tìm thấy kế hoạch active nào ứng với PO này." });
            }

            // 1 PO chỉ thuộc 1 Style (đúng theo giả định "1 Style <-> 1 production_order_id" đã xác nhận
            // ở phần Gom PO) -> không cần user truyền styleId, tự suy ra và validate đồng nhất.
            if (plans.Select(p => p.StyleId).Distinct().Count() > 1)
            {
                return BadRequest(new { success = false, message = "Dữ liệu bất thường: PO này thuộc nhiều Style khác nhau." });
            }

            //var styleId = plans[0].StyleId;

            // version_id giả định đồng nhất trong 1 PO (chưa validate chặt — nếu cần chặn cứng như bên
            // Gom PO, bổ sung: if (plans.Select(p => p.VersionId).Distinct().Count() > 1) return BadRequest(...))
            var versionId = plans[0].VersionId;

            var poName = await _context.SalesOrderHeader
                .AsNoTracking()
                .Where(x => x.No == poId)
                .Select(x => x.POName)
                .FirstOrDefaultAsync();

            // TODO: chưa xác nhận bảng/cột lưu TÊN Style (khác styleId) — tạm để null.
            // Nếu có bảng master Style, thay bằng:
            // var styleName = await _context.MesStyles.Where(s => s.Id == styleId).Select(s => s.Name).FirstOrDefaultAsync();
            string? styleName = null;

            // ===== Toàn bộ size gốc (mọi Màu) từ production_order_items, cùng version với PO gốc =====
            var allItems = await _context.MesProductionOrderItems
                .Where(e => e.StyleId == styleId && e.PoId == poId && e.VersionId == versionId)
                .ToListAsync();

            // Lấy hợp các Màu xuất hiện ở CẢ 2 nguồn (factory_plans và production_order_items) —
            // phòng trường hợp 1 nguồn thiếu dữ liệu Màu mà nguồn kia có.
            var colorIds = plans.Select(p => p.ColorId)
                .Union(allItems.Select(i => i.ColorId))
                .Distinct()
                .ToList();

            var colors = new List<object>();
            var grandTotalQty = 0;

            foreach (var colorId in colorIds)
            {
                var colorPlans = plans.Where(p => p.ColorId == colorId).ToList();

                // totalQty: dòng "chưa phân tổ" (LineNumber = NULL) lưu NGUYÊN tổng gốc của (po, color)
                // — hành vi giống hệt đã phát hiện ở phần Gom PO (không tự trừ phần đã phân tổ).
                var totalQty = colorPlans
                    .Where(p => p.LineNumber == null)
                    .Sum(p => p.TotalPlannedQty);

                // Nếu Màu này đã phân tổ HẾT (không còn dòng LineNumber=NULL) -> totalQty=0 ở trên là sai,
                // fallback lấy tổng theo production_order_items gốc.
                if (totalQty == 0)
                {
                    totalQty = allItems.Where(i => i.ColorId == colorId).Sum(i => i.PlannedQty);
                }

                // assignedTeams: nhóm theo Tổ, KHÔNG tách theo Công đoạn (theo yêu cầu: Tách PO không cần
                // quan tâm Style có phân công đoạn hay không). Nếu Style có phân công đoạn, assignedQty ở
                // đây có thể gộp trùng giữa các công đoạn của cùng 1 Tổ — đã trao đổi và CHẤP NHẬN bỏ qua.
                var assignedTeams = colorPlans
                    .Where(p => !string.IsNullOrWhiteSpace(p.LineNumber))
                    .GroupBy(p => p.LineNumber!)
                    .Join(
                        _context.workShift,
                        g => g.Key,
                        ws => ws.Code,
                        (g, ws) => new
                        {
                            teamId = g.Key,
                            lineNumber = g.Key,
                            lineNumberName = ws.Name,
                            assignedQty = g.Sum(x => x.TotalPlannedQty)
                        })
                    .Select(x => new
                    {
                        x.teamId,
                        x.lineNumber,
                        x.lineNumberName,
                        x.assignedQty,
                        // LƯU Ý: join theo business-key (factory/style/po/color/tổ), KHÁC với cách join
                        // theo plan_id (Guid) đã dùng ở PreviewAsync/GetChildrenAsync bên Gom PO. Cách này
                        // không lọc theo Status -> nếu PO đang xét từng là PO Gom (đã tạo bản sao QLCL
                        // Status=1 và giữ bản gốc Status=0), CẦN kiểm tra có bị đếm trùng không. An toàn hơn
                        // nên thêm .Where(q => q.Status == 1) nếu Status đã được dùng cho mục đích đó.
                        actualQty = _context.qualityManagementSessions
                            .Where(q => q.WorkCenterCode == factoryId && q.Status==1
                                     && q.ItemNo == styleId
                                     && q.SalesOrderNo == poId
                                     && q.ColorNo == colorId
                                     && q.WorkShiftCode == x.lineNumber)
                            .Sum(q => (int?)q.PassOutputQuantity) ?? 0
                    })
                    .OrderBy(x => x.lineNumber)
                    .ToList();

                var assignedQty = assignedTeams.Sum(x => x.assignedQty);

                var sizeBreakdown = allItems
                    .Where(i => i.ColorId == colorId)
                    .GroupBy(i => i.SizeId)
                    .Select(g => new
                    {
                        sizeId = g.Key,
                        plannedQty = g.Sum(x => x.PlannedQty)
                    })
                    .OrderBy(x => x.sizeId)
                    .ToList();

                grandTotalQty += totalQty;

                colors.Add(new
                {
                    colorId,
                    colorName = colorId, // TODO: chưa có bảng master Màu -> tạm dùng colorId làm tên hiển thị
                    totalQty,
                    assignedQty,
                    remainingQty = Math.Max(0, totalQty - assignedQty),
                    allocated = assignedTeams.Count > 0,
                    assignedTeams,
                    sizeBreakdown
                });
            }

            var payload = new
            {
                factoryId,
                styleId,
                styleName,
                poId,
                poName,
                totalQty = grandTotalQty, // tổng toàn PO, mọi Màu cộng lại
                colors
            };

            return Ok(new { success = true, data = payload });
        }

    //    [HttpGet("split-po/actual-qty")]
    //    [ProducesResponseType(200)]
    //    [ProducesResponseType(400)]
    //    public async Task<ActionResult<object>> GetSplitActualQtyData(
    //[FromQuery] string factoryId,
    //[FromQuery] string styleId,
    //[FromQuery] string poId,
    //[FromQuery] string colorId,
    //[FromQuery] string? lineNumber = null)
    //    {
    //        if (string.IsNullOrWhiteSpace(factoryId)
    //            || string.IsNullOrWhiteSpace(styleId)
    //            || string.IsNullOrWhiteSpace(poId)
    //            || string.IsNullOrWhiteSpace(colorId))
    //        {
    //            return BadRequest(new { success = false, message = "Thiếu factoryId/styleId/poId/colorId." });
    //        }

    //        // Lấy plan của CHÍNH poId được truyền vào (có thể là PO gốc hoặc 1 PO con bất kỳ trong nhóm)
    //        var currentPlans = await _context.MesFactoryPlans
    //            .AsNoTracking()
    //            .Where(p => p.FactoryId == factoryId
    //                && p.StyleId == styleId
    //                && p.PoId == poId
    //                && p.ColorId == colorId
    //                && (p.IsActive == null || p.IsActive == true))
    //            .OrderBy(p => p.LineNumber)
    //            .ThenBy(p => p.CreatedAt)
    //            .ToListAsync();

    //        if (currentPlans.Count == 0)
    //        {
    //            return NotFound(new { success = false, message = "Không tìm thấy dữ liệu kế hoạch cho PO này." });
    //        }

    //        var currentRootPlan = currentPlans.FirstOrDefault(p => string.IsNullOrWhiteSpace(p.LineNumber)) ?? currentPlans.First();

    //        // *** ĐIỂM SỬA CHÍNH ***
    //        // Nếu currentRootPlan CÓ ParentPlanId -> nghĩa là poId truyền vào chính là 1 PO CON,
    //        //     phải nhảy ngược lên plan CHA THẬT SỰ để tìm đủ các anh em.
    //        // Nếu currentRootPlan KHÔNG có ParentPlanId -> nghĩa là poId truyền vào chính là PO GỐC,
    //        //     dùng luôn Id của nó làm mốc để tìm children.
    //        var originPlanId = currentRootPlan.ParentPlanId ?? currentRootPlan.Id;

    //        // Lấy plan gốc thật sự (để lấy đúng PoId/PlanName của PO gốc hiển thị header modal)
    //        var originRootPlan = currentRootPlan.ParentPlanId.HasValue
    //            ? await _context.MesFactoryPlans.AsNoTracking().FirstOrDefaultAsync(p => p.Id == originPlanId)
    //            : currentRootPlan;

    //        if (originRootPlan == null)
    //        {
    //            return NotFound(new { success = false, message = "Không tìm thấy dữ liệu PO gốc." });
    //        }

    //        var totalPlannedQty = originRootPlan.TotalPlannedQty;

    //        var poName = await _context.SalesOrderHeader
    //            .AsNoTracking()
    //            .Where(x => x.No == originRootPlan.PoId)
    //            .Select(x => x.POName)
    //            .FirstOrDefaultAsync();
    //        if (string.IsNullOrWhiteSpace(poName))
    //        {
    //            poName = await _context.MesProductionOrderItems
    //                .AsNoTracking()
    //                .Where(x => x.PoId == originRootPlan.PoId)
    //                .Select(x => x.POName)
    //                .FirstOrDefaultAsync();
    //        }

    //        var lineNumberName = string.IsNullOrWhiteSpace(lineNumber)
    //            ? null
    //            : await _context.workShift
    //                .AsNoTracking()
    //                .Where(x => x.Code == lineNumber)
    //                .Select(x => x.Name)
    //                .FirstOrDefaultAsync();

    //        // *** SỬA: tìm children theo ParentPlanId == originPlanId (Id của plan GỐC thật sự) ***
    //        var childEntries = await _context.MesFactoryPlans
    //            .AsNoTracking()
    //            .Where(p => p.FactoryId == factoryId
    //                && p.StyleId == styleId
    //                && p.ColorId == colorId
    //                && p.ParentPlanId == originPlanId
    //                && (p.IsActive == null || p.IsActive == true))
    //            .GroupBy(p => p.PoId)
    //            .Select(g => new
    //            {
    //                poId = g.Key,
    //                plannedQty = g.Sum(x => x.TotalPlannedQty),
    //            })
    //            .ToListAsync();

    //        if (childEntries.Count == 0)
    //        {
    //            return BadRequest(new { success = false, message = "PO này chưa có PO con nào được tách." });
    //        }

    //        // Actual query riêng (giữ nguyên logic cũ, chỉ áp dụng sau khi có đúng danh sách PoId con)
    //        var childActuals = new List<(string PoId, int ActualQty)>();
    //        foreach (var child in childEntries)
    //        {
    //            var actual = await _context.qualityManagementSessions
    //                .AsNoTracking()
    //                .Where(q => q.WorkCenterCode == factoryId
    //                    && q.ItemNo == styleId
    //                    && q.SalesOrderNo == child.poId
    //                    && q.ColorNo == colorId
    //                    && (string.IsNullOrWhiteSpace(lineNumber) || q.WorkShiftCode == lineNumber))
    //                .SumAsync(q => (int?)q.PassOutputQuantity) ?? 0;
    //            childActuals.Add((child.poId, actual));
    //        }

    //        var totalActualQty = await _context.qualityManagementSessions
    //        .AsNoTracking()
    //        .Where(q => q.WorkCenterCode == factoryId
    //            && q.ItemNo == styleId
    //            && q.SalesOrderNo == originRootPlan.PoId   // *** PO GỐC, không phải PO con ***
    //            && q.ColorNo == colorId
    //            && (string.IsNullOrWhiteSpace(lineNumber) || q.WorkShiftCode == lineNumber))
    //        .SumAsync(q => (int?)q.PassOutputQuantity) ?? 0;

    //        var payload = new
    //        {
    //            factoryId,
    //            styleId,
    //            poId = originRootPlan.PoId,
    //            poName = poName ?? originRootPlan.PoId,
    //            colorId,
    //            lineNumber,
    //            lineNumberName,
    //            totalPlannedQty,
    //            totalActualQty,
    //            splitSource = new
    //            {
    //                poId = originRootPlan.PoId,
    //                poName = poName ?? originRootPlan.PoId,
    //                lineNumber = originRootPlan.LineNumber,
    //                totalPlannedQty,
    //            },
    //            children = childEntries.Select(x =>
    //            {
    //                var actualQty = childActuals.First(a => a.PoId == x.poId).ActualQty;
    //                return new
    //                {
    //                    poId = x.poId,
    //                    plannedQty = x.plannedQty,
    //                    actualQty,
    //                    remainingQty = Math.Max(0, x.plannedQty - actualQty),
    //                };
    //            }).ToList(),
    //        };

    //        return Ok(new { success = true, data = payload });
    //    }

        [HttpPost("split-po")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<object>> SaveSplitPo([FromBody] SplitPoSaveRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.FactoryId)
                || string.IsNullOrWhiteSpace(request.StyleId)
                || string.IsNullOrWhiteSpace(request.PoId)
                || string.IsNullOrWhiteSpace(request.CreatedBy))
            {
                return BadRequest(new { success = false, message = "Thiếu factoryId/styleId/poId/createdBy." });
            }

            if (request.Children == null || request.Children.Count == 0)
                return BadRequest(new { success = false, message = "Chưa có PO con để tách." });

            if (request.Colors == null || request.Colors.Count == 0)
                return BadRequest(new { success = false, message = "Chưa có dữ liệu Màu để tách." });

            // ---- Validate mã PO con (label) không trùng, không trùng PO gốc ----
            var duplicateLabels = request.Children.GroupBy(x => x.PoId).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (duplicateLabels.Count > 0)
                return BadRequest(new { success = false, message = $"Mã PO con bị trùng: {string.Join(", ", duplicateLabels)}" });
            if (request.Children.Any(x => x.PoId == request.PoId))
                return BadRequest(new { success = false, message = "Mã PO con không được trùng với PO gốc." });

            var childLabels = request.Children.Select(c => c.PoId).ToHashSet();

            // ---- Tìm ProductionOrder theo Style ----
            // TODO: xác nhận cách map Factory+Style -> ProductionOrder đúng convention thật (PlanCode? bảng lookup?)
            var planCode = $"{request.FactoryId}|{request.StyleId}";
            var productionOrder = await _context.MesProductionOrders
                .FirstOrDefaultAsync(x => x.PlanCode == planCode && x.IsActive);
            if (productionOrder == null)
                return BadRequest(new { success = false, message = "Không tìm thấy kế hoạch gốc cho Style/Factory này." });

            // ---- Lấy TOÀN BỘ factory_plans của PO gốc, MỌI Màu ----
            var sourcePlans = await _context.MesFactoryPlans
                .Where(x => x.FactoryId == request.FactoryId && x.StyleId == request.StyleId
                    && x.PoId == request.PoId
                    && (x.IsActive == null || x.IsActive == true))
                .ToListAsync();

            if (sourcePlans.Count == 0)
                return BadRequest(new { success = false, message = "Không tìm thấy PO gốc hoặc PO đã được tách/gom trước đó." });

            // Toàn bộ Màu của PO gốc phải cùng version — nếu lệch là dữ liệu bất thường.
            if (sourcePlans.Select(p => p.VersionId).Distinct().Count() > 1)
                return BadRequest(new { success = false, message = "Dữ liệu bất thường: các Màu của PO này thuộc các version khác nhau." });

            var currentVersion = await _context.MesProductionOrderVersions
                .Where(v => v.ProductionOrderId == productionOrder.Id
                         && v.VersionStatus == "ACTIVE"
                         && v.Id == sourcePlans[0].VersionId)
                .OrderByDescending(v => v.VersionNumber)
                .FirstOrDefaultAsync();
            if (currentVersion == null)
                return BadRequest(new { success = false, message = "Không tìm thấy version active cho kế hoạch này." });

            var allCurrentItems = await _context.MesProductionOrderItems
                .Where(x => x.VersionId == currentVersion.Id && (x.IsActive == null || x.IsActive == true))
                .ToListAsync();

            var sourceItems = allCurrentItems.Where(x => x.PoId == request.PoId).ToList();
            if (sourceItems.Count == 0)
                return BadRequest(new { success = false, message = "Không tìm thấy chi tiết size của PO gốc hoặc đã bị tách/không còn active." });

            var unrelatedItems = allCurrentItems.Except(sourceItems).ToList();

            // ---- Đối chiếu tập hợp Màu: request.Colors phải khớp CHÍNH XÁC với Màu có trong PO gốc ----
            var sourceColorIds = sourceItems.Select(i => i.ColorId).Distinct().ToHashSet();
            var requestColorIds = request.Colors.Select(c => c.ColorId).Distinct().ToHashSet();

            var missingColors = sourceColorIds.Except(requestColorIds).ToList();
            if (missingColors.Count > 0)
                return BadRequest(new { success = false, message = $"Thiếu dữ liệu tách cho Màu: {string.Join(", ", missingColors)}" });

            var extraColors = requestColorIds.Except(sourceColorIds).ToList();
            if (extraColors.Count > 0)
                return BadRequest(new { success = false, message = $"Màu không thuộc PO gốc: {string.Join(", ", extraColors)}" });

            const string UNASSIGNED_KEY = "__UNASSIGNED__";

            // ---- Validate + tính toán từng Màu (chưa ghi DB, chỉ kiểm tra + chuẩn bị dữ liệu) ----
            var colorContexts = new List<ColorSplitContext>();
            var errors = new List<string>();

            foreach (var colorInput in request.Colors)
            {
                var colorSourceItems = sourceItems.Where(i => i.ColorId == colorInput.ColorId).ToList();
                var colorSourcePlans = sourcePlans.Where(p => p.ColorId == colorInput.ColorId).ToList();

                var sourceSizeTotals = colorSourceItems
                    .GroupBy(x => x.SizeId)
                    .ToDictionary(g => g.Key, g => g.Sum(x => x.PlannedQty));

                var requestSizeIds = colorInput.SizeAllocations.Select(s => s.SizeId).ToHashSet();
                var sourceSizeIds = sourceSizeTotals.Keys.ToHashSet();

                var missingSizes = sourceSizeIds.Except(requestSizeIds).ToList();
                if (missingSizes.Count > 0)
                    errors.Add($"Màu {colorInput.ColorId}: thiếu size {string.Join(", ", missingSizes)}.");

                var extraSizes = requestSizeIds.Except(sourceSizeIds).ToList();
                if (extraSizes.Count > 0)
                    errors.Add($"Màu {colorInput.ColorId}: size không thuộc PO gốc {string.Join(", ", extraSizes)}.");

                // childTotalsForColor[childLabel] = tổng SL của PO con đó trong Màu này (cộng dồn mọi size)
                var childTotalsForColor = childLabels.ToDictionary(c => c, _ => 0);

                foreach (var sizeAlloc in colorInput.SizeAllocations)
                {
                    if (sizeAlloc.Splits.Any(s => s.PlannedQty < 0))
                    {
                        errors.Add($"Màu {colorInput.ColorId}, size {sizeAlloc.SizeId}: số lượng âm không hợp lệ.");
                        continue;
                    }

                    var unknownChild = sizeAlloc.Splits.Select(s => s.ChildPoId).FirstOrDefault(c => !childLabels.Contains(c));
                    if (unknownChild != null)
                    {
                        errors.Add($"Màu {colorInput.ColorId}, size {sizeAlloc.SizeId}: PO con '{unknownChild}' không nằm trong danh sách children.");
                        continue;
                    }

                    var splitTotal = sizeAlloc.Splits.Sum(s => s.PlannedQty);
                    var sourceTotal = sourceSizeTotals.TryGetValue(sizeAlloc.SizeId, out var st) ? st : 0;
                    if (splitTotal != sourceTotal)
                    {
                        errors.Add($"Màu {colorInput.ColorId}, size {sizeAlloc.SizeId}: tổng chia ({splitTotal}) phải bằng SL gốc ({sourceTotal}).");
                        continue;
                    }

                    foreach (var split in sizeAlloc.Splits)
                        childTotalsForColor[split.ChildPoId] += split.PlannedQty;
                }

                var colorTotalQty = colorSourceItems.Sum(x => x.PlannedQty);

                var teamAssignedPlans = colorSourcePlans.Where(p => !string.IsNullOrWhiteSpace(p.LineNumber)).ToList();
                var hasTeamData = teamAssignedPlans.Count > 0;

                if (!hasTeamData && colorInput.TeamAllocations is { Count: > 0 })
                {
                    errors.Add($"Màu {colorInput.ColorId}: chưa phân tổ, không được gửi teamAllocations.");
                }

                //var teamAllocationRows = new List<(string TeamId, string ChildPoId, int Qty)>();
                var teamAllocationRows = new List<(string TeamId, string ChildPoId, int Qty, bool IsUnallocated)>();
                if (hasTeamData)
                {
                    if (colorInput.TeamAllocations == null || colorInput.TeamAllocations.Count == 0)
                    {
                        errors.Add($"Màu {colorInput.ColorId}: đã phân tổ, vui lòng cung cấp teamAllocations.");
                    }
                    else
                    {
                        // unassignedQty tính từ TỔNG THEO ITEM (đã đối chiếu ở trên) trừ SL đã gán Tổ —
                        // KHÔNG lấy trực tiếp TotalPlannedQty của dòng LineNumber=NULL, vì dòng đó lưu
                        // NGUYÊN tổng gốc chứ không tự trừ phần đã phân tổ (xem giải thích ở phần Gom PO).
                        var unassignedQty = colorTotalQty - teamAssignedPlans.Sum(p => p.TotalPlannedQty);
                        if (unassignedQty < 0)
                            errors.Add($"Màu {colorInput.ColorId}: dữ liệu không nhất quán — SL đã gán Tổ vượt quá tổng SL Màu.");

                        var teamPlannedTotals = teamAssignedPlans
                            .GroupBy(x => x.LineNumber!)
                            .ToDictionary(g => g.Key, g => g.Sum(x => x.TotalPlannedQty));
                        if (unassignedQty > 0)
                            teamPlannedTotals[UNASSIGNED_KEY] = unassignedQty;

                        //teamAllocationRows = colorInput.TeamAllocations
                        //    .Where(t => !t.IsUnallocated || t.LineNumber == UNASSIGNED_KEY) // giữ dòng UNASSIGNED thật, loại phần đánh dấu bỏ qua khác nếu có
                        //    .SelectMany(t => t.Allocations.Select(a => (TeamId: t.LineNumber, ChildPoId: a.PoId, Qty: a.PlannedQty)))
                        //    .ToList();
                        teamAllocationRows = colorInput.TeamAllocations
                                .Where(t => t.Allocations != null && t.Allocations.Count > 0)
                                .SelectMany(t => t.Allocations.Select(a => (
                                    TeamId: t.LineNumber,
                                    ChildPoId: a.PoId,
                                    Qty: a.PlannedQty,
                                    IsUnallocated: t.IsUnallocated
                                )))
                                .ToList();

                        // Đối chiếu: mỗi PO con, tổng chia theo Tổ (trong Màu này) phải khớp tổng SL PO con
                        // đã tính từ sizeAllocations (childTotalsForColor).
                        foreach (var childLabel in childLabels)
                        {
                            var allocForChild = teamAllocationRows.Where(x => x.ChildPoId == childLabel).Sum(x => x.Qty);
                            var expected = childTotalsForColor[childLabel];
                            if (allocForChild != expected)
                                errors.Add($"Màu {colorInput.ColorId}, PO con '{childLabel}': phân bổ theo Tổ ({allocForChild}) phải khớp SL đã chia theo size ({expected}).");

                        }

                        // TODO: chưa validate "mỗi Tổ chia đúng bằng KH còn lại của Tổ đó" (đã bỏ theo code gốc,
                        // giữ nguyên mức lỏng này — chỉ bắt buộc khớp tổng theo PO con).
                    }
                }

                colorContexts.Add(new ColorSplitContext
                {
                    ColorId = colorInput.ColorId,
                    ColorSourceItems = colorSourceItems,
                    RootPlan = colorSourcePlans.FirstOrDefault(p => string.IsNullOrWhiteSpace(p.LineNumber)),
                    TeamAssignedPlans = teamAssignedPlans,
                    HasTeamData = hasTeamData,
                    ChildTotals = childTotalsForColor,
                    TeamAllocationRows = teamAllocationRows,
                    SizeAllocations = colorInput.SizeAllocations
                });
            }

            if (errors.Count > 0)
                return BadRequest(new { success = false, message = "Dữ liệu tách chưa hợp lệ.", errors });

            // ---- Bắt đầu ghi dữ liệu (transaction) ----
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var createdAt = DateTime.UtcNow;
                var nextVersionNumber = await _context.MesProductionOrderVersions
        .Where(v => v.ProductionOrderId == productionOrder.Id)
        .MaxAsync(v => (int?)v.VersionNumber) ?? 0;
                nextVersionNumber += 1;
                var newVersion = new MesProductionOrderVersion
                {
                    Id = Guid.NewGuid(),
                    ProductionOrderId = productionOrder.Id,
                    VersionNumber = nextVersionNumber,
                    VersionStatus = "ACTIVE",
                    TriggerType = "SPLIT",
                    SourcePoType = currentVersion.SourcePoType,
                    CreatedBy = request.CreatedBy,
                    CreatedAt = createdAt,
                };
                _context.MesProductionOrderVersions.Add(newVersion);
                await _context.SaveChangesAsync();

                currentVersion.VersionStatus = "SUPERSEDED";
                currentVersion.SupersededBy = newVersion.Id;
                productionOrder.CurrentVersion = newVersion.VersionNumber;
                productionOrder.UpdatedAt = createdAt;

                // Copy-forward các item KHÔNG liên quan (PO khác cùng Style) sang version mới
                foreach (var item in unrelatedItems)
                {
                    _context.MesProductionOrderItems.Add(new MesProductionOrderItem
                    {
                        Id = Guid.NewGuid(),
                        VersionId = newVersion.Id,
                        FactoryId = item.FactoryId,
                        StyleId = item.StyleId,
                        PoId = item.PoId,
                        ColorId = item.ColorId,
                        SizeId = item.SizeId,
                        PlannedQty = item.PlannedQty,
                        TargetDeliveryDate = item.TargetDeliveryDate,
                        POName = item.POName,
                        Status = item.Status,
                        IsActive = true,
                        CreatedAt = createdAt,
                        UpdatedAt = createdAt,
                        ParentItemId = item.Id,
                    });
                }

                // Sinh 1 po_id THẬT duy nhất cho mỗi PO con — DÙNG CHUNG cho mọi Màu của PO con đó.
                //var newPoIdByChildLabel = new Dictionary<string, string>();
                //foreach (var child in request.Children)
                //    newPoIdByChildLabel[child.PoId] = await GenerateSplitPoCodeAsync(request.FactoryId);
                var sequence = await GetOrCreateSplitSequenceAsync(request.FactoryId);

                var newPoIdByChildLabel = new Dictionary<string, string>();

                foreach (var child in request.Children)
                {
                    sequence.LastSeq++;
                    sequence.UpdatedAt = DateTime.Now;

                    newPoIdByChildLabel[child.PoId] =
                        $"TACH-{request.FactoryId}-{sequence.Period}-{sequence.LastSeq:D4}";
                }

                var createdChildren = new List<object>();

                foreach (var child in request.Children)
                {
                    var newPoId = newPoIdByChildLabel[child.PoId];

                    var targetDeliveryDate = DateOnly.FromDateTime(createdAt.Date.AddDays(7));
                    if (!string.IsNullOrWhiteSpace(child.ShipDate)
                        && DateOnly.TryParseExact(child.ShipDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
                        targetDeliveryDate = parsedDate;

                    var childSizeBreakdownAllColors = new List<object>();

                    foreach (var ctx in colorContexts)
                    {
                        var childTotalForColor = ctx.ChildTotals[child.PoId];
                        if (childTotalForColor <= 0)
                            continue; // Màu này không chia gì cho PO con này -> không tạo dòng rỗng

                        // ---- production_order_items theo từng size (chỉ các size có SL > 0 cho PO con này) ----
                        foreach (var sizeAlloc in ctx.SizeAllocations)
                        {
                            var split = sizeAlloc.Splits.FirstOrDefault(s => s.ChildPoId == child.PoId);
                            if (split == null || split.PlannedQty <= 0) continue;

                            var parentItem = ctx.ColorSourceItems.FirstOrDefault(x => x.SizeId == sizeAlloc.SizeId);

                            _context.MesProductionOrderItems.Add(new MesProductionOrderItem
                            {
                                Id = Guid.NewGuid(),
                                VersionId = newVersion.Id,
                                FactoryId = request.FactoryId,
                                StyleId = request.StyleId,
                                PoId = newPoId,
                                POName = child.PoId,
                                ColorId = ctx.ColorId,
                                SizeId = sizeAlloc.SizeId,
                                PlannedQty = split.PlannedQty,
                                TargetDeliveryDate = targetDeliveryDate,
                                Status = "ACTIVE",
                                IsActive = true,
                                CreatedAt = createdAt,
                                UpdatedAt = createdAt,
                                ParentItemId = parentItem?.Id,
                            });

                            childSizeBreakdownAllColors.Add(new { colorId = ctx.ColorId, sizeId = sizeAlloc.SizeId, plannedQty = split.PlannedQty });
                        }

                        // ---- factory_plans "root" (LineNumber = NULL) cho (PO con, Màu) ----
                        _context.MesFactoryPlans.Add(new MesFactoryPlan
                        {
                            Id = Guid.NewGuid(),
                            PlanCode = $"SPLIT-{child.PoId}-{ctx.ColorId}-ROOT-{createdAt:yyyyMMddHHmmss}",
                            ProductionOrderId = productionOrder.Id,
                            VersionId = newVersion.Id,
                            FactoryId = request.FactoryId,
                            StyleId = request.StyleId,
                            PoId = newPoId,
                            ColorId = ctx.ColorId,
                            LineNumber = null,
                            //ProductionStartDate = DateOnly.FromDateTime(createdAt.Date),
                            ProductionStartDate = null,
                            TotalPlannedQty = childTotalForColor,
                            Status = "split",
                            CreatedBy = request.CreatedBy,
                            CreatedAt = createdAt,
                            UpdatedAt = createdAt,
                            IsActive = true,
                            ParentPlanId = ctx.RootPlan?.Id,
                        });

                        // ---- factory_plans theo Tổ (nếu Màu này đã phân tổ) ----
                        if (ctx.HasTeamData)
                        {
                            //var teamGroups = ctx.TeamAllocationRows
                            //    .Where(x => x.ChildPoId == child.PoId && x.TeamId != UNASSIGNED_KEY)
                            //    .GroupBy(x => x.TeamId);
                            var teamGroups = ctx.TeamAllocationRows
                              .Where(x => x.ChildPoId == child.PoId && !x.IsUnallocated)   // <-- chỉ lấy Tổ thật
                              .GroupBy(x => x.TeamId);

                            foreach (var teamGroup in teamGroups)
                            {
                                var teamId = teamGroup.Key;
                                var sourceTeamPlan = ctx.TeamAssignedPlans.FirstOrDefault(p => p.LineNumber == teamId);

                                _context.MesFactoryPlans.Add(new MesFactoryPlan
                                {
                                    Id = Guid.NewGuid(),
                                    PlanCode = $"SPLIT-{child.PoId}-{ctx.ColorId}-{teamId}-{createdAt:yyyyMMddHHmmss}",
                                    ProductionOrderId = productionOrder.Id,
                                    VersionId = newVersion.Id,
                                    FactoryId = request.FactoryId,
                                    StyleId = request.StyleId,
                                    PoId = newPoId,
                                    ColorId = ctx.ColorId,
                                    LineNumber = teamId,
                                    //ProductionStartDate = DateOnly.FromDateTime(createdAt.Date),
                                    ProductionStartDate = null,
                                    TotalPlannedQty = teamGroup.Sum(x => x.Qty),
                                    Status = "split",
                                    CreatedBy = request.CreatedBy,
                                    CreatedAt = createdAt,
                                    UpdatedAt = createdAt,
                                    IsActive = true,
                                    ParentPlanId = sourceTeamPlan?.Id,
                                });
                            }
                        }
                    }

                    createdChildren.Add(new { poId = newPoId, label = child.PoId, sizeBreakdown = childSizeBreakdownAllColors });
                }

                // Khoá plan gốc (mọi Màu) — đã tách xong, không active nữa
                foreach (var plan in sourcePlans)
                {
                    plan.IsActive = false;
                    plan.Status = "split_source";
                    plan.UpdatedAt = createdAt;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new
                {
                    success = true,
                    message = "Tách PO thành công.",
                    data = new { productionOrderId = productionOrder.Id, versionId = newVersion.Id, children = createdChildren },
                });
            }
            catch (InvalidOperationException ex)
            {
                await transaction.RollbackAsync();
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private class ColorSplitContext
        {
            public string ColorId { get; set; } = default!;
            public List<MesProductionOrderItem> ColorSourceItems { get; set; } = new();
            public MesFactoryPlan? RootPlan { get; set; }
            public List<MesFactoryPlan> TeamAssignedPlans { get; set; } = new();
            public bool HasTeamData { get; set; }
            public Dictionary<string, int> ChildTotals { get; set; } = new();
            //public List<(string TeamId, string ChildPoId, int Qty)> TeamAllocationRows { get; set; } = new();
            public List<(string TeamId, string ChildPoId, int Qty, bool IsUnallocated)> TeamAllocationRows { get; set; }
            public List<SplitPoSizeAllocationDto> SizeAllocations { get; set; } = new();
        }
        private async Task<MesPoCodeSequences> GetOrCreateSplitSequenceAsync(string factoryId)
        {
            var period = DateTime.Now.ToString("yyyyMM");
            var seqKey = $"{factoryId}_TACH";

            // Kiểm tra entity đã được EF tracking chưa
            var seqRow = _context.MesPoCodeSequences.Local
                .FirstOrDefault(x => x.FactoryId == seqKey);

            if (seqRow == null)
            {
                seqRow = await _context.MesPoCodeSequences
                    .FromSqlInterpolated($@"
                SELECT *
                FROM mes.po_code_sequence WITH (UPDLOCK, HOLDLOCK)
                WHERE factory_id = {seqKey}
                  AND period = {period}")
                    .FirstOrDefaultAsync();
            }

            if (seqRow == null)
            {
                seqRow = new MesPoCodeSequences
                {
                    FactoryId = seqKey,
                    Period = period,
                    LastSeq = 0,
                    UpdatedAt = DateTime.Now
                };

                _context.MesPoCodeSequences.Add(seqRow);
            }

            return seqRow;
        }
        //private async Task<string> GenerateSplitPoCodeAsync(string factoryId)
        //{
        //    var period = DateTime.Now.ToString("yyyyMM");
        //    var seqKey = $"{factoryId}_TACH"; // composite key tạm, tránh đụng sequence của Gom PO

        //    var seqRow = await _context.MesPoCodeSequences
        //        .FromSqlInterpolated($@"
        //        SELECT *
        //        FROM mes.po_code_sequence WITH (UPDLOCK, HOLDLOCK)
        //        WHERE factory_id = {seqKey}
        //          AND period = {period}")
        //        .FirstOrDefaultAsync();

        //    int nextSeq;
        //    if (seqRow == null)
        //    {
        //        nextSeq = 1;
        //        _context.MesPoCodeSequences.Add(new MesPoCodeSequences
        //        {
        //            FactoryId = seqKey,
        //            Period = period,
        //            LastSeq = nextSeq,
        //            UpdatedAt = DateTime.Now
        //        });
        //    }
        //    else
        //    {
        //        nextSeq = seqRow.LastSeq + 1;
        //        seqRow.LastSeq = nextSeq;
        //        seqRow.UpdatedAt = DateTime.Now;
        //    }
        //    return $"TACH-{factoryId}-{period}-{nextSeq:D4}";
        //}
        private static List<SplitPoSizeInputDto> BuildFallbackSizeBreakdown(int totalQty, List<SplitPoSizeInputDto> sourceSizeTotals)
        {
            if (sourceSizeTotals == null || sourceSizeTotals.Count == 0)
            {
                return new List<SplitPoSizeInputDto>();
            }

            var normalized = sourceSizeTotals
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.SizeId))
                .Select(x => new
                {
                    SizeId = x.SizeId,
                    PlannedQty = x.PlannedQty,
                })
                .ToList();

            if (normalized.Count == 0)
            {
                return new List<SplitPoSizeInputDto>();
            }

            var totalSource = normalized.Sum(x => x.PlannedQty);
            if (totalSource <= 0)
            {
                return normalized.Select(x => new SplitPoSizeInputDto
                {
                    SizeId = x.SizeId,
                    PlannedQty = 0,
                }).ToList();
            }

            var results = new List<SplitPoSizeInputDto>();
            var remaining = totalQty;
            for (var i = 0; i < normalized.Count; i++)
            {
                var sourceSize = normalized[i];
                var sizeQty = (int)Math.Round((double)sourceSize.PlannedQty / totalSource * totalQty);
                if (i == normalized.Count - 1)
                {
                    sizeQty = remaining;
                }
                else
                {
                    remaining -= sizeQty;
                }
                results.Add(new SplitPoSizeInputDto
                {
                    SizeId = sourceSize.SizeId,
                    PlannedQty = Math.Max(0, sizeQty),
                });
            }
            return results;
        }

        [HttpPost("sync-from-nav")]
        [ProducesResponseType(typeof(object), 200)]
        [ProducesResponseType(400)]
        //Mới lấy từ đơn hàng thôi
        public async Task<IActionResult> SyncFromNav([FromQuery] DateOnly? fromDate, [FromQuery] DateOnly? toDate)
        {
            try
            {
                var now = DateTime.UtcNow;
                int syncedOrders = 0;
                int updatedOrders = 0;
                int syncedItems = 0;
                int unchangedItems = 0;
                int mismatchedItems = 0;
                int syncedFactoryPlans = 0;
                int updatedFactoryPlans = 0;
                int mismatchedFactoryPlans = 0;
                int syncedFactoryPlanLines = 0;
                var itemWarnings = new List<object>();
                var factoryPlanWarnings = new List<object>();

                // ── 1. Đọc dữ liệu từ NAV ─────────────────────────────────────────────
                var navFactory = await _context.SalesOrderLineFactories
                .AsNoTracking()
                .Where(f => f.Status != -1 && f.Status != 0 && f.Status != 1 &&
                    (!fromDate.HasValue || (f.ModifiedDate.HasValue && DateOnly.FromDateTime(f.ModifiedDate.Value.Date) >= fromDate.Value)) &&
                    (!toDate.HasValue || (f.ModifiedDate.HasValue && DateOnly.FromDateTime(f.ModifiedDate.Value.Date) <= toDate.Value)))
                .ToListAsync();
                //var navFactory = await _context.SalesOrderLineFactories
                //    .AsNoTracking()
                //    .Where(f => f.Status >= 2 && (!fromDate.HasValue || (f.PlanExportDate.HasValue && DateOnly.FromDateTime(f.PlanExportDate.Value.Date) >= fromDate.Value)) &&
                //        (!toDate.HasValue || (f.PlanExportDate.HasValue && DateOnly.FromDateTime(f.PlanExportDate.Value.Date) <= toDate.Value)))
                //    .ToListAsync();
                // Giữ dạng IQueryable (CHƯA gọi ToListAsync) để dùng làm subquery bên dưới
                var navFactoryAllQuery = _context.SalesOrderLineFactories
                    .AsNoTracking()
                    .Where(f => f.Status != -1 && f.Status != 0 && f.Status != 1);

                var navFactoryAll = await navFactoryAllQuery.ToListAsync();
                var navDetails = await _context.SalesOrderLineDetailFactories
                    .AsNoTracking()
                    .Where(d => navFactoryAllQuery.Any(f =>
                        f.DocumentNo == d.DocumentNo &&
                        f.WorkCenterCode == d.WorkCenterCode))
                    .ToListAsync();

                var factoryDescLookup = navFactory
                    .GroupBy(f => $"{f.WorkCenterCode}|{f.ItemNo}")
                    .ToDictionary(g => g.Key, g => g.First());

                //var itemNoByDocSolLine = navFactory
                //    .GroupBy(f => $"{f.DocumentNo}|{f.SolLineNo}", StringComparer.OrdinalIgnoreCase)
                //    .ToDictionary(g => g.Key, g => g.First().ItemNo ?? string.Empty, StringComparer.OrdinalIgnoreCase);
                var itemNoByDocSolLine = navFactoryAll
                    .GroupBy(f => $"{f.DocumentNo}|{f.SolLineNo}", StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First().ItemNo ?? string.Empty, StringComparer.OrdinalIgnoreCase);
                // ── 2. UPSERT production_orders ───────────────────────────────────────

                var planGroups = navFactory
                    .GroupBy(f => $"{f.WorkCenterCode}|{f.ItemNo}")
                    .ToList();
                // Load toàn bộ orders hiện tại để upsert (không skip)
                var existingOrders = await _context.MesProductionOrders
                    .ToDictionaryAsync(o => o.PlanCode, o => o, StringComparer.OrdinalIgnoreCase);

                var orderIdMap = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
                var isNewOrderSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var newOrders = new List<MesProductionOrder>();

                foreach (var group in planGroups)
                {
                    var planKey = group.Key;
                    factoryDescLookup.TryGetValue(planKey, out var factoryRow);


                    if (existingOrders.TryGetValue(planKey, out var existingOrder))
                    {
                        // CẬP NHẬT order đã tồn tại thay vì bỏ qua
                        existingOrder.PlanPeriodFrom = null;
                        existingOrder.PlanPeriodTo = null;
                        existingOrder.UpdatedAt = now;
                        orderIdMap[planKey] = existingOrder.Id;
                        updatedOrders++;
                    }
                    else
                    {
                        var order = new MesProductionOrder
                        {
                            Id = Guid.NewGuid(),
                            PlanCode = planKey,
                            PlanName = factoryRow?.Description ?? planKey,
                            PlanPeriodFrom = null,
                            PlanPeriodTo = null,
                            Status = "APPROVED",
                            CurrentVersion = 1,
                            ApprovedBy = null,
                            ApprovedAt = null,
                            Notes = null,
                            CreatedBy = "sync",
                            CreatedAt = now,
                            UpdatedAt = now,
                            IsActive = true,
                        };
                        newOrders.Add(order);
                        orderIdMap[planKey] = order.Id;
                        isNewOrderSet.Add(planKey);
                        syncedOrders++;
                    }
                }

                if (newOrders.Count > 0)
                    await _context.MesProductionOrders.AddRangeAsync(newOrders);
                await _context.SaveChangesAsync();
                foreach (var (planKey, existingOrder) in existingOrders)
                {
                    if (!orderIdMap.ContainsKey(planKey))
                    {
                        orderIdMap[planKey] = existingOrder.Id;
                        // Không tăng updatedOrders/syncedOrders, không set UpdatedAt
                        // vì đây không phải "sync" thực sự trong lần chạy này, chỉ để phục vụ so sánh.
                    }
                }
                // ── 3. Versions ───────────────────────────────────────────────────────
                // Orders mới → tạo version 1
                // Orders đã tồn tại → dùng lại active version (update in-place), xoá items cũ
                var preExistingOrderIds = existingOrders.Values.Select(o => o.Id).ToList();

                var existingActiveVersionsList = preExistingOrderIds.Count > 0
                    ? await _context.MesProductionOrderVersions
                        .Where(v => preExistingOrderIds.Contains(v.ProductionOrderId) && v.VersionStatus == "ACTIVE")
                        .ToListAsync()
                    : new List<MesProductionOrderVersion>();

                // Phòng trường hợp dữ liệu không nhất quán có nhiều active version
                var existingActiveVersions = existingActiveVersionsList
                    .GroupBy(v => v.ProductionOrderId)
                    .ToDictionary(g => g.Key, g => g.OrderByDescending(v => v.VersionNumber).First());

                var orderIdToVersionId = new Dictionary<Guid, Guid>();
                var newVersions = new List<MesProductionOrderVersion>();

                foreach (var (planKey, orderId) in orderIdMap)
                {
                    if (isNewOrderSet.Contains(planKey))
                    {
                        var versionId = Guid.NewGuid();
                        newVersions.Add(new MesProductionOrderVersion
                        {
                            Id = versionId,
                            ProductionOrderId = orderId,
                            VersionNumber = 1,
                            VersionStatus = "ACTIVE",
                            TriggerType = "MANUAL",
                            SourcePoType = "TEMP",
                            ErpPoId = planKey,
                            DeltaSummary = null,
                            SupersededBy = null,
                            CreatedBy = "sync",
                            CreatedAt = now,
                        });
                        orderIdToVersionId[orderId] = versionId;
                    }
                    else if (existingActiveVersions.TryGetValue(orderId, out var activeVer))
                    {
                        // Tái sử dụng version ID hiện tại, items sẽ được xoá và chèn lại bên dưới
                        orderIdToVersionId[orderId] = activeVer.Id;
                    }
                }

                if (newVersions.Count > 0)
                    await _context.MesProductionOrderVersions.AddRangeAsync(newVersions);
                await _context.SaveChangesAsync();

                // ── 4. Items: chỉ tạo mới nếu đơn hàng (Style/PO/Color/Size) chưa có ở MES ─────────
                // Trước đây: xoá sạch item cũ của version rồi chèn lại toàn bộ theo ERP -> làm mất dữ liệu
                // mà MES đã tự xử lý (gom/tách PO, đổi tên PO, sửa SL kế hoạch...).
                // Nay: so khớp theo khoá Style|PO|Color|Size; nếu đã có ở MES thì giữ nguyên, chỉ cảnh báo khi SL lệch với ERP.
                var versionIdsToRefresh = existingActiveVersions.Values.Select(v => v.Id).ToList();
                string Normalize(string? s) => (s ?? string.Empty).Trim().ToLowerInvariant();

                var existingItemsByKey = new Dictionary<string, MesProductionOrderItem>(StringComparer.OrdinalIgnoreCase);
                if (versionIdsToRefresh.Count > 0)
                {
                    var currentItems = await _context.MesProductionOrderItems
                        .Where(i => versionIdsToRefresh.Contains(i.VersionId) && i.IsActive==true)
                        .ToListAsync();
                    foreach (var i in currentItems)
                    {
                        var existingKey = $"{i.VersionId:N}|{Normalize(i.FactoryId)}|{Normalize(i.StyleId)}|{Normalize(i.PoId)}|{Normalize(i.ColorId)}|{Normalize(i.SizeId)}";
                        existingItemsByKey[existingKey] = i;
                    }
                }

                var itemGroups = new Dictionary<string, (MesProductionOrderItem item, int totalQty)>(StringComparer.OrdinalIgnoreCase);
                //var factoryLineLookup = navFactory
                //.GroupBy(f => $"{f.DocumentNo}|{f.SolLineNo}|{f.WorkCenterCode}", StringComparer.OrdinalIgnoreCase)
                //.ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
                var factoryLineLookup = navFactoryAll   // đổi từ navFactory -> navFactoryAll
                    .GroupBy(f => $"{f.DocumentNo}|{f.SolLineNo}|{f.WorkCenterCode}", StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
                foreach (var detail in navDetails)
                {
                    var docSolKey = $"{detail.DocumentNo}|{detail.SolLineNo}";
                    itemNoByDocSolLine.TryGetValue(docSolKey, out var styleId);
                    styleId ??= string.Empty;

                    var wkCenter = detail.WorkCenterCode ?? string.Empty;
                    var planKey = $"{wkCenter}|{styleId}";
                    if (!orderIdMap.TryGetValue(planKey, out var orderId)) continue;
                    if (!orderIdToVersionId.TryGetValue(orderId, out var versionId)) continue;

                    // Lấy đúng dòng Factory theo Document No_ + SOL Line No_ + WorkCenterCode
                    var factoryLineKey = $"{detail.DocumentNo}|{detail.SolLineNo}|{wkCenter}";
                    factoryLineLookup.TryGetValue(factoryLineKey, out var factoryLine);

                    var qty = Math.Max(0, detail.Quantity ?? 0);
                    var targetDate = factoryLine?.PlanExportDate.HasValue == true
                        ? DateOnly.FromDateTime(factoryLine.PlanExportDate!.Value)
                        : DateOnly.FromDateTime(now); // fallback nếu không có Plan Export Date

                    var key = $"{versionId:N}|{Normalize(wkCenter)}|{Normalize(styleId)}|{Normalize(detail.DocumentNo)}|{Normalize(detail.ColorNo)}|{Normalize(detail.SizeNo)}";

                    if (itemGroups.TryGetValue(key, out var existingItem))
                    {
                        itemGroups[key] = (existingItem.item, existingItem.totalQty + qty);
                    }
                    else
                    {
                        var item = new MesProductionOrderItem
                        {
                            Id = Guid.NewGuid(),
                            VersionId = versionId,
                            FactoryId = wkCenter,
                            StyleId = styleId,
                            PoId = detail.DocumentNo ?? "",   // gợi ý: dùng luôn Document No_ làm PoId thay vì để rỗng
                            ColorId = detail.ColorNo,
                            ShadeId = null,
                            SizeId = detail.SizeNo,
                            PlannedQty = 0,
                            TargetDeliveryDate = targetDate,
                            DisposalAction = null,
                            DisposalNotes = null,
                            POName = null,     // không còn nguồn từ navPlans
                            FinishName = null, // không còn nguồn từ navPlans
                            CreatedAt = now,
                            IsActive = true
                        };
                        itemGroups[key] = (item, qty);
                    }
                }

                var newItems = new List<MesProductionOrderItem>();

                foreach (var (key, group) in itemGroups)
                {
                    if (existingItemsByKey.TryGetValue(key, out var existingItem))
                    {
                        // Đơn hàng (Style/PO/Color/Size) đã có ở MES -> không tạo mới, không ghi đè dữ liệu MES
                        if (existingItem.PlannedQty != group.totalQty)
                        {
                            mismatchedItems++;
                            itemWarnings.Add(new
                            {
                                factoryId = existingItem.FactoryId,
                                styleId = existingItem.StyleId,
                                poId = existingItem.PoId,
                                colorId = existingItem.ColorId,
                                sizeId = existingItem.SizeId,
                                mesPlannedQty = existingItem.PlannedQty,
                                erpPlannedQty = group.totalQty,
                                message = $"Đơn hàng Style={existingItem.StyleId}, PO={existingItem.PoId}, Color={existingItem.ColorId}, Size={existingItem.SizeId} có SL kế hoạch ở MES ({existingItem.PlannedQty}) khác với ERP ({group.totalQty}). Vui lòng kiểm tra lại.",
                            });
                        }
                        else
                        {
                            unchangedItems++;
                        }
                        continue;
                    }

                    // Chưa tồn tại ở MES -> tạo mới
                    group.item.PlannedQty = group.totalQty;
                    newItems.Add(group.item);
                }

                syncedItems = newItems.Count;

                if (newItems.Count > 0)
                    await _context.MesProductionOrderItems.AddRangeAsync(newItems);
                await _context.SaveChangesAsync();

                // ── 5. UPSERT factory plans + xoá/chèn lại lines ─────────────────────
                // Nhóm theo WorkCenterCode | ColorNo | ItemNo (bỏ WorkShiftCode, RowID vì không còn nguồn)
                var fpGroups = navDetails
                    .Select(d =>
                    {
                        var docSolKey = $"{d.DocumentNo}|{d.SolLineNo}";
                        itemNoByDocSolLine.TryGetValue(docSolKey, out var styleId);
                        return (Detail: d, StyleId: styleId ?? string.Empty);
                    })
                    .GroupBy(x => $"{x.Detail.WorkCenterCode}|{x.Detail.ColorNo}|{x.StyleId}|{x.Detail.DocumentNo}", StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var computedFpPlanCodes = fpGroups
                    .Select(g =>
                    {
                        var f = g.First();
                        return $"{f.StyleId}|{f.Detail.ColorNo}|{f.Detail.WorkCenterCode}|{f.Detail.DocumentNo}";
                    })
                    .Distinct()
                    .ToList();

                var existingFactoryPlanRows = await _context.MesFactoryPlans
                    .Where(fp => computedFpPlanCodes.Contains(fp.PlanCode)
                              && fp.IsActive == true
                              && fp.ParentPlanId == null)   // ✅ chỉ match dòng gốc, bỏ qua dòng đã bị tách (con)
                    .ToListAsync();

                // Phòng trường hợp dữ liệu không nhất quán có nhiều plan cùng PlanCode -> lấy bản mới nhất
                var existingFactoryPlanMap = existingFactoryPlanRows
                    .GroupBy(fp => fp.PlanCode, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.OrderByDescending(fp => fp.UpdatedAt).First(), StringComparer.OrdinalIgnoreCase);


                var newFactoryPlans = new List<MesFactoryPlan>();

                foreach (var fpGroup in fpGroups)
                {
                    var first = fpGroup.First();
                    var wkCenter = first.Detail.WorkCenterCode;
                    var fpStyleId = first.StyleId;
                    var colorNo = first.Detail.ColorNo;
                    var orderKey = $"{wkCenter}|{fpStyleId}";
                    var poNo = first.Detail.DocumentNo;
                    if (!orderIdMap.TryGetValue(orderKey, out var fpOrderId)) continue;
                    if (!orderIdToVersionId.TryGetValue(fpOrderId, out var fpVersionId)) continue;

                    var fpTotalQty = fpGroup.Sum(x => x.Detail.Quantity ?? 0); // = tổng theo màu (đúng theo mô tả: sum size = tổng)
                    var fpPlanCode = $"{fpStyleId}|{colorNo}|{wkCenter}|{poNo}";

                    Guid fpId;
                    if (existingFactoryPlanMap.TryGetValue(fpPlanCode, out var existingFp))
                    {
                        // Đã tồn tại ở MES -> chỉ cập nhật liên kết version, KHÔNG ghi đè SL kế hoạch/ngày sản xuất
                        // (MES có thể đã gom/tách PO hoặc chỉnh tay các giá trị này).
                        existingFp.VersionId = fpVersionId;
                        existingFp.UpdatedAt = now;
                        existingFp.IsActive = true;
                        fpId = existingFp.Id;

                        if (existingFp.TotalPlannedQty != fpTotalQty)
                        {
                            mismatchedFactoryPlans++;
                            factoryPlanWarnings.Add(new
                            {
                                factoryId = wkCenter,
                                styleId = fpStyleId,
                                poId = poNo,
                                colorId = colorNo,
                                mesPlannedQty = existingFp.TotalPlannedQty,
                                erpPlannedQty = fpTotalQty,
                                message = $"Factory plan Style={fpStyleId}, Color={colorNo}, Factory={wkCenter}, PO={poNo} có SL kế hoạch ở MES ({existingFp.TotalPlannedQty}) khác với ERP ({fpTotalQty}).",
                            });
                        }
                        else
                        {
                            updatedFactoryPlans++;
                        }
                    }
                    else
                    {
                        fpId = Guid.NewGuid();
                        newFactoryPlans.Add(new MesFactoryPlan
                        {
                            Id = fpId,
                            PlanCode = fpPlanCode,
                            ProductionOrderId = fpOrderId,
                            VersionId = fpVersionId,
                            FactoryId = wkCenter,
                            StyleId = fpStyleId ?? string.Empty,
                            PoId = poNo,
                            ColorId = colorNo,
                            LineNumber = null, // trước lấy từ WorkShiftCode, nay không còn nguồn
                            ProductionStartDate = null,
                            ProductionEndDate = null,
                            BufferDays = 0,
                            TotalPlannedQty = fpTotalQty,
                            Status = "normal",
                            CreatedBy = "sync",
                            CreatedAt = now,
                            UpdatedAt = now,
                            IsActive = true,
                        });
                        syncedFactoryPlans++;
                    }

                    // Mỗi dòng Size trong Detail = 1 Factory Plan Line (thay vì 1 dòng/ngày như trước)

                }

                if (newFactoryPlans.Count > 0)
                    await _context.MesFactoryPlans.AddRangeAsync(newFactoryPlans);
                await _context.SaveChangesAsync();



                var hasWarnings = itemWarnings.Count > 0 || factoryPlanWarnings.Count > 0;
                var message = $"Đồng bộ thành công: {syncedOrders} orders mới, {updatedOrders} orders cập nhật ";
                          //$"{syncedItems} items mới, {unchangedItems} items đã khớp, {syncedFactoryPlans} factory plans mới, {updatedFactoryPlans} factory plans đã khớp ";
                if (hasWarnings)
                {
                    message += $" CẢNH BÁO: {mismatchedItems} item và {mismatchedFactoryPlans} factory plan có SL kế hoạch lệch giữa MES và ERP , vui lòng kiểm tra lại để dữ liệu khớp nhau.";
                }

                return Ok(new
                {
                    success = true,
                    message,
                    hasWarnings,
                    synced = syncedOrders + updatedOrders + syncedItems + syncedFactoryPlans + updatedFactoryPlans + syncedFactoryPlanLines,
                    syncedOrders,
                    updatedOrders,
                    syncedItems,
                    unchangedItems,
                    mismatchedItems,
                    syncedFactoryPlans,
                    updatedFactoryPlans,
                    mismatchedFactoryPlans,
                    syncedFactoryPlanLines,
                    itemWarnings,
                    factoryPlanWarnings,
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Đồng bộ thất bại.",
                    detail = ex.Message,
                    stackTrace = ex.StackTrace,
                    innerDetail = ex.InnerException?.Message,
                    innerStack = ex.InnerException?.StackTrace,
                });
            }
        }

       

        //Không có PO
        //public async Task<IActionResult> SyncFromNav([FromQuery] DateOnly? fromDate, [FromQuery] DateOnly? toDate)
        //{
        //    try
        //    {
        //        var now = DateTime.UtcNow;
        //        int syncedOrders = 0;
        //        int updatedOrders = 0;
        //        int syncedItems = 0;
        //        int syncedFactoryPlans = 0;
        //        int updatedFactoryPlans = 0;
        //        int syncedFactoryPlanLines = 0;

        //        // ── 1. Đọc dữ liệu từ NAV ─────────────────────────────────────────────
        //        var navFactory = await _context.SalesOrderLineFactories
        //            .AsNoTracking()
        //            .Where(f => f.Status >= 2)
        //            .ToListAsync();

        //        var navPlans = await _context.SalesOrderLineFactoryPlans
        //            .AsNoTracking()
        //            .Where(p =>
        //                (!fromDate.HasValue || (p.StartingDate.HasValue && DateOnly.FromDateTime(p.StartingDate.Value.Date) >= fromDate.Value)) &&
        //                (!toDate.HasValue || (p.EndingDate.HasValue && DateOnly.FromDateTime(p.EndingDate.Value.Date) <= toDate.Value)))
        //            .ToListAsync();

        //        var navDetails = await _context.SalesOrderLineDetailFactories
        //            .AsNoTracking()
        //            .ToListAsync();

        //        var factoryDescLookup = navFactory
        //            .GroupBy(f => $"{f.WorkCenterCode}|{f.ItemNo}")
        //            .ToDictionary(g => g.Key, g => g.First());

        //        var itemNoByDocSolLine = navFactory
        //            .GroupBy(f => $"{f.DocumentNo}|{f.SolLineNo}", StringComparer.OrdinalIgnoreCase)
        //            .ToDictionary(g => g.Key, g => g.First().ItemNo ?? string.Empty, StringComparer.OrdinalIgnoreCase);

        //        var planLookupByWorkCenter = navPlans
        //            .Where(p => p.WorkCenterCode != null)
        //            .GroupBy(p => p.WorkCenterCode!, StringComparer.OrdinalIgnoreCase)
        //            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        //        // ── 2. UPSERT production_orders ───────────────────────────────────────
        //        var planGroups = navPlans
        //            .GroupBy(p => $"{p.WorkCenterCode}|{p.ItemNo}")
        //            .ToList();

        //        // Load toàn bộ orders hiện tại để upsert (không skip)
        //        var existingOrders = await _context.MesProductionOrders
        //            .ToDictionaryAsync(o => o.PlanCode, o => o, StringComparer.OrdinalIgnoreCase);

        //        var orderIdMap = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        //        var isNewOrderSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        //        var newOrders = new List<MesProductionOrder>();

        //        foreach (var group in planGroups)
        //        {
        //            var planKey = group.Key;
        //            factoryDescLookup.TryGetValue(planKey, out var factoryRow);

        //            var periodFrom = group
        //                .Where(p => p.StartingDate.HasValue)
        //                .Select(p => DateOnly.FromDateTime(p.StartingDate!.Value))
        //                .DefaultIfEmpty(DateOnly.FromDateTime(now))
        //                .Min();

        //            var periodTo = group
        //                .Where(p => p.EndingDate.HasValue)
        //                .Select(p => DateOnly.FromDateTime(p.EndingDate!.Value))
        //                .DefaultIfEmpty(DateOnly.FromDateTime(now))
        //                .Max();

        //            if (existingOrders.TryGetValue(planKey, out var existingOrder))
        //            {
        //                // CẬP NHẬT order đã tồn tại thay vì bỏ qua
        //                existingOrder.PlanPeriodFrom = periodFrom;
        //                existingOrder.PlanPeriodTo = periodTo;
        //                existingOrder.UpdatedAt = now;
        //                orderIdMap[planKey] = existingOrder.Id;
        //                updatedOrders++;
        //            }
        //            else
        //            {
        //                var order = new MesProductionOrder
        //                {
        //                    Id = Guid.NewGuid(),
        //                    PlanCode = planKey,
        //                    PlanName = factoryRow?.Description ?? planKey,
        //                    PlanPeriodFrom = periodFrom,
        //                    PlanPeriodTo = periodTo,
        //                    Status = "DRAFT",
        //                    CurrentVersion = 1,
        //                    ApprovedBy = null,
        //                    ApprovedAt = null,
        //                    Notes = null,
        //                    CreatedBy = "sync",
        //                    CreatedAt = now,
        //                    UpdatedAt = now,
        //                    IsActive = true,
        //                };
        //                newOrders.Add(order);
        //                orderIdMap[planKey] = order.Id;
        //                isNewOrderSet.Add(planKey);
        //                syncedOrders++;
        //            }
        //        }

        //        if (newOrders.Count > 0)
        //            await _context.MesProductionOrders.AddRangeAsync(newOrders);
        //        await _context.SaveChangesAsync();

        //        // ── 3. Versions ───────────────────────────────────────────────────────
        //        // Orders mới → tạo version 1
        //        // Orders đã tồn tại → dùng lại active version (update in-place), xoá items cũ
        //        var preExistingOrderIds = existingOrders.Values.Select(o => o.Id).ToList();

        //        var existingActiveVersionsList = preExistingOrderIds.Count > 0
        //            ? await _context.MesProductionOrderVersions
        //                .Where(v => preExistingOrderIds.Contains(v.ProductionOrderId) && v.VersionStatus == "ACTIVE")
        //                .ToListAsync()
        //            : new List<MesProductionOrderVersion>();

        //        // Phòng trường hợp dữ liệu không nhất quán có nhiều active version
        //        var existingActiveVersions = existingActiveVersionsList
        //            .GroupBy(v => v.ProductionOrderId)
        //            .ToDictionary(g => g.Key, g => g.OrderByDescending(v => v.VersionNumber).First());

        //        var orderIdToVersionId = new Dictionary<Guid, Guid>();
        //        var newVersions = new List<MesProductionOrderVersion>();

        //        foreach (var (planKey, orderId) in orderIdMap)
        //        {
        //            if (isNewOrderSet.Contains(planKey))
        //            {
        //                var versionId = Guid.NewGuid();
        //                newVersions.Add(new MesProductionOrderVersion
        //                {
        //                    Id = versionId,
        //                    ProductionOrderId = orderId,
        //                    VersionNumber = 1,
        //                    VersionStatus = "ACTIVE",
        //                    TriggerType = "MANUAL",
        //                    SourcePoType = "TEMP",
        //                    ErpPoId = planKey,
        //                    DeltaSummary = null,
        //                    SupersededBy = null,
        //                    CreatedBy = "sync",
        //                    CreatedAt = now,
        //                });
        //                orderIdToVersionId[orderId] = versionId;
        //            }
        //            else if (existingActiveVersions.TryGetValue(orderId, out var activeVer))
        //            {
        //                // Tái sử dụng version ID hiện tại, items sẽ được xoá và chèn lại bên dưới
        //                orderIdToVersionId[orderId] = activeVer.Id;
        //            }
        //        }

        //        if (newVersions.Count > 0)
        //            await _context.MesProductionOrderVersions.AddRangeAsync(newVersions);
        //        await _context.SaveChangesAsync();

        //        // Xoá items cũ của các active version sẽ được làm mới
        //        var versionIdsToRefresh = existingActiveVersions.Values.Select(v => v.Id).ToList();
        //        if (versionIdsToRefresh.Count > 0)
        //        {
        //            var staleItems = await _context.MesProductionOrderItems
        //                .Where(i => versionIdsToRefresh.Contains(i.VersionId))
        //                .ToListAsync();
        //            if (staleItems.Count > 0)
        //                _context.MesProductionOrderItems.RemoveRange(staleItems);
        //            await _context.SaveChangesAsync();
        //        }

        //        // ── 4. Items: chèn lại cho tất cả versions (mới + đã refresh) ─────────
        //        var itemGroups = new Dictionary<string, (MesProductionOrderItem item, int totalQty)>(StringComparer.OrdinalIgnoreCase);
        //        string Normalize(string? s) => (s ?? string.Empty).Trim().ToLowerInvariant();

        //        foreach (var detail in navDetails)
        //        {
        //            var docSolKey = $"{detail.DocumentNo}|{detail.SolLineNo}";
        //            itemNoByDocSolLine.TryGetValue(docSolKey, out var styleId);
        //            styleId ??= string.Empty;

        //            var wkCenter = detail.WorkCenterCode ?? string.Empty;
        //            var planKey = $"{wkCenter}|{styleId}";
        //            if (!orderIdMap.TryGetValue(planKey, out var orderId)) continue;
        //            if (!orderIdToVersionId.TryGetValue(orderId, out var versionId)) continue;

        //            planLookupByWorkCenter.TryGetValue(wkCenter, out var planLine);

        //            var qty = Math.Max(0, detail.Quantity ?? 0);
        //            var targetDate = planLine?.ShippingDate.HasValue == true
        //                ? DateOnly.FromDateTime(planLine.ShippingDate!.Value)
        //                : DateOnly.FromDateTime(now);

        //            var key = $"{versionId:N}|{Normalize(wkCenter)}|{Normalize(styleId)}|{Normalize(detail.DocumentNo)}|{Normalize(detail.ColorNo)}|{Normalize(detail.SizeNo)}";

        //            if (itemGroups.TryGetValue(key, out var existingItem))
        //            {
        //                itemGroups[key] = (existingItem.item, existingItem.totalQty + qty);
        //            }
        //            else
        //            {
        //                var item = new MesProductionOrderItem
        //                {
        //                    Id = Guid.NewGuid(),
        //                    VersionId = versionId,
        //                    FactoryId = wkCenter,
        //                    StyleId = styleId,
        //                    PoId = "",
        //                    ColorId = detail.ColorNo,
        //                    ShadeId = null,
        //                    SizeId = detail.SizeNo,
        //                    PlannedQty = 0,
        //                    TargetDeliveryDate = targetDate,
        //                    DisposalAction = null,
        //                    DisposalNotes = null,
        //                    POName = planLine?.PoName,
        //                    FinishName = planLine?.FinishName,
        //                    CreatedAt = now,
        //                };
        //                itemGroups[key] = (item, qty);
        //            }
        //        }

        //        var newItems = itemGroups.Values.Select(g =>
        //        {
        //            g.item.PlannedQty = g.totalQty;
        //            return g.item;
        //        }).ToList();

        //        syncedItems = newItems.Count;

        //        if (newItems.Count > 0)
        //            await _context.MesProductionOrderItems.AddRangeAsync(newItems);
        //        await _context.SaveChangesAsync();

        //        // ── 5. UPSERT factory plans + xoá/chèn lại lines ─────────────────────
        //        var fpGroups = navPlans
        //            .GroupBy(p => $"{p.WorkCenterCode}|{p.ColorNo}|{p.WorkShiftCode}|{p.ItemNo}")
        //            .ToList();

        //        // Tính trước PlanCode để load existing factory plans
        //        var computedFpPlanCodes = fpGroups
        //            .Select(g =>
        //            {
        //                var f = g.First();
        //                return $"{f.ItemNo}|{f.ColorNo}|{f.WorkCenterCode}|{f.WorkShiftCode}|{f.RowID}";
        //            })
        //            .Distinct()
        //            .ToList();

        //        var existingFactoryPlanMap = await _context.MesFactoryPlans
        //            .Where(fp => computedFpPlanCodes.Contains(fp.PlanCode))
        //            .ToDictionaryAsync(fp => fp.PlanCode, fp => fp, StringComparer.OrdinalIgnoreCase);

        //        // Xoá lines cũ của các factory plans sẽ được cập nhật
        //        var fpIdsToRefresh = existingFactoryPlanMap.Values.Select(fp => fp.Id).ToList();
        //        if (fpIdsToRefresh.Count > 0)
        //        {
        //            var staleLines = await _context.MesFactoryPlanLines
        //                .Where(l => fpIdsToRefresh.Contains(l.FactoryPlanId))
        //                .ToListAsync();
        //            if (staleLines.Count > 0)
        //                _context.MesFactoryPlanLines.RemoveRange(staleLines);
        //            await _context.SaveChangesAsync();
        //        }

        //        var newFactoryPlans = new List<MesFactoryPlan>();
        //        var allNewFactoryPlanLines = new List<MesFactoryPlanLine>(); // cho cả plans mới lẫn updated

        //        foreach (var fpGroup in fpGroups)
        //        {
        //            var firstPlan = fpGroup.First();
        //            var wkCenter = firstPlan.WorkCenterCode;
        //            var fpStyleId = firstPlan.ItemNo;
        //            var orderKey = $"{wkCenter}|{fpStyleId}";
        //            var rowID = firstPlan.RowID;

        //            if (!orderIdMap.TryGetValue(orderKey, out var fpOrderId)) continue;
        //            if (!orderIdToVersionId.TryGetValue(fpOrderId, out var fpVersionId)) continue;

        //            var workShift = firstPlan.WorkShiftCode ?? string.Empty;
        //            var colorNo = firstPlan.ColorNo;

        //            var fpStartDate = fpGroup
        //                .Where(p => p.StartingDate.HasValue)
        //                .Select(p => DateOnly.FromDateTime(p.StartingDate!.Value))
        //                .DefaultIfEmpty(DateOnly.FromDateTime(now))
        //                .Min();

        //            var fpEndDate = fpGroup
        //                .Where(p => p.EndingDate.HasValue)
        //                .Select(p => DateOnly.FromDateTime(p.EndingDate!.Value))
        //                .DefaultIfEmpty(fpStartDate)
        //                .Max();

        //            var fpTotalQty = fpGroup.Sum(p => p.PlanQuantity ?? 0);
        //            var fpPlanCode = $"{fpStyleId}|{colorNo}|{wkCenter}|{workShift}|{rowID}";

        //            Guid fpId;
        //            if (existingFactoryPlanMap.TryGetValue(fpPlanCode, out var existingFp))
        //            {
        //                // CẬP NHẬT factory plan đã tồn tại
        //                existingFp.VersionId = fpVersionId;
        //                existingFp.ProductionStartDate = fpStartDate;
        //                existingFp.ProductionEndDate = fpEndDate;
        //                existingFp.TotalPlannedQty = fpTotalQty;
        //                existingFp.UpdatedAt = now;
        //                existingFp.IsActive = true;
        //                fpId = existingFp.Id;
        //                updatedFactoryPlans++;
        //            }
        //            else
        //            {
        //                fpId = Guid.NewGuid();
        //                newFactoryPlans.Add(new MesFactoryPlan
        //                {
        //                    Id = fpId,
        //                    PlanCode = fpPlanCode,
        //                    ProductionOrderId = fpOrderId,
        //                    VersionId = fpVersionId,
        //                    FactoryId = wkCenter,
        //                    StyleId = fpStyleId ?? string.Empty,
        //                    PoId = "",
        //                    ColorId = colorNo,
        //                    LineNumber = workShift,
        //                    ProductionStartDate = fpStartDate,
        //                    ProductionEndDate = fpEndDate,
        //                    BufferDays = 0,
        //                    TotalPlannedQty = fpTotalQty,
        //                    Status = "DRAFT",
        //                    CreatedBy = "sync",
        //                    CreatedAt = now,
        //                    UpdatedAt = now,
        //                    IsActive = true,
        //                });
        //                syncedFactoryPlans++;
        //            }

        //            // Luôn tạo lại lines (dù plan mới hay đã cập nhật)
        //            var allDates = new HashSet<DateTime>();
        //            foreach (var planRow in fpGroup)
        //            {
        //                var rowStart = (planRow.StartingDate ?? now).Date;
        //                var rowEnd = (planRow.EndingDate ?? rowStart).Date;
        //                if (rowEnd < rowStart) rowEnd = rowStart;
        //                for (var d = rowStart; d <= rowEnd; d = d.AddDays(1))
        //                    allDates.Add(d);
        //            }

        //            foreach (var d in allDates.OrderBy(x => x))
        //            {
        //                allNewFactoryPlanLines.Add(new MesFactoryPlanLine
        //                {
        //                    Id = Guid.NewGuid(),
        //                    FactoryPlanId = fpId,
        //                    SizeId = string.Empty,
        //                    PlannedQty = fpGroup.Select(e => e.PlanCapacityQty).FirstOrDefault() ?? 0,
        //                    DemandCreatedQty = 0,
        //                    CreatedAt = d,
        //                    UpdatedAt = d,
        //                });
        //                syncedFactoryPlanLines++;
        //            }
        //        }

        //        if (newFactoryPlans.Count > 0)
        //            await _context.MesFactoryPlans.AddRangeAsync(newFactoryPlans);
        //        await _context.SaveChangesAsync();

        //        if (allNewFactoryPlanLines.Count > 0)
        //            await _context.MesFactoryPlanLines.AddRangeAsync(allNewFactoryPlanLines);
        //        await _context.SaveChangesAsync();

        //        // ── 6. Upsert QLCL_ProductionPlan (mới + đã cập nhật) ───────────────
        //        int insertedProductionPlans = 0;
        //        int updatedProductionPlans = 0;

        //        try
        //        {
        //            // Gom cả factory plans mới lẫn đã tồn tại (đã được EF track & update)
        //            var allFactoryPlansById = newFactoryPlans
        //                .Concat(existingFactoryPlanMap.Values)
        //                .GroupBy(p => p.Id)
        //                .ToDictionary(g => g.Key, g => g.First());

        //            foreach (var line in allNewFactoryPlanLines)
        //            {
        //                if (!allFactoryPlansById.TryGetValue(line.FactoryPlanId, out var fp))
        //                    continue;

        //                var planDate = line.CreatedAt.Date;

        //                var existingPp = await _context.ProductionPlans.FirstOrDefaultAsync(pp =>
        //                    pp.WorkCenterCode == fp.FactoryId &&
        //                    pp.WorkShiftCode == fp.LineNumber &&
        //                    pp.ItemNo == fp.StyleId &&
        //                    pp.ColorNo == fp.ColorId &&
        //                    pp.ActualStartingDate.HasValue &&
        //                    pp.ActualStartingDate.Value.Date == planDate);

        //                if (existingPp != null)
        //                {
        //                    existingPp.TargetQuantityPerDay = line.PlannedQty;
        //                    existingPp.ActualStartingDate = line.CreatedAt;
        //                    existingPp.PlanQuantity = fp.TotalPlannedQty;
        //                    existingPp.ErpPlanID = line.Id.ToString();
        //                    updatedProductionPlans++;
        //                }
        //                else
        //                {
        //                    var newPp = new ProductionPlan
        //                    {
        //                        SalesOrderNo = null,
        //                        WorkCenterCode = fp.FactoryId,
        //                        WorkShiftCode = fp.LineNumber,
        //                        ItemNo = fp.StyleId,
        //                        ColorNo = fp.ColorId,
        //                        PlanQuantity = fp.TotalPlannedQty,
        //                        TargetQuantityPerDay = line.PlannedQty,
        //                        ActualStartingDate = line.CreatedAt,
        //                        DocumentDate = now,
        //                        POName = null,
        //                        ItemName = null,
        //                        WorkCenterName = null,
        //                        WorkShiftName = null,
        //                        ParentNo = null,
        //                        ErpPlanID = line.Id.ToString(),
        //                    };
        //                    await _context.ProductionPlans.AddAsync(newPp);
        //                    insertedProductionPlans++;
        //                }
        //            }

        //            await _context.SaveChangesAsync();
        //        }
        //        catch (Exception ex)
        //        {
        //            Console.Error.WriteLine("Error while upserting QLCL_ProductionPlan: " + ex.Message);
        //        }

        //        return Ok(new
        //        {
        //            success = true,
        //            message = $"Đồng bộ thành công: {syncedOrders} orders mới, {updatedOrders} orders cập nhật, " +
        //                      $"{syncedItems} items, {syncedFactoryPlans} factory plans mới, {updatedFactoryPlans} factory plans cập nhật, " +
        //                      $"{syncedFactoryPlanLines} lines. ProductionPlan: +{insertedProductionPlans} thêm mới, {updatedProductionPlans} cập nhật.",
        //            synced = syncedOrders + updatedOrders + syncedItems + syncedFactoryPlans + updatedFactoryPlans + syncedFactoryPlanLines,
        //            syncedOrders,
        //            updatedOrders,
        //            syncedItems,
        //            syncedFactoryPlans,
        //            updatedFactoryPlans,
        //            syncedFactoryPlanLines,
        //            insertedProductionPlans,
        //            updatedProductionPlans,
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        return BadRequest(new
        //        {
        //            success = false,
        //            message = "Đồng bộ thất bại.",
        //            detail = ex.Message,
        //            stackTrace = ex.StackTrace,
        //            innerDetail = ex.InnerException?.Message,
        //            innerStack = ex.InnerException?.StackTrace,
        //        });
        //    }
        //}

        //public async Task<IActionResult> SyncFromNav()
        //{
        //    try
        //    {
        //        var now = DateTime.UtcNow;
        //        int syncedOrders = 0;
        //        int syncedItems = 0;

        //        // ── 1. Đọc dữ liệu nguồn từ NAV ──────────────────────────────────────
        //        // Đọc toàn bộ về client rồi lọc (tránh SqlNullValueException khi EF dịch Contains sang SQL)
        //        var navFactoryAll = await _context.SalesOrderLineFactories
        //            .AsNoTracking()
        //            .ToListAsync();

        //        // Chỉ lấy SalesOrderLineFactory có Status = 2
        //        var navFactory = navFactoryAll
        //            .Where(f => f.Status >= 2)
        //            .ToList();

        //        // Tập DocumentNo hợp lệ (có trong navFactory Status=2)
        //        var validDocNos = navFactory
        //            .Select(f => f.DocumentNo)
        //            .Where(d => d != null)
        //            .Select(d => d!)
        //            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        //        var navPlansAll = await _context.SalesOrderLineFactoryPlans
        //            .AsNoTracking()
        //            .ToListAsync();
        //        var navPlans = navPlansAll
        //            .Where(p => p.SalesOrderNo != null && validDocNos.Contains(p.SalesOrderNo))
        //            .ToList();

        //        var navDetailsAll = await _context.SalesOrderLineDetailFactories
        //            .AsNoTracking()
        //            .ToListAsync();
        //        var navDetails = navDetailsAll
        //            .Where(d => d.DocumentNo != null && validDocNos.Contains(d.DocumentNo))
        //            .ToList();

        //        // Lookup nhanh: DocumentNo → SalesOrderLineFactory đầu tiên (lấy thông tin chung)
        //        var factoryLookup = navFactory
        //            .GroupBy(f => f.DocumentNo)
        //            .ToDictionary(g => g.Key, g => g.First());

        //        // ── 2. Upsert mes.production_orders ──────────────────────────────────
        //        // Mỗi Document No_ (SalesOrderNo) = 1 production_order
        //        var planGroups = navPlans
        //            .GroupBy(p => p.SalesOrderNo)
        //            .ToList();

        //        // Load các plan_code đã tồn tại để quyết định insert/update
        //        var existingPlanCodes = await _context.MesProductionOrders
        //            .Select(o => o.PlanCode)
        //            .ToListAsync();
        //        var existingPlanCodeSet = new HashSet<string>(existingPlanCodes, StringComparer.OrdinalIgnoreCase);

        //        // Map plan_code → Id đã lưu (dùng khi tạo items)
        //        var existingOrderMap = await _context.MesProductionOrders
        //            .ToDictionaryAsync(o => o.PlanCode, o => o.Id);

        //        var newOrders = new List<MesProductionOrder>();

        //        foreach (var group in planGroups)
        //        {
        //            var docNo = group.Key;
        //            factoryLookup.TryGetValue(docNo, out var factoryRow);

        //            var periodFrom = group
        //                .Where(p => p.StartingDate.HasValue)
        //                .Select(p => DateOnly.FromDateTime(p.StartingDate!.Value))
        //                .DefaultIfEmpty(DateOnly.FromDateTime(now))
        //                .Min();

        //            var periodTo = group
        //                .Where(p => p.EndingDate.HasValue)
        //                .Select(p => DateOnly.FromDateTime(p.EndingDate!.Value))
        //                .DefaultIfEmpty(DateOnly.FromDateTime(now))
        //                .Max();

        //            //var statusInt = group.First().Status;
        //            var statusInt = 0;
        //            var statusStr = statusInt switch
        //            {
        //                0 => "DRAFT",
        //                1 => "IN_PROGRESS",
        //                2 => "COMPLETED",
        //                3 => "CANCELLED",
        //                _ => "DRAFT"
        //            };

        //            if (existingPlanCodeSet.Contains(docNo))
        //            {
        //                // Update
        //                var existing = await _context.MesProductionOrders
        //                    .FirstAsync(o => o.PlanCode == docNo);
        //                existing.PlanName = factoryRow?.Description ?? docNo;
        //                existing.PlanPeriodFrom = periodFrom;
        //                existing.PlanPeriodTo = periodTo;
        //                existing.Status = statusStr;
        //                existing.UpdatedAt = now;
        //                syncedOrders++;
        //            }
        //            else
        //            {
        //                // Insert
        //                var order = new MesProductionOrder
        //                {
        //                    Id = Guid.NewGuid(),
        //                    PlanCode = docNo,
        //                    PlanName = factoryRow?.Description ?? docNo,
        //                    PlanPeriodFrom = periodFrom,
        //                    PlanPeriodTo = periodTo,
        //                    Status = statusStr,
        //                    CurrentVersion = 1,
        //                    ApprovedBy = null,
        //                    ApprovedAt = null,
        //                    Notes = null,
        //                    CreatedBy = "sync",
        //                    CreatedAt = now,
        //                    UpdatedAt = now,
        //                    IsActive = true,
        //                };
        //                newOrders.Add(order);
        //                existingOrderMap[docNo] = order.Id;
        //                syncedOrders++;
        //            }
        //        }

        //        if (newOrders.Count > 0)
        //            await _context.MesProductionOrders.AddRangeAsync(newOrders);

        //        await _context.SaveChangesAsync();

        //        // ── 3. Upsert mes.production_order_versions ───────────────────────────
        //        // Mỗi production_order cần đúng 1 version (version_number=1) để items tham chiếu vào
        //        var allOrderIds = existingOrderMap.Values.ToList();
        //        var existingVersionMap = await _context.MesProductionOrderVersions
        //            .Where(v => allOrderIds.Contains(v.ProductionOrderId))
        //            .ToDictionaryAsync(v => v.ProductionOrderId, v => v.Id);

        //        var newVersions = new List<MesProductionOrderVersion>();
        //        // Map: production_order_id → version_id (dùng cho items bên dưới)
        //        var orderIdToVersionId = new Dictionary<Guid, Guid>();

        //        foreach (var (planCode, orderId) in existingOrderMap)
        //        {
        //            if (existingVersionMap.TryGetValue(orderId, out var existingVersionId))
        //            {
        //                orderIdToVersionId[orderId] = existingVersionId;
        //            }
        //            else
        //            {
        //                var versionId = Guid.NewGuid();
        //                newVersions.Add(new MesProductionOrderVersion
        //                {
        //                    Id = versionId,
        //                    ProductionOrderId = orderId,
        //                    VersionNumber = 1,
        //                    VersionStatus = "ACTIVE",
        //                    TriggerType = "MANUAL",
        //                    SourcePoType = "TEMP",
        //                    ErpPoId = planCode,
        //                    DeltaSummary = null,
        //                    SupersededBy = null,
        //                    CreatedBy = "sync",
        //                    CreatedAt = now,
        //                });
        //                orderIdToVersionId[orderId] = versionId;
        //            }
        //        }

        //        if (newVersions.Count > 0)
        //            await _context.MesProductionOrderVersions.AddRangeAsync(newVersions);

        //        await _context.SaveChangesAsync();

        //        // ── 4. Upsert mes.production_order_items ─────────────────────────────
        //        // Xóa toàn bộ items cũ (re-sync hoàn toàn) rồi insert lại
        //        var docNoList = existingOrderMap.Keys.ToList();
        //        var oldItems = await _context.MesProductionOrderItems
        //            .Where(i => docNoList.Contains(i.PoId))
        //            .ToListAsync();

        //        if (oldItems.Count > 0)
        //            _context.MesProductionOrderItems.RemoveRange(oldItems);

        //        // Lookup nhanh cho factory (style) và plan (shipping date)
        //        var factoryByDocSolLine = navFactory
        //            .GroupBy(f => $"{f.DocumentNo}|{f.SolLineNo}")
        //            .ToDictionary(g => g.Key, g => g.First());
        //        var planByDocColorSize = navPlans
        //            .GroupBy(p => $"{p.SalesOrderNo}")
        //            .ToDictionary(g => g.Key, g => g.First());

        //        // Group theo UQ_poi_key: (version_id, factory_id, style_id, po_id, color_id, size_id)
        //        // cộng dồn PlannedQty để tránh vi phạm unique constraint
        //        var itemGroups = new Dictionary<string, (MesProductionOrderItem item, int totalQty)>();

        //        foreach (var detail in navDetails)
        //        {
        //            if (!existingOrderMap.TryGetValue(detail.DocumentNo, out var orderId))
        //                continue;
        //            if (!orderIdToVersionId.TryGetValue(orderId, out var versionId))
        //                continue;

        //            factoryByDocSolLine.TryGetValue($"{detail.DocumentNo}|{detail.SolLineNo}", out var factoryLine);
        //            planByDocColorSize.TryGetValue($"{detail.DocumentNo}", out var planLine);

        //            var styleId = factoryLine?.ItemNo ?? string.Empty;
        //            var factoryId = detail.WorkCenterCode ?? string.Empty;
        //            var qty = Math.Max(0, detail.Quantity ?? 0); // CK_poi_planned_qty: >= 0
        //            var targetDate = planLine?.ShippingDate.HasValue == true
        //                ? DateOnly.FromDateTime(planLine.ShippingDate!.Value)
        //                : DateOnly.FromDateTime(now);

        //            // Key theo đúng các cột trong UQ_poi_key
        //            var key = $"{versionId}|{factoryId}|{styleId}|{detail.DocumentNo}|{detail.ColorNo}|{detail.SizeNo}";

        //            if (itemGroups.TryGetValue(key, out var existing))
        //            {
        //                itemGroups[key] = (existing.item, existing.totalQty + qty);
        //            }
        //            else
        //            {
        //                itemGroups[key] = (new MesProductionOrderItem
        //                {
        //                    Id = Guid.NewGuid(),
        //                    VersionId = versionId,
        //                    FactoryId = factoryId,
        //                    StyleId = styleId,
        //                    PoId = detail.DocumentNo,
        //                    ColorId = detail.ColorNo,
        //                    ShadeId = null,
        //                    SizeId = detail.SizeNo,
        //                    PlannedQty = 0, // sẽ cập nhật bên dưới
        //                    TargetDeliveryDate = targetDate,
        //                    DisposalAction = null,
        //                    DisposalNotes = null,
        //                    POName = planLine?.PoName,
        //                    FinishName = planLine?.FinishName,
        //                    CreatedAt = now,
        //                }, qty);
        //            }
        //        }

        //        var newItems = itemGroups.Values.Select(g =>
        //        {
        //            g.item.PlannedQty = g.totalQty;
        //            return g.item;
        //        }).ToList();
        //        syncedItems = newItems.Count;

        //        if (newItems.Count > 0)
        //            await _context.MesProductionOrderItems.AddRangeAsync(newItems);

        //        await _context.SaveChangesAsync();

        //        // ── 5. Upsert mes.factory_plans + mes.factory_plan_lines ─────────────
        //        // Dùng ExecuteDeleteAsync để xóa thẳng trên DB — tránh load entity về (có thể có NULL column)
        //        // Xóa lines trước (FK), rồi xóa plans
        //        var fpIdsToDelete = await _context.MesFactoryPlans
        //            .Where(p => docNoList.Contains(p.PoId))
        //            .Select(p => p.Id)
        //            .ToListAsync();

        //        if (fpIdsToDelete.Count > 0)
        //        {
        //            await _context.MesFactoryPlanLines
        //                .Where(l => fpIdsToDelete.Contains(l.FactoryPlanId))
        //                .ExecuteDeleteAsync();

        //            await _context.MesFactoryPlans
        //                .Where(p => fpIdsToDelete.Contains(p.Id))
        //                .ExecuteDeleteAsync();
        //        }

        //        // No longer need a separate lookup: `fpGroups` is already grouped by ItemNo,
        //        // so we will take styleId directly from the grouped records (firstPlan.ItemNo).

        //        // Mỗi (SalesOrderNo, WorkCenterCode, ColorNo, WorkShiftCode) → 1 factory_plan
        //        var fpGroups = navPlans
        //            .GroupBy(p => $"{p.SalesOrderNo}|{p.WorkCenterCode}|{p.ColorNo}|{p.WorkShiftCode}|{p.ItemNo}")
        //            .ToList();

        //        int syncedFactoryPlans = 0;
        //        int syncedFactoryPlanLines = 0;
        //        var newFactoryPlans = new List<MesFactoryPlan>();
        //        var newFactoryPlanLines = new List<MesFactoryPlanLine>();

        //        foreach (var fpGroup in fpGroups)
        //        {
        //            var firstPlan = fpGroup.First();
        //            var fpDocNo = firstPlan.SalesOrderNo;
        //            var wkCenter = firstPlan.WorkCenterCode;
        //            var workShift = firstPlan.WorkShiftCode ?? string.Empty;
        //            var colorNo = firstPlan.ColorNo;
        //            if (!existingOrderMap.TryGetValue(fpDocNo, out var fpOrderId)) continue;
        //            if (!orderIdToVersionId.TryGetValue(fpOrderId, out var fpVersionId)) continue;

        //            // lấy styleId trực tiếp từ dữ liệu nhóm (fpGroup đã bao gồm ItemNo)
        //            var fpStyleId = firstPlan.ItemNo;

        //            var fpStartDate = fpGroup
        //                .Where(p => p.StartingDate.HasValue)
        //                .Select(p => DateOnly.FromDateTime(p.StartingDate!.Value))
        //                .DefaultIfEmpty(DateOnly.FromDateTime(now))
        //                .Min();

        //            var fpEndDate = fpGroup
        //                .Where(p => p.EndingDate.HasValue)
        //                .Select(p => DateOnly.FromDateTime(p.EndingDate!.Value))
        //                .DefaultIfEmpty(fpStartDate)
        //                .Max();

        //            var fpTotalQty = fpGroup.Sum(p => p.PlanQuantity ?? 0);
        //            // plan_code: unique theo (docNo, styleId, colorNo, workShift)
        //            var fpPlanCode = $"{fpDocNo}|{fpStyleId}|{colorNo}||{wkCenter}|{workShift}";
        //            var fpId = Guid.NewGuid();

        //            newFactoryPlans.Add(new MesFactoryPlan
        //            {
        //                Id = fpId,
        //                PlanCode = fpPlanCode,
        //                ProductionOrderId = fpOrderId,
        //                VersionId = fpVersionId,
        //                FactoryId = wkCenter,
        //                StyleId = fpStyleId ?? string.Empty,
        //                PoId = fpDocNo,
        //                ColorId = colorNo,
        //                LineNumber = workShift,
        //                ProductionStartDate = fpStartDate,
        //                ProductionEndDate = fpEndDate,
        //                BufferDays = 0,
        //                TotalPlannedQty = fpTotalQty,
        //                Status = "DRAFT",
        //                CreatedBy = "sync",
        //                CreatedAt = now,
        //                UpdatedAt = now,
        //                IsActive = true,
        //            });

        //            // SizeNo không sử dụng → 1 line per ngày (CreatedAt), PlannedQty = tổng các planRow active ngày đó
        //            // Gom tất cả ngày unique trong toàn group
        //            var allDates = new HashSet<DateTime>();
        //            foreach (var planRow in fpGroup)
        //            {
        //                var rowStart = (planRow.StartingDate ?? now).Date;
        //                var rowEnd = (planRow.EndingDate ?? rowStart).Date;
        //                if (rowEnd < rowStart) rowEnd = rowStart;
        //                for (var d = rowStart; d <= rowEnd; d = d.AddDays(1))
        //                    allDates.Add(d);
        //            }

        //            foreach (var d in allDates.OrderBy(x => x))
        //            {
        //                // PlanCapacityQty = năng lực sản xuất mỗi ngày của ca (Work Shift)
        //                var qtyForDay = fpGroup
        //                    .Where(p => (p.StartingDate ?? now).Date <= d && d <= (p.EndingDate ?? (p.StartingDate ?? now)).Date)
        //                    .Sum(p => p.PlanCapacityQty ?? 0);

        //                newFactoryPlanLines.Add(new MesFactoryPlanLine
        //                {
        //                    Id = Guid.NewGuid(),
        //                    FactoryPlanId = fpId,
        //                    SizeId = string.Empty,
        //                    PlannedQty = qtyForDay,
        //                    DemandCreatedQty = 0,
        //                    CreatedAt = d,
        //                    UpdatedAt = d,
        //                });
        //                syncedFactoryPlanLines++;
        //            }
        //            syncedFactoryPlans++;
        //        }

        //        // Lưu factory_plans TRƯỚC để FK hợp lệ khi insert lines
        //        if (newFactoryPlans.Count > 0)
        //            await _context.MesFactoryPlans.AddRangeAsync(newFactoryPlans);
        //        await _context.SaveChangesAsync();

        //        // Sau đó mới lưu factory_plan_lines
        //        if (newFactoryPlanLines.Count > 0)
        //            await _context.MesFactoryPlanLines.AddRangeAsync(newFactoryPlanLines);
        //        await _context.SaveChangesAsync();

        //        // ── 6. Upsert QLCL_ProductionPlan (tương ứng với mỗi factory_plan_line) ──
        //        int insertedProductionPlans = 0;
        //        int updatedProductionPlans = 0;

        //        try
        //        {
        //            // Map factoryPlanId -> factory plan object
        //            var fpMap = newFactoryPlans.ToDictionary(p => p.Id);

        //            foreach (var line in newFactoryPlanLines)
        //            {
        //                if (!fpMap.TryGetValue(line.FactoryPlanId, out var fp))
        //                    continue;

        //                var planDate = line.CreatedAt.Date;

        //                // Tìm bản ghi QLCL_ProductionPlan tương ứng
        //                var existing = await _context.ProductionPlans.FirstOrDefaultAsync(pp =>
        //                    pp.SalesOrderNo == fp.PoId &&
        //                    pp.WorkCenterCode == fp.FactoryId &&
        //                    pp.WorkShiftCode == fp.LineNumber &&
        //                    pp.ItemNo == fp.StyleId &&
        //                    pp.ColorNo == fp.ColorId &&
        //                    pp.ActualStartingDate.HasValue &&
        //                    pp.ActualStartingDate.Value.Date == planDate);

        //                if (existing != null)
        //                {
        //                    existing.TargetQuantityPerDay = line.PlannedQty;
        //                    existing.ActualStartingDate = line.CreatedAt;
        //                    existing.PlanQuantity = fp.TotalPlannedQty;
        //                    existing.ErpPlanID = line.Id.ToString();
        //                    updatedProductionPlans++;
        //                }
        //                else
        //                {
        //                    var newPp = new ProductionPlan
        //                    {
        //                        SalesOrderNo = fp.PoId,
        //                        WorkCenterCode = fp.FactoryId,
        //                        WorkShiftCode = fp.LineNumber,
        //                        ItemNo = fp.StyleId,
        //                        ColorNo = fp.ColorId,
        //                        PlanQuantity = fp.TotalPlannedQty,
        //                        TargetQuantityPerDay = line.PlannedQty,
        //                        ActualStartingDate = line.CreatedAt,
        //                        DocumentDate = now,
        //                        POName = null,
        //                        ItemName = null,
        //                        WorkCenterName = null,
        //                        WorkShiftName = null,
        //                        ParentNo = null,
        //                        ErpPlanID = line.Id.ToString(),
        //                    };
        //                    await _context.ProductionPlans.AddAsync(newPp);
        //                    insertedProductionPlans++;
        //                }
        //            }

        //            await _context.SaveChangesAsync();
        //        }
        //        catch (Exception ex)
        //        {
        //            // Không block toàn bộ sync nếu upsert production plan lỗi — chỉ ghi log
        //            Console.Error.WriteLine("Error while upserting QLCL_ProductionPlan: " + ex.Message);
        //        }

        //        return Ok(new
        //        {
        //            success = true,
        //            message = $"Đồng bộ thành công: {syncedOrders} production orders, {syncedItems} items, {syncedFactoryPlans} factory plans, {syncedFactoryPlanLines} factory plan lines. ProductionPlan: +{insertedProductionPlans} inserted, {updatedProductionPlans} updated.",
        //            synced = syncedOrders + syncedItems + syncedFactoryPlans + syncedFactoryPlanLines,
        //            syncedOrders,
        //            syncedItems,
        //            syncedFactoryPlans,
        //            syncedFactoryPlanLines,
        //            insertedProductionPlans,
        //            updatedProductionPlans,
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        return BadRequest(new
        //        {
        //            success = false,
        //            message = "Đồng bộ thất bại.",
        //            detail = ex.Message,
        //            stackTrace = ex.StackTrace,
        //            innerDetail = ex.InnerException?.Message,
        //            innerStack = ex.InnerException?.StackTrace,
        //        });
        //    }
        //}

        // GET /api/production/order-items
        // Lấy danh sách production_order_items để người dùng chọn khi tạo factory plan
        [HttpGet("order-items")]
        [ProducesResponseType(typeof(object), 200)]
        public async Task<IActionResult> GetOrderItems(
            [FromQuery] string? factoryId,
            [FromQuery] string? poId,
            [FromQuery] string? styleId,
            [FromQuery] string? colorId)
        {
            var query = _context.MesProductionOrderItems
                .AsNoTracking()
                .Join(_context.MesProductionOrderVersions,
                    item    => item.VersionId,
                    version => version.Id,
                    (item, version) => new { item, version })
                .Join(_context.MesProductionOrders,
                    x     => x.version.ProductionOrderId,
                    order => order.Id,
                    (x, order) => new { x.item, x.version, order });

            if (!string.IsNullOrWhiteSpace(factoryId))
                query = query.Where(x => x.item.FactoryId == factoryId);
            if (!string.IsNullOrWhiteSpace(poId))
                query = query.Where(x => x.item.PoId == poId);
            if (!string.IsNullOrWhiteSpace(styleId))
                query = query.Where(x => x.item.StyleId == styleId);
            if (!string.IsNullOrWhiteSpace(colorId))
                query = query.Where(x => x.item.ColorId == colorId);

            var items = await query
                .OrderBy(x => x.item.PoId)
                .ThenBy(x => x.item.StyleId)
                .ThenBy(x => x.item.ColorId)
                .Select(x => new
                {
                    id                  = x.item.Id,
                    versionId           = x.item.VersionId,
                    productionOrderId   = x.version.ProductionOrderId,
                    planCode            = x.order.PlanCode,
                    planName            = x.order.PlanName,
                    factoryId           = x.item.FactoryId,
                    styleId             = x.item.StyleId,
                    poId                = x.item.PoId,
                    colorId             = x.item.ColorId,
                    sizeId              = x.item.SizeId,
                    plannedQty          = x.item.PlannedQty,
                    targetDeliveryDate  = x.item.TargetDeliveryDate,
                    poName              = x.item.POName,
                    finishName          = x.item.FinishName,
                })
                .ToListAsync();

            return Ok(items);
        }

        // GET /api/production/production-orders
        // Lấy danh sách production orders để chọn khi tạo factory plan.
        // ?search=FP-xxx  → tìm theo planCode, styleId, poId (contains, không phân biệt hoa/thường)
        [HttpGet("production-orders")]
        [ProducesResponseType(typeof(object), 200)]
        public async Task<IActionResult> GetProductionOrders([FromQuery] string? search)
        {
            // Join: production_orders → versions → items (lấy 1 item đại diện để lấy factoryId, styleId, poId, colorId)
            var query = _context.MesProductionOrders
                .AsNoTracking()
                .Where(o => o.IsActive)
                .Join(_context.MesProductionOrderVersions,
                    o => o.Id,
                    v => v.ProductionOrderId,
                    (o, v) => new { order = o, version = v })
                .GroupJoin(_context.MesProductionOrderItems,
                    x => x.version.Id,
                    item => item.VersionId,
                    (x, items) => new { x.order, x.version, items })
                .SelectMany(
                    x => x.items.DefaultIfEmpty(),
                    (x, item) => new { x.order, x.version, item });

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(x =>
                    x.order.PlanCode.ToLower().Contains(s) ||
                    (x.item != null && x.item.StyleId.ToLower().Contains(s)) ||
                    (x.item != null && x.item.PoId.ToLower().Contains(s)));
            }

            // Lấy 1 item đại diện (group by order) — lấy item có PlannedQty lớn nhất
            var rows = await query
                .GroupBy(x => new { OrderId = x.order.Id, x.order.PlanCode, x.order.PlanName, VersionId = x.version.Id })
                .Select(g => new
                {
                    id                  = g.Key.OrderId,
                    planCode            = g.Key.PlanCode,
                    planName            = g.Key.PlanName,
                    versionId           = g.Key.VersionId,
                    factoryId           = g.Where(x => x.item != null).Select(x => x.item!.FactoryId).FirstOrDefault(),
                    styleId             = g.Where(x => x.item != null).Select(x => x.item!.StyleId).FirstOrDefault(),
                    style               = g.Where(x => x.item != null).Select(x => x.item!.FinishName).FirstOrDefault(),
                    poId                = g.Where(x => x.item != null).Select(x => x.item!.PoId).FirstOrDefault(),
                    poName              = g.Where(x => x.item != null).Select(x => x.item!.POName).FirstOrDefault(),
                    colorId             = g.Where(x => x.item != null).Select(x => x.item!.ColorId).FirstOrDefault(),
                    totalPlannedQty     = g.Where(x => x.item != null).Sum(x => x.item!.PlannedQty),
                    targetDeliveryDate  = g.Where(x => x.item != null)
                                          .Select(x => (DateOnly?)x.item!.TargetDeliveryDate)
                                          .OrderByDescending(d => d)
                                          .FirstOrDefault(),
                })
                .OrderByDescending(r => r.planCode)
                .Take(100)
                .ToListAsync();

            return Ok(rows);
        }

        [HttpPut("production-orders/{versionId}")]
        [ProducesResponseType(typeof(object), 200)]
        public async Task<IActionResult> Update_SLKH(
        [FromRoute] Guid versionId,
        [FromBody] UpdateSizeQtyRequest request)
        {
            // 1. Validate payload cơ bản
            if (request.SizeIds == null || request.SizeIds.Count == 0)
                return BadRequest(new { success = false, message = "Danh sách size không được để trống" });

            if (request.SizeIds.Any(s => s.PlannedQty < 0))
                return BadRequest(new { success = false, message = "Số lượng không được nhỏ hơn 0" });

            var duplicated = request.SizeIds
                .GroupBy(s => s.SizeId)
                .Any(g => g.Count() > 1);
            if (duplicated)
                return BadRequest(new { success = false, message = "Danh sách size bị trùng lặp" });

            // 2. Lấy các dòng size hiện tại (production_order_items) đúng version + style + po + color
            var items = await _context.MesProductionOrderItems
                .Where(e => e.VersionId == versionId
                         && e.StyleId == request.StyleId
                         && e.PoId == request.PoId
                         && e.ColorId == request.ColorId)
                .ToListAsync();

            if (items == null || items.Count == 0)
                return NotFound(new { success = false, message = "Không tìm thấy đơn hàng tương ứng" });

            // 3. Validate tập size gửi lên phải khớp tập size gốc (không thêm/bớt size)
            var currentSizeIds = items.Select(s => s.SizeId).ToHashSet();
            var incomingSizeIds = request.SizeIds.Select(s => s.SizeId).ToHashSet();
            if (!currentSizeIds.SetEquals(incomingSizeIds))
                return BadRequest(new { success = false, message = "Danh sách size không khớp với đơn hàng gốc" });

            // 4. Tính tổng mới (KH tổng)
            var newKhTong = request.SizeIds.Sum(s => s.PlannedQty);

            // 5. Lấy toàn bộ dòng factory_plans của đơn hàng này (gồm dòng tổng + các dòng đã gán tổ)
            var plans = await _context.MesFactoryPlans
                .Where(e => e.VersionId == versionId && e.IsActive==true
                         && e.StyleId == request.StyleId
                         && e.PoId == request.PoId
                         && e.ColorId == request.ColorId)
                .ToListAsync();

            if (plans == null || plans.Count == 0)
                return NotFound(new { success = false, message = "Không tìm thấy kế hoạch tương ứng" });

            // Dòng tổng gốc: line_number IS NULL
            var masterPlan = plans.FirstOrDefault(p => string.IsNullOrEmpty(p.LineNumber));
            if (masterPlan == null)
                return NotFound(new { success = false, message = "Không tìm thấy dòng kế hoạch tổng (chưa gán tổ)" });

            // Các dòng đã gán tổ: line_number khác NULL
            var assignedPlans = plans.Where(p => !string.IsNullOrEmpty(p.LineNumber)).ToList();
            var daGanTong = assignedPlans.Sum(p =>
    !string.IsNullOrEmpty(p.OperationCode)
        ? p.TotalPlannedQty / 2m
        : p.TotalPlannedQty
);


            // 6. Không cho giảm tổng KH xuống dưới số đã gán tổ
            if (newKhTong < daGanTong)
            {
                return Conflict(new
                {
                    success = false,
                    message = $"Tổng số lượng mới ({newKhTong}) nhỏ hơn số đã phân bổ cho tổ ({daGanTong}). Cần chỉnh lại số lượng phân bổ cho tổ"
                });
            }

            // 7. Cập nhật dữ liệu trong transaction
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 7a. Cập nhật planned_qty từng size
                foreach (var sizeItem in request.SizeIds)
                {
                    var entity = items.First(i => i.SizeId == sizeItem.SizeId);
                    entity.PlannedQty = sizeItem.PlannedQty;
                }

                // 7b. Cập nhật dòng tổng gốc bên factory_plans
                var conLaiChuaPhanBo = newKhTong - daGanTong;
                masterPlan.TotalPlannedQty = newKhTong;
                masterPlan.IsActive = conLaiChuaPhanBo > 0; // còn dư => active, hết dư => inactive
                masterPlan.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new
                {
                    success = true,
                    message = "Cập nhật số lượng kế hoạch thành công",
                    data = new
                    {
                        versionId,
                        styleId = request.StyleId,
                        poId = request.PoId,
                        colorId = request.ColorId,
                        khTong = newKhTong,
                        daGanTong,
                        conLaiChuaPhanBo
                    }
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { success = false, message = "Lỗi khi lưu dữ liệu: " + ex.Message });
            }
        }

        // Kết quả tra cứu tên -> mã. Key là tên (không phân biệt hoa thường); lỗi cũng lưu theo tên.
        private sealed class StylePoNameResolution
        {
            public Dictionary<string, string> StyleIds { get; } = new(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, string> StyleErrors { get; } = new(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, string> PoIds { get; } = new(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, string> PoErrors { get; } = new(StringComparer.OrdinalIgnoreCase);
        }

        // Tên Style -> StyleId (Item.Name -> Item.No).
        // Tên PO -> PoId (SalesOrderHeader."PO Customer Name" -> No; không có thì fallback mes.production_order_items.POName -> PoId).
        // Không khớp hoặc khớp nhiều mã đều ghi lỗi theo tên.
        private async Task<StylePoNameResolution> ResolveStyleAndPoNamesAsync(IEnumerable<string> styleNames, IEnumerable<string> poNames)
        {
            var result = new StylePoNameResolution();
            var styles = styleNames.Where(s => !string.IsNullOrEmpty(s)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var pos = poNames.Where(s => !string.IsNullOrEmpty(s)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            var itemCandidates = await _context.Item
                .Where(i => styles.Contains(i.Name))
                .Select(i => new { i.Name, i.No })
                .ToListAsync();
            var salesOrderCandidates = await _context.SalesOrderHeader
                .Where(s => pos.Contains(s.POName))
                .Select(s => new { s.No, s.POName })
                .ToListAsync();
            var poItemCandidates = await _context.MesProductionOrderItems
                .Where(i => i.IsActive != false && pos.Contains(i.POName))
                .Select(i => new { i.PoId, i.POName })
                .ToListAsync();

            foreach (var name in styles)
            {
                var ids = itemCandidates
                    .Where(i => SameName(i.Name, name))
                    .Select(i => i.No?.Trim() ?? string.Empty)
                    .Where(v => v != string.Empty)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (ids.Count == 0)
                    result.StyleErrors[name] = $"Không tìm thấy Style \"{name}\" trong danh mục Item";
                else if (ids.Count > 1)
                    result.StyleErrors[name] = $"Style \"{name}\" khớp nhiều mã hàng: {string.Join(", ", ids)}";
                else
                    result.StyleIds[name] = ids[0];
            }

            foreach (var name in pos)
            {
                var ids = salesOrderCandidates
                    .Where(s => SameName(s.POName, name))
                    .Select(s => s.No?.Trim() ?? string.Empty)
                    .Where(v => v != string.Empty)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (ids.Count == 0)
                {
                    ids = poItemCandidates
                        .Where(p => SameName(p.POName, name))
                        .Select(p => p.PoId?.Trim() ?? string.Empty)
                        .Where(v => v != string.Empty)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                }
                if (ids.Count == 0)
                    result.PoErrors[name] = $"Không tìm thấy Tên PO \"{name}\" (Sales Order / đơn hàng sản xuất)";
                else if (ids.Count > 1)
                    result.PoErrors[name] = $"Tên PO \"{name}\" khớp nhiều mã đơn hàng: {string.Join(", ", ids)}";
                else
                    result.PoIds[name] = ids[0];
            }

            return result;
        }

        private static bool SameName(string? a, string b) => string.Equals(a?.Trim(), b, StringComparison.OrdinalIgnoreCase);

        // POST /api/production/production-orders/import-size
        // Import số lượng size từ file Excel (FE đã parse). Khớp đơn hàng theo style + po + color.
        // - Cập nhật planned_qty từng size ở mes.production_order_items
        // - Cập nhật total_planned_qty của dòng tổng gốc (line_number IS NULL) ở mes.factory_plans
        // All-or-nothing: có bất kỳ dòng lỗi nào thì không lưu gì cả và trả về danh sách lỗi.
        [HttpPost("production-orders/import-size")]
        [ProducesResponseType(typeof(object), 200)]
        public async Task<IActionResult> ImportOrderSizes([FromBody] ImportOrderSizeRequest request)
        {
            if (request?.Items == null || request.Items.Count == 0)
                return BadRequest(new { success = false, message = "Danh sách import không được để trống" });

            var errors = new List<object>();
            void AddError(int row, ImportOrderSizeRow? r, string message) =>
                errors.Add(new { row, styleName = r?.StyleName, poName = r?.PoName, colorId = r?.ColorId, message });

            var rows = request.Items
                .Select((r, i) => new { Row = r, Index = i + 1 })
                .ToList();

            // 1. Validate payload + chuẩn hoá
            foreach (var x in rows)
            {
                var r = x.Row;
                if (r == null)
                {
                    AddError(x.Index, null, "Dòng dữ liệu rỗng");
                    continue;
                }
                r.StyleName = r.StyleName?.Trim() ?? string.Empty;
                r.PoName = r.PoName?.Trim() ?? string.Empty;
                r.ColorId = r.ColorId?.Trim() ?? string.Empty;

                if (r.StyleName == "" || r.PoName == "" || r.ColorId == "")
                {
                    AddError(x.Index, r, "Thiếu Style / Tên PO / Màu");
                    continue;
                }
                if (r.SizeBreakdown == null || r.SizeBreakdown.Count == 0)
                {
                    AddError(x.Index, r, "Danh sách size không được để trống");
                    continue;
                }
                foreach (var s in r.SizeBreakdown) s.SizeId = s.SizeId?.Trim() ?? string.Empty;

                if (r.SizeBreakdown.Any(s => s.SizeId == ""))
                    AddError(x.Index, r, "Có size không có mã");
                if (r.SizeBreakdown.Any(s => s.PlannedQty < 0))
                    AddError(x.Index, r, "Số lượng không được nhỏ hơn 0");
                if (r.SizeBreakdown.GroupBy(s => s.SizeId, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
                    AddError(x.Index, r, "Danh sách size bị trùng lặp");
            }

            // 1b. Suy ra StyleId / PoId từ Style (tên) và Tên PO
            var nameRows = rows.Where(x => x.Row != null && x.Row.StyleName != "" && x.Row.PoName != "").ToList();
            var names = await ResolveStyleAndPoNamesAsync(
                nameRows.Select(x => x.Row.StyleName),
                nameRows.Select(x => x.Row.PoName));

            foreach (var x in nameRows)
            {
                var r = x.Row;
                if (names.StyleErrors.TryGetValue(r.StyleName, out var styleError))
                {
                    AddError(x.Index, r, styleError);
                    continue;
                }
                if (names.PoErrors.TryGetValue(r.PoName, out var poError))
                {
                    AddError(x.Index, r, poError);
                    continue;
                }
                r.StyleId = names.StyleIds[r.StyleName];
                r.PoId = names.PoIds[r.PoName];
            }

            // Trùng style + po + color trong cùng file (sau khi đã suy ra StyleId / PoId)
            foreach (var g in rows.Where(x => x.Row != null && x.Row.StyleId != "" && x.Row.PoId != "")
                                  .GroupBy(x => $"{x.Row.StyleId}|{x.Row.PoId}|{x.Row.ColorId}", StringComparer.OrdinalIgnoreCase)
                                  .Where(g => g.Count() > 1))
            {
                foreach (var x in g.Skip(1))
                    AddError(x.Index, x.Row, $"Trùng với dòng {g.First().Index} (cùng Style + Tên PO + Màu)");
            }

            if (errors.Count > 0)
                return BadRequest(new { success = false, message = "Dữ liệu import không hợp lệ", errors });

            // 2. Tải dữ liệu liên quan (1 lần, lọc theo style/po/color rồi ghép trong bộ nhớ)
            var styleIds = rows.Select(x => x.Row.StyleId).Distinct().ToList();
            var poIds = rows.Select(x => x.Row.PoId).Distinct().ToList();
            var colorIds = rows.Select(x => x.Row.ColorId).Distinct().ToList();

            var allItems = await _context.MesProductionOrderItems
                .Where(e => styleIds.Contains(e.StyleId) && poIds.Contains(e.PoId) && colorIds.Contains(e.ColorId)
                         && e.IsActive != false)
                .ToListAsync();

            var allPlans = await _context.MesFactoryPlans
                .Where(e => e.IsActive == true
                         && styleIds.Contains(e.StyleId) && poIds.Contains(e.PoId) && colorIds.Contains(e.ColorId))
                .ToListAsync();

            var results = new List<object>();
            var now = DateTime.UtcNow;
            var updatedBy = string.IsNullOrWhiteSpace(request.CreatedBy) ? null : request.CreatedBy.Trim();

            // 3. Xử lý từng dòng (chỉ thay đổi entity trong bộ nhớ, chưa lưu)
            foreach (var x in rows)
            {
                var r = x.Row;
                bool Match(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
                                
                // Lấy plan trước (có thể lọc theo r.VersionId nếu người dùng có truyền)
                var plans = allPlans
                    .Where(p => Match(p.StyleId, r.StyleId) && Match(p.PoId, r.PoId) && Match(p.ColorId, r.ColorId)
                             && (r.VersionId == null || p.VersionId == r.VersionId))
                    .ToList();

                var planVersionIds = plans.Select(p => p.VersionId).Distinct().ToList();
                if (planVersionIds.Count == 0)
                {
                    AddError(x.Index, r, "Không tìm thấy kế hoạch tương ứng (Mã hàng + PO + Màu)");
                    continue;
                }
                if (planVersionIds.Count > 1)
                {
                    AddError(x.Index, r, "Kế hoạch thuộc nhiều version, cần truyền versionId");
                    continue;
                }
                var versionId = planVersionIds[0];

                var items = allItems
                    .Where(i => Match(i.StyleId, r.StyleId) && Match(i.PoId, r.PoId) && Match(i.ColorId, r.ColorId)
                             && i.VersionId == versionId)
                    .ToList();
                if (items.Count == 0)
                {
                    AddError(x.Index, r, "Không tìm thấy đơn hàng tương ứng với version của kế hoạch");
                    continue;
                }

                var masterPlan = plans.FirstOrDefault(p => string.IsNullOrEmpty(p.LineNumber));
                // ... phần còn lại giữ nguyên
                if (masterPlan == null)
                {
                    AddError(x.Index, r, "Không tìm thấy dòng kế hoạch tổng (chưa gán tổ)");
                    continue;
                }
                // size trong file phải nằm trong tập size của đơn hàng (không thêm size mới);
                // size không có trong đơn hàng mà SL = 0 thì bỏ qua
                var currentSizes = items.ToDictionary(i => i.SizeId, StringComparer.OrdinalIgnoreCase);
                var invalidSizes = r.SizeBreakdown
                    .Where(s => !currentSizes.ContainsKey(s.SizeId) && s.PlannedQty > 0)
                    .Select(s => s.SizeId)
                    .ToList();
                if (invalidSizes.Count > 0)
                {
                    AddError(x.Index, r, $"Size không có trong đơn hàng: {string.Join(", ", invalidSizes)}");
                    continue;
                }
                var daGanTong = plans
                    .Where(p => !string.IsNullOrEmpty(p.LineNumber))
                    .Sum(p => !string.IsNullOrEmpty(p.OperationCode) ? p.TotalPlannedQty / 2m : p.TotalPlannedQty);

                // Tính tổng mới trên toàn bộ size của đơn hàng (size không có trong file giữ nguyên)
                var incoming = r.SizeBreakdown
                    .Where(s => currentSizes.ContainsKey(s.SizeId))
                    .ToDictionary(s => currentSizes[s.SizeId].SizeId, s => s.PlannedQty, StringComparer.OrdinalIgnoreCase);
                var newKhTong = items.Sum(i => incoming.TryGetValue(i.SizeId, out var q) ? q : i.PlannedQty);

                if (newKhTong < daGanTong)
                {
                    AddError(x.Index, r,
                        $"Tổng số lượng mới ({newKhTong}) nhỏ hơn số đã phân bổ cho tổ ({daGanTong}). Cần chỉnh lại số lượng phân bổ cho tổ");
                    continue;
                }

                var oldKhTong = masterPlan.TotalPlannedQty;
                foreach (var item in items)
                {
                    if (!incoming.TryGetValue(item.SizeId, out var qty)) continue;
                    item.PlannedQty = qty;
                    item.UpdatedAt = now;
                    item.UpdatedBy = updatedBy;
                }

                var conLaiChuaPhanBo = newKhTong - daGanTong;
                masterPlan.TotalPlannedQty = newKhTong;
                masterPlan.IsActive = conLaiChuaPhanBo > 0;
                masterPlan.UpdatedAt = now;
                if (updatedBy != null) masterPlan.UpdatedBy = updatedBy;

                results.Add(new
                {
                    row = x.Index,
                    versionId,
                    styleId = r.StyleId,
                    poId = r.PoId,
                    colorId = r.ColorId,
                    oldKhTong,
                    khTong = newKhTong,
                    daGanTong,
                    conLaiChuaPhanBo
                });
            }

            // All-or-nothing: có lỗi thì bỏ toàn bộ thay đổi đang track
            if (errors.Count > 0)
            {
                _context.ChangeTracker.Clear();
                return BadRequest(new { success = false, message = "Import thất bại, không có dữ liệu nào được lưu", errors });
            }

            // 4. Lưu trong transaction
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new
                {
                    success = true,
                    message = $"Import size thành công {results.Count} đơn hàng",
                    data = results
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { success = false, message = "Lỗi khi lưu dữ liệu: " + ex.Message });
            }
        }

        // POST /api/production/production-orders/import-delivery-date
        // Import ngày giao hàng từ file Excel (FE đã parse). Khớp đơn hàng theo Style (tên) + Tên PO (tất cả version, tất cả màu).
        // Cập nhật target_delivery_date các dòng ACTIVE của đơn hàng. All-or-nothing.
        [HttpPost("production-orders/import-delivery-date")]
        [ProducesResponseType(typeof(object), 200)]
        public async Task<IActionResult> ImportDeliveryDates([FromBody] ImportDeliveryDateRequest request)
        {
            if (request?.Items == null || request.Items.Count == 0)
                return BadRequest(new { success = false, message = "Danh sách import không được để trống" });

            var errors = new List<object>();
            void AddError(int row, ImportDeliveryDateRow? r, string message) =>
                errors.Add(new { row, styleName = r?.StyleName, poName = r?.PoName, message });

            var rows = request.Items
            .Select((r, i) => new { Row = r, Index = i + 1 })
            .ToList();

            // 1. Validate payload + chuẩn hoá
            foreach (var x in rows)
            {
            var r = x.Row;
            if (r == null)
            {
                AddError(x.Index, null, "Dòng dữ liệu rỗng");
                continue;
            }
            r.StyleName = r.StyleName?.Trim() ?? string.Empty;
            r.PoName = r.PoName?.Trim() ?? string.Empty;

            if (r.StyleName == "" || r.PoName == "")
                AddError(x.Index, r, "Thiếu Style / Tên PO");
            if (r.NewDeliveryDate == default)
                AddError(x.Index, r, "Ngày giao hàng không hợp lệ");
            }

            // 1b. Suy ra StyleId / PoId từ Style (tên) và Tên PO
            var nameRows = rows.Where(x => x.Row != null && x.Row.StyleName != "" && x.Row.PoName != "").ToList();
            var names = await ResolveStyleAndPoNamesAsync(
                nameRows.Select(x => x.Row.StyleName),
                nameRows.Select(x => x.Row.PoName));

            foreach (var x in nameRows)
            {
                var r = x.Row;
                if (names.StyleErrors.TryGetValue(r.StyleName, out var styleError))
                {
                    AddError(x.Index, r, styleError);
                    continue;
                }
                if (names.PoErrors.TryGetValue(r.PoName, out var poError))
                {
                    AddError(x.Index, r, poError);
                    continue;
                }
                r.StyleId = names.StyleIds[r.StyleName];
                r.PoId = names.PoIds[r.PoName];
            }

            // Trùng style + po trong cùng file (ngày giao có thể mâu thuẫn), kiểm tra sau khi đã suy ra ID
            foreach (var g in rows.Where(x => x.Row != null && x.Row.StyleId != "" && x.Row.PoId != "")
                                  .GroupBy(x => $"{x.Row.StyleId}|{x.Row.PoId}", StringComparer.OrdinalIgnoreCase)
                                  .Where(g => g.Count() > 1))
            {
                foreach (var x in g.Skip(1))
                    AddError(x.Index, x.Row, $"Trùng với dòng {g.First().Index} (cùng Style + Tên PO)");
            }

            if (errors.Count > 0)
            return BadRequest(new { success = false, message = "Dữ liệu import không hợp lệ", errors });

            // 2. Tải dữ liệu liên quan (1 lần, lọc theo style/po rồi ghép trong bộ nhớ; gồm mọi version và màu)
            var styleIds = rows.Select(x => x.Row.StyleId).Distinct().ToList();
            var poIds = rows.Select(x => x.Row.PoId).Distinct().ToList();

            var allItems = await _context.MesProductionOrderItems
            .Where(e => styleIds.Contains(e.StyleId) && poIds.Contains(e.PoId)
                     && e.IsActive != false)
            .ToListAsync();

            var results = new List<object>();
            var now = DateTime.UtcNow;
            var updatedBy = string.IsNullOrWhiteSpace(request.CreatedBy) ? null : request.CreatedBy.Trim();
            bool Match(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

            // 3. Xử lý từng dòng (chỉ thay đổi entity trong bộ nhớ, chưa lưu)
            foreach (var x in rows)
            {
            var r = x.Row;
            var items = allItems
                .Where(i => Match(i.StyleId, r.StyleId) && Match(i.PoId, r.PoId))
                .ToList();

            if (items.Count == 0)
            {
                AddError(x.Index, r, "Không tìm thấy đơn hàng tương ứng (Style + Tên PO)");
                continue;
            }

            foreach (var item in items)
            {
                item.TargetDeliveryDate = r.NewDeliveryDate;
                item.UpdatedAt = now;
                item.UpdatedBy = updatedBy;
            }

            results.Add(new
            {
                row = x.Index,
                styleId = r.StyleId,
                poId = r.PoId,
                newDeliveryDate = r.NewDeliveryDate,
                versionCount = items.Select(i => i.VersionId).Distinct().Count(),
                updatedRows = items.Count
            });
            }

            // All-or-nothing: có lỗi thì bỏ toàn bộ thay đổi đang track
            if (errors.Count > 0)
            {
                _context.ChangeTracker.Clear();
                return BadRequest(new { success = false, message = "Import thất bại, không có dữ liệu nào được lưu", errors });
            }

            // 4. Lưu trong transaction
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new
                {
                    success = true,
                    message = $"Import ngày giao hàng thành công {results.Count} đơn hàng",
                    data = results
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { success = false, message = "Lỗi khi lưu dữ liệu: " + ex.Message });
            }
        }

        // POST /api/production/production-orders/import-po-name
        // Import đổi tên PO từ file Excel (FE đã parse). Tra Style (tên) -> StyleId, Tên PO cũ -> PoId (helper dùng chung với import size),
        // áp dụng cho mọi version và màu. Logic đổi tên và ghi po_rename_history giống RenamePo. All-or-nothing.
        [HttpPost("production-orders/import-po-name")]
        [ProducesResponseType(typeof(object), 200)]
        public async Task<IActionResult> ImportPoNames([FromBody] ImportPoNameRequest request)
        {
            if (request?.Items == null || request.Items.Count == 0)
                return BadRequest(new { success = false, message = "Danh sách import không được để trống" });

            var errors = new List<object>();
            void AddError(int row, ImportPoNameRow? r, string message) =>
                errors.Add(new { row, styleName = r?.StyleName, oldPoName = r?.OldPoName, newPoName = r?.NewPoName, message });

            var rows = request.Items
                .Select((r, i) => new { Row = r, Index = i + 1 })
                .ToList();

            // 1. Validate payload + chuẩn hoá
            foreach (var x in rows)
            {
                var r = x.Row;
                if (r == null)
                {
                    AddError(x.Index, null, "Dòng dữ liệu rỗng");
                    continue;
                }
                r.StyleName = r.StyleName?.Trim() ?? string.Empty;
                r.OldPoName = r.OldPoName?.Trim() ?? string.Empty;
                r.NewPoName = r.NewPoName?.Trim() ?? string.Empty;

                if (r.StyleName == "" || r.OldPoName == "")
                    AddError(x.Index, r, "Thiếu Style / Tên PO cũ");
                if (r.NewPoName == "")
                    AddError(x.Index, r, "Tên PO mới không được để trống");
                else if (r.NewPoName.Length > 100)
                    AddError(x.Index, r, "Tên PO mới quá dài (tối đa 100 ký tự)");
            }

            // 1b. Suy ra StyleId / PoId từ Style (tên) và Tên PO cũ
            var nameRows = rows.Where(x => x.Row != null && x.Row.StyleName != "" && x.Row.OldPoName != "").ToList();
            var names = await ResolveStyleAndPoNamesAsync(
                nameRows.Select(x => x.Row.StyleName),
                nameRows.Select(x => x.Row.OldPoName));

            foreach (var x in nameRows)
            {
                var r = x.Row;
                if (names.StyleErrors.TryGetValue(r.StyleName, out var styleError))
                {
                    AddError(x.Index, r, styleError);
                    continue;
                }
                if (names.PoErrors.TryGetValue(r.OldPoName, out var poError))
                {
                    AddError(x.Index, r, poError);
                    continue;
                }
                r.StyleId = names.StyleIds[r.StyleName];
                r.PoId = names.PoIds[r.OldPoName];
            }

            // Trùng style + po trong cùng file (tên mới có thể mâu thuẫn), kiểm tra sau khi đã suy ra ID
            foreach (var g in rows.Where(x => x.Row != null && x.Row.StyleId != "" && x.Row.PoId != "")
                                  .GroupBy(x => $"{x.Row.StyleId}|{x.Row.PoId}", StringComparer.OrdinalIgnoreCase)
                                  .Where(g => g.Count() > 1))
            {
                foreach (var x in g.Skip(1))
                    AddError(x.Index, x.Row, $"Trùng với dòng {g.First().Index} (cùng Style + Tên PO cũ)");
            }

            // 1c. Tên PO mới không được trùng với PO khác (mỗi po_id ứng với 1 tên PO)
            var renameRows = rows.Where(x => x.Row != null && x.Row.PoId != "" && x.Row.NewPoName != "").ToList();
            var newNames = renameRows.Select(x => x.Row.NewPoName).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (newNames.Count > 0)
            {
                var salesOrderNameOwners = await _context.SalesOrderHeader
                    .Where(s => newNames.Contains(s.POName))
                    .Select(s => new { s.No, s.POName })
                    .ToListAsync();
                var poItemNameOwners = await _context.MesProductionOrderItems
                    .Where(i => i.IsActive != false && newNames.Contains(i.POName))
                    .Select(i => new { i.PoId, i.POName })
                    .ToListAsync();

                foreach (var x in renameRows)
                {
                    var r = x.Row;
                    var owners = salesOrderNameOwners
                        .Where(s => SameName(s.POName, r.NewPoName) && !SameName(s.No, r.PoId))
                        .Select(s => s.No?.Trim() ?? string.Empty)
                        .Concat(poItemNameOwners
                            .Where(p => SameName(p.POName, r.NewPoName) && !SameName(p.PoId, r.PoId))
                            .Select(p => p.PoId?.Trim() ?? string.Empty))
                        .Where(v => v != string.Empty)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    if (owners.Count > 0)
                        AddError(x.Index, r, $"Tên PO mới \"{r.NewPoName}\" đã được dùng cho đơn hàng khác: {string.Join(", ", owners)}");
                }

                // Trong cùng file: 2 PO khác nhau không được đổi thành cùng 1 tên
                foreach (var g in renameRows
                    .GroupBy(x => x.Row.NewPoName, StringComparer.OrdinalIgnoreCase)
                    .Where(g => g.Select(x => x.Row.PoId).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1))
                {
                    foreach (var x in g)
                        AddError(x.Index, x.Row, $"Tên PO mới \"{x.Row.NewPoName}\" bị trùng cho nhiều đơn hàng khác nhau trong file");
                }
            }

            if (errors.Count > 0)
                return BadRequest(new { success = false, message = "Dữ liệu import không hợp lệ", errors });

            // 2. Tải dữ liệu liên quan (1 lần, lọc theo style/po rồi ghép trong bộ nhớ)
            var styleIds = rows.Select(x => x.Row.StyleId).Distinct().ToList();
            var poIds = rows.Select(x => x.Row.PoId).Distinct().ToList();

            var allItems = await _context.MesProductionOrderItems
            .Where(e => styleIds.Contains(e.StyleId) && poIds.Contains(e.PoId)
                     && e.IsActive != false)
            .ToListAsync();

            var results = new List<object>();
            var now = DateTime.UtcNow;
            var changedBy = string.IsNullOrWhiteSpace(request.CreatedBy) ? "unknown" : request.CreatedBy.Trim();
            bool Match(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

            // 3. Xử lý từng dòng (chỉ thay đổi entity và thêm lịch sử trong bộ nhớ, chưa lưu)
            foreach (var x in rows)
            {
            var r = x.Row;
            var items = allItems
                .Where(i => Match(i.StyleId, r.StyleId) && Match(i.PoId, r.PoId))
                .ToList();

            if (items.Count == 0)
            {
                AddError(x.Index, r, "Không tìm thấy đơn hàng tương ứng (Mã hàng + Mã đơn hàng)");
                continue;
            }

            var oldNames = items.Select(i => i.POName).Distinct().ToList();
            var changedCount = 0;

            foreach (var item in items)
            {
                if (string.Equals(item.POName, r.NewPoName, StringComparison.Ordinal))
                    continue;

                _context.Set<MesPoRenameHistory>().Add(new MesPoRenameHistory
                {
                    Id = Guid.NewGuid(),
                    ItemId = item.Id,
                    OldPoName = item.POName,
                    NewPoName = r.NewPoName,
                    ChangedBy = changedBy,
                    ChangedAt = now,
                });

                item.POName = r.NewPoName;
                item.UpdatedAt = now;
                item.UpdatedBy = changedBy;
                changedCount++;
            }

            results.Add(new
            {
                row = x.Index,
                styleId = r.StyleId,
                poId = r.PoId,
                newPoName = r.NewPoName,
                oldPoNames = oldNames,
                versionCount = items.Select(i => i.VersionId).Distinct().Count(),
                updatedRows = changedCount
            });
            }

            // All-or-nothing: có lỗi thì bỏ toàn bộ thay đổi đang track
            if (errors.Count > 0)
            {
                _context.ChangeTracker.Clear();
                return BadRequest(new { success = false, message = "Import thất bại, không có dữ liệu nào được lưu", errors });
            }

            // 4. Lưu trong transaction
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new
                {
                    success = true,
                    message = $"Import đổi tên PO thành công {results.Count} đơn hàng",
                    data = results
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { success = false, message = "Lỗi khi lưu dữ liệu: " + ex.Message });
            }
        }

        // GET /api/production/workcenters
        // Trả về danh sách xưởng (Work Center) dùng cho dropdown trên FE
        [HttpGet("workcenters")]
        [ProducesResponseType(typeof(List<EquipManagementAPI.Models.DTOs.WorkCenter_DTO>), 200)]
        public async Task<IActionResult> GetWorkCenters()
        {
            try
            {
                var list = await _context.workCenters
                    .Where(w => w.Type == 0)
                    .Select(w => new WorkCenter_DTO { Value = w.No, Label = w.Name })
                    .ToListAsync();
                return Ok(list);
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = "Không thể lấy danh sách xưởng.", detail = ex.Message });
            }
        }

        // GET /api/production/item-sizes
        // Trả về danh sách Size (bảng [Item Size]) sắp xếp theo [Ordinal Number] tăng dần,
        // dùng để FE sắp xếp thứ tự hiển thị cột Size trong tab "Đơn hàng"
        [HttpGet("item-sizes")]
        [ProducesResponseType(typeof(List<EquipManagementAPI.Models.DTOs.ItemSizeDto>), 200)]
        public async Task<IActionResult> GetItemSizes()
        {
            try
            {
                var list = await _context.ItemSizes
                    .AsNoTracking()
                    .OrderBy(s => s.OrdinalNumber)
                    .Select(s => new ItemSizeDto
                    {
                        SizeId = s.No,
                        Description = s.Description,
                        OrdinalNumber = s.OrdinalNumber
                    })
                    .ToListAsync();
                return Ok(list);
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = "Không thể lấy danh sách size.", detail = ex.Message });
            }
        }

        // POST /api/production/factory-plans
        // Tạo mới factory plan kèm size lines và kế hoạch ngày
        [HttpPost("factory-plans")]
        [ProducesResponseType(typeof(object), 201)]
        public async Task<IActionResult> CreateFactoryPlan([FromBody] CreateFactoryPlanRequest req)
        {
            if (req == null)
                return BadRequest(new { success = false, message = "Request body is required." });

            if (string.IsNullOrWhiteSpace(req.FactoryId))
                return BadRequest(new { success = false, message = "factoryId is required." });
            if (string.IsNullOrWhiteSpace(req.StyleId))
                return BadRequest(new { success = false, message = "styleId is required." });
            if (string.IsNullOrWhiteSpace(req.PoId))
                return BadRequest(new { success = false, message = "poId is required." });
            if (string.IsNullOrWhiteSpace(req.ColorId))
                return BadRequest(new { success = false, message = "colorId is required." });
            if (string.IsNullOrWhiteSpace(req.LineNumber))
                return BadRequest(new { success = false, message = "lineNumber is required." });
            if (req.ProductionStartDate == default)
                return BadRequest(new { success = false, message = "productionStartDate is required." });
            if (req.TotalPlannedQty <= 0)
                return BadRequest(new { success = false, message = "totalPlannedQty must be > 0." });

            var now = DateTime.UtcNow;
            var planCode = $"FP-{req.FactoryId}-{now:yyyyMMddHHmmss}";

            var plan = new MesFactoryPlan
            {
                Id                  = Guid.NewGuid(),
                PlanCode            = planCode,
                ProductionOrderId   = req.ProductionOrderId,
                VersionId           = req.VersionId,
                FactoryId           = req.FactoryId,
                StyleId             = req.StyleId,
                PoId                = req.PoId,
                ColorId             = req.ColorId,
                LineNumber          = req.LineNumber,
                ProductionStartDate = req.ProductionStartDate,
                BufferDays          = req.BufferDays,
                // BtpRequiredDate là computed column trên DB, không set trực tiếp
                TotalPlannedQty     = req.TotalPlannedQty,
                Status              = "chua_san_xuat",
                CreatedBy           = req.CreatedBy ?? "system",
                CreatedAt           = now,
                UpdatedAt           = now,
                IsActive            = true,
            };

            var lines = (req.Lines ?? []).Select(l => new MesFactoryPlanLine
            {
                Id               = Guid.NewGuid(),
                FactoryPlanId = plan.Id,
                SizeId           = l.SizeId,
                PlannedQty       = l.PlannedQty,
                DemandCreatedQty = 0,
                WorkDate        = now,
                UpdatedAt        = now,
            }).ToList();

            var dailies = (req.Daily ?? []).Select(d => new MesFactoryPlanDaily
            {
                Id            = Guid.NewGuid(),
                FactoryPlanId = plan.Id,
                WorkDate      = d.WorkDate,
                PlannedQty    = d.PlannedQty,
                Note          = d.Note,
                CreatedAt     = now,
                UpdatedAt     = now,
            }).ToList();

            await _context.MesFactoryPlans.AddAsync(plan);
            if (lines.Count > 0)
                await _context.MesFactoryPlanLines.AddRangeAsync(lines);
            if (dailies.Count > 0)
                await _context.MesFactoryPlanDailies.AddRangeAsync(dailies);

            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(CreateFactoryPlan), new { id = plan.Id }, new
            {
                success     = true,
                message     = "Tạo factory plan thành công.",
                id          = plan.Id,
                planCode    = plan.PlanCode,
                linesCount  = lines.Count,
                dailyCount  = dailies.Count,
            });
        }

        // GET /api/production/factory-plans-view
        // Lấy danh sách factory plans + kế hoạch ngày theo xưởng/tổ/style/tháng để hiển thị bảng
        [HttpGet("factory-plans-view")]
        public async Task<IActionResult> GetFactoryPlansView(
            [FromQuery] string factoryId,
            [FromQuery] int year,
            [FromQuery] int month,
            [FromQuery] string? lineNumber = null,
            [FromQuery] string? styleName  = null)
        {
            if (string.IsNullOrWhiteSpace(factoryId))
                return BadRequest(new { success = false, message = "Vui lòng truyền factoryId." });
            if (year <= 0 || month < 1 || month > 12)
                return BadRequest(new { success = false, message = "year và month không hợp lệ." });

            var monthStart   = new DateOnly(year, month, 1);
            var monthEnd     = monthStart.AddMonths(1).AddDays(-1);
            var monthStartDt = monthStart.ToDateTime(TimeOnly.MinValue);
            var monthEndDt   = monthEnd.ToDateTime(new TimeOnly(23, 59, 59));

            // Lọc plans: factory_id khớp + khoảng SX giao với tháng
            var query = _context.MesFactoryPlans
                .Where(p => p.FactoryId == factoryId
                         && p.ProductionStartDate <= monthEnd
                         && p.ProductionEndDate   >= monthStart);

            if (!string.IsNullOrWhiteSpace(lineNumber))
                query = query.Where(p => p.LineNumber == lineNumber);

            var plans   = await query.OrderBy(p => p.LineNumber).ThenBy(p => p.StyleId).ToListAsync();
            var planIds = plans.Select(p => p.Id).ToList();

            // Chỉ load lines trong tháng
            var lines = await _context.MesFactoryPlanLines
                .Where(l => planIds.Contains(l.FactoryPlanId)
                         && l.WorkDate >= monthStartDt
                         && l.WorkDate <= monthEndDt)
                .ToListAsync();

            var linesByPlanId = lines
                .GroupBy(l => l.FactoryPlanId)
                .ToDictionary(g => g.Key, g => g.ToList());

            // Lấy FinishName từ production_order_items — join theo (VersionId, FactoryId, StyleId, PoId, ColorId)
            var versionIds = plans.Select(p => p.VersionId).Distinct().ToList();
            //var finishNameLookup = await _context.MesProductionOrderItems
            //    .Where(i => versionIds.Contains(i.VersionId))
            //    .Select(i => new { i.VersionId, i.FactoryId, i.StyleId, i.ColorId, i.FinishName })
            //    .Distinct()
            //    .ToListAsync();
            //var finishNameMap = finishNameLookup
            //    .GroupBy(i => $"{i.VersionId}|{i.FactoryId}|{i.StyleId}|{i.ColorId}")
            //    .ToDictionary(g => g.Key, g => new { FinishName = g.First().FinishName ?? string.Empty });
            var styleIds = plans.Select(p => p.StyleId).Distinct().ToList();

            var styleNameLookup = await _context.SalesOrderLineFactoryPlans
                .Where(x => styleIds.Contains(x.ItemNo))
                .Select(x => new
                {
                    x.ItemNo,
                    x.FinishName // hoặc ItemName nếu cột tên style thực sự là ItemName
                })
                .Distinct()
                .ToListAsync();

            var styleNameMap = styleNameLookup
                .GroupBy(x => x.ItemNo)
                .ToDictionary(
                    g => g.Key,
                    g => g.First().FinishName ?? string.Empty
                );
            // Lọc theo styleName nếu có (contains, không phân biệt hoa thường)
            //if (!string.IsNullOrWhiteSpace(styleName))
            //{
            //    var styleNameLower = styleName.Trim().ToLowerInvariant();
            //    plans = plans.Where(p =>
            //    {
            //        var key = $"{p.VersionId}|{p.FactoryId}|{p.StyleId}|{p.ColorId}";
            //        var fn  = finishNameMap.TryGetValue(key, out var v) ? v.FinishName : string.Empty;
            //        return fn.ToLowerInvariant().Contains(styleNameLower);
            //    }).ToList();
            //}
            // Lọc theo styleName nếu có (contains, không phân biệt hoa thường)
            if (!string.IsNullOrWhiteSpace(styleName))
            {
                var styleNameLower = styleName.Trim().ToLowerInvariant();

                plans = plans.Where(p =>
                    styleNameMap.TryGetValue(p.StyleId, out var name)
                    && name.ToLowerInvariant().Contains(styleNameLower)
                ).ToList();
            }
            // Tổng total_planned_qty theo từng line_number (tính lại sau khi lọc styleName)
            var totalByTeam = plans
                .GroupBy(p => p.LineNumber)
                .ToDictionary(g => g.Key, g => g.Sum(p => p.TotalPlannedQty));

            var data = plans.Select(p =>
            {
                var pLines     = linesByPlanId.TryGetValue(p.Id, out var ls) ? ls : new List<MesFactoryPlanLine>();
                var monthlyQty = pLines.Sum(l => l.PlannedQty);

                // { ngày_trong_tháng (1-31): tổng planned_qty }
                var dailyMap = pLines
                    .GroupBy(l => l.WorkDate.Day)
                    .ToDictionary(g => g.Key, g => g.Sum(l => l.PlannedQty));

                var finishKey = $"{p.VersionId}|{p.FactoryId}|{p.StyleId}|{p.PoId}|{p.ColorId}";
                //var styleName = finishNameMap.TryGetValue(finishKey, out var fn) ? fn.FinishName : string.Empty;
                //var poName    = finishNameMap.TryGetValue(finishKey, out var fn2) ? fn2.POName : string.Empty;
                var styleName = styleNameMap.TryGetValue(p.StyleId,out var style)? style: string.Empty;
                return new
                {
                    id                  = p.Id,
                    planCode            = p.PlanCode,
                    factoryId           = p.FactoryId,
                    lineNumber          = p.LineNumber,
                    styleId             = p.StyleId,
                    styleName ,
                    poId                = p.PoId,
                    //poName,
                    colorId             = p.ColorId,
                    productionStartDate = p.ProductionStartDate,
                    productionEndDate   = p.ProductionEndDate,
                    totalPlannedQty     = p.TotalPlannedQty,
                    teamTotalQty        = totalByTeam.TryGetValue(p.LineNumber, out var t) ? t : 0,
                    monthlyPlannedQty   = monthlyQty,
                    dailyQty            = dailyMap,
                };
            }).ToList();

            return Ok(new { success = true, data });
        }
       
        public record SizeQtyDto(string SizeId, decimal PlannedQty);
        // GET /api/production/factory-plans-v2-view
        // Lấy danh sách factory plans + kế hoạch ngày theo xưởng/tổ/style/tháng để hiển thị bảng

        //    [HttpGet("factory-plans-v2-view")]
        //    public async Task<IActionResult> GetFactoryPlans_V2_View(
        //[FromQuery] string factoryId,
        //[FromQuery] int year,
        //[FromQuery] int month,
        //[FromQuery] int toYear,
        //[FromQuery] int toMonth,
        //[FromQuery] string? lineNumber = null,
        //[FromQuery] string? styleName = null)
        //    {
        //        if (string.IsNullOrWhiteSpace(factoryId))
        //            return BadRequest(new { success = false, message = "Vui lòng truyền factoryId." });
        //        if (year <= 0 || toYear <= 0 || month < 1 || month > 12 || toMonth < 1 || toMonth > 12)
        //            return BadRequest(new { success = false, message = "year và month không hợp lệ." });

        //        var monthStart = new DateOnly(year, month, 1);
        //        var toMonthEnd = new DateOnly(toYear, toMonth, 1).AddMonths(1).AddDays(-1);

        //        // ---- B1: Lọc plans theo factoryId / lineNumber / styleName ----
        //        var planQuery = _context.MesFactoryPlans.AsNoTracking()
        //            .Where(p => p.FactoryId == factoryId && p.IsActive);

        //        if (!string.IsNullOrWhiteSpace(lineNumber))
        //            planQuery = planQuery.Where(p => p.LineNumber == lineNumber);

        //        if (!string.IsNullOrWhiteSpace(styleName))
        //        {
        //            var trimKeyword = styleName.Trim();
        //            var keyword = _context.Item
        //                .Where(p => p.Name == trimKeyword)
        //                .Select(p => p.No)
        //                .FirstOrDefault();
        //            if (!string.IsNullOrEmpty(keyword))
        //            {
        //                planQuery = planQuery.Where(p => p.StyleId.Contains(keyword));
        //            }
        //        }

        //        // ---- TAB KẾ HOẠCH SẢN XUẤT: lọc riêng theo NGÀY SẢN XUẤT (production_start_date/production_end_date),
        //        // KHÔNG dùng ngày giao hàng như tab Đơn hàng. Chỉ lấy các dòng đã gán tổ (LineNumber) và có khoảng
        //        // ngày sản xuất giao với kỳ lọc [monthStart, toMonthEnd].
        //        //var planLinePlans = await planQuery
        //        //    .Where(p => !string.IsNullOrWhiteSpace(p.LineNumber)
        //        //             && p.ProductionStartDate != null
        //        //             && p.ProductionEndDate != null
        //        //             && p.ProductionStartDate <= toMonthEnd
        //        //             && p.ProductionEndDate >= monthStart)
        //        //    .OrderBy(p => p.LineNumber)
        //        //    .ThenBy(p => p.StyleId)
        //        //    .ToListAsync();

        //        // Bộ khóa distinct để JOIN — vẫn là IQueryable, chưa gọi DB
        //        var planKeys = planQuery
        //            .Select(p => new { p.VersionId, p.FactoryId, p.StyleId, p.PoId, p.ColorId })
        //            .Distinct();

        //        // JOIN đúng tổ hợp 5 cột thay vì 4 Contains rời rạc
        //        var filteredGroups = await _context.MesProductionOrderItems
        //            .AsNoTracking()
        //            .Where(x => x.FactoryId == factoryId)
        //            .Join(planKeys,
        //                x => new { x.VersionId, x.FactoryId, x.StyleId, x.PoId, x.ColorId },
        //                k => new { k.VersionId, k.FactoryId, k.StyleId, k.PoId, k.ColorId },
        //                (x, k) => x)
        //            .GroupBy(x => new { x.VersionId, x.FactoryId, x.StyleId, x.PoId, x.ColorId })
        //            .Select(g => new
        //            {
        //                g.Key.VersionId,
        //                g.Key.FactoryId,
        //                g.Key.StyleId,
        //                g.Key.PoId,
        //                g.Key.ColorId,
        //                TargetDeliveryDate = g.Max(x => x.TargetDeliveryDate)
        //            })
        //            .Where(g => g.TargetDeliveryDate >= monthStart && g.TargetDeliveryDate <= toMonthEnd)
        //            .ToListAsync();

        //        // Chỉ return sớm khi CẢ orders (theo ngày giao hàng) VÀ planLines (theo ngày sản xuất) đều rỗng
        //        if (filteredGroups.Count == 0)
        //            return Ok(new { success = true, data = new { orders = Array.Empty<object>(), planLines = Array.Empty<object>() } });

        //        var validKeys = filteredGroups
        //            .Select(g => (g.VersionId, g.FactoryId, g.StyleId, g.PoId, g.ColorId))
        //            .ToHashSet();

        //        var targetDeliveryMap = filteredGroups.ToDictionary(
        //            g => (g.VersionId, g.FactoryId, g.StyleId, g.PoId, g.ColorId),
        //            g => g.TargetDeliveryDate);

        //        //// planLinePlans có thể chứa PO có ngày giao hàng NẰM NGOÀI kỳ lọc (vì giờ lọc theo ngày sản xuất) —
        //        //// bổ sung targetDeliveryDate cho các tổ hợp này (không giới hạn theo monthStart/toMonthEnd) để FE vẫn
        //        //// hiển thị đúng ngày giao hàng ở tab Kế hoạch sản xuất thay vì để trống.
        //        //var planLineComboKeys = planLinePlans
        //        //    .Select(p => new { p.VersionId, p.FactoryId, p.StyleId, p.PoId, p.ColorId })
        //        //    .Distinct()
        //        //    .Where(k => !targetDeliveryMap.ContainsKey((k.VersionId, k.FactoryId, k.StyleId, k.PoId, k.ColorId)))
        //        //    .ToList();

        //        //if (planLineComboKeys.Count > 0)
        //        //{
        //        //    var planLineVersionIds = planLineComboKeys.Select(k => k.VersionId).Distinct().ToList();
        //        //    var planLineStyleIds = planLineComboKeys.Select(k => k.StyleId).Distinct().ToList();
        //        //    var planLinePoIds = planLineComboKeys.Select(k => k.PoId).Distinct().ToList();
        //        //    var planLineColorIds = planLineComboKeys.Select(k => k.ColorId).Distinct().ToList();

        //        //    var extraDeliveryGroups = await _context.MesProductionOrderItems
        //        //        .AsNoTracking()
        //        //        .Where(x => x.FactoryId == factoryId
        //        //                 && planLineVersionIds.Contains(x.VersionId)
        //        //                 && planLineStyleIds.Contains(x.StyleId)
        //        //                 && planLinePoIds.Contains(x.PoId)
        //        //                 && planLineColorIds.Contains(x.ColorId))
        //        //        .GroupBy(x => new { x.VersionId, x.FactoryId, x.StyleId, x.PoId, x.ColorId })
        //        //        .Select(g => new
        //        //        {
        //        //            g.Key.VersionId,
        //        //            g.Key.FactoryId,
        //        //            g.Key.StyleId,
        //        //            g.Key.PoId,
        //        //            g.Key.ColorId,
        //        //            TargetDeliveryDate = g.Max(x => x.TargetDeliveryDate)
        //        //        })
        //        //        .ToListAsync();

        //        //    foreach (var g in extraDeliveryGroups)
        //        //    {
        //        //        var comboKey = (g.VersionId, g.FactoryId, g.StyleId, g.PoId, g.ColorId);
        //        //        if (!targetDeliveryMap.ContainsKey(comboKey))
        //        //            targetDeliveryMap[comboKey] = g.TargetDeliveryDate;
        //        //    }
        //        //}

        //        var groupVersionIds = filteredGroups.Select(g => g.VersionId).Distinct().ToList();
        //        var groupStyleIds = filteredGroups.Select(g => g.StyleId).Distinct().ToList();
        //        var groupPoIds = filteredGroups.Select(g => g.PoId).Distinct().ToList();
        //        var groupColorIds = filteredGroups.Select(g => g.ColorId).Distinct().ToList();

        //        var candidatePlans = await planQuery
        //            .AsNoTracking()
        //            .Where(p => groupVersionIds.Contains(p.VersionId)
        //                     && groupStyleIds.Contains(p.StyleId)
        //                     && groupPoIds.Contains(p.PoId)
        //                     && groupColorIds.Contains(p.ColorId))
        //            .OrderBy(p => p.LineNumber)
        //            .ThenBy(p => p.StyleId)
        //            .ToListAsync();

        //        // Lọc chính xác lại theo tổ hợp 5 cột (Contains riêng lẻ ở trên có thể match chéo giữa các plan khác nhau)
        //        var plans = candidatePlans
        //            .Where(p => validKeys.Contains((p.VersionId, p.FactoryId, p.StyleId, p.PoId, p.ColorId)))
        //            .ToList();

        //        if (plans.Count == 0)
        //            return Ok(new { success = true, data = new { orders = Array.Empty<object>(), planLines = Array.Empty<object>() } });
        //        var planLinePlans = plans
        //            .Where(p => !string.IsNullOrWhiteSpace(p.LineNumber))
        //            .ToList();
        //        // Các id list giờ suy ra từ "plans" đã lọc (tập nhỏ hơn nhiều so với allPlans trước đây)
        //        var versionIds = plans.Select(p => p.VersionId).Distinct().ToList();
        //        var styleIds = plans.Select(p => p.StyleId).Distinct().ToList();
        //        var poIds = plans.Select(p => p.PoId).Distinct().ToList();
        //        var colorIds = plans.Select(p => p.ColorId).Distinct().ToList();

        //        // ---- Lookup StyleName từ bảng Item (No -> Name) ----
        //        var planStyleIds = plans.Select(p => p.StyleId).Concat(planLinePlans.Select(p => p.StyleId)).Distinct().ToList();
        //        var itemNameMap = await _context.Item
        //            .Where(i => planStyleIds.Contains(i.No))
        //            .Select(i => new { i.No, i.Name })
        //            .ToDictionaryAsync(i => i.No, i => i.Name ?? string.Empty);

        //        // ---- Lookup PoName từ bảng Sales Order Header ----
        //        var planPoIds = plans.Select(p => p.PoId).Concat(planLinePlans.Select(p => p.PoId)).Distinct().ToList();
        //        // Bước 1: Lấy PoName từ MesProductionOrderItems trước (nguồn ưu tiên)
        //        var mesPoNameMap = await _context.MesProductionOrderItems
        //            .Where(i => planPoIds.Contains(i.PoId) && !string.IsNullOrEmpty(i.POName))
        //            .GroupBy(i => i.PoId)
        //            .Select(g => new { PoId = g.Key, POName = g.First().POName })
        //            .ToDictionaryAsync(x => x.PoId, x => x.POName ?? string.Empty);

        //        var salesPoNameMap = await _context.SalesOrderHeader
        //            .Where(s => planPoIds.Contains(s.No))
        //            .Select(s => new { s.No, s.POName })
        //            .ToDictionaryAsync(s => s.No, s => s.POName ?? string.Empty);

        //        var poNameMap = new Dictionary<string, string>();
        //        foreach (var poId in planPoIds)
        //        {
        //            if (mesPoNameMap.TryGetValue(poId, out var mesName) && !string.IsNullOrWhiteSpace(mesName))
        //            {
        //                poNameMap[poId] = mesName;
        //            }
        //            else if (salesPoNameMap.TryGetValue(poId, out var salesName))
        //            {
        //                poNameMap[poId] = salesName;
        //            }
        //            else
        //            {
        //                poNameMap[poId] = string.Empty;
        //            }
        //        }

        //        var missingPoIds = planPoIds
        //            .Where(id => !poNameMap.TryGetValue(id, out var name) || string.IsNullOrWhiteSpace(name))
        //            .ToList();

        //        // Bước 3: nếu còn thiếu, tìm bổ sung ở production_order_items
        //        if (missingPoIds.Count > 0)
        //        {
        //            var fallbackMap = await _context.MesProductionOrderItems
        //                .Where(i => missingPoIds.Contains(i.PoId) && !string.IsNullOrEmpty(i.POName))
        //                .GroupBy(i => i.PoId)
        //                // 1 PoId có thể có nhiều dòng item -> lấy 1 tên đại diện (mới nhất theo CreatedAt, hoặc First tùy nghiệp vụ)
        //                .Select(g => new { PoId = g.Key, POName = g.OrderByDescending(x => x.CreatedAt).Select(x => x.POName).First() })
        //                .ToDictionaryAsync(x => x.PoId, x => x.POName ?? string.Empty);

        //            // Bước 4: merge vào map chính (ghi đè/bổ sung)
        //            foreach (var kv in fallbackMap)
        //            {
        //                poNameMap[kv.Key] = kv.Value;
        //            }
        //        }
        //        var planTeamIds = plans.Select(p => p.LineNumber).Concat(planLinePlans.Select(p => p.LineNumber)).Distinct().ToList();
        //        var teamIdMap = await _context.workShift
        //            .Where(s => planTeamIds.Contains(s.Code))
        //            .Select(s => new { s.Code, s.Name })
        //            .ToDictionaryAsync(s => s.Code, s => s.Name ?? string.Empty);
        //        // ---- Lookup teamTotalQty (từng ngày) — chỉ cần cho các dòng ĐÃ có tổ ----
        //        var monthStartDt = monthStart.ToDateTime(TimeOnly.MinValue);
        //        var toMonthEndDt = toMonthEnd.ToDateTime(TimeOnly.MaxValue);

        //        // Dùng planLinePlans (đã lọc theo ngày sản xuất) thay vì plans (lọc theo ngày giao hàng)
        //        var planIdsWithTeam = plans
        //            .Select(p => p.Id)
        //            .ToList();

        //        var planLinesRaw = await _context.MesFactoryPlanLines.AsNoTracking()
        //            .Where(l => planIdsWithTeam.Contains(l.FactoryPlanId)
        //                     && l.WorkDate >= monthStartDt
        //                     && l.WorkDate <= toMonthEndDt)
        //            .ToListAsync();

        //        var teamTotalQtyMap = planLinesRaw
        //            .GroupBy(l => l.FactoryPlanId)
        //            .ToDictionary(
        //                g => g.Key,
        //                g => g
        //                    .GroupBy(l => l.WorkDate.Date)              // gộp nếu trùng ngày (tránh lỗi duplicate key)
        //                    .ToDictionary(
        //                        dg => dg.Key.ToString("yyyy-MM-dd"),
        //                        dg => dg.Sum(x => x.PlannedQty)
        //                    )
        //            );
        //        // ---- Lookup PassOutput theo từng ngày (Thực hiện) — dựa theo PlanId ----
        //        var qlclByPlanRaw = await _context.qualityManagementSessions.AsNoTracking()
        //         .Where(q => q.PlanId != null
        //                  && planIdsWithTeam.Contains(q.PlanId.Value)
        //                  && q.InspectionDate >= monthStartDt
        //                  && q.InspectionDate <= toMonthEndDt && q.Status == 1 && q.OperationType == "1")
        //         .Select(q => new { q.PlanId, q.InspectionDate, q.PassOutputQuantity })
        //         .ToListAsync();

        //        var actualQtyByDateMap = qlclByPlanRaw
        //            .GroupBy(q => q.PlanId)
        //            .ToDictionary(
        //                g => g.Key,
        //                g => g
        //                    .GroupBy(x => x.InspectionDate.Date)   // gộp nếu trùng ngày
        //                    .ToDictionary(
        //                        dg => dg.Key.ToString("yyyy-MM-dd"),
        //                        dg => dg.Sum(x => x.PassOutputQuantity)
        //                    )
        //            );
        //        var today = DateTime.Today;
        //        // planned_qty tại work_date = hôm nay, cho TẤT CẢ các dòng đã gán tổ
        //        //    (không giới hạn theo khoảng monthStart..toMonthEnd, vì "hôm nay" có thể
        //        //    nằm ngoài khoảng filter đang xem)
        //        var todayPlannedQtyMap = await _context.MesFactoryPlanLines
        //            .Where(l => planIdsWithTeam.Contains(l.FactoryPlanId) && l.WorkDate.Date == today)
        //            .GroupBy(l => l.FactoryPlanId)
        //            .Select(g => new { FactoryPlanId = g.Key, Qty = g.Sum(x => x.PlannedQty) })
        //            .ToDictionaryAsync(x => x.FactoryPlanId, x => x.Qty);

        //        //Bỏ active=true
        //        var mergedSourcePlans = await _context.MesFactoryPlans
        //        .Where(s => s.ParentPlanId != null
        //                 && planIdsWithTeam.Contains(s.ParentPlanId.Value)
        //                 && s.LineNumber != null)      // <-- THÊM điều kiện này
        //        .ToListAsync();

        //        var mergedSourceByParentId = mergedSourcePlans
        //            .GroupBy(s => s.ParentPlanId!.Value)
        //            .ToDictionary(g => g.Key, g => g.ToList());

        //        var sourcePoIds = mergedSourcePlans
        //        .Select(s => s.PoId)
        //        .Distinct()
        //        .Where(id => !poNameMap.ContainsKey(id) || string.IsNullOrWhiteSpace(poNameMap[id]))
        //        .ToList();

        //        if (sourcePoIds.Count > 0)
        //        {
        //            var sourcePoNameFromHeader = await _context.SalesOrderHeader
        //                .Where(s => sourcePoIds.Contains(s.No))
        //                .Select(s => new { s.No, s.POName })
        //                .ToDictionaryAsync(s => s.No, s => s.POName ?? string.Empty);

        //            foreach (var kv in sourcePoNameFromHeader)
        //                poNameMap[kv.Key] = kv.Value;

        //            var stillMissing = sourcePoIds
        //                .Where(id => !poNameMap.TryGetValue(id, out var n) || string.IsNullOrWhiteSpace(n))
        //                .ToList();

        //            if (stillMissing.Count > 0)
        //            {
        //                var fallback = await _context.MesProductionOrderItems
        //                    .Where(i => stillMissing.Contains(i.PoId) && !string.IsNullOrEmpty(i.POName))
        //                    .GroupBy(i => i.PoId)
        //                    .Select(g => new
        //                    {
        //                        PoId = g.Key,
        //                        POName = g.OrderByDescending(x => x.CreatedAt).Select(x => x.POName).First()
        //                    })
        //                    .ToDictionaryAsync(x => x.PoId, x => x.POName ?? string.Empty);

        //                foreach (var kv in fallback)
        //                    poNameMap[kv.Key] = kv.Value;
        //            }
        //        }

        //        // =========================================================
        //        // Lookup planned_qty theo NGÀY cho từng dòng PO GỐC đã gộp (MesFactoryPlanLines)
        //        // Cùng khoảng [monthStart, toMonthEnd] như teamTotalQtyMap để nhất quán dữ liệu hiển thị.
        //        // =========================================================
        //        var mergedSourcePlanIds = mergedSourcePlans.Select(s => s.Id).ToList();

        //        var mergedSourceLinesRaw = mergedSourcePlanIds.Count > 0
        //            ? await _context.MesFactoryPlanLines
        //                .Where(l => mergedSourcePlanIds.Contains(l.FactoryPlanId)
        //                         && l.WorkDate >= monthStartDt
        //                         && l.WorkDate <= toMonthEndDt)
        //                .ToListAsync()
        //            : new List<MesFactoryPlanLine>();

        //        var mergedSourceDailyQtyMap = mergedSourceLinesRaw
        //            .GroupBy(l => l.FactoryPlanId)
        //            .ToDictionary(
        //                g => g.Key,
        //                g => g
        //                    .GroupBy(l => l.WorkDate.Date)          // gộp nếu trùng ngày
        //                    .ToDictionary(
        //                        dg => dg.Key.ToString("yyyy-MM-dd"),
        //                        dg => dg.Sum(x => x.PlannedQty)
        //                    )
        //            );
        //        // =========================================================
        //        // Lookup Actual (PassOutput) theo NGÀY cho từng dòng PO GỐC đã gộp
        //        // (tương tự actualQtyByDateMap nhưng áp dụng cho mergedSourcePlanIds thay vì planIdsWithTeam)
        //        // =========================================================
        //        var mergedSourceQlclRaw = await _context.qualityManagementSessions
        //        .Where(q => q.PlanId != null
        //                 && mergedSourcePlanIds.Contains(q.PlanId.Value)
        //                 && q.InspectionDate >= monthStartDt && q.Status == 0
        //                 && q.InspectionDate <= toMonthEndDt)
        //        .Select(q => new
        //        {
        //            PlanId = q.PlanId.Value,
        //            q.InspectionDate,
        //            q.PassOutputQuantity
        //        })
        //        .ToListAsync();

        //        var mergedSourceActualByDateMap = mergedSourceQlclRaw
        //            .GroupBy(q => q.PlanId)
        //            .ToDictionary(
        //                g => g.Key,
        //                g => g
        //                    .GroupBy(x => x.InspectionDate.Date)
        //                    .ToDictionary(
        //                        dg => dg.Key.ToString("yyyy-MM-dd"),
        //                        dg => dg.Sum(x => x.PassOutputQuantity)
        //                    )
        //            );
        //        // Helper: build danh sách PO gốc đã gộp cho 1 dòng factory_plan hiện tại
        //        List<object> ResolveMergedFromPos(Guid planId)
        //        {
        //            if (!mergedSourceByParentId.TryGetValue(planId, out var sources))
        //                return new List<object>();

        //            return sources
        //                .OrderBy(s => s.PoId)
        //                .Select(s => new
        //                {
        //                    factoryPlanId = s.Id,
        //                    poId = s.PoId,
        //                    poName = poNameMap.TryGetValue(s.PoId, out var pn) ? pn : string.Empty,
        //                    planCode = s.PlanCode,
        //                    lineNumber = s.LineNumber,
        //                    operationCode = s.OperationCode,
        //                    totalPlannedQty = s.TotalPlannedQty,
        //                    status = s.Status,       // "merged_source"
        //                    mergedAt = s.MergedAt,
        //                    mergedBy = s.MergedBy,

        //                    // ===== MỚI: KH theo từng ngày (work_date) của dòng PO gốc này =====
        //                    dailyPlannedQty = mergedSourceDailyQtyMap.TryGetValue(s.Id, out var qtyByDate)
        //                        ? qtyByDate
        //                        : new Dictionary<string, int>(),
        //                    // ===== MỚI: actual theo ngày của PO gốc =====
        //                    dailyActualQty = mergedSourceActualByDateMap.TryGetValue(s.Id, out var actualByDate)
        //                        ? actualByDate
        //                        : new Dictionary<string, int>(),

        //                    // Cờ để FE biết PO gốc này có dữ liệu thực hiện hay không -> hiển thị badge cảnh báo nếu cần
        //                    hasActualData = mergedSourceActualByDateMap.ContainsKey(s.Id),
        //                })
        //                .ToList<object>();
        //        }

        //        var todayEndDt = DateTime.Today.AddDays(1).AddTicks(-1); // 23:59:59.999... hôm nay

        //        // Mở rộng style/color thêm từ planLinePlans để actualQtyMap phủ đủ cho tab Kế hoạch sản xuất
        //        var allStyleIdsForActual = styleIds.Concat(planLinePlans.Select(p => p.StyleId)).Distinct().ToList();
        //        var allColorIdsForActual = colorIds.Concat(planLinePlans.Select(p => p.ColorId)).Distinct().ToList();

        //        var qlclRaw = await _context.qualityManagementSessions
        //            .Where(q => allStyleIdsForActual.Contains(q.ItemNo)
        //                     && allColorIdsForActual.Contains(q.ColorNo)
        //                     && q.InspectionDate <= todayEndDt
        //                     && planTeamIds.Contains(q.WorkShiftCode) && q.WorkCenterCode == factoryId && q.Status == 1)
        //            .Select(q => new { q.WorkShiftCode, q.ItemNo, q.ColorNo, q.PassOutputQuantity })
        //            .ToListAsync();

        //        var actualQtyMap = qlclRaw
        //            .GroupBy(q => (WorkShift: q.WorkShiftCode, StyleId: q.ItemNo, ColorId: q.ColorNo))
        //            .ToDictionary(g => g.Key, g => g.Sum(x => x.PassOutputQuantity));

        //        // ---- Lookup Đơn giá CM: lấy giá còn hiệu lực trong khoảng [monthStart, toMonthEnd] ----
        //        var allCmPrices = await _context.StyleCmPrice.AsNoTracking()
        //            .Where(c => planStyleIds.Contains(c.StyleId) && c.IsActive == true)
        //            .ToListAsync();

        //        var cmPriceByStyle = allCmPrices
        //            .GroupBy(c => c.StyleId, StringComparer.OrdinalIgnoreCase)
        //            .Select(g =>
        //            {
        //                var picked = g
        //                    .Where(c => c.EffectiveDate <= toMonthEndDt)
        //                    .OrderByDescending(c => c.EffectiveDate)
        //                     .ThenByDescending(c => c.CreatedAt)
        //                    .FirstOrDefault();
        //                return new { StyleId = g.Key, Price = picked };
        //            })
        //            .Where(x => x.Price != null)
        //            .ToDictionary(x => x.StyleId, x => x.Price!, StringComparer.OrdinalIgnoreCase);

        //        var cmPriceIds = cmPriceByStyle.Values.Select(c => c.Id).ToList();

        //        var cmSplits = await _context.StyleCmSplits.AsNoTracking()
        //            .Where(s => cmPriceIds.Contains(s.StyleCmPriceId))
        //            .ToListAsync();

        //        var splitsByCmPriceId = cmSplits
        //            .GroupBy(s => s.StyleCmPriceId)
        //            .ToDictionary(g => g.Key, g => g.ToList());

        //        // Hàm helper: lấy đơn giá CM đã resolve (ưu tiên theo operationCode nếu có split khớp)
        //        object? ResolveCmPrice(string styleId, string? operationCode)
        //        {
        //            if (!cmPriceByStyle.TryGetValue(styleId, out var cmPrice)) return null;
        //            splitsByCmPriceId.TryGetValue(cmPrice.Id, out var splitsForThisPrice);
        //            var matchedSplit = splitsForThisPrice?.FirstOrDefault(s => s.OperationCode == operationCode);
        //            return new
        //            {
        //                styleId = cmPrice.StyleId,
        //                price = matchedSplit?.SplitPrice ?? cmPrice.CmPrice,
        //                currency = cmPrice.Currency,
        //                effectiveDate = cmPrice.EffectiveDate,
        //                operationCode = operationCode,
        //                operationName = matchedSplit?.OperationName,
        //            };
        //        }

        //        // =========================================================
        //        // TAB ĐƠN HÀNG: gộp theo VersionId + StyleId + PoId + ColorId
        //        // =========================================================
        //        var rawItems = await _context.MesProductionOrderItems
        //         .Where(x => styleIds.Contains(x.StyleId)
        //                  && poIds.Contains(x.PoId)
        //                  && colorIds.Contains(x.ColorId) && x.FactoryId == factoryId && x.IsActive == true)
        //         .Select(x => new { x.StyleId, x.PoId, x.ColorId, x.SizeId, x.PlannedQty, x.VersionId })
        //         .ToListAsync();

        //        var sizeIdDict = rawItems
        //        .GroupBy(x => (x.StyleId, x.PoId, x.ColorId, x.VersionId))
        //        .ToDictionary(
        //            g => g.Key,
        //            g => g
        //                .GroupBy(x => x.SizeId)
        //                .Select(sg => new SizeQtyDto(sg.Key, sg.Sum(x => x.PlannedQty)))
        //                .ToList()
        //        );
        //        // ---- Precompute thông tin nhóm để phát hiện "đã tách" (cùng group key với orders) ----
        //        var groupedForSplit = plans
        //            .GroupBy(p => new { p.VersionId, p.StyleId, p.PoId, p.ColorId })
        //            .ToDictionary(
        //                g => g.Key,
        //                g => new
        //                {
        //                    AssignedCount = g.Count(p => !string.IsNullOrWhiteSpace(p.LineNumber)),
        //                    HasGoc = g.Any(p => string.IsNullOrWhiteSpace(p.LineNumber)),
        //                    KhTongGoc = g.Where(p => string.IsNullOrWhiteSpace(p.LineNumber))
        //                                 .Select(p => (int?)p.TotalPlannedQty)
        //                                 .DefaultIfEmpty(null)
        //                                 .Max(),
        //                });
        //        var orders = plans
        //            .GroupBy(p => new { p.VersionId, p.StyleId, p.PoId, p.ColorId })
        //            .Select(g =>
        //            {
        //                var first = g.First();
        //                var resolvedStyleName = itemNameMap.TryGetValue(first.StyleId, out var sName) ? sName : string.Empty;
        //                var resolvedPoName = poNameMap.TryGetValue(first.PoId, out var pName) ? pName : string.Empty;
        //                var key = (first.VersionId, first.FactoryId, first.StyleId, first.PoId, first.ColorId);
        //                var resolvedTargetDeliveryDate = targetDeliveryMap.TryGetValue(key, out var tDate) ? tDate : (DateOnly?)null;
        //                // Chỉ lấy các dòng có LineNumber == null (đại diện KH tổng gốc)
        //                var dongGoc = g.Where(p => string.IsNullOrWhiteSpace(p.LineNumber)).ToList();

        //                // Chỉ lấy các dòng đã gán tổ
        //                var dongDaGan = g.Where(p => !string.IsNullOrWhiteSpace(p.LineNumber)).ToList();

        //                var sizeKey = (first.StyleId, first.PoId, first.ColorId, first.VersionId);
        //                var resolvedSizeQtyList = sizeIdDict.TryGetValue(sizeKey, out var sizeQtyList)
        //                    ? sizeQtyList
        //                    : new List<SizeQtyDto>();

        //                var khTong = dongGoc.Count > 0
        //        ? dongGoc.Max(p => p.TotalPlannedQty)   // dùng Max thay vì Sum để tránh cộng dồn nếu có dòng gốc trùng do bug dữ liệu
        //        : dongDaGan
        //            .GroupBy(p => p.OperationCode, StringComparer.OrdinalIgnoreCase)
        //            .Select(gr => gr.Sum(p => p.TotalPlannedQty))
        //            .DefaultIfEmpty(0)
        //            .Max();
        //                var conLaiChuaPhanBo = dongGoc.Sum(p => p.TotalPlannedQty); // giữ nguyên nếu chỉ có đúng 1 dòng gốc
        //                                                                            //        var daPhanBo = khTong - conLaiChuaPhanBo;
        //                string trangThai;
        //                if (dongDaGan.Count > 0)
        //                {
        //                    // Đã có ít nhất 1 dòng được gán tổ => coi là đã phân bổ,
        //                    // bất kể daPhanBo tính ra có bằng 0 hay không (tránh sai do bug dữ liệu)
        //                    trangThai = "da_phan_bo";
        //                }
        //                else
        //                {
        //                    trangThai = "chua_phan_to";
        //                }


        //                var danhSachToDaGan = g
        //                    .Where(p => !string.IsNullOrWhiteSpace(p.LineNumber))
        //                    .Select(p => new
        //                    {
        //                        factoryPlanId = p.Id,
        //                        lineNumber = p.LineNumber,
        //                        operationCode = p.OperationCode,
        //                        soLuong = p.TotalPlannedQty,
        //                        cmPrice = ResolveCmPrice(p.StyleId, p.OperationCode),
        //                    })
        //                    .ToList();

        //                return new
        //                {
        //                    id = first.Id,
        //                    versionId = first.VersionId,
        //                    styleId = first.StyleId,
        //                    styleName = resolvedStyleName,
        //                    poId = first.PoId,
        //                    poName = resolvedPoName,
        //                    colorId = first.ColorId,
        //                    sizeIds = resolvedSizeQtyList.Select(x => new { sizeId = x.SizeId, plannedQty = x.PlannedQty }).ToList(),
        //                    colorName = first.ColorId,
        //                    targetDeliveryDate = resolvedTargetDeliveryDate?.ToString("dd-MM-yyyy") ?? string.Empty,
        //                    khTong,
        //                    conLaiChuaPhanBo,
        //                    trangThai,
        //                    danhSachToDaGan,
        //                    cmPrice = ResolveCmPrice(first.StyleId, null), // giá tổng hiển thị ở header dòng
        //                    status = first.Status,
        //                };
        //            })
        //            .OrderBy(o => o.styleId)
        //            .ThenBy(o => o.poId)
        //            .ToList();

        //        // =========================================================
        //        // TAB KẾ HOẠCH: chỉ các dòng ĐÃ có tổ, kèm lịch theo ngày
        //        // =========================================================
        //        // ---- Lookup các plan CHA (PO gốc trước khi tách) cho các dòng đã gán tổ có ParentPlanId ----
        //        var parentPlanIds = planLinePlans
        //            .Where(p => p.ParentPlanId != null)
        //            .Select(p => p.ParentPlanId!.Value)
        //            .Distinct()
        //            .ToList();
        //        //Bỏ isactive=true
        //        var parentPlansMap = parentPlanIds.Count > 0
        //            ? await _context.MesFactoryPlans
        //                .Where(pp => parentPlanIds.Contains(pp.Id))
        //                .ToDictionaryAsync(pp => pp.Id)
        //            : new Dictionary<Guid, MesFactoryPlan>();

        //        // Bổ sung PoName / StyleName cho PO gốc (phòng trường hợp PoId/StyleId gốc chưa có trong map hiện tại)
        //        var parentPoIds = parentPlansMap.Values
        //            .Select(pp => pp.PoId)
        //            .Distinct()
        //            .Where(id => !poNameMap.ContainsKey(id) || string.IsNullOrWhiteSpace(poNameMap[id]))
        //            .ToList();

        //        if (parentPoIds.Count > 0)
        //        {
        //            var parentPoNameFromHeader = await _context.SalesOrderHeader
        //                .Where(s => parentPoIds.Contains(s.No))
        //                .Select(s => new { s.No, s.POName })
        //                .ToDictionaryAsync(s => s.No, s => s.POName ?? string.Empty);

        //            foreach (var kv in parentPoNameFromHeader)
        //                poNameMap[kv.Key] = kv.Value;

        //            var stillMissingParentPo = parentPoIds
        //                .Where(id => !poNameMap.TryGetValue(id, out var n) || string.IsNullOrWhiteSpace(n))
        //                .ToList();

        //            if (stillMissingParentPo.Count > 0)
        //            {
        //                var fallback = await _context.MesProductionOrderItems
        //                    .Where(i => stillMissingParentPo.Contains(i.PoId) && !string.IsNullOrEmpty(i.POName))
        //                    .GroupBy(i => i.PoId)
        //                    .Select(g => new
        //                    {
        //                        PoId = g.Key,
        //                        POName = g.OrderByDescending(x => x.CreatedAt).Select(x => x.POName).First()
        //                    })
        //                    .ToDictionaryAsync(x => x.PoId, x => x.POName ?? string.Empty);

        //                foreach (var kv in fallback)
        //                    poNameMap[kv.Key] = kv.Value;
        //            }
        //        }

        //        var parentStyleIds = parentPlansMap.Values
        //            .Select(pp => pp.StyleId)
        //            .Distinct()
        //            .Where(id => !itemNameMap.ContainsKey(id))
        //            .ToList();

        //        if (parentStyleIds.Count > 0)
        //        {
        //            var parentItemNames = await _context.Item
        //                .Where(i => parentStyleIds.Contains(i.No))
        //                .Select(i => new { i.No, i.Name })
        //                .ToDictionaryAsync(i => i.No, i => i.Name ?? string.Empty);

        //            foreach (var kv in parentItemNames)
        //                itemNameMap[kv.Key] = kv.Value;
        //        }

        //        // =========================================================
        //        // Lookup planned_qty & actual theo NGÀY cho PO GỐC (trước khi tách) — dùng cho splitFromPo
        //        // Cùng khoảng [monthStart, toMonthEnd] để nhất quán với mergedSource...
        //        // =========================================================
        //        var parentPlanLinesRaw = parentPlanIds.Count > 0
        //            ? await _context.MesFactoryPlanLines
        //                .Where(l => parentPlanIds.Contains(l.FactoryPlanId)
        //                         && l.WorkDate >= monthStartDt
        //                         && l.WorkDate <= toMonthEndDt)
        //                .ToListAsync()
        //            : new List<MesFactoryPlanLine>();

        //        var parentPlanDailyQtyMap = parentPlanLinesRaw
        //            .GroupBy(l => l.FactoryPlanId)
        //            .ToDictionary(
        //                g => g.Key,
        //                g => g
        //                    .GroupBy(l => l.WorkDate.Date)
        //                    .ToDictionary(
        //                        dg => dg.Key.ToString("yyyy-MM-dd"),
        //                        dg => dg.Sum(x => x.PlannedQty)
        //                    )
        //            );

        //        var parentPlanQlclRaw = await _context.qualityManagementSessions
        //            .Where(q => q.PlanId != null
        //                     && parentPlanIds.Contains(q.PlanId.Value)
        //                     && q.InspectionDate >= monthStartDt
        //                     && q.InspectionDate <= toMonthEndDt && q.Status == 0)
        //            .Select(q => new { PlanId = q.PlanId.Value, q.InspectionDate, q.PassOutputQuantity })
        //            .ToListAsync();

        //        var parentPlanActualByDateMap = parentPlanQlclRaw
        //            .GroupBy(q => q.PlanId)
        //            .ToDictionary(
        //                g => g.Key,
        //                g => g
        //                    .GroupBy(x => x.InspectionDate.Date)
        //                    .ToDictionary(
        //                        dg => dg.Key.ToString("yyyy-MM-dd"),
        //                        dg => dg.Sum(x => x.PassOutputQuantity)
        //                    )
        //            );

        //        // Đếm số dòng (tổ) đã được tách ra từ cùng 1 plan cha -> dùng cho soDongDaTach
        //        var splitSiblingCountMap = planLinePlans
        //            .Where(p => p.ParentPlanId != null)
        //            .GroupBy(p => p.ParentPlanId!.Value)
        //            .ToDictionary(g => g.Key, g => g.Count());

        //        var planLines = planLinePlans
        //            .Select(p =>
        //            {
        //                var resolvedStyleName = itemNameMap.TryGetValue(p.StyleId, out var sName) ? sName : string.Empty;
        //                var resolvedPoName = poNameMap.TryGetValue(p.PoId, out var pName) ? pName : string.Empty;

        //                var key = (p.VersionId, p.FactoryId, p.StyleId, p.PoId, p.ColorId);
        //                var resolvedTargetDeliveryDate = targetDeliveryMap.TryGetValue(key, out var tDate) ? tDate : (DateOnly?)null;
        //                var resolvedTeamTotalQty = teamTotalQtyMap.TryGetValue(p.Id, out var qtyByDate)
        //                    ? qtyByDate
        //                    : new Dictionary<string, int>();
        //                // === NEW: PassOutput theo ngày (Thực hiện) ===
        //                var resolvedActualQtyByDate = actualQtyByDateMap.TryGetValue(p.Id, out var actualByDate)
        //                    ? actualByDate
        //                    : new Dictionary<string, int>();
        //                // ==== tính status ====
        //                var todayPlannedQty = todayPlannedQtyMap.TryGetValue(p.Id, out var tpq) ? tpq : 0;

        //                var actualKey = (WorkShift: p.LineNumber, StyleId: p.StyleId, ColorId: p.ColorId);
        //                var actualQty = actualQtyMap.TryGetValue(actualKey, out var aq) ? aq : 0;

        //                var remainingQty = p.TotalPlannedQty - actualQty;

        //                string computedStatus;
        //                if (actualQty >= p.TotalPlannedQty)
        //                {
        //                    computedStatus = "hoan_thanh";       // Hoàn thành
        //                }
        //                else if (todayPlannedQty > 0)
        //                {
        //                    computedStatus = "dang_san_xuat";    // Đang sản xuất
        //                }
        //                else if (remainingQty > 0)
        //                {
        //                    computedStatus = "tam_dung";         // Tạm dừng
        //                }
        //                else
        //                {
        //                    computedStatus = p.Status; // fallback hiếm khi xảy ra
        //                }
        //                // ===== MỚI: xác định dòng này có phải là kết quả của việc TÁCH PO gốc không =====
        //                bool isSplited = p.ParentPlanId != null && parentPlansMap.ContainsKey(p.ParentPlanId.Value);

        //                object? splitFromPo = null;
        //                if (isSplited)
        //                {
        //                    var parentPlan = parentPlansMap[p.ParentPlanId!.Value];
        //                    var parentPoName = poNameMap.TryGetValue(parentPlan.PoId, out var ppn) ? ppn : string.Empty;
        //                    var parentStyleName = itemNameMap.TryGetValue(parentPlan.StyleId, out var psn) ? psn : string.Empty;
        //                    var parentDailyPlannedQty = parentPlanDailyQtyMap.TryGetValue(parentPlan.Id, out var pQtyByDate)
        //                       ? pQtyByDate
        //                       : new Dictionary<string, int>();
        //                    var parentDailyActualQty = parentPlanActualByDateMap.TryGetValue(parentPlan.Id, out var pActualByDate)
        //                        ? pActualByDate
        //                        : new Dictionary<string, int>();

        //                    splitFromPo = new
        //                    {
        //                        factoryPlanId = parentPlan.Id,          // id của plan gốc
        //                        poId = parentPlan.PoId,                 // PO GỐC (đúng yêu cầu)
        //                        poName = parentPoName,
        //                        styleId = parentPlan.StyleId,
        //                        styleName = parentStyleName,
        //                        colorId = parentPlan.ColorId,
        //                        versionId = parentPlan.VersionId,
        //                        planCode = parentPlan.PlanCode,
        //                        khTongGoc = parentPlan.TotalPlannedQty,  // tổng KH của PO gốc trước khi tách
        //                        soDongDaTach = splitSiblingCountMap.TryGetValue(parentPlan.Id, out var cnt) ? cnt : 1,
        //                        // ===== MỚI: KH & thực tế theo ngày của PO gốc trước khi tách =====
        //                        dailyPlannedQty = parentDailyPlannedQty,
        //                        dailyActualQty = parentDailyActualQty,
        //                        hasActualData = parentPlanActualByDateMap.ContainsKey(parentPlan.Id),
        //                    };
        //                }

        //                return new
        //                {
        //                    id = p.Id,
        //                    planCode = p.PlanCode,
        //                    factoryId = p.FactoryId,
        //                    lineNumber = p.LineNumber,
        //                    styleId = p.StyleId,
        //                    styleName = resolvedStyleName,
        //                    poId = p.PoId,
        //                    poName = resolvedPoName,
        //                    colorId = p.ColorId,
        //                    colorName = p.ColorId,
        //                    //productionStartDate = p.ProductionStartDate,
        //                    //productionEndDate = p.ProductionEndDate,
        //                    //targetDeliveryDate = resolvedTargetDeliveryDate,
        //                    totalPlannedQty = p.TotalPlannedQty,
        //                    teamTotalQty = resolvedTeamTotalQty,
        //                    teamActualQty = resolvedActualQtyByDate,
        //                    operationCode = p.OperationCode,
        //                    cmPrice = ResolveCmPrice(p.StyleId, p.OperationCode),
        //                    status = computedStatus,
        //                    // ===== MỚI: các PO gốc đã gộp vào dòng này (nếu có) =====
        //                    isMerged = mergedSourceByParentId.ContainsKey(p.Id),
        //                    mergedFromPos = ResolveMergedFromPos(p.Id),
        //                    isSplited = isSplited,
        //                    splitFromPo = splitFromPo,
        //                    targetDeliveryDate = resolvedTargetDeliveryDate?.ToString("dd-MM-yyyy") ?? string.Empty,
        //                };
        //            })
        //            .OrderBy(pl => pl.lineNumber)
        //            .ThenBy(pl => pl.styleId)
        //            .ToList();

        //        return Ok(new
        //        {
        //            success = true,
        //            data = new { orders, planLines }
        //        });
        //    }

        [HttpGet("factory-plans-v2/orders")]
        public async Task<IActionResult> GetFactoryOrders_V2(
            [FromQuery] string factoryId,
            [FromQuery] int year, [FromQuery] int month,
            [FromQuery] int toYear, [FromQuery] int toMonth,
            [FromQuery] string? lineNumber = null,
            [FromQuery] string? styleName = null)
        {
            var error = ValidatePeriod(factoryId, year, month, toYear, toMonth, out var from, out var to);
            if (error != null) return error;

            var orders = await BuildOrdersAsync(factoryId, from, to, lineNumber, styleName);
            return Ok(new { success = true, data = new { orders } });
        }

        [HttpGet("factory-plans-v2/plan-lines")]
        public async Task<IActionResult> GetFactoryPlanLines_V2(
            [FromQuery] string factoryId,
            [FromQuery] int year, [FromQuery] int month,
            [FromQuery] int toYear, [FromQuery] int toMonth,
            [FromQuery] string? lineNumber = null,
            [FromQuery] string? styleName = null)
        {
            var error = ValidatePeriod(factoryId, year, month, toYear, toMonth, out var from, out var to);
            if (error != null) return error;

            var planLines = await BuildPlanLinesAsync(factoryId, from, to, lineNumber, styleName);
            return Ok(new { success = true, data = new { planLines } });
        }

        private IActionResult? ValidatePeriod(string factoryId, int year, int month, int toYear, int toMonth,
    out DateOnly from, out DateOnly to)
        {
            from = default;
            to = default;

            if (string.IsNullOrWhiteSpace(factoryId))
                return BadRequest(new { success = false, message = "Vui lòng truyền factoryId." });
            if (year <= 0 || toYear <= 0 || month < 1 || month > 12 || toMonth < 1 || toMonth > 12)
                return BadRequest(new { success = false, message = "year và month không hợp lệ." });

            from = new DateOnly(year, month, 1);
            to = new DateOnly(toYear, toMonth, 1).AddMonths(1).AddDays(-1);
            return null;
        }

        private sealed class PlanBoardBase
        {
            public List<MesFactoryPlan> Plans { get; init; } = new();
            public Dictionary<(Guid VersionId, string FactoryId, string StyleId, string PoId, string ColorId), DateOnly?>
                TargetDeliveryMap
            { get; init; } = new();
        }

        /// <summary>
        /// Query plan nền dùng chung cho 2 tab: plan active của nhà máy, lọc theo Tổ / Style.
        /// onlyAssigned = true: chỉ lấy các dòng ĐÃ gán Tổ (tab Kế hoạch sản xuất).
        /// </summary>
        private async Task<IQueryable<MesFactoryPlan>> CreatePlanQueryAsync(
            string factoryId, string? lineNumber, string? styleName, bool onlyAssigned)
        {
            var planQuery = _context.MesFactoryPlans.AsNoTracking()
                .Where(p => p.FactoryId == factoryId && p.IsActive);

            if (onlyAssigned)
                planQuery = planQuery.Where(p => p.LineNumber != null && p.LineNumber != "");

            if (!string.IsNullOrWhiteSpace(lineNumber))
                planQuery = planQuery.Where(p => p.LineNumber == lineNumber);

            if (!string.IsNullOrWhiteSpace(styleName))
            {
                var trimKeyword = styleName.Trim();
                var keyword = await _context.Item
                    .Where(p => p.Name == trimKeyword)
                    .Select(p => p.No)
                    .FirstOrDefaultAsync();
                if (!string.IsNullOrEmpty(keyword))
                    planQuery = planQuery.Where(p => p.StyleId.Contains(keyword));
            }

            return planQuery;
        }

        /// <summary>
        /// Bước lọc nền cho TAB ĐƠN HÀNG: plan active của nhà máy -> JOIN production_order_items theo tổ hợp
        /// 5 cột -> giữ các tổ hợp có TargetDeliveryDate (ngày giao hàng) nằm trong kỳ [from, to].
        /// (Tab Kế hoạch sản xuất KHÔNG dùng hàm này — nó lọc theo ProductionStartDate, xem BuildPlanLinesAsync.)
        /// </summary>
        private async Task<PlanBoardBase> LoadPlanBoardBaseAsync(
            string factoryId, DateOnly from, DateOnly to,
            string? lineNumber, string? styleName)
        {
            var planQuery = await CreatePlanQueryAsync(factoryId, lineNumber, styleName, onlyAssigned: false);

            var planKeys = planQuery
                .Select(p => new { p.VersionId, p.FactoryId, p.StyleId, p.PoId, p.ColorId })
                .Distinct();

            var filteredGroups = await _context.MesProductionOrderItems
                .AsNoTracking()
                .Where(x => x.FactoryId == factoryId)
                .Join(planKeys,
                    x => new { x.VersionId, x.FactoryId, x.StyleId, x.PoId, x.ColorId },
                    k => new { k.VersionId, k.FactoryId, k.StyleId, k.PoId, k.ColorId },
                    (x, k) => x)
                .GroupBy(x => new { x.VersionId, x.FactoryId, x.StyleId, x.PoId, x.ColorId })
                .Select(g => new
                {
                    g.Key.VersionId,
                    g.Key.FactoryId,
                    g.Key.StyleId,
                    g.Key.PoId,
                    g.Key.ColorId,
                    TargetDeliveryDate = g.Max(x => x.TargetDeliveryDate)
                })
                .Where(g => g.TargetDeliveryDate >= from && g.TargetDeliveryDate <= to)
                .ToListAsync();

            if (filteredGroups.Count == 0)
                return new PlanBoardBase();

            var validKeys = filteredGroups
                .Select(g => (g.VersionId, g.FactoryId, g.StyleId, g.PoId, g.ColorId))
                .ToHashSet();

            var targetDeliveryMap = new Dictionary<(Guid VersionId, string FactoryId, string StyleId, string PoId, string ColorId), DateOnly?>();
            foreach (var g in filteredGroups)
                targetDeliveryMap[(g.VersionId, g.FactoryId, g.StyleId, g.PoId, g.ColorId)] = g.TargetDeliveryDate;

            var groupVersionIds = filteredGroups.Select(g => g.VersionId).Distinct().ToList();
            var groupStyleIds = filteredGroups.Select(g => g.StyleId).Distinct().ToList();
            var groupPoIds = filteredGroups.Select(g => g.PoId).Distinct().ToList();
            var groupColorIds = filteredGroups.Select(g => g.ColorId).Distinct().ToList();

            var candidatePlans = await planQuery
                .Where(p => groupVersionIds.Contains(p.VersionId)
                         && groupStyleIds.Contains(p.StyleId)
                         && groupPoIds.Contains(p.PoId)
                         && groupColorIds.Contains(p.ColorId))
                .OrderBy(p => p.LineNumber)
                .ThenBy(p => p.StyleId)
                .ToListAsync();

            // Lọc chính xác lại theo tổ hợp 5 cột (Contains riêng lẻ ở trên có thể match chéo giữa các plan khác nhau)
            var plans = candidatePlans
                .Where(p => validKeys.Contains((p.VersionId, p.FactoryId, p.StyleId, p.PoId, p.ColorId)))
                .ToList();

            return new PlanBoardBase { Plans = plans, TargetDeliveryMap = targetDeliveryMap };
        }

        private async Task<Dictionary<string, string>> LoadItemNamesAsync(IEnumerable<string> styleIds)
        {
            var ids = styleIds.Distinct().ToList();
            if (ids.Count == 0) return new Dictionary<string, string>();

            return await _context.Item
                .Where(i => ids.Contains(i.No))
                .Select(i => new { i.No, i.Name })
                .ToDictionaryAsync(i => i.No, i => i.Name ?? string.Empty);
        }

        /// <summary>
        /// Bổ sung tên PO còn thiếu vào poNameMap. Gộp 3 khối tra tên trùng lặp của code cũ làm 1.
        /// Ưu tiên: production_order_items (tên mới nhất) -> Sales Order Header.
        /// </summary>
        private async Task EnsurePoNamesAsync(Dictionary<string, string> poNameMap, IEnumerable<string> poIds)
        {
            var missing = poIds.Distinct()
                .Where(id => !poNameMap.TryGetValue(id, out var n) || string.IsNullOrWhiteSpace(n))
                .ToList();
            if (missing.Count == 0) return;

            var fromMes = await _context.MesProductionOrderItems
                .Where(i => missing.Contains(i.PoId) && !string.IsNullOrEmpty(i.POName))
                .GroupBy(i => i.PoId)
                .Select(g => new
                {
                    PoId = g.Key,
                    POName = g.OrderByDescending(x => x.CreatedAt).Select(x => x.POName).First()
                })
                .ToDictionaryAsync(x => x.PoId, x => x.POName ?? string.Empty);

            foreach (var kv in fromMes)
                poNameMap[kv.Key] = kv.Value;

            var stillMissing = missing
                .Where(id => !poNameMap.TryGetValue(id, out var n) || string.IsNullOrWhiteSpace(n))
                .ToList();
            if (stillMissing.Count == 0) return;

            var fromHeader = await _context.SalesOrderHeader
                .Where(s => stillMissing.Contains(s.No))
                .Select(s => new { s.No, s.POName })
                .ToDictionaryAsync(s => s.No, s => s.POName ?? string.Empty);

            foreach (var kv in fromHeader)
                poNameMap[kv.Key] = kv.Value;
        }

        /// <summary>Trả về hàm tra đơn giá CM (ưu tiên split theo operationCode nếu có) — dùng chung 2 tab.</summary>
        private async Task<Func<string, string?, object?>> BuildCmPriceResolverAsync(IEnumerable<string> styleIds, DateTime toDt)
        {
            var ids = styleIds.Distinct().ToList();

            var allCmPrices = await _context.StyleCmPrice.AsNoTracking()
                .Where(c => ids.Contains(c.StyleId) && c.IsActive == true)
                .ToListAsync();

            var cmPriceByStyle = allCmPrices
                .GroupBy(c => c.StyleId, StringComparer.OrdinalIgnoreCase)
                .Select(g => new
                {
                    StyleId = g.Key,
                    Price = g.Where(c => c.EffectiveDate <= toDt)
                             .OrderByDescending(c => c.EffectiveDate)
                             .ThenByDescending(c => c.CreatedAt)
                             .FirstOrDefault()
                })
                .Where(x => x.Price != null)
                .ToDictionary(x => x.StyleId, x => x.Price!, StringComparer.OrdinalIgnoreCase);

            var cmPriceIds = cmPriceByStyle.Values.Select(c => c.Id).ToList();

            var cmSplits = await _context.StyleCmSplits.AsNoTracking()
                .Where(s => cmPriceIds.Contains(s.StyleCmPriceId))
                .ToListAsync();

            var splitsByCmPriceId = cmSplits
                .GroupBy(s => s.StyleCmPriceId)
                .ToDictionary(g => g.Key, g => g.ToList());

            return (styleId, operationCode) =>
            {
                if (!cmPriceByStyle.TryGetValue(styleId, out var cmPrice)) return null;
                splitsByCmPriceId.TryGetValue(cmPrice.Id, out var splitsForThisPrice);
                var matchedSplit = splitsForThisPrice?.FirstOrDefault(s => s.OperationCode == operationCode);
                return new
                {
                    styleId = cmPrice.StyleId,
                    price = matchedSplit?.SplitPrice ?? cmPrice.CmPrice,
                    currency = cmPrice.Currency,
                    effectiveDate = cmPrice.EffectiveDate,
                    operationCode = operationCode,
                    operationName = matchedSplit?.OperationName,
                };
            };
        }

        /// <summary>Gộp dòng theo (planId -> yyyy-MM-dd -> tổng) — thay cho 6 đoạn GroupBy/ToDictionary lặp lại.</summary>
        private static Dictionary<Guid, Dictionary<string, int>> ToDailyMap<T>(
            IEnumerable<T> rows, Func<T, Guid> planId, Func<T, DateTime> date, Func<T, int> qty)
        {
            return rows
                .GroupBy(planId)
                .ToDictionary(
                    g => g.Key,
                    g => g.GroupBy(r => date(r).Date)               // gộp nếu trùng ngày
                          .ToDictionary(dg => dg.Key.ToString("yyyy-MM-dd"), dg => dg.Sum(qty)));
        }


        // ---------------------------------------------------------------------------
        // 3) TAB ĐƠN HÀNG
        // ---------------------------------------------------------------------------

        private async Task<List<object>> BuildOrdersAsync(
            string factoryId, DateOnly from, DateOnly to, string? lineNumber, string? styleName)
        {
            // Tab Đơn hàng cần TẤT CẢ dòng: dòng gốc chưa phân tổ + dòng đã gán Tổ
            var baseCtx = await LoadPlanBoardBaseAsync(factoryId, from, to, lineNumber, styleName);
            var plans = baseCtx.Plans;
            if (plans.Count == 0) return new List<object>();

            var targetDeliveryMap = baseCtx.TargetDeliveryMap;

            var styleIds = plans.Select(p => p.StyleId).Distinct().ToList();
            var poIds = plans.Select(p => p.PoId).Distinct().ToList();
            var colorIds = plans.Select(p => p.ColorId).Distinct().ToList();

            var itemNameMap = await LoadItemNamesAsync(styleIds);
            var poNameMap = new Dictionary<string, string>();
            await EnsurePoNamesAsync(poNameMap, poIds);
            var resolveCmPrice = await BuildCmPriceResolverAsync(styleIds, to.ToDateTime(TimeOnly.MaxValue));

            // Breakdown size gốc theo (Style, PO, Màu, Version)
            var rawItems = await _context.MesProductionOrderItems
                .Where(x => styleIds.Contains(x.StyleId)
                         && poIds.Contains(x.PoId)
                         && colorIds.Contains(x.ColorId)
                         && x.FactoryId == factoryId && x.IsActive == true)
                .Select(x => new { x.StyleId, x.PoId, x.ColorId, x.SizeId, x.PlannedQty, x.VersionId })
                .ToListAsync();

            var sizeIdDict = rawItems
                .GroupBy(x => (x.StyleId, x.PoId, x.ColorId, x.VersionId))
                .ToDictionary(
                    g => g.Key,
                    g => g.GroupBy(x => x.SizeId)
                          .Select(sg => new SizeQtyDto(sg.Key, sg.Sum(x => x.PlannedQty)))
                          .ToList());

            return plans
                .GroupBy(p => new { p.VersionId, p.StyleId, p.PoId, p.ColorId })
                .Select(g =>
                {
                    var first = g.First();
                    var resolvedStyleName = itemNameMap.TryGetValue(first.StyleId, out var sName) ? sName : string.Empty;
                    var resolvedPoName = poNameMap.TryGetValue(first.PoId, out var pName) ? pName : string.Empty;

                    var key = (first.VersionId, first.FactoryId, first.StyleId, first.PoId, first.ColorId);
                    var resolvedTargetDeliveryDate = targetDeliveryMap.TryGetValue(key, out var tDate) ? tDate : (DateOnly?)null;

                    // Dòng LineNumber == null: đại diện KH tổng gốc. Dòng còn lại: đã gán tổ.
                    var dongGoc = g.Where(p => string.IsNullOrWhiteSpace(p.LineNumber)).ToList();
                    var dongDaGan = g.Where(p => !string.IsNullOrWhiteSpace(p.LineNumber)).ToList();

                    var sizeKey = (first.StyleId, first.PoId, first.ColorId, first.VersionId);
                    var resolvedSizeQtyList = sizeIdDict.TryGetValue(sizeKey, out var sizeQtyList)
                        ? sizeQtyList
                        : new List<SizeQtyDto>();

                    var khTong = dongGoc.Count > 0
                        ? dongGoc.Max(p => p.TotalPlannedQty)   // Max thay vì Sum để tránh cộng dồn nếu có dòng gốc trùng do bug dữ liệu
                        : dongDaGan
                            .GroupBy(p => p.OperationCode, StringComparer.OrdinalIgnoreCase)
                            .Select(gr => gr.Sum(p => p.TotalPlannedQty))
                            .DefaultIfEmpty(0)
                            .Max();

                    var conLaiChuaPhanBo = dongGoc.Sum(p => p.TotalPlannedQty);

                    // Đã có ít nhất 1 dòng gán tổ => coi là đã phân bổ
                    var trangThai = dongDaGan.Count > 0 ? "da_phan_bo" : "chua_phan_to";

                    var danhSachToDaGan = dongDaGan
                        .Select(p => new
                        {
                            factoryPlanId = p.Id,
                            lineNumber = p.LineNumber,
                            operationCode = p.OperationCode,
                            soLuong = p.TotalPlannedQty,
                            cmPrice = resolveCmPrice(p.StyleId, p.OperationCode),
                        })
                        .ToList();

                    return new
                    {
                        id = first.Id,
                        versionId = first.VersionId,
                        styleId = first.StyleId,
                        styleName = resolvedStyleName,
                        poId = first.PoId,
                        poName = resolvedPoName,
                        colorId = first.ColorId,
                        sizeIds = resolvedSizeQtyList.Select(x => new { sizeId = x.SizeId, plannedQty = x.PlannedQty }).ToList(),
                        colorName = first.ColorId,
                        targetDeliveryDate = resolvedTargetDeliveryDate?.ToString("dd-MM-yyyy") ?? string.Empty,
                        khTong,
                        conLaiChuaPhanBo,
                        trangThai,
                        danhSachToDaGan,
                        cmPrice = resolveCmPrice(first.StyleId, null),   // giá tổng hiển thị ở header dòng
                        status = first.Status,
                    };
                })
                .OrderBy(o => o.styleId)
                .ThenBy(o => o.poId)
                .Cast<object>()
                .ToList();
        }


        // ---------------------------------------------------------------------------
        // 4) TAB KẾ HOẠCH SẢN XUẤT
        // ---------------------------------------------------------------------------

        private async Task<List<object>> BuildPlanLinesAsync(
            string factoryId, DateOnly from, DateOnly to, string? lineNumber, string? styleName)
        {
            // Tab Kế hoạch sản xuất: chỉ các dòng ĐÃ gán Tổ, lọc theo ProductionStartDate nằm trong [from, to].
            // (KHÔNG lọc theo ngày giao hàng như tab Đơn hàng.)
            var planQuery = await CreatePlanQueryAsync(factoryId, lineNumber, styleName, onlyAssigned: true);

            var planLinePlans = await planQuery
                .Where(p =>
                    // Case đã phân kế hoạch ngày: lọc theo khoảng [ProductionStartDate, ProductionEndDate] giao với kỳ đang xem.
                    (p.ProductionStartDate != null
                     && p.ProductionEndDate != null
                     && p.ProductionStartDate <= to
                     && p.ProductionEndDate >= from)
                    // Case đã phân tổ nhưng chưa phân kế hoạch ngày: vẫn hiển thị để user phân ngày.
                    || (p.ProductionStartDate == null && p.ProductionEndDate == null && p.LineNumber != null))
                .OrderBy(p => p.LineNumber)
                .ThenBy(p => p.StyleId)
                .ToListAsync();

            if (planLinePlans.Count == 0) return new List<object>();

            // Ngày giao hàng ở tab này CHỈ để hiển thị (không dùng để lọc) -> tra riêng, không giới hạn theo kỳ.
            var dlVersionIds = planLinePlans.Select(p => p.VersionId).Distinct().ToList();
            var dlStyleIds = planLinePlans.Select(p => p.StyleId).Distinct().ToList();
            var dlPoIds = planLinePlans.Select(p => p.PoId).Distinct().ToList();
            var dlColorIds = planLinePlans.Select(p => p.ColorId).Distinct().ToList();

            var deliveryGroups = await _context.MesProductionOrderItems
                .AsNoTracking()
                .Where(x => x.FactoryId == factoryId
                         && dlVersionIds.Contains(x.VersionId)
                         && dlStyleIds.Contains(x.StyleId)
                         && dlPoIds.Contains(x.PoId)
                         && dlColorIds.Contains(x.ColorId))
                .GroupBy(x => new { x.VersionId, x.FactoryId, x.StyleId, x.PoId, x.ColorId })
                .Select(g => new
                {
                    g.Key.VersionId,
                    g.Key.FactoryId,
                    g.Key.StyleId,
                    g.Key.PoId,
                    g.Key.ColorId,
                    TargetDeliveryDate = g.Max(x => x.TargetDeliveryDate)
                })
                .ToListAsync();

            var targetDeliveryMap = new Dictionary<(Guid VersionId, string FactoryId, string StyleId, string PoId, string ColorId), DateOnly?>();
            foreach (var g in deliveryGroups)
                targetDeliveryMap[(g.VersionId, g.FactoryId, g.StyleId, g.PoId, g.ColorId)] = g.TargetDeliveryDate;

            var fromDt = from.ToDateTime(TimeOnly.MinValue);
            var toDt = to.ToDateTime(TimeOnly.MaxValue);
            var planIds = planLinePlans.Select(p => p.Id).ToList();   // giờ chỉ gồm dòng ĐÃ có tổ (trước đây gồm cả dòng gốc)

            // ---- Tên Style / PO / giá CM ----
            var itemNameMap = await LoadItemNamesAsync(planLinePlans.Select(p => p.StyleId));
            var poNameMap = new Dictionary<string, string>();
            await EnsurePoNamesAsync(poNameMap, planLinePlans.Select(p => p.PoId));
            var resolveCmPrice = await BuildCmPriceResolverAsync(planLinePlans.Select(p => p.StyleId), toDt);

            // ---- KH theo ngày (trong kỳ) ----
            var planLinesRaw = await _context.MesFactoryPlanLines.AsNoTracking()
                .Where(l => planIds.Contains(l.FactoryPlanId) && l.WorkDate >= fromDt && l.WorkDate <= toDt)
                .ToListAsync();
            var teamTotalQtyMap = ToDailyMap(planLinesRaw, l => l.FactoryPlanId, l => l.WorkDate, l => l.PlannedQty);

            // ---- Thực hiện theo ngày (trong kỳ) ----
            var qlclByPlanRaw = await _context.qualityManagementSessions.AsNoTracking()
                .Where(q => q.PlanId != null
                         && planIds.Contains(q.PlanId.Value)
                         && q.InspectionDate >= fromDt && q.InspectionDate <= toDt
                         && q.Status == 1 && q.OperationType == "1")
                .Select(q => new { PlanId = q.PlanId!.Value, q.InspectionDate, q.PassOutputQuantity })
                .ToListAsync();
            var actualQtyByDateMap = ToDailyMap(qlclByPlanRaw, q => q.PlanId, q => q.InspectionDate, q => q.PassOutputQuantity);

            // ---- KH của hôm nay (không giới hạn theo kỳ đang xem) — dùng tính trạng thái "đang sản xuất" ----
            var today = DateTime.Today;
            var todayPlannedQtyMap = await _context.MesFactoryPlanLines
                .Where(l => planIds.Contains(l.FactoryPlanId) && l.WorkDate.Date == today)
                .GroupBy(l => l.FactoryPlanId)
                .Select(g => new { FactoryPlanId = g.Key, Qty = g.Sum(x => x.PlannedQty) })
                .ToDictionaryAsync(x => x.FactoryPlanId, x => x.Qty);

            // ---- Thực hiện LŨY KẾ đến hôm nay theo (Tổ, Style, Màu) — dùng tính trạng thái ----
            var styleIdsForActual = planLinePlans.Select(p => p.StyleId).Distinct().ToList();
            var colorIdsForActual = planLinePlans.Select(p => p.ColorId).Distinct().ToList();
            var teamIds = planLinePlans.Select(p => p.LineNumber!).Distinct().ToList();
            var todayEndDt = DateTime.Today.AddDays(1).AddTicks(-1);

            var qlclRaw = await _context.qualityManagementSessions
                .Where(q => styleIdsForActual.Contains(q.ItemNo)
                         && colorIdsForActual.Contains(q.ColorNo)
                         && q.InspectionDate <= todayEndDt
                         && teamIds.Contains(q.WorkShiftCode)
                         && q.WorkCenterCode == factoryId && q.Status == 1)
                .Select(q => new { q.WorkShiftCode, q.ItemNo, q.ColorNo, q.PassOutputQuantity })
                .ToListAsync();

            var actualQtyMap = qlclRaw
                .GroupBy(q => (WorkShift: q.WorkShiftCode, StyleId: q.ItemNo, ColorId: q.ColorNo))
                .ToDictionary(g => g.Key, g => g.Sum(x => x.PassOutputQuantity));

            // =========================================================
            // PO NGUỒN ĐÃ GỘP vào các dòng hiện tại (đã bỏ điều kiện IsActive — nguồn luôn IsActive=false)
            // =========================================================
            var mergedSourcePlans = await _context.MesFactoryPlans
                .Where(s => s.ParentPlanId != null
                         && planIds.Contains(s.ParentPlanId.Value)
                         && s.LineNumber != null)
                .ToListAsync();

            var mergedSourceByParentId = mergedSourcePlans
                .GroupBy(s => s.ParentPlanId!.Value)
                .ToDictionary(g => g.Key, g => g.ToList());

            await EnsurePoNamesAsync(poNameMap, mergedSourcePlans.Select(s => s.PoId));

            var mergedSourcePlanIds = mergedSourcePlans.Select(s => s.Id).ToList();

            var mergedSourceLinesRaw = mergedSourcePlanIds.Count > 0
                ? await _context.MesFactoryPlanLines
                    .Where(l => mergedSourcePlanIds.Contains(l.FactoryPlanId) && l.WorkDate >= fromDt && l.WorkDate <= toDt)
                    .ToListAsync()
                : new List<MesFactoryPlanLine>();
            var mergedSourceDailyQtyMap = ToDailyMap(mergedSourceLinesRaw, l => l.FactoryPlanId, l => l.WorkDate, l => l.PlannedQty);

            var mergedSourceQlclRaw = await _context.qualityManagementSessions
                .Where(q => q.PlanId != null
                         && mergedSourcePlanIds.Contains(q.PlanId.Value)
                         && q.InspectionDate >= fromDt && q.InspectionDate <= toDt
                         && q.Status == 0)
                .Select(q => new { PlanId = q.PlanId!.Value, q.InspectionDate, q.PassOutputQuantity })
                .ToListAsync();
            var mergedSourceActualByDateMap = ToDailyMap(mergedSourceQlclRaw, q => q.PlanId, q => q.InspectionDate, q => q.PassOutputQuantity);

            List<object> ResolveMergedFromPos(Guid planId)
            {
                if (!mergedSourceByParentId.TryGetValue(planId, out var sources))
                    return new List<object>();

                return sources
                    .OrderBy(s => s.PoId)
                    .Select(s => new
                    {
                        factoryPlanId = s.Id,
                        poId = s.PoId,
                        poName = poNameMap.TryGetValue(s.PoId, out var pn) ? pn : string.Empty,
                        planCode = s.PlanCode,
                        lineNumber = s.LineNumber,
                        operationCode = s.OperationCode,
                        totalPlannedQty = s.TotalPlannedQty,
                        status = s.Status,          // "merged_source"
                        mergedAt = s.MergedAt,
                        mergedBy = s.MergedBy,
                        dailyPlannedQty = mergedSourceDailyQtyMap.TryGetValue(s.Id, out var qtyByDate)
                            ? qtyByDate : new Dictionary<string, int>(),
                        dailyActualQty = mergedSourceActualByDateMap.TryGetValue(s.Id, out var actualByDate)
                            ? actualByDate : new Dictionary<string, int>(),
                        hasActualData = mergedSourceActualByDateMap.ContainsKey(s.Id),
                    })
                    .ToList<object>();
            }

            // =========================================================
            // PO GỐC ĐÃ TÁCH ra các dòng hiện tại (đã bỏ điều kiện IsActive — PO gốc luôn IsActive=false)
            // =========================================================
            var parentPlanIds = planLinePlans
                .Where(p => p.ParentPlanId != null)
                .Select(p => p.ParentPlanId!.Value)
                .Distinct()
                .ToList();

            var parentPlansMap = parentPlanIds.Count > 0
                ? await _context.MesFactoryPlans
                    .Where(pp => parentPlanIds.Contains(pp.Id))
                    .ToDictionaryAsync(pp => pp.Id)
                : new Dictionary<Guid, MesFactoryPlan>();

            await EnsurePoNamesAsync(poNameMap, parentPlansMap.Values.Select(pp => pp.PoId));

            var missingParentStyleIds = parentPlansMap.Values
                .Select(pp => pp.StyleId)
                .Where(id => !itemNameMap.ContainsKey(id))
                .Distinct()
                .ToList();
            if (missingParentStyleIds.Count > 0)
            {
                foreach (var kv in await LoadItemNamesAsync(missingParentStyleIds))
                    itemNameMap[kv.Key] = kv.Value;
            }

            var parentPlanLinesRaw = parentPlanIds.Count > 0
                ? await _context.MesFactoryPlanLines
                    .Where(l => parentPlanIds.Contains(l.FactoryPlanId) && l.WorkDate >= fromDt && l.WorkDate <= toDt)
                    .ToListAsync()
                : new List<MesFactoryPlanLine>();
            var parentPlanDailyQtyMap = ToDailyMap(parentPlanLinesRaw, l => l.FactoryPlanId, l => l.WorkDate, l => l.PlannedQty);

            var parentPlanQlclRaw = await _context.qualityManagementSessions
                .Where(q => q.PlanId != null
                         && parentPlanIds.Contains(q.PlanId.Value)
                         && q.InspectionDate >= fromDt && q.InspectionDate <= toDt
                         && q.Status == 0)
                .Select(q => new { PlanId = q.PlanId!.Value, q.InspectionDate, q.PassOutputQuantity })
                .ToListAsync();
            var parentPlanActualByDateMap = ToDailyMap(parentPlanQlclRaw, q => q.PlanId, q => q.InspectionDate, q => q.PassOutputQuantity);

            // Số dòng (tổ) đã được tách ra từ cùng 1 plan cha
            var splitSiblingCountMap = planLinePlans
                .Where(p => p.ParentPlanId != null)
                .GroupBy(p => p.ParentPlanId!.Value)
                .ToDictionary(g => g.Key, g => g.Count());

            // =========================================================
            // PROJECTION
            // =========================================================
            return planLinePlans
                .Select(p =>
                {
                    var resolvedStyleName = itemNameMap.TryGetValue(p.StyleId, out var sName) ? sName : string.Empty;
                    var resolvedPoName = poNameMap.TryGetValue(p.PoId, out var pName) ? pName : string.Empty;

                    var key = (p.VersionId, p.FactoryId, p.StyleId, p.PoId, p.ColorId);
                    var resolvedTargetDeliveryDate = targetDeliveryMap.TryGetValue(key, out var tDate) ? tDate : (DateOnly?)null;

                    var resolvedTeamTotalQty = teamTotalQtyMap.TryGetValue(p.Id, out var qtyByDate)
                        ? qtyByDate : new Dictionary<string, int>();
                    var resolvedActualQtyByDate = actualQtyByDateMap.TryGetValue(p.Id, out var actualByDate)
                        ? actualByDate : new Dictionary<string, int>();

                    // ---- Trạng thái ----
                    var todayPlannedQty = todayPlannedQtyMap.TryGetValue(p.Id, out var tpq) ? tpq : 0;
                    var actualKey = (WorkShift: p.LineNumber, StyleId: p.StyleId, ColorId: p.ColorId);
                    var actualQty = actualQtyMap.TryGetValue(actualKey, out var aq) ? aq : 0;
                    var remainingQty = p.TotalPlannedQty - actualQty;

                    string computedStatus;
                    if (actualQty >= p.TotalPlannedQty) computedStatus = "hoan_thanh";
                    else if (todayPlannedQty > 0) computedStatus = "dang_san_xuat";
                    else if (remainingQty > 0) computedStatus = "tam_dung";
                    else computedStatus = p.Status;

                    // ---- Kết quả của việc TÁCH PO gốc? ----
                    bool isSplited = p.ParentPlanId != null && parentPlansMap.ContainsKey(p.ParentPlanId.Value);

                    object? splitFromPo = null;
                    if (isSplited)
                    {
                        var parentPlan = parentPlansMap[p.ParentPlanId!.Value];
                        splitFromPo = new
                        {
                            factoryPlanId = parentPlan.Id,
                            poId = parentPlan.PoId,
                            poName = poNameMap.TryGetValue(parentPlan.PoId, out var ppn) ? ppn : string.Empty,
                            styleId = parentPlan.StyleId,
                            styleName = itemNameMap.TryGetValue(parentPlan.StyleId, out var psn) ? psn : string.Empty,
                            colorId = parentPlan.ColorId,
                            versionId = parentPlan.VersionId,
                            planCode = parentPlan.PlanCode,
                            khTongGoc = parentPlan.TotalPlannedQty,
                            soDongDaTach = splitSiblingCountMap.TryGetValue(parentPlan.Id, out var cnt) ? cnt : 1,
                            dailyPlannedQty = parentPlanDailyQtyMap.TryGetValue(parentPlan.Id, out var pQtyByDate)
                                ? pQtyByDate : new Dictionary<string, int>(),
                            dailyActualQty = parentPlanActualByDateMap.TryGetValue(parentPlan.Id, out var pActualByDate)
                                ? pActualByDate : new Dictionary<string, int>(),
                            hasActualData = parentPlanActualByDateMap.ContainsKey(parentPlan.Id),
                        };
                    }

                    return new
                    {
                        id = p.Id,
                        planCode = p.PlanCode,
                        factoryId = p.FactoryId,
                        lineNumber = p.LineNumber,
                        styleId = p.StyleId,
                        styleName = resolvedStyleName,
                        poId = p.PoId,
                        poName = resolvedPoName,
                        colorId = p.ColorId,
                        colorName = p.ColorId,
                        totalPlannedQty = p.TotalPlannedQty,
                        teamTotalQty = resolvedTeamTotalQty,
                        teamActualQty = resolvedActualQtyByDate,
                        operationCode = p.OperationCode,
                        cmPrice = resolveCmPrice(p.StyleId, p.OperationCode),
                        status = computedStatus,
                        isMerged = mergedSourceByParentId.ContainsKey(p.Id),
                        mergedFromPos = ResolveMergedFromPos(p.Id),
                        isSplited,
                        splitFromPo,
                        targetDeliveryDate = resolvedTargetDeliveryDate?.ToString("dd-MM-yyyy") ?? string.Empty,
                    };
                })
                .OrderBy(pl => pl.lineNumber)
                .ThenBy(pl => pl.styleId)
                .Cast<object>()
                .ToList();
        }
        //Helper

        private class StyleOperationKeyComparer : IEqualityComparer<(string StyleId, string OperationCode)>
        {
            public bool Equals((string StyleId, string OperationCode) x, (string StyleId, string OperationCode) y)
                => string.Equals(x.StyleId, y.StyleId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.OperationCode, y.OperationCode, StringComparison.OrdinalIgnoreCase);

            public int GetHashCode((string StyleId, string OperationCode) obj)
                => HashCode.Combine(
                    obj.StyleId?.ToUpperInvariant(),
                    obj.OperationCode?.ToUpperInvariant());
        }
        
        [HttpPost("factory-plans-v2-save")]
        public async Task<IActionResult> SaveFactoryPlanAssignments([FromBody] SaveFactoryPlanAssignmentRequest request)
        {
            if (request?.Rows == null || !request.Rows.Any())
                return BadRequest(new { success = false, message = "Không có dữ liệu để lưu." });

            if (string.IsNullOrWhiteSpace(request.FactoryId) || request.Year <= 0 || request.Month is < 1 or > 12)
                return BadRequest(new { success = false, message = "Thiếu thông tin factoryId/year/month." });

            var overPlannedErrors = new List<string>();
            //foreach (var row in request.Rows)
            //{
            //    var totalDaily = row.DailyQuantities?.Sum(d => d.PlannedQty) ?? 0;
            //    if (totalDaily > row.TotalPlannedQty)
            //    {
            //        overPlannedErrors.Add(
            //            $"Style={row.StyleId}, PO={row.PoId}, Màu={row.ColorId}: " +
            //            $"Tổng KH theo ngày ({totalDaily:N0}) vượt quá KH tổng ({row.TotalPlannedQty:N0}).");
            //    }
            //}
            if (overPlannedErrors.Any())
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Một số dòng có tổng kế hoạch theo ngày vượt quá KH tổng. Vui lòng kiểm tra lại.",
                    errors = overPlannedErrors
                });
            }
            var monthStart = new DateOnly(request.Year, request.Month, 1);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);

            var currentUser = request.UserId;

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                foreach (var row in request.Rows)
                {
                    var plan = await _context.MesFactoryPlans.FirstOrDefaultAsync(p =>
                        p.Id == row.Id  && p.IsActive==true&&
                        p.StyleId == row.StyleId &&
                        p.PoId == row.PoId &&
                        p.ColorId == row.ColorId);

                    if (plan == null)
                    {
                        plan = new MesFactoryPlan
                        {
                            FactoryId = request.FactoryId,
                            StyleId = row.StyleId,
                            PoId = row.PoId,
                            ColorId = row.ColorId,
                            Status = "chua_san_xuat",
                            CreatedBy = currentUser,
                            CreatedAt = DateTime.Now,
                            IsActive = true
                        };
                        _context.MesFactoryPlans.Add(plan);
                    }
                    else
                    {
                        if (plan.Status is "hoan_thanh")
                        {
                            return Conflict(new
                            {
                                success = false,
                                message = $"Kế hoạch (Style={row.StyleId}, PO={row.PoId}, Màu={row.ColorId}) đang ở trạng thái '{plan.Status}', không thể chỉnh sửa."
                            });
                        }
                    }

                    // Update các field còn thiếu / gán tổ
                    //plan.LineNumber = row.LineNumber;
                    plan.TotalPlannedQty = row.TotalPlannedQty;
                    plan.UpdatedBy = currentUser;
                    plan.UpdatedAt = DateTime.Now;

                    await _context.SaveChangesAsync();

                    // Bước 2: Upsert factory_plan_lines theo từng ngày
                    if (row.DailyQuantities?.Any() == true)
                    {
                        var dates = row.DailyQuantities.Select(d => d.Date.ToDateTime(TimeOnly.MinValue).Date).ToList();

                        var existingLines = await _context.MesFactoryPlanLines
                            .Where(l => l.FactoryPlanId == plan.Id && dates.Contains(l.WorkDate.Date))
                            .ToListAsync();

                        foreach (var d in row.DailyQuantities)
                        {
                            var dayDt = d.Date.ToDateTime(TimeOnly.MinValue);
                            var existing = existingLines.FirstOrDefault(l => l.WorkDate.Date == dayDt.Date);

                            if (d.PlannedQty <= 0)
                            {
                                if (existing != null)
                                {
                                    if (existing.DemandCreatedQty <= 0)
                                    {
                                        // Chưa phát sinh nhu cầu từ dòng này -> xoá cho sạch dữ liệu
                                        _context.MesFactoryPlanLines.Remove(existing);
                                    }
                                    else
                                    {
                                        // Đã có nhu cầu tạo ra từ trước -> không xoá, chỉ hạ planned về 0
                                        existing.PlannedQty = 0;
                                        existing.RemainingQty = 0 - existing.DemandCreatedQty; // có thể âm để biết đang vượt nhu cầu
                                        existing.UpdatedAt = DateTime.Now;
                                        existing.UpdatedBy = currentUser;
                                    }
                                }
                                // Không tồn tại và planned = 0 -> bỏ qua, không tạo mới
                                continue;
                            }

                            // PlannedQty > 0
                            if (existing != null)
                            {
                                existing.PlannedQty = d.PlannedQty;
                                existing.RemainingQty = d.PlannedQty - existing.DemandCreatedQty;
                                existing.UpdatedAt = DateTime.Now;
                                existing.UpdatedBy = currentUser;
                            }
                            else
                            {
                                _context.MesFactoryPlanLines.Add(new MesFactoryPlanLine
                                {
                                    FactoryPlanId = plan.Id,
                                    SizeId = row.SizeId,
                                    PlannedQty = d.PlannedQty,
                                    DemandCreatedQty = 0,
                                    RemainingQty = d.PlannedQty,
                                    WorkDate = dayDt,
                                    UpdatedAt = DateTime.Now,
                                    UpdatedBy = currentUser
                            });
                            }
                        }

                        await _context.SaveChangesAsync();

                        // Bước 3: Tính lại production_start_date / production_end_date
                        var datesWithQty = await _context.MesFactoryPlanLines
                            .Where(l => l.FactoryPlanId == plan.Id && l.PlannedQty > 0)
                            .Select(l => l.WorkDate.Date)
                            .ToListAsync();

                        if (datesWithQty.Any())
                        {
                            plan.ProductionStartDate = DateOnly.FromDateTime(datesWithQty.Min());
                            plan.ProductionEndDate = DateOnly.FromDateTime(datesWithQty.Max());
                        }
                        else
                        {
                            //plan.ProductionStartDate = monthStart;
                            //plan.ProductionEndDate = monthEnd;
                            plan.ProductionStartDate = null;
                            plan.ProductionEndDate = null;
                        }

                        plan.UpdatedAt = DateTime.Now;
                        plan.UpdatedBy = currentUser;
                    }
                    else
                    {
                        plan.UpdatedAt = DateTime.Now;
                        plan.UpdatedBy = currentUser;
                        // Không có DailyQuantities gửi lên -> giữ nguyên hành vi cũ (theo tháng)
                        plan.ProductionStartDate = monthStart;
                        plan.ProductionEndDate = monthEnd;
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { success = false, message = "Lỗi khi lưu kế hoạch.", detail = ex.Message });
            }

            return Ok(new { success = true, message = "Đã lưu kế hoạch sản xuất thành công." });
        }

        // PUT /api/production/factory-plans/batch-update-daily
        // Cập nhật planned_qty theo ngày cho các factory_plan_lines và QLCL_ProductionPlan
        // Body: { updates: [{ factoryPlanId, day, year, month, plannedQty }] }
        [HttpPut("factory-plans/batch-update-daily")]
        [ProducesResponseType(typeof(object), 200)]
        public async Task<IActionResult> BatchUpdateDailyQty([FromBody] BatchUpdateDailyRequest req)
        {
            if (req?.Updates == null || req.Updates.Count == 0)
                return BadRequest(new { success = false, message = "Không có dữ liệu cần cập nhật." });

            var now = DateTime.UtcNow;
            int updatedLines = 0;
            int updatedPp    = 0;

            // Nhóm theo factoryPlanId để load lines 1 lần
            var grouped = req.Updates
                .GroupBy(u => u.FactoryPlanId)
                .ToList();

            // Load factory_plans để biết (PoId, FactoryId, StyleId, ColorId, LineNumber)
            var planIds = grouped.Select(g => g.Key).ToList();
            var plans = await _context.MesFactoryPlans
                .Where(p => planIds.Contains(p.Id)&&p.IsActive==true)
                .ToDictionaryAsync(p => p.Id);

            // Load tất cả lines liên quan
            var lines = await _context.MesFactoryPlanLines
                .Where(l => planIds.Contains(l.FactoryPlanId))
                .ToListAsync();

            // Lookup nhanh: factoryPlanId → { dateOnly → line }
            var lineMap = lines
                .GroupBy(l => l.FactoryPlanId)
                .ToDictionary(
                    g => g.Key,
                    g => g.ToDictionary(l => DateOnly.FromDateTime(l.WorkDate)));

            foreach (var upd in req.Updates)
            {
                if (!plans.TryGetValue(upd.FactoryPlanId, out var plan)) continue;

                var targetDate     = new DateOnly(upd.Year, upd.Month, upd.Day);
                var targetDateTime = targetDate.ToDateTime(TimeOnly.MinValue);

                // ── 1. Cập nhật factory_plan_lines ──────────────────────────
                if (lineMap.TryGetValue(upd.FactoryPlanId, out var dayMap) &&
                    dayMap.TryGetValue(targetDate, out var line))
                {
                    line.PlannedQty = upd.PlannedQty;
                    line.UpdatedAt  = now;
                    updatedLines++;
                }

                // ── 2. Cập nhật QLCL_ProductionPlan ─────────────────────────
                var pp = await _context.ProductionPlans
                    .FirstOrDefaultAsync(p =>
                        //p.SalesOrderNo    == plan.PoId &&
                        p.WorkCenterCode  == plan.FactoryId &&
                        p.WorkShiftCode   == plan.LineNumber &&
                        p.ItemNo          == plan.StyleId &&
                        p.ColorNo         == plan.ColorId &&
                        p.ActualStartingDate.HasValue &&
                        p.ActualStartingDate.Value.Date == targetDateTime.Date);

                if (pp != null)
                {
                    pp.TargetQuantityPerDay = upd.PlannedQty;
                    pp.ActualStartingDate   = targetDateTime;
                    updatedPp++;
                }
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success      = true,
                message      = $"Đã cập nhật thành công.",
                updatedLines,
                updatedPp,
            });
        }


        [HttpGet("exchange-rate")]
        [ProducesResponseType(typeof(object), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetExchangeRate(
    [FromQuery] string base_,   // tên param "base" là từ khoá C#, dùng base_ rồi đọc từ query
    [FromQuery] string target,
    [FromQuery] DateTime? date = null) // optional, mặc định lấy tháng hiện tại
        {
            // ASP.NET không bind được tham số tên "base" → đọc thủ công từ QueryString
            var baseCurrency = (Request.Query["base"].FirstOrDefault() ?? base_ ?? "").Trim().ToUpperInvariant();
            var targetCurrency = (Request.Query["target"].FirstOrDefault() ?? target ?? "").Trim().ToUpperInvariant();

            if (string.IsNullOrEmpty(baseCurrency) || string.IsNullOrEmpty(targetCurrency))
                return BadRequest(new { success = false, message = "Thiếu tham số base hoặc target." });

            // Cùng đồng tiền → tỷ giá = 1
            if (baseCurrency == targetCurrency)
                return Ok(new { rate = 1.0, @base = baseCurrency, target = targetCurrency, updatedAt = (string?)null });

            // Đồng nội tệ (LCY) trong NAV thường lưu bằng chuỗi rỗng hoặc "VND"
            const string LCY = "VND"; // ← điều chỉnh nếu NAV dùng ký hiệu khác

            // Chuẩn hóa mốc thời gian tra tỷ giá: ngày đầu tháng gần nhất (<= tháng cần tra)
            var refDate = date ?? DateTime.Today;
            var monthStart = new DateTime(refDate.Year, refDate.Month, 1);

            try
            {
                // Hàm lấy tỷ giá: 1 đơn vị foreignCode = ? đơn vị LCY
                // Lấy bản ghi có StartingDate (ngày đầu tháng) gần nhất, <= monthStart
                async Task<decimal?> GetRateToLCY(string foreignCode)
                {
                    var row = await _context.CurrencyExchangeRates
                        .Where(e => e.CurrencyCode == foreignCode
                                 && e.StartingDate <= monthStart
                                 && (e.RelationalCurrencyCode == null
                                     || e.RelationalCurrencyCode == ""
                                     || e.RelationalCurrencyCode == LCY)) // đảm bảo relational đúng là VND
                        .OrderByDescending(e => e.StartingDate)
                        .FirstOrDefaultAsync();

                    if (row == null) return null;

                    var exAmt = row.ExchangeRateAmount;
                    var relAmt = row.RelationalExchRateAmount;

                    // Chỉ dùng công thức chuẩn NAV khi CẢ HAI đều hợp lệ (>0)
                    //if (exAmt > 0 && relAmt > 0)
                    //    return relAmt / exAmt;
                    
                    // Fallback: dùng LocalExchange nếu có
                    if (row.RelationalExchRateAmount > 0)
                        return row.RelationalExchRateAmount;

                    return null;
                }

                decimal rate;

                if (targetCurrency == LCY)
                {
                    // base → LCY: lấy trực tiếp
                    var r = await GetRateToLCY(baseCurrency);
                    if (r == null || r <= 0)
                        return NotFound(new { success = false, message = $"Không tìm thấy tỷ giá cho {baseCurrency} tính đến {monthStart:dd/MM/yyyy}." });
                    rate = r.Value;
                }
                else if (baseCurrency == LCY)
                {
                    // LCY → target: nghịch đảo của (target → LCY)
                    var r = await GetRateToLCY(targetCurrency);
                    if (r == null || r <= 0)
                        return NotFound(new { success = false, message = $"Không tìm thấy tỷ giá cho {targetCurrency} tính đến {monthStart:dd/MM/yyyy}." });
                    rate = 1m / r.Value;
                }
                else
                {
                    // Cross-rate: base → LCY → target
                    var rBase = await GetRateToLCY(baseCurrency);
                    var rTarget = await GetRateToLCY(targetCurrency);
                    if (rBase == null || rBase <= 0)
                        return NotFound(new { success = false, message = $"Không tìm thấy tỷ giá cho {baseCurrency} tính đến {monthStart:dd/MM/yyyy}." });
                    if (rTarget == null || rTarget <= 0)
                        return NotFound(new { success = false, message = $"Không tìm thấy tỷ giá cho {targetCurrency} tính đến {monthStart:dd/MM/yyyy}." });
                    rate = rBase.Value / rTarget.Value;
                }

                return Ok(new
                {
                    rate = Math.Round((double)rate, 6),
                    @base = baseCurrency,
                    target = targetCurrency,
                    asOfMonth = monthStart.ToString("yyyy-MM-dd"),
                    updatedAt = DateTime.UtcNow.ToString("o"),
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi server.", detail = ex.Message });
            }
        }

        [HttpGet("revenue-details-by-month")]
        public async Task<ActionResult<IEnumerable<RevenueDetailItemDto>>> GetRevenueDetailsByMonth(
    [FromQuery] int month,
    [FromQuery] int year,
    [FromQuery] string? factoryId = null,
    [FromQuery] string? teamId = null)
        {
            if (month < 1 || month > 12 || year < 2000)
                return BadRequest("month (1-12) hoặc year không hợp lệ.");

            var result = await ComputeRevenueDetailsAsync(month, year, factoryId, teamId);
            return Ok(result);
        }

        // Tách logic tính doanh thu chi tiết theo Tổ/Style/Ngày để tái sử dụng cho "revenue-cm-details-by-month"
        private async Task<List<RevenueDetailItemDto>> ComputeRevenueDetailsAsync(
            int month, int year, string? factoryId, string? teamId)
        {
            var daysInMonth = DateTime.DaysInMonth(year, month);
            var startDate = new DateTime(year, month, 1);
            var endDate = new DateTime(year, month, daysInMonth);
            var monthStart = startDate;

            // ── 1. Lấy dữ liệu sản lượng thực hiện theo ngày ────────────────────────
            var qBase = _context.qualityManagementSessions
                .Where(q => q.InspectionDate >= startDate && q.Status == 1
                         && q.InspectionDate <= endDate
                         && q.OperationType == "1")
                .AsNoTracking();

            if (!string.IsNullOrWhiteSpace(factoryId) && factoryId != "all")
                qBase = qBase.Where(q => q.WorkCenterCode == factoryId);
            if (!string.IsNullOrWhiteSpace(teamId) && teamId != "all")
                qBase = qBase.Where(q => q.WorkShiftCode.ToString() == teamId);

            var rawSessions = await qBase
                .Join(_context.workShift.AsNoTracking(),
                    q => q.WorkShiftCode.ToString(),
                    w => w.Code,
                    (q, w) => new
                    {
                        Id = w.Code,
                        WorkShiftName = w.Name,      // → map thành "Tổ"
                        q.InspectionDate,
                        q.ItemNo,
                        q.PassOutputQuantity
                    })
                .ToListAsync();

            if (rawSessions.Count == 0)
                return new List<RevenueDetailItemDto>();

            var itemNos = rawSessions.Select(x => x.ItemNo).Distinct().ToList();
            var shiftCodes = rawSessions.Select(x => x.Id).Distinct().ToList();

            // ── 2. Lấy tên sản phẩm (Style) trực tiếp từ bảng Item theo ItemNo ──────
            var itemNameByNo = await _context.Item
                .AsNoTracking()
                .Where(i => itemNos.Contains(i.No))
                .Select(i => new { i.No, i.Name })
                .ToDictionaryAsync(i => i.No, i => i.Name);

            // ── 3. Lấy đơn giá CM từ mes.style_cm_prices theo Style ─────────────────
            // is_operation = 0 → dùng thẳng cm_price; is_operation = 1 → phải tra theo
            // công đoạn (operation_code) mà Tổ đó đang sản xuất cho Style này.
            var cmPricesRaw = await _context.StyleCmPrice
                .Where(p => itemNos.Contains(p.StyleId)
                         && p.IsActive
                         && p.EffectiveDate <= endDate
                         && (p.ExpiredDate == null || p.ExpiredDate > startDate))
                .Include(p => p.StyleCmSplits)
                .AsNoTracking()
                .ToListAsync();

            // 1 style có thể có nhiều version hiệu lực trong kỳ → lấy version mới nhất
            var cmPriceByStyle = cmPricesRaw
                .GroupBy(p => p.StyleId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.EffectiveDate).First());

            // ── 3b. Với các Style có phân công đoạn, tra operation_code mà từng Tổ (line_number)
            // đang sản xuất từ mes.factory_plans, để chọn đúng split_price tương ứng ──────────
            var operationStyleIds = cmPriceByStyle.Values.Where(p => p.IsOperation).Select(p => p.StyleId).Distinct().ToList();
            var operationByTeamStyle = operationStyleIds.Count == 0
                ? new Dictionary<(string StyleId, string LineNumber), string>()
                : (await _context.MesFactoryPlans
                    .Where(p => operationStyleIds.Contains(p.StyleId)
                             && p.LineNumber != null && shiftCodes.Contains(p.LineNumber)
                             && p.IsActive && p.OperationCode != null)
                    .Select(p => new { p.StyleId, p.LineNumber, p.OperationCode, p.CreatedAt })
                    .ToListAsync())
                    .GroupBy(p => (StyleId: p.StyleId, LineNumber: p.LineNumber!))
                    .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.CreatedAt).First().OperationCode!);

            // Trả về (đơn giá, operation_code đã dùng - nếu có) cho 1 cặp (Style, Tổ)
            (decimal UnitPrice, string? OperationCode) ResolveUnitPrice(string styleId, string lineNumber)
            {
                if (!cmPriceByStyle.TryGetValue(styleId, out var cmPrice))
                    return (0m, null);

                if (!cmPrice.IsOperation)
                    return (cmPrice.CmPrice, null);

                if (operationByTeamStyle.TryGetValue((styleId, lineNumber), out var operationCode))
                {
                    var split = cmPrice.StyleCmSplits.FirstOrDefault(s => s.OperationCode == operationCode);
                    if (split != null)
                        return (split.SplitPrice, operationCode);
                }

                // Không xác định được công đoạn Tổ đang sản xuất → chưa có đơn giá
                return (0m, null);
            }

            // ── 4. Lấy tỷ giá VND (Local Exchange), theo ngày đầu tháng gần nhất ────
            var localExchangeRate = await _context.CurrencyExchangeRates
                .Where(e => e.StartingDate <= monthStart)
                .OrderByDescending(e => e.StartingDate)
                .Select(e => e.LocalExchange)
                .FirstOrDefaultAsync();

            if (localExchangeRate == 0) localExchangeRate = 1m;

            var grouped = rawSessions
                .Select(q =>
                {
                    var (unitPrice, operationCode) = ResolveUnitPrice(q.ItemNo, q.Id);

                    // Style luôn lấy từ Item.Name theo ItemNo, không phụ thuộc vào price
                    itemNameByNo.TryGetValue(q.ItemNo, out var itemName);

                    return new
                    {
                        Id = q.Id,
                        ToMay = q.WorkShiftName.ToString(),
                        Style = itemName ?? q.ItemNo,
                        UnitPrice = unitPrice,
                        OperationCode = operationCode,
                        Day = q.InspectionDate.Day,
                        Revenue = unitPrice * (decimal)q.PassOutputQuantity
                    };
                })
                .GroupBy(x => new { x.Id, x.ToMay, x.Style, x.UnitPrice, x.OperationCode })
                .Select(g => new RevenueDetailItemDto
                {
                    Id = g.Key.Id,
                    ToMay = g.Key.ToMay,
                    Style = g.Key.Style,
                    UnitPrice = g.Key.UnitPrice,
                    OperationCode = g.Key.OperationCode,
                    Daily = g.GroupBy(x => x.Day)
                             .ToDictionary(
                                 dg => new DateTime(year, month, dg.Key).ToString("yyyy-MM-dd"),
                                 dg => dg.Sum(x => x.Revenue)
                             )
                })
                .OrderBy(x => x.ToMay)
                .ThenBy(x => x.Customer)
                .ToList();

            return grouped;
        }

        // GET /api/production/labor-details-by-month
        // Công lao động theo Tổ + theo ngày trong tháng:
        //   - Tổng công       = Sum(WorkDay) từ "HR Daily Working Hour", nhóm theo Work Shift Code + DateIn
        //   - Tổng giờ tăng ca = Sum(OverTime) từ "Register OverTime", nhóm theo Work Shift Code + Posting Date
        [HttpGet("labor-details-by-month")]
        public async Task<ActionResult<IEnumerable<LaborDetailItemDto>>> GetLaborDetailsByMonth(
    [FromQuery] int month,
    [FromQuery] int year,
    [FromQuery] string? factoryId = null,
    [FromQuery] string? teamId = null)
        {
            if (month < 1 || month > 12 || year < 2000)
                return BadRequest("month (1-12) hoặc year không hợp lệ.");

            var result = await ComputeLaborDetailsAsync(month, year, factoryId, teamId);
            return Ok(result);
        }

        // Tách logic tính công lao động theo Tổ/Ngày để tái sử dụng cho "revenue-cm-details-by-month"
        private async Task<List<LaborDetailItemDto>> ComputeLaborDetailsAsync(
            int month, int year, string? factoryId, string? teamId)
        {
            var daysInMonth = DateTime.DaysInMonth(year, month);
            var startDate = new DateTime(year, month, 1);
            var endDate = new DateTime(year, month, daysInMonth, 23, 59, 59);

            // ── 1. Tổng công — HR Daily Working Hour theo Tổ (Work Shift Code) + Ngày (DateIn) ──
            var hrQuery = _context.HRDailyWorkingHours
                .Where(h => h.DateIn >= startDate && h.DateIn <= endDate)
                .AsNoTracking();

            if (!string.IsNullOrWhiteSpace(factoryId) && factoryId != "all")
                hrQuery = hrQuery.Where(h => h.WorkCenterCode == factoryId);
            if (!string.IsNullOrWhiteSpace(teamId) && teamId != "all")
                hrQuery = hrQuery.Where(h => h.WorkShiftCode == teamId);

            var congByTeamDay = await hrQuery
                .GroupBy(h => new { h.WorkShiftCode, Day = h.DateIn.Day })
                .Select(g => new { g.Key.WorkShiftCode, g.Key.Day, Cong = g.Sum(x => x.WorkDay ?? 0) })
                .ToListAsync();

            // ── 2. Giờ tăng ca — Register OverTime theo Tổ (Work Shift Code) + Ngày (Posting Date) ──
            // Register OverTime không có Work Center Code -> lọc theo nhà máy gián tiếp qua các mã Tổ
            // đã xuất hiện ở bước 1 (khi có filter factoryId), hoặc lọc trực tiếp theo teamId nếu có.
            var teamCodesInFactory = congByTeamDay.Select(x => x.WorkShiftCode).Distinct().ToList();

            var otQuery = _context.RegisterOverTimes
                .Where(o => o.PostingDate >= startDate && o.PostingDate <= endDate && o.WorkShiftCode != null)
                .AsNoTracking();

            if (!string.IsNullOrWhiteSpace(teamId) && teamId != "all")
                otQuery = otQuery.Where(o => o.WorkShiftCode == teamId);
            else if (!string.IsNullOrWhiteSpace(factoryId) && factoryId != "all")
                otQuery = otQuery.Where(o => teamCodesInFactory.Contains(o.WorkShiftCode));

            var overTimeByTeamDay = await otQuery
                .GroupBy(o => new { WorkShiftCode = o.WorkShiftCode!, Day = o.PostingDate.Day })
                .Select(g => new { g.Key.WorkShiftCode, g.Key.Day, OverTime = g.Sum(x => x.OverTime ?? 0) })
                .ToListAsync();

            // ── 3. Gộp danh sách mã Tổ (từ cả 2 nguồn) + tra tên Tổ từ workShift ──
            var allTeamCodes = congByTeamDay.Select(x => x.WorkShiftCode)
                .Union(overTimeByTeamDay.Select(x => x.WorkShiftCode))
                .Distinct()
                .ToList();

            if (allTeamCodes.Count == 0)
                return new List<LaborDetailItemDto>();

            var teamNameLookup = await _context.workShift
                .Where(w => allTeamCodes.Contains(w.Code))
                .AsNoTracking()
                .ToDictionaryAsync(w => w.Code, w => w.Name ?? w.Code);

            var congLookup = congByTeamDay.ToDictionary(x => (x.WorkShiftCode, x.Day), x => x.Cong);
            var otLookup = overTimeByTeamDay.ToDictionary(x => (x.WorkShiftCode, x.Day), x => x.OverTime);

            var result = allTeamCodes.Select(code =>
            {
                var daily = new Dictionary<string, LaborDayDetailDto>();
                for (var day = 1; day <= daysInMonth; day++)
                {
                    var cong = congLookup.GetValueOrDefault((code, day), 0m);
                    var overTime = otLookup.GetValueOrDefault((code, day), 0m);
                    if (cong == 0 && overTime == 0) continue;

                    var iso = new DateTime(year, month, day).ToString("yyyy-MM-dd");
                    daily[iso] = new LaborDayDetailDto { Cong = cong, OverTimeHours = overTime };
                }

                return new LaborDetailItemDto
                {
                    Id = code,
                    ToMay = teamNameLookup.GetValueOrDefault(code, code),
                    Daily = daily
                };
            })
            .OrderBy(x => x.ToMay)
            .ToList();

            return result;
        }

        // GET /api/production/revenue-cm-details-by-month
        // Doanh thu CM (USD) theo Tổ + theo ngày = Doanh thu (USD) / (Tổng công + Tổng giờ tăng ca / 9)
        [HttpGet("revenue-cm-details-by-month")]
        public async Task<ActionResult<IEnumerable<CmDetailItemDto>>> GetRevenueCmDetailsByMonth(
    [FromQuery] int month,
    [FromQuery] int year,
    [FromQuery] string? factoryId = null,
    [FromQuery] string? teamId = null)
        {
            if (month < 1 || month > 12 || year < 2000)
                return BadRequest("month (1-12) hoặc year không hợp lệ.");

            var revenueRows = await ComputeRevenueDetailsAsync(month, year, factoryId, teamId);
            var laborRows = await ComputeLaborDetailsAsync(month, year, factoryId, teamId);

            // Gộp doanh thu theo Tổ + Ngày (1 Tổ có thể có nhiều dòng do khác Style/OperationCode)
            var revenueByTeamDay = new Dictionary<string, Dictionary<string, decimal>>();
            var teamNameById = new Dictionary<string, string>();
            foreach (var row in revenueRows)
            {
                teamNameById.TryAdd(row.Id, row.ToMay);
                if (!revenueByTeamDay.TryGetValue(row.Id, out var bucket))
                    revenueByTeamDay[row.Id] = bucket = new Dictionary<string, decimal>();
                foreach (var (iso, revenue) in row.Daily ?? new Dictionary<string, decimal>())
                    bucket[iso] = bucket.GetValueOrDefault(iso) + revenue;
            }

            var laborByTeam = new Dictionary<string, LaborDetailItemDto>();
            foreach (var row in laborRows)
            {
                teamNameById.TryAdd(row.Id, row.ToMay);
                laborByTeam[row.Id] = row;
            }

            var allTeamIds = revenueByTeamDay.Keys.Union(laborByTeam.Keys).ToList();
            if (allTeamIds.Count == 0)
                return Ok(Array.Empty<CmDetailItemDto>());

            var daysInMonth = DateTime.DaysInMonth(year, month);
            var result = allTeamIds.Select(id =>
            {
                var daily = new Dictionary<string, CmDayDetailDto>();
                revenueByTeamDay.TryGetValue(id, out var revenueBucket);
                laborByTeam.TryGetValue(id, out var laborRow);

                for (var day = 1; day <= daysInMonth; day++)
                {
                    var iso = new DateTime(year, month, day).ToString("yyyy-MM-dd");
                    var revenue = revenueBucket?.GetValueOrDefault(iso) ?? 0m;
                    LaborDayDetailDto? laborCell = null;
                    laborRow?.Daily?.TryGetValue(iso, out laborCell);
                    var labor = (laborCell?.Cong ?? 0m) + (laborCell?.OverTimeHours ?? 0m) / 9m;
                    if (revenue == 0 && labor == 0) continue;

                    daily[iso] = new CmDayDetailDto
                    {
                        RevenueUsd = revenue,
                        Labor = labor,
                        Cm = labor > 0 ? revenue / labor : 0m
                    };
                }

                return new CmDetailItemDto
                {
                    Id = id,
                    ToMay = teamNameById.GetValueOrDefault(id, id),
                    Daily = daily
                };
            })
            .OrderBy(x => x.ToMay)
            .ToList();

            return Ok(result);
        }

        // GET /api/production/revenue-summary-by-month
        // Báo cáo "Tổng hợp" doanh thu theo Tổ/Khách hàng/PO/Style — cộng dồn cả kỳ (không tách theo ngày)
        [HttpGet("revenue-summary-by-month")]
        public async Task<ActionResult<IEnumerable<RevenueSummaryItemDto>>> GetRevenueSummaryByMonth(
    [FromQuery] int month,
    [FromQuery] int year,
    [FromQuery] string? factoryId = null,
    [FromQuery] string? teamId = null)
        {
            if (month < 1 || month > 12 || year < 2000)
                return BadRequest("month (1-12) hoặc year không hợp lệ.");

            var startDate = new DateTime(year, month, 1);
            var endDate = new DateTime(year, month, DateTime.DaysInMonth(year, month));
            var monthStart = startDate;

            // ── 1. Lấy dữ liệu sản lượng thực hiện trong kỳ ─────────────────────────
            var qBase = _context.qualityManagementSessions
                .Where(q => q.InspectionDate >= startDate && q.Status == 1
                         && q.InspectionDate <= endDate
                         && q.OperationType == "1")
                .AsNoTracking();

            if (!string.IsNullOrWhiteSpace(factoryId) && factoryId != "all")
                qBase = qBase.Where(q => q.WorkCenterCode == factoryId);
            if (!string.IsNullOrWhiteSpace(teamId) && teamId != "all")
                qBase = qBase.Where(q => q.WorkShiftCode.ToString() == teamId);

            var rawSessions = await qBase
                .Join(_context.workShift.AsNoTracking(),
                    q => q.WorkShiftCode.ToString(),
                    w => w.Code,
                    (q, w) => new
                    {
                        Id = w.Code,
                        WorkShiftName = w.Name,
                        q.CustomerNo_,
                        q.SalesOrderNo,
                        q.PoName,
                        q.ItemNo,
                        q.PassOutputQuantity
                    })
                .ToListAsync();

            if (rawSessions.Count == 0)
                return Ok(Array.Empty<RevenueSummaryItemDto>());

            var itemNos = rawSessions.Select(x => x.ItemNo).Distinct().ToList();
            var shiftCodes = rawSessions.Select(x => x.Id).Distinct().ToList();

            // ── 2. Tên sản phẩm (Style) từ bảng Item ────────────────────────────────
            var itemNameByNo = await _context.Item
                .AsNoTracking()
                .Where(i => itemNos.Contains(i.No))
                .Select(i => new { i.No, i.Name })
                .ToDictionaryAsync(i => i.No, i => i.Name);

            // ── 3. Đơn giá CM (giống revenue-details-by-month) ──────────────────────
            var cmPricesRaw = await _context.StyleCmPrice
                .Where(p => itemNos.Contains(p.StyleId)
                         && p.IsActive
                         && p.EffectiveDate <= endDate
                         && (p.ExpiredDate == null || p.ExpiredDate > startDate))
                .Include(p => p.StyleCmSplits)
                .AsNoTracking()
                .ToListAsync();

            var cmPriceByStyle = cmPricesRaw
                .GroupBy(p => p.StyleId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.EffectiveDate).First());

            var operationStyleIds = cmPriceByStyle.Values.Where(p => p.IsOperation).Select(p => p.StyleId).Distinct().ToList();
            var operationByTeamStyle = operationStyleIds.Count == 0
                ? new Dictionary<(string StyleId, string LineNumber), string>()
                : (await _context.MesFactoryPlans
                    .Where(p => operationStyleIds.Contains(p.StyleId)
                             && p.LineNumber != null && shiftCodes.Contains(p.LineNumber)
                             && p.IsActive && p.OperationCode != null)
                    .Select(p => new { p.StyleId, p.LineNumber, p.OperationCode, p.CreatedAt })
                    .ToListAsync())
                    .GroupBy(p => (StyleId: p.StyleId, LineNumber: p.LineNumber!))
                    .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.CreatedAt).First().OperationCode!);

            (decimal UnitPrice, string? OperationCode) ResolveUnitPrice(string styleId, string lineNumber)
            {
                if (!cmPriceByStyle.TryGetValue(styleId, out var cmPrice))
                    return (0m, null);

                if (!cmPrice.IsOperation)
                    return (cmPrice.CmPrice, null);

                if (operationByTeamStyle.TryGetValue((styleId, lineNumber), out var operationCode))
                {
                    var split = cmPrice.StyleCmSplits.FirstOrDefault(s => s.OperationCode == operationCode);
                    if (split != null)
                        return (split.SplitPrice, operationCode);
                }

                return (0m, null);
            }

            // ── 4. Tỷ giá VND (Local Exchange), theo ngày đầu tháng gần nhất ────────
            var localExchangeRate = await _context.CurrencyExchangeRates
                .Where(e => e.StartingDate <= monthStart)
                .OrderByDescending(e => e.StartingDate)
                .Select(e => e.LocalExchange)
                .FirstOrDefaultAsync();

            if (localExchangeRate == 0) localExchangeRate = 1m;

            // ── 5. Gộp theo Tổ + Khách hàng + PO + Style, cộng dồn cả kỳ (không tách ngày) ──
            var result = rawSessions
                .Select(q =>
                {
                    var (unitPrice, operationCode) = ResolveUnitPrice(q.ItemNo, q.Id);
                    itemNameByNo.TryGetValue(q.ItemNo, out var itemName);

                    return new
                    {
                        q.Id,
                        ToMay = q.WorkShiftName.ToString(),
                        Customer = q.CustomerNo_,
                        q.SalesOrderNo,
                        q.PoName,
                        ItemNo = q.ItemNo,
                        Style = itemName ?? q.ItemNo,
                        UnitPrice = unitPrice,
                        OperationCode = operationCode,
                        q.PassOutputQuantity,
                        Revenue = unitPrice * (decimal)q.PassOutputQuantity
                    };
                })
                .GroupBy(x => new { x.Id, x.ToMay, x.Customer, x.SalesOrderNo, x.PoName, x.ItemNo, x.Style, x.UnitPrice, x.OperationCode })
                .Select(g => new RevenueSummaryItemDto
                {
                    Id = g.Key.Id,
                    ToMay = g.Key.ToMay,
                    Customer = g.Key.Customer,
                    SalesOrderNo = g.Key.SalesOrderNo,
                    PoName = g.Key.PoName,
                    ItemNo = g.Key.ItemNo,
                    Style = g.Key.Style,
                    UnitPrice = g.Key.UnitPrice,
                    OperationCode = g.Key.OperationCode,
                    Quantity = g.Sum(x => x.PassOutputQuantity),
                    RevenueUsd = g.Sum(x => x.Revenue),
                    RevenueVnd = g.Sum(x => x.Revenue) * localExchangeRate
                })
                .OrderBy(x => x.ToMay)
                .ThenBy(x => x.SalesOrderNo)
                .ToList();

            return Ok(result);
        }

        // GET /api/mes/cm-prices?factoryId=&year=&month=
        [HttpGet("cm-prices")]
        public async Task<IActionResult> GetCmPrices(
            [FromQuery] string factoryId, [FromQuery] int year, [FromQuery] int month, [FromQuery] int toYear, [FromQuery] int toMonth)
        {
            try
            {
                var result = await _productionPlanService.GetCmPricesAsync(factoryId, year, month,toYear,toMonth);
                return Ok(result);
            }
            catch (BusinessValidationException ex)
            {
                return BadRequest(new ApiErrorResponse { Message = ex.Message });
            }
        }

        [HttpPost("cm-prices")]
        public async Task<IActionResult> CreateCmPrice([FromBody] CmPriceCreateRequest request)
        {
            if (request == null)
                return BadRequest(new ApiErrorResponse { Message = "Body không hợp lệ" });

            var currentUser = User?.Identity?.Name;

            try
            {
                var result = await _productionPlanService.CreateCmPriceAsync(request);
                return CreatedAtAction(nameof(GetCmPrices), new { }, result);
            }
            catch (BusinessValidationException ex)
            {
                return BadRequest(new ApiErrorResponse { Message = ex.Message });
            }
        }

        [HttpGet("operation")]
        public async Task<IActionResult> GetOperation()
        {
            try
            {
                var result = await _productionPlanService.GetOperation();
                return Ok(result);
            }
            catch (BusinessValidationException ex)
            {
                return BadRequest(new ApiErrorResponse { Message = ex.Message });
            }
        }

        // ─── GET /api/production/user-role ───────────────────────────────────────
        // Tra role của user (dùng để FE kiểm tra quyền Admin trước khi mở khóa chỉnh sửa
        // SLKH ngoài phạm vi 3 ngày). Đọc trực tiếp cột [Role] theo [User ID] trong bảng
        // [User Setup Mobile] (bảng dùng chung với hệ thống đăng nhập mobile/web, chưa có
        // EF model riêng nên dùng raw SQL có tham số hoá để tránh SQL injection).
        [HttpGet("user-role")]
        public async Task<IActionResult> GetUserRole([FromQuery] string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return BadRequest(new { success = false, message = "Thiếu userId." });

            try
            {
                var role = await _context.Database
                    .SqlQueryRaw<string>(
                        "SELECT [Role] FROM [User Setup Mobile] WHERE [User ID] = {0}",
                        userId)
                    .FirstOrDefaultAsync();

                return Ok(new { success = true, userId, role = role ?? string.Empty });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("operation-style")]
        public async Task<IActionResult> GetOperationByStyle([FromQuery] string styleId)
        {
            try
            {
                var result = await _productionPlanService.GetOperationByStyle(styleId);
                return Ok(result);
            }
            catch (BusinessValidationException ex)
            {
                return BadRequest(new ApiErrorResponse { Message = ex.Message });
            }
        }
        //Lấy giá CM từng công đoạn đã phân bổ
        [HttpGet("cm-operation")]
        public async Task<IActionResult> GetCMPriceOperation([FromQuery] string styleId)
        {
            try
            {
                var result = await _productionPlanService.GetCMPriceOperationAsync(styleId);
                return Ok(result);
            }
            catch (BusinessValidationException ex)
            {
                return BadRequest(new ApiErrorResponse { Message = ex.Message });
            }
        }

        [HttpPost("{styleId}/cm-splits")]
        public async Task<IActionResult> SaveCmSplits(
    [FromRoute] string styleId,
    [FromBody] CmSplitBatchRequest request)
        {
            if (request is null)
                return BadRequest(new ApiErrorResponse { Message = "Body không hợp lệ" });

            // Lấy userId từ JWT claim thay vì nhận từ client
            var userId = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                      ?? User?.Identity?.Name;

            try
            {
                var result = await _productionPlanService
                    .CreateCmSplitAsync(styleId, request, userId);
                return Ok(result);
            }
            catch (BusinessValidationException ex)
            {
                return BadRequest(new ApiErrorResponse { Message = ex.Message });
            }
        }

        // ─── POST /api/production/team-allocations ──────────────────────────────────
        /// <summary>
        /// Xác nhận gán tổ sản xuất cho một dòng Kế hoạch (Style/PO/Màu).
        /// Mỗi allocation tương ứng 1 tổ (toId) + số lượng KH tổ (qty).
        /// Logic:
        ///   1. Deactivate các factory_plan cũ của (factoryId, style, po, color) không còn trong danh sách mới.
        ///   2. Upsert mes.factory_plans — mỗi tổ được cấp 1 dòng riêng.
        ///   3. Đảm bảo mes.style_cm_prices tồn tại cho styleId (tạo placeholder nếu chưa có).
        ///   4. Trả về danh sách factory_plan mới kèm dữ liệu cm_price / cm_splits,
        ///      cấu trúc tương tự GetFactoryPlans_V2_View.
        /// </summary>
        
        [HttpPost("team-allocations")]
        public async Task<IActionResult> SaveTeamAllocation([FromBody] SaveTeamAllocationRequest request)
        {
            // ── Validate ─────────────────────────────────────────────────────────────
            if (request == null)
                return BadRequest(new { success = false, message = "Request body không hợp lệ." });
            if (string.IsNullOrWhiteSpace(request.FactoryId))
                return BadRequest(new { success = false, message = "Thiếu factoryId." });
            if (string.IsNullOrWhiteSpace(request.Style))
                return BadRequest(new { success = false, message = "Thiếu style (styleId)." });
            if (string.IsNullOrWhiteSpace(request.Po))
                return BadRequest(new { success = false, message = "Thiếu po (poId)." });
            if (string.IsNullOrWhiteSpace(request.Color))
                return BadRequest(new { success = false, message = "Thiếu color (colorId)." });
            if (request.Allocations == null || request.Allocations.Count == 0)
                return BadRequest(new { success = false, message = "Danh sách phân bổ tổ không được rỗng." });
            if (request.Allocations.Any(a => string.IsNullOrWhiteSpace(a.ToId) || a.Qty <= 0))
                return BadRequest(new { success = false, message = "Mỗi phân bổ phải có toId hợp lệ và qty > 0." });
            var hasSplits = await _productionPlanService
    .StyleHasCmSplitsAsync(request.Style);

            if (hasSplits)
            {
                var missingOp = request.Allocations
                    .Where(a => string.IsNullOrWhiteSpace(a.OperationCode))
                    .Select(a => a.ToId)
                    .ToList();

                if (missingOp.Any())
                    return BadRequest(new
                    {
                        success = false,
                        message = $"Style này có phân bổ công đoạn. " +
                                  $"Vui lòng chọn công đoạn cần phân bổ cho tổ!"
                    });
            }
            var now = DateTime.Now;
            var currentUser = User?.Identity?.Name ?? "system";

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // ── Bước 1: Load TẤT CẢ plan hiện có, tách riêng dòng gốc và dòng đã gán tổ ──
                var existingPlans = await _context.MesFactoryPlans
                    .Where(p => p.FactoryId == request.FactoryId
                             && p.StyleId == request.Style
                             && p.PoId == request.Po
                             && p.ColorId == request.Color
                             && p.IsActive)
                    .ToListAsync();

                // Dòng gốc: LineNumber = null, đại diện cho KH tổng chưa/đang phân bổ
                var originPlan = await _context.MesFactoryPlans
    .FirstOrDefaultAsync(p => p.FactoryId == request.FactoryId
                            && p.StyleId == request.Style
                            && p.PoId == request.Po
                            && p.ColorId == request.Color
                            && p.LineNumber == null);
                // bỏ điều kiện IsActive, hoặc tách riêng cột IsFullyAllocated

                if (originPlan == null)
                    return BadRequest(new { success = false, message = "Không tìm thấy dòng KH gốc để phân bổ." });

                // Nếu dòng gốc từng bị tắt do "đã phân bổ đủ" ở lần lưu trước, bật lại để tiếp tục cho phép sửa
                if (!originPlan.IsActive)
                {
                    originPlan.IsActive = true;
                    originPlan.UpdatedAt = now;
                }

                // Các dòng đã gán tổ trước đó (nếu có)
                var teamPlans = existingPlans.Where(p => p.LineNumber != null).ToList();

                // ── Bước 2: Validate tổng số lượng KHÔNG vượt KH tổng của dòng gốc ──
                var groupedByOperation = request.Allocations
    .GroupBy(a => a.OperationCode, StringComparer.OrdinalIgnoreCase)
    .ToList();

                foreach (var group in groupedByOperation)
                {
                    var opTotalQty = group.Sum(a => a.Qty);
                    if (opTotalQty > originPlan.TotalPlannedQty)
                        return BadRequest(new
                        {
                            success = false,
                            message = $"Công đoạn '{group.Key}': tổng SL gán ({opTotalQty}) vượt quá KH tổng ({originPlan.TotalPlannedQty})."
                        });
                }
               

                // ── Bước 3: Deactivate các tổ không còn trong danh sách phân bổ mới ──
                var requestedKeys = request.Allocations
    .Select(a => $"{a.ToId}|{a.OperationCode}")
    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                foreach (var old in teamPlans.Where(p =>
             !requestedKeys.Contains($"{p.LineNumber}|{p.OperationCode}")))
                {
                    // TODO: chặn nếu tổ này đã có sản lượng thực tế (Status = DONE hoặc có actual output)
                    old.IsActive = false;
                    old.UpdatedAt = now;
                }
                // ── Bước 4: Upsert factory_plans — mỗi tổ 1 dòng, kế thừa ProductionOrderId/VersionId từ dòng gốc ──
                var resultPlanIds = new List<Guid>();

                foreach (var alloc in request.Allocations)
                {
                    var existing = teamPlans.FirstOrDefault(p =>
                        string.Equals(p.LineNumber, alloc.ToId, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(p.OperationCode, alloc.OperationCode, StringComparison.OrdinalIgnoreCase));

                    if (existing != null)
                    {
                        existing.TotalPlannedQty = alloc.Qty;
                        existing.IsActive = true;
                        existing.UpdatedAt = now;

                        //if (existing.Status is "APPROVED")
                        //{
                        //    existing.Status = "DRAFT";
                        //    existing.ApprovedBy = null;
                        //    existing.ApprovedAt = null;
                        //}
                        //else if (existing.Status != "DONE" && existing.Status != "CANCELLED")
                        //{
                        //    existing.Status = "ACTIVE";
                        //}

                        resultPlanIds.Add(existing.Id);
                    }
                    else
                    {
                        var newPlan = new MesFactoryPlan
                        {
                            Id = Guid.NewGuid(),
                            PlanCode = originPlan.PlanCode,
                            ProductionOrderId = originPlan.ProductionOrderId,
                            VersionId = originPlan.VersionId,
                            FactoryId = request.FactoryId,
                            StyleId = request.Style,
                            PoId = request.Po,
                            ColorId = request.Color,
                            LineNumber = alloc.ToId,
                            OperationCode = alloc.OperationCode,
                            TotalPlannedQty = alloc.Qty,
                            Status = "normal",
                            BufferDays = 0,
                            CreatedBy = currentUser,
                            CreatedAt = now,
                            UpdatedAt = now,
                            IsActive = true,
                        };
                        _context.MesFactoryPlans.Add(newPlan);
                        resultPlanIds.Add(newPlan.Id);
                    }
                }
                // ── Bước 5: Deactivate dòng gốc CHỈ KHI TẤT CẢ operation trong request đã phân bổ đủ 100% ──
                bool allOperationsFullyAllocated = groupedByOperation.All(g => g.Sum(a => a.Qty) == originPlan.TotalPlannedQty);

                originPlan.UpdatedAt = now;

                await _context.SaveChangesAsync();

                var cmPrice = await _context.StyleCmPrice
                    .Where(c => c.StyleId == request.Style && c.IsActive)
                    .OrderByDescending(c => c.EffectiveDate)
                    .FirstOrDefaultAsync();

                if (cmPrice == null)
                {
                    cmPrice = new StyleCmPrice
                    {
                        Id            = Guid.NewGuid(),
                        StyleId       = request.Style,
                        CmPrice       = 0,
                        Currency      = "USD",
                        EntryMethod   = "MANUAL",
                        EffectiveDate = now,
                        IsActive      = true,
                        CreatedBy     = currentUser,
                        CreatedAt     = now,
                        IsOperation = false
                    };
                    _context.StyleCmPrice.Add(cmPrice);
                    await _context.SaveChangesAsync();
                }

                var cmSplits = await _context.StyleCmSplits
                    .Where(s => s.StyleCmPriceId == cmPrice.Id)
                    .OrderBy(s => s.OperationCode)
                    .ToListAsync();

                await transaction.CommitAsync();

                var resultPlans = await _context.MesFactoryPlans
                    .Where(p => resultPlanIds.Contains(p.Id) && p.IsActive==true)
                    .OrderBy(p => p.LineNumber)
                    .ToListAsync();

                var styleName = await _context.Item
                    .Where(i => i.No == request.Style)
                    .Select(i => i.Name ?? string.Empty)
                    .FirstOrDefaultAsync() ?? string.Empty;

                var poName = await _context.SalesOrderHeader
                    .Where(s => s.No == request.Po)
                    .Select(s => s.POName ?? string.Empty)
                    .FirstOrDefaultAsync() ?? string.Empty;

                var cmPriceDto = new TeamAllocationCmPrice
                {
                    StyleId       = cmPrice.StyleId,
                    Price         = cmPrice.CmPrice,
                    Currency      = cmPrice.Currency,
                    EffectiveDate = cmPrice.EffectiveDate,
                    Splits        = cmSplits.Select(s => new TeamAllocationCmSplit
                    {
                        Id            = s.Id,
                        OperationCode = s.OperationCode,
                        OperationName = s.OperationName,
                        SplitRatio    = s.SplitRatio,
                        SplitPrice    = s.SplitPrice,
                        DefaultLineId = s.DefaultLineId,
                    }).ToList(),
                };

                var data = resultPlans.Select(p =>
                {
                    // Tìm split tương ứng với operation của dòng plan này
                    var matchedSplit = cmSplits.FirstOrDefault(s =>
                        string.Equals(s.OperationCode, p.OperationCode, StringComparison.OrdinalIgnoreCase));

                    var rowCmPriceDto = new TeamAllocationCmPrice
                    {
                        StyleId = cmPrice.StyleId,
                        Price = matchedSplit?.SplitPrice ?? cmPrice.CmPrice, // ưu tiên đơn giá theo công đoạn, fallback đơn giá tổng nếu không tìm thấy
                        Currency = cmPrice.Currency,
                        EffectiveDate = cmPrice.EffectiveDate,
                        Splits = cmSplits.Select(s => new TeamAllocationCmSplit
                        {
                            Id = s.Id,
                            OperationCode = s.OperationCode,
                            OperationName = s.OperationName,
                            SplitRatio = s.SplitRatio,
                            SplitPrice = s.SplitPrice,
                            DefaultLineId = s.DefaultLineId,
                        }).ToList(),
                    };

                    return new TeamAllocationPlanRow
                    {
                        Id = p.Id,
                        PlanCode = p.PlanCode,
                        FactoryId = p.FactoryId,
                        LineNumber = p.LineNumber,
                        StyleId = p.StyleId,
                        StyleName = styleName,
                        PoId = p.PoId,
                        PoName = poName,
                        ColorId = p.ColorId,
                        ColorName = p.ColorId,
                        ProductionStartDate = p.ProductionStartDate,
                        ProductionEndDate = p.ProductionEndDate,
                        TargetDeliveryDate = null,
                        TotalPlannedQty = p.TotalPlannedQty,
                        TeamTotalQty = new Dictionary<string, int>(),
                        MonthlyPlannedQty = 0,
                        CmPrice = rowCmPriceDto,
                    };
                }).ToList();

                return Ok(new SaveTeamAllocationResult
                {
                    Success = true,
                    Message = $"Đã phân bổ {data.Count} tổ cho Style={request.Style}, PO={request.Po}, Màu={request.Color}.",
                    Data    = data,
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return BadRequest(new
                {
                    success = false,
                    message = "Lỗi khi lưu phân bổ tổ.",
                    detail  = ex.Message,
                });
            }
        }


        // ===================== API: Xoá phân bổ =====================
        // FE lấy Id từ API GetTeamAllocation (team-allocations) khi mở modal,
        // nên xoá xác định trực tiếp qua Id - an toàn hơn xác định qua bộ khoá nghiệp vụ.

        [HttpDelete("team-allocations/{id}")]
        public async Task<IActionResult> XoaPhanBo(Guid id, [FromQuery] bool force = false)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var plan = await _context.MesFactoryPlans
                    .FirstOrDefaultAsync(p => p.Id == id && p.IsActive);

                if (plan == null)
                    return NotFound(new { success = false, message = "Không tìm thấy dòng phân bổ." });

                if (string.IsNullOrWhiteSpace(plan.LineNumber))
                    return BadRequest(new { success = false, message = "Không thể xoá dòng chưa phân tổ." });

                // ---- Kiểm tra đã có lịch rải theo ngày chưa ----
                var scheduledLines = await _context.MesFactoryPlanLines
                    .Where(l => l.FactoryPlanId == id)
                    .ToListAsync();

                if (scheduledLines.Count > 0 && !force)
                {
                    await transaction.RollbackAsync();
                    return Conflict(new
                    {
                        success = false,
                        requireConfirm = true,
                        scheduledDays = scheduledLines.Count,
                        totalScheduledQty = scheduledLines.Sum(l => l.PlannedQty),
                        message = $"Lỗi: Tổ {plan.LineNumber} đã có phân bổ kế hoạch ngày)!"
                    });
                }

                // ---- Xoá cứng lịch rải theo ngày (nếu có và đã được force confirm) ----
                if (scheduledLines.Count > 0)
                {
                    _context.MesFactoryPlanLines.RemoveRange(scheduledLines);
                }

                var soLuongTraLai = plan.TotalPlannedQty;

                // Lưu lại các field khoá đơn hàng TRƯỚC khi Remove, vì sau khi Remove
                // entity plan coi như đã bị đánh dấu xoá, không nên dựa vào nó để query tiếp
                var factoryId = plan.FactoryId;
                var versionId = plan.VersionId;
                var styleId = plan.StyleId;
                var poId = plan.PoId;
                var colorId = plan.ColorId;

                // ---- Xoá cứng dòng phân bổ ----
                _context.MesFactoryPlans.Remove(plan);

                // ---- Cộng lại số lượng vừa xoá vào dòng "chưa phân tổ" cùng đơn hàng ----
                var unassignedPlan = await _context.MesFactoryPlans
                    .FirstOrDefaultAsync(p => p.FactoryId == factoryId && p.IsActive==true
                                           && p.VersionId == versionId
                                           && p.StyleId == styleId
                                           && p.PoId == poId
                                           && p.ColorId == colorId
                                           && string.IsNullOrWhiteSpace(p.LineNumber));

                if (unassignedPlan != null)
                {
                    unassignedPlan.IsActive = true; // trước đó có thể đã bị tắt do "đã phân bổ hết"
                    unassignedPlan.TotalPlannedQty += soLuongTraLai;
                    unassignedPlan.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    _context.MesFactoryPlans.Add(new MesFactoryPlan
                    {
                        FactoryId = factoryId,
                        VersionId = versionId,
                        StyleId = styleId,
                        PoId = poId,
                        ColorId = colorId,
                        LineNumber = null,
                        OperationCode = null,
                        TotalPlannedQty = soLuongTraLai,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                    });
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { success = true, message = "Xoá phân bổ thành công.", soLuongTraLai });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra khi xoá phân bổ.", detail = ex.Message });
            }
        }

        [HttpGet("team-allocations")]
        public async Task<IActionResult> GetTeamAllocation(
    [FromQuery] string factoryId,
    [FromQuery] string style,
    [FromQuery] string po,
    [FromQuery] string color)
        {
            if (string.IsNullOrWhiteSpace(factoryId) || string.IsNullOrWhiteSpace(style)
                || string.IsNullOrWhiteSpace(po) || string.IsNullOrWhiteSpace(color))
                return BadRequest(new { success = false, message = "Thiếu tham số bắt buộc." });

            var plans = await _context.MesFactoryPlans
                .Where(p => p.FactoryId == factoryId
                         && p.StyleId == style
                         && p.PoId == po
                         && p.ColorId == color
                         && p.IsActive)
                .ToListAsync();

            if (plans.Count == 0)
                return NotFound(new { success = false, message = "Không tìm thấy kế hoạch." });

            // Tổng KH = tổng tất cả các dòng (đã phân bổ + chưa phân bổ)
            var totalDemandQty = plans.Sum(p => p.TotalPlannedQty);

            // Các dòng đã phân bổ cho tổ
            var teamPlans = plans.Where(p => p.LineNumber != null).ToList();
            var allocatedQty = teamPlans.Sum(p => p.TotalPlannedQty);

            var data = new GetTeamAllocationResult
            {
                Style = style,
                Po = po,
                Color = color,
                TotalDemandQty = totalDemandQty,
                AllocatedQty = allocatedQty,
                RemainingQty = totalDemandQty - allocatedQty,
                Allocations = teamPlans.Select(p => new TeamAllocationItem
                {
                    Id = p.Id.ToString(),
                    ToId = p.LineNumber!,
                    Qty = p.TotalPlannedQty,
                    OperationCode = p.OperationCode
                }).ToList(),
            };

            return Ok(new { success = true, data });
        }

        [HttpPost("production-plans/{planId}/transfer")]
        public async Task<IActionResult> TransferPlan(Guid planId, [FromBody] TransferPlanRequest request)
        {
            var sourcePlan = await _context.MesFactoryPlans.FindAsync(planId);
            if (sourcePlan == null)
                return NotFound(new { success = false, message = "Không tìm thấy kế hoạch" });

            if (request.TargetLineNumber == sourcePlan.LineNumber)
                return BadRequest(new { success = false, message = "Tổ đích phải khác tổ hiện tại" });

            if (request.TransferQty <= 0)
                return BadRequest(new { success = false, message = "Số lượng chuyển phải lớn hơn 0" });

            // 1. Tính remaining thực tế (chưa sản xuất) của tổ nguồn
            var actualQty = await _context.qualityManagementSessions
                .Where(q => q.WorkShiftCode == sourcePlan.LineNumber && q.SalesOrderNo == sourcePlan.PoId && q.ItemNo == sourcePlan.StyleId && q.ColorNo == sourcePlan.ColorId && q.Status==1)
                .SumAsync(q => (int?)q.PassOutputQuantity) ?? 0;

            var remaining = sourcePlan.TotalPlannedQty - actualQty;

            if (request.TransferQty > remaining)
                return BadRequest(new
                {
                    success = false,
                    message = $"Chỉ được chuyển tối đa {remaining} (phần chưa sản xuất)"
                });

            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                // 2. Trừ số lượng tổ nguồn
                sourcePlan.TotalPlannedQty -= request.TransferQty;
                sourcePlan.UpdatedAt = DateTime.UtcNow;

                // 3. Dọn các ô ngày tương lai của tổ nguồn (giống cơ chế Tạm dừng)
                var futureDaily = await _context.MesFactoryPlanLines
                    .Where(d => d.FactoryPlanId == planId && DateOnly.FromDateTime(d.WorkDate) >= request.EffectiveDate)
                    .ToListAsync();
                foreach (var d in futureDaily) d.PlannedQty = 0;

                // 4. Tìm hoặc tạo dòng ở tổ đích
                var targetPlan = await _context.MesFactoryPlans.FirstOrDefaultAsync(p =>
                    p.LineNumber == request.TargetLineNumber && p.IsActive==true
                    && p.StyleId == sourcePlan.StyleId
                    && p.PoId == sourcePlan.PoId
                    && p.ColorId == sourcePlan.ColorId);

                if (targetPlan == null)
                {
                    targetPlan = new MesFactoryPlan
                    {
                        Id = Guid.NewGuid(),
                        VersionId = sourcePlan.VersionId,
                        FactoryId = sourcePlan.FactoryId,
                        LineNumber = request.TargetLineNumber,
                        StyleId = sourcePlan.StyleId,
                        PoId = sourcePlan.PoId,
                        ColorId = sourcePlan.ColorId,
                        TotalPlannedQty = request.TransferQty,
                        CreatedAt = DateTime.UtcNow,
                        PlanCode = sourcePlan.PlanCode,
                        ProductionOrderId = sourcePlan.ProductionOrderId,
                        Status = sourcePlan.Status,
                        UpdatedAt = DateTime.UtcNow,
                        CreatedBy = sourcePlan.CreatedBy,
                        IsActive = true
                    };
                    _context.MesFactoryPlans.Add(targetPlan);
                }
                else
                {
                    targetPlan.TotalPlannedQty += request.TransferQty;
                    targetPlan.UpdatedAt = DateTime.UtcNow;
                }

                // 5. Ghi lịch sử (audit) cho cả 2 bên
                _context.FactoryPlanStatusHistory.AddRange(
                    new FactoryPlanStatusHistory
                    {
                        PlanId = sourcePlan.Id,
                        Note = $"Chuyển {request.TransferQty} sang tổ {request.TargetLineNumber}",
                        EffectiveDate = request.EffectiveDate,
                        ChangedBy = request.UserId,
                        ChangedAt = DateTime.UtcNow
                    },
                    new FactoryPlanStatusHistory
                    {
                        PlanId = targetPlan.Id,
                        Note = $"Nhận {request.TransferQty} từ tổ {sourcePlan.LineNumber}",
                        EffectiveDate = request.EffectiveDate,
                        ChangedBy = request.UserId,
                        ChangedAt = DateTime.UtcNow
                    }
                );

                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                return Ok(new
                {
                    success = true,
                    sourceRemaining = sourcePlan.TotalPlannedQty - actualQty,
                    targetPlanId = targetPlan.Id,
                    targetTotalPlannedQty = targetPlan.TotalPlannedQty
                });
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // POST /api/production/plans/merge/preview
        //[HttpPost("merge/preview")]
        //public async Task<IActionResult> Preview([FromBody] MergePreviewRequest request)
        //{
        //    try
        //    {
        //        var result = await _productionPlanService.PreviewAsync(request);
        //        return Ok(result);
        //    }
        //    catch (BusinessValidationException ex)
        //    {
        //        return BadRequest(new { message = ex.Message });
        //    }
        //}

        // POST /api/production/plans/merge
        [HttpPost("plans/merge")]
        public async Task<IActionResult> Execute([FromBody] MergeExecuteRequest request)
        {
            try
            {
                var result = await _productionPlanService.ExecuteAsync(request);
                return StatusCode(StatusCodes.Status201Created, result);
            }
            catch (BusinessValidationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
        [HttpGet("plans/merge/detail")]
        public async Task<IActionResult> GetMergedDetail([FromQuery] string poId)
        {
            if (string.IsNullOrWhiteSpace(poId))
                return BadRequest(new { message = "Thiếu tham số poId." });

            try
            {
                var result = await _productionPlanService.GetMergedPoDetailAsync(poId);
                return Ok(result);
                // Nếu hệ thống đang dùng envelope chung dạng { success, data, message } cho toàn bộ API
                // (FE có xử lý fallback data?.data ?? data cho thấy khả năng này), đổi thành:
                // return Ok(new { success = true, data = result });
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        private async Task<object?> BuildPoDetailAsync(string factoryId, string styleId, string poId, string colorId)
        {
            // LƯU Ý: KHÔNG lọc theo IsActive ở đây. Hàm này dùng để tra chi tiết CHO BẤT KỲ po_id nào,
            // kể cả PO gốc đã bị đánh dấu is_active=0 sau khi bị tách (giống cách PO gốc bị đánh dấu
            // is_active=0/status='merged' sau khi Gom PO). Nếu lọc IsActive=1, PO gốc sẽ luôn trả về rỗng
            // -> splitFrom = null dù dữ liệu vẫn còn nguyên trong DB.
            var plans = await _context.MesFactoryPlans
                .AsNoTracking()
                .Where(p => p.FactoryId == factoryId
                    && p.StyleId == styleId
                    && p.PoId == poId
                    && p.ColorId == colorId )
                .OrderBy(p => p.LineNumber)
                .ThenBy(p => p.CreatedAt)
                .ToListAsync();

            if (plans.Count == 0)
                return null;

            var assignedRows = plans.Where(p => !string.IsNullOrWhiteSpace(p.LineNumber)).ToList();
            var unassignedRows = plans.Where(p => string.IsNullOrWhiteSpace(p.LineNumber)).ToList();

            // ===== BUG ĐÃ SỬA #1: Actual phải join theo plan_id (Guid, factory_plans.Id),
            // KHÔNG join theo po_id dạng chuỗi — QC ghi nhận theo plan_id cụ thể. Join theo po_id text
            // sẽ luôn ra 0 cho PO vừa tách (vì mã PO tách mới, chưa từng tồn tại trong QLCL.SalesOrderNo).
            var planIds = plans.Select(p => p.Id).ToList();
            var actualByPlanId = await _context.qualityManagementSessions
                .Where(q => q.PlanId.HasValue && planIds.Contains(q.PlanId.Value) && q.Status==1)
                .GroupBy(q => q.PlanId!.Value)
                .Select(g => new { PlanId = g.Key, Actual = g.Sum(x => x.PassOutputQuantity) })
                .ToDictionaryAsync(x => x.PlanId, x => x.Actual);

            var lineNumbers = assignedRows.Select(p => p.LineNumber!).Distinct().ToList();
            var lineNames = await _context.workShift
                .Where(ws => lineNumbers.Contains(ws.Code))
                .ToDictionaryAsync(ws => ws.Code, ws => ws.Name);

            var assignedTeams = assignedRows
                .GroupBy(p => p.LineNumber!)
                .Select(g => new
                {
                    teamId = g.Key,
                    lineNumber = g.Key,
                    lineNumberName = lineNames.TryGetValue(g.Key, out var n) ? n : g.Key,
                    assignedQty = g.Sum(x => x.TotalPlannedQty),
                    actualQty = g.Sum(x => actualByPlanId.TryGetValue(x.Id, out var a) ? a : 0)
                })
                .OrderBy(x => x.lineNumber)
                .ToList();

            var assignedQty = assignedTeams.Sum(x => x.assignedQty);

            // ===== BUG ĐÃ SỬA #2: totalQty tính thiếu khi PO đã phân tổ 1 phần =====
            // Kiểm tra Style có phân công đoạn không (StyleCmPrice.IsOperation), áp dụng đúng công thức
            // net-trừ đã chuẩn hoá ở PreviewAsync/ExecuteAsync thay vì chỉ lấy dòng line_number=NULL.
            var cmPriceEntity = await _context.StyleCmPrice
                .Where(e => e.IsActive == true && e.StyleId == styleId)
                .FirstOrDefaultAsync();
            bool isOperation = cmPriceEntity?.IsOperation == true;

            int totalQty;
            if (isOperation)
            {
                // Cùng 1 po_id: các dòng operation khác nhau theo dõi CÙNG 1 lô -> lấy MAX, không SUM.
                var assignedDeduped = assignedRows.Count > 0 ? assignedRows.Max(x => x.TotalPlannedQty) : 0;
                var unassignedNet = unassignedRows.Sum(u => Math.Max(0, u.TotalPlannedQty - assignedDeduped));
                totalQty = assignedDeduped + unassignedNet;
            }
            else
            {
                // TODO: chưa xác nhận dòng line_number=NULL của Style KHÔNG phân công đoạn có tự trừ phần
                // đã phân tổ hay không (xem trao đổi trước). Tạm thời giữ công thức đơn giản (cộng thẳng),
                // đúng với dữ liệu đã kiểm tra cho trường hợp không phân công đoạn. Nếu sau này phát hiện
                // dòng NULL cũng không tự trừ (giống case phân công đoạn), đổi sang công thức net tương tự.
                totalQty = unassignedRows.Sum(x => x.TotalPlannedQty) + assignedQty;
            }


            // ===== BUG ĐÃ SỬA #3: thiếu lọc factoryId =====
            var sizeBreakdown = await _context.MesProductionOrderItems
                .Where(e => e.FactoryId == factoryId && e.StyleId == styleId && e.PoId == poId && e.ColorId == colorId)
                .GroupBy(x => x.SizeId)
                .Select(g => new
                {
                    sizeId = g.Key,
                    totalQty = g.Sum(x => x.PlannedQty),
                })
                .OrderBy(x => x.sizeId)
                .ToListAsync();

            var poName = await _context.SalesOrderHeader
                .AsNoTracking()
                .Where(x => x.No == poId)
                .Select(x => x.POName)
                .FirstOrDefaultAsync();
            if (string.IsNullOrWhiteSpace(poName))
            {
                poName = await _context.MesProductionOrderItems
                    .AsNoTracking()
                    .Where(e => e.FactoryId == factoryId
                        && e.StyleId == styleId
                        && e.PoId == poId
                        && e.ColorId == colorId
                        && !string.IsNullOrWhiteSpace(e.POName))
                    .Select(e => e.POName)
                    .FirstOrDefaultAsync();
            }
            return new
            {
                factoryId,
                styleId,
                poId,
                poName,
                colorId,
                allocated = assignedTeams.Count > 0,
                totalQty,
                assignedQty,
                remainingQty = Math.Max(0, totalQty - assignedQty),
                assignedTeams,
                sizes = sizeBreakdown,
            };
        }


        // Gắn thêm cờ isCurrent vào object ẩn danh trả về từ BuildPoDetailAsync, để FE biết PO con nào đang được xem.
        private static object WithIsCurrent(object detail, bool isCurrent)
        {
            IDictionary<string, object?> expando = new System.Dynamic.ExpandoObject();
            foreach (var prop in detail.GetType().GetProperties())
            {
                expando[prop.Name] = prop.GetValue(detail);
            }
            expando["isCurrent"] = isCurrent;
            return expando;
        }

        // =====================================================================
        // Controller — trả cả chi tiết PO hiện tại (con) VÀ chi tiết PO gốc (đã tách ra)
        // =====================================================================
        [HttpGet("split-po/po-source")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<object>> GetPoSource(
            [FromQuery] string factoryId,
            [FromQuery] string styleId,
            [FromQuery] string poId,
            [FromQuery] string colorId)
        {
            if (string.IsNullOrWhiteSpace(factoryId)
                || string.IsNullOrWhiteSpace(styleId)
                || string.IsNullOrWhiteSpace(poId)
                || string.IsNullOrWhiteSpace(colorId))
            {
                return BadRequest(new { success = false, message = "Thiếu factoryId/styleId/poId/colorId." });
            }

            var currentDetail = await BuildPoDetailAsync(factoryId, styleId, poId, colorId);
            if (currentDetail == null)
                return NotFound(new { success = false, message = $"Không tìm thấy PO '{poId}'." });

            // ===== Xác định PO gốc qua ParentPlanId, rồi build chi tiết đầy đủ cho PO gốc (size, tổ...) =====
            // Không lọc IsActive ở đây — cùng lý do như BuildPoDetailAsync ở trên.
            var plans = await _context.MesFactoryPlans
                .AsNoTracking()
                .Where(p => p.FactoryId == factoryId && p.StyleId == styleId && p.PoId == poId && p.ColorId == colorId)
                .ToListAsync();

            var parentPlanIds = plans
                .Where(p => p.ParentPlanId.HasValue)
                .Select(p => p.ParentPlanId!.Value)
                .Distinct()
                .ToList();

            object? splitFrom = null;
            string? splitBy = null;
            DateTime? splitAt = null;
            var children = new List<object>();

            if (parentPlanIds.Count > 0)
            {
                var originalPlans = await _context.MesFactoryPlans
                    .AsNoTracking()
                    .Where(p => parentPlanIds.Contains(p.Id))
                    .ToListAsync();

                var originalPoIds = originalPlans.Select(p => p.PoId).Distinct().ToList();

                if (originalPoIds.Count == 1)
                {
                    splitFrom = await BuildPoDetailAsync(factoryId, styleId, originalPoIds[0], colorId);
                }
                else if (originalPoIds.Count > 1)
                {
                    // Trường hợp bất thường: các dòng của PO con này lại trỏ về nhiều PO gốc khác nhau.
                    var list = new List<object>();
                    foreach (var opId in originalPoIds)
                    {
                        var d = await BuildPoDetailAsync(factoryId, styleId, opId, colorId);
                        if (d != null) list.Add(d);
                    }
                    splitFrom = list;
                }

                // TODO: hiện chưa có cột riêng SplitAt/SplitBy trên factory_plans (khác với MergedAt/MergedBy
                // đã có cho Gom PO). Tạm dùng CreatedBy/CreatedAt của chính PO con làm thông tin "tách bởi/lúc".
                // Nếu cần chính xác tuyệt đối (phân biệt được "tạo" khác "tách"), nên bổ sung 2 cột SplitAt/
                // SplitBy tương tự MergedAt/MergedBy.
                var firstPlan = plans.OrderBy(p => p.CreatedAt).FirstOrDefault();
                splitBy = firstPlan?.CreatedBy;
                splitAt = firstPlan?.CreatedAt;

                // ===== Lấy TOÀN BỘ PO con cùng tách ra từ PO gốc này (anh em với poId hiện tại), không chỉ 1 PO =====
                // Cùng ParentPlanId (một hoặc nhiều, do trường hợp bất thường ở trên) + cùng màu đang xem.
                var siblingChildPlans = await _context.MesFactoryPlans
                    .AsNoTracking()
                    .Where(p => p.FactoryId == factoryId
                        && p.StyleId == styleId
                        && p.ColorId == colorId
                        && p.ParentPlanId.HasValue
                        && parentPlanIds.Contains(p.ParentPlanId!.Value))
                    .ToListAsync();

                var siblingPoIds = siblingChildPlans
                    .Select(p => p.PoId)
                    .Distinct()
                    .OrderBy(x => x)
                    .ToList();

                foreach (var childPoId in siblingPoIds)
                {
                    var detail = await BuildPoDetailAsync(factoryId, styleId, childPoId, colorId);
                    if (detail == null) continue;
                    children.Add(WithIsCurrent(detail, childPoId == poId));
                }
            }

            var payload = new
            {
                success = true,
                current = currentDetail,
                isSplit = splitFrom != null,
                splitFrom,      // chi tiết ĐẦY ĐỦ của PO gốc (size, assignedTeams, remainingQty...), null nếu không phải PO tách
                splitBy,
                splitAt,
                children,       // TOÀN BỘ PO con đã tách ra từ cùng PO gốc (bao gồm cả PO đang xem), mỗi phần tử có isCurrent
            };

            return Ok(new
            {
                success = true,
                data = payload,
            });
        }

        private class WorkUnit
        {
            public QualityManagementSession SourceSession { get; set; } = null!;
            public int Qty { get; set; }
            public bool IsRemainderPlaceholder { get; set; }  // true = đây là phần dư sinh ra sau 1 lần tách, chưa có bản ghi DB thật
        }

        private class SessionAllocationPlan
        {
            public List<(QualityManagementSession Session, string TargetPoId)> UpdateInPlace { get; set; } = new();
            public List<(QualityManagementSession SourceSession, int Qty, string TargetPoId)> CreateNew { get; set; } = new();
            public List<(QualityManagementSession SourceSession, int NewQty)> ShrinkInPlace { get; set; } = new();
        }

        private SessionAllocationPlan BuildAllocationPlan(
            List<QualityManagementSession> sessions,
            List<(string PoId, int TargetQty)> targets)
        {
            var plan = new SessionAllocationPlan();
            var queue = new Queue<WorkUnit>(
                sessions.Select(s => new WorkUnit { SourceSession = s, Qty = s.PassOutputQuantity, IsRemainderPlaceholder = false }));

            foreach (var (poId, targetQty) in targets)
            {
                var needed = targetQty;

                while (needed > 0)
                {
                    if (!queue.TryDequeue(out var unit))
                        throw new InvalidOperationException("Tổng SL nhập vượt quá tổng Thực hiện thật của Tổ.");

                    if (unit.Qty <= needed)
                    {
                        if (!unit.IsRemainderPlaceholder)
                            plan.UpdateInPlace.Add((unit.SourceSession, poId));        // session gốc nguyên vẹn -> update tại chỗ
                        else
                            plan.CreateNew.Add((unit.SourceSession, unit.Qty, poId));  // phần dư từ lần tách trước -> tạo bản ghi mới

                        needed -= unit.Qty;
                    }
                    else
                    {
                        var keepQty = needed;
                        var remainderQty = unit.Qty - keepQty;

                        if (!unit.IsRemainderPlaceholder)
                        {
                            // Lần đầu tách session gốc: session gốc giữ lại "keepQty", gán cho PO hiện tại
                            plan.ShrinkInPlace.Add((unit.SourceSession, keepQty));
                            plan.UpdateInPlace.Add((unit.SourceSession, poId));
                        }
                        else
                        {
                            // Placeholder bị tách tiếp (trường hợp >2 PO con, dư lại vẫn còn lớn) -> tạo bản ghi mới cho phần lấy được
                            plan.CreateNew.Add((unit.SourceSession, keepQty, poId));
                        }

                        queue.Enqueue(new WorkUnit { SourceSession = unit.SourceSession, Qty = remainderQty, IsRemainderPlaceholder = true });
                        needed = 0;
                    }
                }
            }

            var totalRemaining = queue.Sum(u => u.Qty);
            if (totalRemaining > 0)
                throw new InvalidOperationException($"Còn {totalRemaining}sp Thực hiện chưa được phân bổ hết.");

            return plan;
        }

        private void ApplyAllocationPlan(
    DbContext context,
    SessionAllocationPlan plan,
    Dictionary<string, Guid> childPlanIdByPoId)   // PoId -> MesFactoryPlan.Id (đã lọc đúng theo Tổ)
        {
            // 1. Session gốc nguyên vẹn, chỉ đổi PO/Plan, KHÔNG đổi SL
            foreach (var (session, targetPoId) in plan.UpdateInPlace)
            {
                session.SalesOrderNo = targetPoId;
                session.PlanId = childPlanIdByPoId[targetPoId];
            }

            // 2. Session gốc bị tách lần đầu -> giảm PassOutputQuantity xuống đúng phần giữ lại
            foreach (var (session, newQty) in plan.ShrinkInPlace)
            {
                session.PassOutputQuantity = newQty;
            }

            // 3. Tạo bản ghi mới cho phần dư (đã gán đủ PO ở bước cuối)
            foreach (var (sourceSession, qty, targetPoId) in plan.CreateNew)
            {
                var newSession = new QualityManagementSession
                {
                    No = Guid.NewGuid(),
                    TimeSlotId = sourceSession.TimeSlotId,
                    OperationType = sourceSession.OperationType,
                    WorkCenterCode = sourceSession.WorkCenterCode,
                    WorkShiftCode = sourceSession.WorkShiftCode,
                    CustomerNo_ = sourceSession.CustomerNo_,
                    SalesOrderNo = targetPoId,                          // *** đổi ***
                    ItemNo = sourceSession.SalesOrderNo,
                    ColorNo = sourceSession.ColorNo,
                    Status = sourceSession.Status,
                    InspectionDate = sourceSession.InspectionDate,
                    PlanOutputQuantity = sourceSession.PlanOutputQuantity,
                    PassOutputQuantity = qty,                             // *** đổi ***
                    CheckedQuantity = sourceSession.CheckedQuantity,
                    ItemName = sourceSession.ItemName,
                    Inspector = sourceSession.Inspector,
                    Size = sourceSession.Size,
                    PoName = sourceSession.PoName,
                    DefectiveProductCount = sourceSession.DefectiveProductCount,
                    TargetQtyPerDate = sourceSession.TargetQtyPerDate,
                    //applied_cm_price = sourceSession.applied_cm_price,
                    //applied_exchange_rate = sourceSession.applied_exchange_rate,
                    //revenue_amount = sourceSession.revenue_amount,
                    PlanId = childPlanIdByPoId[targetPoId],              // *** đổi ***
                };
                context.Add(newSession);
            }
        }
        [HttpPost("split-po/reconcile-actual")]
        public async Task<ActionResult<object>> ReconcileSplitActual([FromBody] ReconcileActualRequestDto request)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                foreach (var teamAlloc in request.TeamAllocations)
                {
                    var sessions = await _context.qualityManagementSessions
                        .Where(x => x.SalesOrderNo == request.ParentPoId && x.Status==1
                            && x.WorkShiftCode == teamAlloc.TeamId)
                        .OrderBy(x => x.InspectionDate)
                        .ToListAsync();

                    var totalActual = sessions.Sum(s => s.PassOutputQuantity);
                    var totalRequested = teamAlloc.Allocations.Sum(a => a.Qty);
                    if (totalActual != totalRequested)
                    {
                        return BadRequest(new
                        {
                            success = false,
                            message = $"Tổ {teamAlloc.TeamId}: tổng SL nhập ({totalRequested}) phải khớp tổng Thực hiện thật ({totalActual}).",
                        });
                    }

                    var childPlans = await _context.MesFactoryPlans
                        .Where(p => teamAlloc.Allocations.Select(a => a.SplitPoId).Contains(p.PoId)
                            && p.LineNumber == teamAlloc.TeamId && p.IsActive == true)
                        .ToListAsync();
                    var childPlanIdByPoId = childPlans.ToDictionary(p => p.PoId, p => p.Id);

                    var targets = teamAlloc.Allocations.Select(a => (a.SplitPoId, a.Qty)).ToList();
                    var plan = BuildAllocationPlan(sessions, targets);

                    ApplyAllocationPlan(_context, plan, childPlanIdByPoId);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return Ok(new { success = true });
            }
            catch (InvalidOperationException ex)
            {
                await transaction.RollbackAsync();
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPatch("production-plans/{planId}/rename-po")]
        [ProducesResponseType(typeof(object), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> RenamePo(Guid planId, [FromBody] RenamePoRequest request)
        {
            try
            {
                var newName = request?.NewPoName?.Trim();
                if (string.IsNullOrWhiteSpace(newName))
                {
                    return BadRequest(new { success = false, message = "Tên PO mới không được để trống." });
                }
                if (newName.Length > 100) // điều chỉnh theo giới hạn cột thực tế của POName
                {
                    return BadRequest(new { success = false, message = "Tên PO mới quá dài (tối đa 100 ký tự)." });
                }

                // 1. Tìm item gốc theo VersionId để lấy PoId
                var anchorItem = await _context.MesProductionOrderItems
                    .FirstOrDefaultAsync(i => i.VersionId == planId);

                if (anchorItem == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy đơn hàng." });
                }

                var poId = anchorItem.PoId;
                var oldName = anchorItem.POName;

                // 2. Lấy TẤT CẢ các item có cùng PoId (đây là điểm khác biệt chính)
                var relatedItems = await _context.MesProductionOrderItems
                    .Where(i => i.PoId == poId)
                    .ToListAsync();

                if (relatedItems.Count == 0)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy đơn hàng." });
                }

                if (string.Equals(oldName, newName, StringComparison.Ordinal))
                {
                    // Không có gì thay đổi -> trả thành công, không tạo lịch sử rác
                    return Ok(new
                    {
                        success = true,
                        message = "Tên PO không thay đổi.",
                        data = new { anchorItem.Id, anchorItem.PoId, POName = anchorItem.POName },
                    });
                }

                var now = DateTime.UtcNow;

                var changedBy = string.IsNullOrWhiteSpace(request?.UserId) ? "unknown" : request!.UserId!;

                // 3. Update toàn bộ + ghi lịch sử cho từng item bị ảnh hưởng
                foreach (var item in relatedItems)
                {
                    var itemOldName = item.POName;

                    if (string.Equals(itemOldName, newName, StringComparison.Ordinal))
                    {
                        continue; // item này đã đúng tên mới rồi (hiếm khi xảy ra), bỏ qua
                    }

                    item.POName = newName;
                    // item.UpdatedAt = now; // nếu có cột này

                    _context.Set<MesPoRenameHistory>().Add(new MesPoRenameHistory
                    {
                        Id = Guid.NewGuid(),
                        ItemId = item.Id,
                        OldPoName = itemOldName,
                        NewPoName = newName,
                        ChangedBy = changedBy,
                        ChangedAt = now,
                    });
                }

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = $"Đã đổi tên PO từ \"{oldName}\" thành \"{newName}\" cho {relatedItems.Count} dòng liên quan.",
                    data = new
                    {
                        PoId = poId,
                        POName = newName,
                        AffectedCount = relatedItems.Count,
                        AffectedIds = relatedItems.Select(i => i.Id),
                    },
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = "Đổi tên PO thất bại." });
            }
        }
        [HttpGet("style-options")]
        public async Task<ActionResult<IEnumerable<string>>> GetStyleOptions(
    [FromQuery] string? factoryId,
    [FromQuery] string? search)
        {
            try
            {
                var query = _context.MesProductionOrderItems
                    .AsNoTracking()
                    .Where(x => x.IsActive != false);

                if (!string.IsNullOrWhiteSpace(factoryId) && factoryId != "all")
                {
                    query = query.Where(x => x.FactoryId == factoryId);
                }

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var keyword = search.Trim();
                    query = query.Where(x => x.StyleId != null &&
                        x.StyleId.ToLower().Contains(keyword.ToLower()));
                }

                var styleList = await (
                    from x in query
                    where !string.IsNullOrWhiteSpace(x.StyleId)
                    join item in _context.Item.AsNoTracking()
                        on x.StyleId equals item.No   // đổi khoá cho đúng
                    select new StyleOptionDto
                    {
                        StyleId = x.StyleId,
                        StyleName = item.Name            // đổi tên cột cho đúng
                    })
                    .Distinct()
                    .OrderBy(x => x.StyleId)
                    .ToListAsync();

                return Ok(styleList);
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Lấy danh sách Style thất bại.",
                    detail = ex.Message
                });
            }
        }

        [HttpGet("style-po-options")]
        public async Task<ActionResult<IEnumerable<object>>> GetStylePoOptions(
            [FromQuery] string styleId,
            [FromQuery] string? factoryId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(styleId))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "styleId là bắt buộc."
                    });
                }

                var query = _context.MesProductionOrderItems
                    .AsNoTracking()
                    .Where(x => x.StyleId == styleId && x.IsActive != false);

                if (!string.IsNullOrWhiteSpace(factoryId) && factoryId != "all")
                {
                    query = query.Where(x => x.FactoryId == factoryId);
                }

                var result = await (
                    from x in query
                    join so in _context.SalesOrderHeader.AsNoTracking()
                        on x.PoId equals so.No into soJoin
                    from so in soJoin.DefaultIfEmpty()
                    select new
                    {
                        x.PoId,
                        PoName = so != null ? so.POName : x.PoId,
                        x.ColorId,
                        x.PlannedQty
                    })
                    .ToListAsync(); // fetch về trước, xử lý group ở phía client (C# in-memory)

                var grouped = result
                    .GroupBy(x => new { x.PoId, x.PoName })
                    .Select(g => new
                    {
                        poId = g.Key.PoId,
                        poName = string.IsNullOrWhiteSpace(g.Key.PoName) ? g.Key.PoId : g.Key.PoName,
                        totalQty = g.Sum(x => x.PlannedQty),
                        colors = g
                            .GroupBy(c => c.ColorId)              // <-- gộp theo màu
                            .Select(cg => new
                            {
                                colorId = cg.Key,
                                qty = cg.Sum(c => c.PlannedQty)    // <-- cộng dồn qty theo màu
                            })
                            .OrderBy(c => c.colorId)
                            .ToList()
                    })
                    .OrderBy(x => x.poId)
                    .ToList();

                return Ok(grouped);

            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Lấy danh sách PO theo Style thất bại.",
                    detail = ex.Message
                });
            }
        }

        // ===== GET: dùng khi MỞ modal — lấy số liệu header (Actual gốc / Đã phân bổ cả nhóm / Còn lại) =====
        [HttpGet("split-po/split-actual-summary")]
        public async Task<ActionResult<object>> GetSplitActualSummary([FromQuery] Guid childPlanId)
        {
            var childPlan = await _context.MesFactoryPlans.FirstOrDefaultAsync(p => p.Id == childPlanId);
            if (childPlan?.ParentPlanId == null)
                return BadRequest(new { success = false, message = "PO con không hợp lệ." });

            var parentPlanId = childPlan.ParentPlanId.Value;

            var rootPlan = await _context.MesFactoryPlans.FirstOrDefaultAsync(p => p.Id == parentPlanId);
            if (rootPlan == null)
                return BadRequest(new { success = false, message = "Không tìm thấy plan gốc tương ứng." });

            var (originalTotal, allocatedTotal, siblingPlanIds) = await GetActualRedistributionInfo(parentPlanId);

            var poName = await _context.SalesOrderHeader
                .Where(x => x.No == childPlan.PoId)
                .Select(x => x.POName)
                .FirstOrDefaultAsync();
            if (string.IsNullOrWhiteSpace(poName))
            {
                poName = await _context.MesProductionOrderItems
                    .AsNoTracking()
                    .Where(x => x.PoId == childPlan.PoId)
                    .Select(x => x.POName)
                    .FirstOrDefaultAsync();
            }

            var lineNumberName = await _context.workShift
                .Where(x => x.Code == childPlan.LineNumber)
                .Select(x => x.Name)
                .FirstOrDefaultAsync();

            // ---- Dữ liệu gốc theo từng ngày (của Tổ, trước khi tách) ----
            var originalSessions = await _context.qualityManagementSessions
                .Where(q => q.PlanId == parentPlanId && q.InspectionDate != null)
                .ToListAsync();

            // ---- Dữ liệu đã phân bổ theo từng ngày, của TẤT CẢ PO con cùng nhóm (kể cả chính PO con đang mở) ----
            var siblingSessions = siblingPlanIds.Count == 0
                ? new List<QualityManagementSession>()
                : await _context.qualityManagementSessions
                    .Where(q => q.PlanId != null && siblingPlanIds.Contains(q.PlanId.Value)
                             && q.Status == 1 && q.InspectionDate != null)
                    .ToListAsync();

            var dates = originalSessions.Select(s => s.InspectionDate!.Date).Distinct().OrderBy(d => d).ToList();

            var dateBreakdown = dates.Select(date =>
            {
                var originalQty = originalSessions
                    .Where(s => s.InspectionDate!.Date == date)
                    .Sum(s => s.PassOutputQuantity);

                var allocatedTotalForDate = siblingSessions
                    .Where(s => s.InspectionDate!.Date == date)
                    .Sum(s => s.PassOutputQuantity);

                // Phần CHÍNH PO con đang mở đã nhập trước đó cho ngày này — tách riêng để không tự chặn
                // chính nó khi tính "còn lại có thể nhập".
                var currentChildQty = siblingSessions
                    .Where(s => s.PlanId == childPlanId && s.InspectionDate!.Date == date)
                    .Sum(s => s.PassOutputQuantity);

                var allocatedByOthers = allocatedTotalForDate - currentChildQty;
                //var maxEditable = Math.Max(0, originalQty - allocatedByOthers);

                return new
                {
                    date,
                    originalQty,          // Tổ gốc làm được bao nhiêu ngày này
                    allocatedByOthers,    // PO con KHÁC đã lấy bao nhiêu của ngày này
                    //currentChildQty,      // PO con đang mở đã nhập bao nhiêu ngày này (prefill ô input)
                    //maxEditable           // PO con đang mở còn được nhập tối đa bao nhiêu ngày này
                };
            }).ToList();

            return Ok(new
            {
                success = true,
                data = new
                {
                    poId = childPlan.PoId,
                    poName,
                    lineNumber = rootPlan.LineNumber,
                    lineNumberName,
                    originalTotal,
                    allocatedTotal,
                    remaining = Math.Max(0, originalTotal - allocatedTotal),
                    dateBreakdown
                }
            });
        }
        //[HttpGet("split-po/split-actual-summary")]
        //public async Task<ActionResult<object>> GetSplitActualSummary([FromQuery] Guid childPlanId)
        //{
        //    var childPlan = await _context.MesFactoryPlans.FirstOrDefaultAsync(p => p.Id == childPlanId);
        //    if (childPlan?.ParentPlanId == null)
        //        return BadRequest(new { success = false, message = "PO con không hợp lệ." });
        //    var parentPlanId = childPlan.ParentPlanId.Value;
        //    var parentPlan = await _context.MesFactoryPlans
        //    .AsNoTracking()
        //    .FirstOrDefaultAsync(p => p.Id == parentPlanId);

        //    if (parentPlan == null)
        //    {
        //        return BadRequest(new
        //        {
        //            success = false,
        //            message = "Không tìm thấy PO gốc."
        //        });
        //    }
        //    var poName = await _context.SalesOrderHeader
        //        .AsNoTracking()
        //        .Where(x => x.No == childPlan.PoId)
        //        .Select(x => x.POName)
        //        .FirstOrDefaultAsync();

        //    if (string.IsNullOrWhiteSpace(poName))
        //    {
        //        poName = await _context.MesProductionOrderItems
        //            .AsNoTracking()
        //            .Where(x => x.PoId == childPlan.PoId)
        //            .Select(x => x.POName)
        //            .FirstOrDefaultAsync();
        //    }

        //    // 4. Lấy tên chuyền từ WorkShift
        //    // Code = LineNumber
        //    var lineNumberName = await _context.workShift
        //        .AsNoTracking()
        //        .Where(x => x.Code == childPlan.LineNumber)
        //        .Select(x => x.Name)
        //        .FirstOrDefaultAsync();

        //    var (originalTotal, allocatedTotal, _) = await GetActualRedistributionInfo(childPlan.ParentPlanId.Value);

        //    // 6. Lấy danh sách ngày kiểm từ QualityManagementSession
        //    var inspectionDates = await _context.qualityManagementSessions
        //        .AsNoTracking()
        //        .Where(q => q.PlanId == parentPlanId)
        //        .Select(q => q.InspectionDate.Date)
        //        .Distinct()
        //        .OrderBy(x => x)
        //        .ToListAsync();

        //    var inspectionDateStrings = inspectionDates
        //        .Select(x => x.ToString("yyyy-MM-dd"))
        //        .ToList();
        //    return Ok(new
        //    {
        //        success = true,
        //        data = new
        //        {
        //            poId = childPlan.PoId,
        //            poName = poName,

        //            lineNumber = childPlan.LineNumber,
        //            lineNumberName = lineNumberName,

        //            inspectionDates = inspectionDateStrings,

        //            originalTotal = originalTotal,
        //            allocatedTotal = allocatedTotal,
        //            remaining = Math.Max(0, originalTotal - allocatedTotal)
        //        }
        //    });
        //}

        // ===== Hàm dùng chung =====
        private async Task<(int OriginalTotal, int AllocatedTotal, List<Guid> SiblingPlanIds)> GetActualRedistributionInfo(Guid parentPlanId)
        {
            // Tổng gốc = SUM toàn bộ session gắn với parentPlanId, KHÔNG lọc theo Status — vì Status chỉ
            // đánh dấu "đã bắt đầu phân bổ hay chưa" (phục vụ báo cáo khác tránh đếm trùng), không thay
            // đổi bản chất tổng Actual gốc cần chia của Tổ này (số này cố định theo thời gian).
            var originalTotal = await _context.qualityManagementSessions
                .Where(q => q.PlanId == parentPlanId)
                .SumAsync(q => (int?)q.PassOutputQuantity) ?? 0;

            var siblingPlanIds = await _context.MesFactoryPlans
                .Where(p => p.ParentPlanId == parentPlanId)
                .Select(p => p.Id)
                .ToListAsync();

            var allocatedTotal = siblingPlanIds.Count == 0
                ? 0
                : await _context.qualityManagementSessions
                    .Where(q => q.PlanId != null && siblingPlanIds.Contains(q.PlanId.Value) && q.Status == 0)
                    .SumAsync(q => (int?)q.PassOutputQuantity) ?? 0;

            return (originalTotal, allocatedTotal, siblingPlanIds);
        }



        [HttpPost("split-actual-entries/batch")]
        public async Task<ActionResult<object>> SaveSplitActualEntriesBatch([FromBody] SplitActualBatchRequestDto request)
        {
            var childPlan = await _context.MesFactoryPlans.FirstOrDefaultAsync(p => p.Id == request.ChildPlanId);
            if (childPlan == null || string.IsNullOrWhiteSpace(childPlan.LineNumber))
                return BadRequest(new { success = false, message = "PO con không hợp lệ hoặc chưa gán Tổ." });
            if (childPlan.ParentPlanId == null)
                return BadRequest(new { success = false, message = "PO con này không thuộc về 1 lần tách nào." });

            if (request.Entries.Any(e => e.Quantity < 0))
                return BadRequest(new { success = false, message = "Số lượng không được âm." });

            var duplicateDates = request.Entries.GroupBy(e => e.WorkDate.Date).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (duplicateDates.Count > 0)
                return BadRequest(new { success = false, message = $"Trùng ngày trong danh sách: {string.Join(", ", duplicateDates.Select(d => d.ToString("dd/MM/yyyy")))}" });

            var parentPlanId = childPlan.ParentPlanId.Value;
            var (originalTotal, allocatedTotal, siblingPlanIds) = await GetActualRedistributionInfo(parentPlanId);

            var originalSessionsByDate = await _context.qualityManagementSessions
       .Where(q => q.PlanId == parentPlanId && q.InspectionDate != null && q.Status==1)
       .GroupBy(q => q.InspectionDate!.Date)
       .Select(g => new { Date = g.Key, Qty = g.Sum(x => x.PassOutputQuantity), CheckQty = g.Sum(x => x.CheckedQuantity) })
       .ToDictionaryAsync(x => x.Date, x => new { x.Qty, x.CheckQty });

            var siblingSessionsByDate = new List<(Guid PlanId, DateTime Date, int Qty)>();
            if (siblingPlanIds.Count > 0)
            {
                var rawSiblingSessions = await _context.qualityManagementSessions
                    .Where(q => q.PlanId != null && siblingPlanIds.Contains(q.PlanId.Value)
                             && q.Status == 1 && q.InspectionDate != null)
                    .Select(q => new { PlanId = q.PlanId!.Value, Date = q.InspectionDate!.Date, q.PassOutputQuantity })
                    .ToListAsync();

                siblingSessionsByDate = rawSiblingSessions
                    .Select(x => (PlanId: x.PlanId, Date: x.Date, Qty: x.PassOutputQuantity))
                    .ToList();
            }

            var dateErrors = new List<string>();
            foreach (var entry in request.Entries.Where(e => e.Quantity > 0))
            {
                var date = entry.WorkDate.Date;
                var originalQtyForDate = originalSessionsByDate.TryGetValue(date, out var o) ? o.Qty : 0;

                // Chỉ tính PO con KHÁC (loại chính PO con đang sửa) — batch này thay thế toàn bộ dữ liệu
                // cũ của chính nó nên không được tự chặn chính mình.
                var allocatedByOthersForDate = siblingSessionsByDate
                    .Where(x => x.Date == date && x.PlanId != request.ChildPlanId)
                    .Sum(x => x.Qty);

                var maxForDate = Math.Max(0, originalQtyForDate - allocatedByOthersForDate);

                if (entry.Quantity > maxForDate)
                {
                    dateErrors.Add($"Ngày {date:dd/MM/yyyy}: gốc {originalQtyForDate} sp, PO khác đã dùng {allocatedByOthersForDate} sp, tối đa được nhập {maxForDate} sp (bạn nhập {entry.Quantity} sp).");
                }
            }

            if (dateErrors.Count > 0)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Vượt quá số lượng thực hiện còn lại ở một số ngày cụ thể.",
                    errors = dateErrors
                });
            }


            // Tổng đã phân bổ của CÁC PO con KHÁC (không tính chính PO con đang sửa, vì batch này sẽ
            // THAY THẾ TOÀN BỘ dữ liệu cũ của PO con hiện tại).
            var currentChildOldTotal = await _context.qualityManagementSessions
                .Where(q => q.PlanId == request.ChildPlanId && q.Status == 1)
                .SumAsync(q => (int?)q.PassOutputQuantity) ?? 0;

            var allocatedByOthers = allocatedTotal - currentChildOldTotal;
            var newChildTotal = request.Entries.Sum(e => e.Quantity);
            var newGrandTotal = allocatedByOthers + newChildTotal;

            // ---- CHẶN CỨNG ----
            if (newGrandTotal > originalTotal)
            {
                return BadRequest(new
                {
                    success = false,
                    message = $"Vượt quá Actual còn lại của Tổ. Số lượng thực hiện PO gốc: {originalTotal} sp, các PO con khác đã dùng: {allocatedByOthers} sp, tối đa PO con này được nhập: {originalTotal - allocatedByOthers} sp (bạn đang nhập {newChildTotal} sp)."
                });
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // ===== Đánh dấu PO gốc đã "bắt đầu được phân bổ" — chuyển toàn bộ session gốc Status 1 -> 0 =====
                // Idempotent: nếu đã 0 từ lần Lưu trước (do PO con khác cùng nhóm đã lưu rồi), điều kiện
                // Status==1 không khớp gì -> không có tác dụng phụ, an toàn khi gọi nhiều lần.
                var originalSessionsStillActive = await _context.qualityManagementSessions
                    .Where(q => q.PlanId == parentPlanId && q.Status == 1)
                    .ToListAsync();
                foreach (var s in originalSessionsStillActive)
                    s.Status = 0;

                var sourceSession = await _context.qualityManagementSessions
                .AsNoTracking()
                .Where(q => q.PlanId == parentPlanId)
                .OrderBy(q => q.InspectionDate)
                .FirstOrDefaultAsync();

                if (sourceSession == null)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Không tìm thấy dữ liệu số lượng thực hiện PO gốc để phân bổ."
                    });
                }

                var itemName = await _context.Item.AsNoTracking()
                .Where(x => x.No == childPlan.StyleId)
                .Select(x => x.Name)
                .FirstOrDefaultAsync();

                var poName = await _context.SalesOrderHeader
                    .AsNoTracking()
                    .Where(x => x.No == childPlan.PoId)
                    .Select(x => x.POName)
                    .FirstOrDefaultAsync();

                if (string.IsNullOrWhiteSpace(poName))
                {
                    poName = await _context.MesProductionOrderItems
                        .AsNoTracking()
                        .Where(x => x.PoId == childPlan.PoId)
                        .Select(x => x.POName)
                        .FirstOrDefaultAsync();
                }

                // Xoá toàn bộ entry cũ của PO con này, ghi lại từ đầu theo danh sách mới trong modal.
                var oldEntries = await _context.qualityManagementSessions
                    .Where(q => q.PlanId == request.ChildPlanId && q.Status == 1)
                    .ToListAsync();
                _context.qualityManagementSessions.RemoveRange(oldEntries);

                foreach (var entry in request.Entries.Where(e => e.Quantity > 0))
                {
                    var date = entry.WorkDate.Date;

                    // (2) Tính tỉ lệ Quantity / Qty gốc theo ngày để phân bổ CheckedQuantity tương ứng
                    var hasOriginal = originalSessionsByDate.TryGetValue(date, out var orig);
                    var originalQtyForDate = hasOriginal ? orig.Qty : 0;
                    var originalCheckQtyForDate = hasOriginal ? orig.CheckQty : 0;

                    var ratio = originalQtyForDate > 0 ? (double)entry.Quantity / originalQtyForDate : 0d;
                    var allocatedCheckQty = (int)Math.Round(originalCheckQtyForDate * ratio, MidpointRounding.AwayFromZero);

                    _context.qualityManagementSessions.Add(new QualityManagementSession
                    {
                        No = Guid.NewGuid(),

                        // Lấy thông tin chung từ session gốc đầu tiên
                        OperationType = sourceSession.OperationType,
                        CustomerNo_ = sourceSession.CustomerNo_,

                        // Thông tin PO con
                        WorkCenterCode = childPlan.FactoryId,
                        WorkShiftCode = childPlan.LineNumber,
                        SalesOrderNo = childPlan.PoId,
                        ItemNo = childPlan.StyleId,
                        ColorNo = childPlan.ColorId,

                        Status = 1,
                        InspectionDate = entry.WorkDate,
                        PassOutputQuantity = entry.Quantity,

                        // Lấy từ bảng Item
                        ItemName = itemName,

                        // Lấy từ SalesOrderHeader hoặc MesProductionOrderItems
                        PoName = poName,

                        // Các thông tin khác lấy từ session gốc
                        CheckedQuantity = allocatedCheckQty,
                        Inspector = sourceSession.Inspector,
                        //Size = sourceSession.Size,
                        Size="",
                        DefectiveProductCount = sourceSession.DefectiveProductCount,
                        TargetQtyPerDate = sourceSession.TargetQtyPerDate,
                        PlanId = request.ChildPlanId,
                    });
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new
                {
                    success = true,
                    data = new { remaining = Math.Max(0, originalTotal - newGrandTotal) }
                });
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        [HttpGet("actual-transfer/target-candidates")]
        public async Task<ActionResult<object>> GetTransferTargetCandidates([FromQuery] Guid planId)
        {
            var sourcePlan = await _context.MesFactoryPlans.FirstOrDefaultAsync(p => p.Id == planId);
            if (sourcePlan == null)
                return BadRequest(new { success = false, message = "Không tìm thấy dòng đơn hàng nguồn." });

            var candidates = await _context.MesFactoryPlans
                .Where(p => p.FactoryId == sourcePlan.FactoryId
                         && p.StyleId == sourcePlan.StyleId
                         && p.ColorId == sourcePlan.ColorId
                         && p.LineNumber == sourcePlan.LineNumber
                         && p.PoId != sourcePlan.PoId
                         && p.IsActive)
                .Select(p => p.PoId)
                .Distinct()
                .ToListAsync();

            var poNames = await _context.SalesOrderHeader
                .Where(x => candidates.Contains(x.No))
                .ToDictionaryAsync(x => x.No, x => x.POName);

            var result = candidates
                .OrderBy(poId => poId)
                .Select(poId => new
                {
                    poId,
                    poName = poNames.GetValueOrDefault(poId)
                });

            return Ok(new { success = true, data = result });
        }



        [HttpPost("actual-transfer")]
        public async Task<ActionResult<object>> SaveActualTransfer([FromBody] ActualTransferRequestDto request)
        {
            var sourcePlan = await _context.MesFactoryPlans.FirstOrDefaultAsync(p => p.Id == request.SourcePlanId);
            if (sourcePlan == null)
                return BadRequest(new { success = false, message = "PO nguồn không hợp lệ." });

            if (sourcePlan.PoId == request.TargetPoId)
                return BadRequest(new { success = false, message = "PO đích không được trùng PO nguồn." });

            // Tự tra targetPlanId từ targetPoId — lọc đúng theo Style + Màu + Tổ của Nguồn, không tin
            // tưởng bất kỳ planId nào FE có thể gửi thẳng lên (tránh trường hợp gửi nhầm/gửi mã PO khác).
            var targetPlan = await _context.MesFactoryPlans.FirstOrDefaultAsync(p =>
                p.FactoryId == sourcePlan.FactoryId
                && p.StyleId == sourcePlan.StyleId
                && p.ColorId == sourcePlan.ColorId
                && p.LineNumber == sourcePlan.LineNumber
                && p.PoId == request.TargetPoId
                && p.IsActive);

            if (targetPlan == null)
                return BadRequest(new { success = false, message = "Không tìm thấy PO đích khớp Style/Màu/Tổ với PO nguồn." });

            if (request.Entries.Any(e => e.MoveQty < 0))
                return BadRequest(new { success = false, message = "Số lượng chuyển không được âm." });

            var dupDates = request.Entries.GroupBy(e => e.WorkDate.Date).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (dupDates.Count > 0)
                return BadRequest(new { success = false, message = $"Trùng ngày: {string.Join(", ", dupDates.Select(d => d.ToString("dd/MM/yyyy")))}" });

            var sourceByDate = await _context.qualityManagementSessions
                .Where(q => q.PlanId == request.SourcePlanId && q.Status == 1 && q.InspectionDate != null)
                .GroupBy(q => q.InspectionDate!.Date)
                .Select(g => new { Date = g.Key, Qty = g.Sum(x => x.PassOutputQuantity),CheckQty = g.Sum(x=>x.CheckedQuantity) })
                .ToDictionaryAsync(x => x.Date, x => new { x.Qty, x.CheckQty });

            // ---- CHẶN CỨNG theo từng ngày: không chuyển vượt quá SL hiện có của Nguồn ngày đó ----
            var errors = new List<string>();
            foreach (var entry in request.Entries.Where(e => e.MoveQty > 0))
            {
                var date = entry.WorkDate.Date;
                var available = sourceByDate.TryGetValue(date, out var q) ? q.Qty : 0;
                if (entry.MoveQty > available)
                    errors.Add($"Ngày {date:dd/MM/yyyy}: PO nguồn chỉ còn {available} sp, không thể chuyển {entry.MoveQty} sp.");
            }
            if (errors.Count > 0)
                return BadRequest(new { success = false, message = "Vượt quá SL hiện có của PO nguồn ở một số ngày.", errors });

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                foreach (var entry in request.Entries.Where(e => e.MoveQty > 0))
                {
                    var date = entry.WorkDate.Date;
                    var (available, totalCheckQty) = (sourceByDate[date].Qty, sourceByDate[date].CheckQty);
                    //var available = sourceByDate[date];
                    var remainForSource = available - entry.MoveQty;
                    var ratio = available > 0 ? (double)entry.MoveQty / available : 0d;
                    var moveCheckQty = (int)Math.Round(totalCheckQty * ratio, MidpointRounding.AwayFromZero);
                    // Phần còn lại của Nguồn = tổng - phần đã chuyển (tránh lệch do làm tròn)
                    var remainCheckQty = totalCheckQty - moveCheckQty;
                    var sourceSessionsThisDate = await _context.qualityManagementSessions
                        .Where(q => q.PlanId == request.SourcePlanId && q.Status == 1
                                 && q.InspectionDate != null && q.InspectionDate.Date == date)
                        .ToListAsync();

                    var referenceSession = sourceSessionsThisDate.FirstOrDefault();
                    if (referenceSession == null) continue; // an toàn, không nên xảy ra vì đã validate available > 0

                    // Chốt (retire) toàn bộ session hiện hành của Nguồn ngày này
                    foreach (var s in sourceSessionsThisDate)
                        s.Status = 0;

                    // Nguồn giữ lại phần còn dư (nếu > 0 — cho phép = 0, tức cho đi hết 100%)
                    if (remainForSource > 0)
                        _context.qualityManagementSessions.Add(CloneSession(referenceSession, sourcePlan, remainForSource, date,remainCheckQty));

                    // Đích nhận thêm — CỘNG THÊM, không đụng dữ liệu Actual sẵn có khác của Đích
                    _context.qualityManagementSessions.Add(CloneSession(referenceSession, targetPlan, entry.MoveQty, date,moveCheckQty));
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { success = true });
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private QualityManagementSession CloneSession(QualityManagementSession reference, MesFactoryPlan plan, int qty, DateTime date, int checkedQuantity)
        {
            return new QualityManagementSession
            {
                No = Guid.NewGuid(),
                OperationType = reference.OperationType,
                CustomerNo_ = reference.CustomerNo_,
                WorkCenterCode = plan.FactoryId,
                WorkShiftCode = plan.LineNumber,
                SalesOrderNo = plan.PoId,
                ItemNo = plan.StyleId,
                ItemName = reference.ItemName,
                ColorNo = plan.ColorId,
                Status = 1,
                InspectionDate = date,
                PassOutputQuantity = qty,
                CheckedQuantity = checkedQuantity,
                Inspector = reference.Inspector,
                Size = reference.Size,
                DefectiveProductCount = reference.DefectiveProductCount,
                TargetQtyPerDate = reference.TargetQtyPerDate,
                PlanId = plan.Id,
            };
        }

        public class UpdateDeliveryDateRequestDto
        {
            public string FactoryId { get; set; } = default!;
            public string StyleId { get; set; } = default!;
            public string PoId { get; set; } = default!;
            public DateOnly NewDeliveryDate { get; set; }
            public string UpdatedBy { get; set; } = default!;
        }

        [HttpPut("production-orders/delivery-date")]
        public async Task<ActionResult<object>> UpdateDeliveryDate([FromBody] UpdateDeliveryDateRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.FactoryId) || string.IsNullOrWhiteSpace(request.StyleId) || string.IsNullOrWhiteSpace(request.PoId))
                return BadRequest(new { success = false, message = "Thiếu factoryId/styleId/poId." });

            // Chỉ cập nhật các dòng ACTIVE — không đụng vào dữ liệu lịch sử của PO đã Gom/Tách trước đó
            // (những dòng đó đã chuyển IsActive = false ở các chức năng trước, giữ nguyên làm audit trail).
            var items = await _context.MesProductionOrderItems
                .Where(x => x.FactoryId == request.FactoryId && x.StyleId == request.StyleId && x.PoId == request.PoId
                         && (x.IsActive == null || x.IsActive == true))
                .ToListAsync();

            if (items.Count == 0)
                return BadRequest(new { success = false, message = "Không tìm thấy PO này hoặc PO không còn active." });

            var now = DateTime.UtcNow;
            foreach (var item in items)
            {
                item.TargetDeliveryDate = request.NewDeliveryDate;
                item.UpdatedAt = now;
                item.UpdatedBy = request.UpdatedBy;
            }

            await _context.SaveChangesAsync();

            return Ok(new { success = true, data = new { updatedRows = items.Count } });
        }

        [HttpGet("info-role")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<UserRoleDTO> GetInforRoleUser(string userID)
        {
            try
            {
                var result = await (
            from e in _context.employee
            join wc in _context.Department on e.WorkCenterCode equals wc.Code into wcg
            from wc in wcg.DefaultIfEmpty()
            join ws in _context.locationXSDs on e.WorkShiftCode equals ws.Code into wcf
            from ws in wcf.DefaultIfEmpty()
            where e.No == userID || e.LoginId == userID
            select new
            {
                e.LoginId,
                Dto = new UserRoleDTO
                {
                    Name = e.Name,
                    WorkCenterCode = e.WorkCenterCode,
                    WorkCenterName = wc.Name,
                    WorkShiftCode = e.WorkShiftCode,
                    WorkShiftName = ws.Name
                }
            }
            ).FirstOrDefaultAsync();
                if (result == null) return null;

                var user = result.Dto;

                // 2. Lấy tất cả role của user (nhiều dòng)
                var roles = await _context.UserSetupMobiles
                    .Where(x => x.UserID == userID || x.UserID == result.LoginId)
                    .Select(x => x.Role)
                    .Distinct()
                    .ToListAsync();

                user.Roles = roles.Where(r => !string.IsNullOrWhiteSpace(r)).ToList();

                // 3. Có role chứa "Admin" (vd: System Admin) thì được chọn nhà máy
                user.IsAdmin = user.Roles.Any(r =>
                    r.Contains("Admin", StringComparison.OrdinalIgnoreCase));

                return user;
            }
            catch (Exception ex) { throw new Exception($"Lỗi: {ex.InnerException?.Message ?? ex.Message}"); }
        }


        [HttpGet("company-plan/unallocated-orders")]
        public async Task<ActionResult<object>> GetUnallocatedOrders([FromQuery] UnallocatedOrdersQueryDto request)
        {
            var excludedStatuses = new[] { -1, 0, 1 };

            // Header LEFT JOIN Line
            var query =
                from h in _context.SalesOrderHeader.AsNoTracking()
                where !excludedStatuses.Contains(h.Status)
                join l0 in _context.SalesOrderLine.AsNoTracking()
                    on new { DocNo = h.No, h.SellToCustomerNo }
                    equals new { DocNo = l0.DocumentNo, l0.SellToCustomerNo }
                    into lines
                from l in lines.DefaultIfEmpty()
                    // Tương đương: LEFT JOIN Factory ... WHERE F.[Document No_] IS NULL
                where !_context.SalesOrderLineFactories.Any(f =>
                    f.DocumentNo == l.DocumentNo && f.SolLineNo == l.LineNo)
                select new { h, l };

            // ---- Bộ lọc từ UI ----
            if (!string.IsNullOrWhiteSpace(request.Status) && request.Status != "all"
                && int.TryParse(request.Status, out var statusVal))
            {
                query = query.Where(x => x.h.Status == statusVal);
            }

            if (request.PlanDateFrom.HasValue)
            {
                var fromDate = request.PlanDateFrom.Value.ToDateTime(TimeOnly.MinValue);
                query = query.Where(x => x.h.OrderDate >= fromDate);
            }

            if (request.PlanDateTo.HasValue)
            {
                var toDate = request.PlanDateTo.Value.AddDays(1).ToDateTime(TimeOnly.MinValue);
                query = query.Where(x => x.h.OrderDate < toDate);
            }

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(x => x.h.OrderDate)
                .ThenBy(x => x.h.No)
                .ThenBy(x => x.l.LineNo)
                // .Skip((request.Page - 1) * request.PageSize)
                // .Take(request.PageSize)
                .Select(x => new
                {
                    documentNo = x.h.No,
                    poCustomerName = x.h.POName,
                    customerNo = x.h.SellToCustomerNo,
                    orderType =  x.h.Type == 1 ? "Gia Công"
              : x.h.Type == 2 ? "FOB"
              : "",
                    orderDate = x.h.OrderDate,
                    requestedDeliveryDate = x.h.RequestedDeliveryDate,
                    status = x.h.Status,
                    loginId = x.h.LoginId,

                    lineNo = (int?)x.l.LineNo,
                    documentType = (int?)x.l.DocumentType,
                    itemNo = x.l.No,
                    colorNo = x.l.ColorNo,
                    unitOfMeasure = x.l.UnitOfMeasure,
                    quantity = (decimal?)x.l.Quantity,

                    // LEFT JOIN [Item]
                    itemName = _context.Item
                        .Where(i => i.No == x.l.No)
                        .Select(i => i.Name)
                        .FirstOrDefault(),

                    // SUM(Quantity) từ Sales Order Line Detail, ISNULL(..., 0)
                    //totalQty = _context.SalesOrderLineDetails
                    //    .Where(d => d.DocumentNo == x.l.DocumentNo
                    //             && d.ItemLineNo == x.l.LineNo
                    //             && d.ItemNo == x.l.No)
                    //    .Sum(d => (decimal?)d.Quantity) ?? 0m
                })
                .ToListAsync();

            return Ok(new
            {
                success = true,
                data = new { items, totalCount, page = request.Page, pageSize = request.PageSize }
            });
        }

        private static readonly int[] ExcludedUnallocatedStatuses = { -1, 0, 1 };

        private static readonly Dictionary<string, string> AllocationSkipMessages = new()
        {
            ["NOT_FOUND"] = "Không tìm thấy dòng đơn hàng trong ERP.",
            ["STATUS_NOT_APPROVED"] = "Đơn hàng chưa được duyệt trong ERP.",
            ["ALREADY_ALLOCATED"] = "Dòng đơn hàng đã được phân cho nhà máy.",
            ["DUPLICATE_IN_FACTORY"] = "Nhà máy đã có kế hoạch cùng Style, PO, Màu.",
            ["DUPLICATE_IN_REQUEST"] = "Trùng Style, PO, Màu với dòng khác trong cùng lần thêm.",
            ["NO_QUANTITY"] = "Chưa nhập số lượng.",
            ["INVALID_QUANTITY"] = "Số lượng không hợp lệ.",
            ["EXCEEDS_REMAINING"] = "Vượt số lượng còn lại theo size.",
        };

        private static string LineKey(string? documentNo, int lineNo)
            => $"{(documentNo ?? string.Empty).Trim().ToUpperInvariant()}|{lineNo}";

        private static string MatchKey(params string?[] parts)
            => string.Join("|", parts.Select(p => (p ?? string.Empty).Trim().ToUpperInvariant()));

        // Tính thông tin từng dòng ERP: trạng thái, trùng nhà máy, SL ERP/đã phân/còn lại theo size.
        // Kết quả trả về đúng thứ tự và số lượng với refs đầu vào.
        private async Task<List<UnallocatedOrderLineSizesDto>> BuildUnallocatedLineSizesAsync(
            string factoryId, IEnumerable<UnallocatedOrderLineRefDto> refs)
        {
            var refList = refs.ToList();
            var docNos = refList.Select(r => r.DocumentNo).Distinct().ToList();

            var headerByNo = (await _context.SalesOrderHeader.AsNoTracking()
                    .Where(h => docNos.Contains(h.No))
                    .Select(h => new { h.No, h.POName, h.Status, h.RequestedDeliveryDate })
                    .ToListAsync())
                .GroupBy(h => h.No, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var lineByKey = (await _context.SalesOrderLine.AsNoTracking()
                    .Where(l => docNos.Contains(l.DocumentNo))
                    .Select(l => new { l.DocumentNo, l.LineNo, ItemNo = l.No, l.ColorNo })
                    .ToListAsync())
                .GroupBy(l => LineKey(l.DocumentNo, l.LineNo))
                .ToDictionary(g => g.Key, g => g.First());

            var allocatedLineKeys = (await _context.SalesOrderLineFactories.AsNoTracking()
                    .Where(f => docNos.Contains(f.DocumentNo) && f.SolLineNo != null)
                    .Select(f => new { f.DocumentNo, f.SolLineNo })
                    .ToListAsync())
                .Select(f => LineKey(f.DocumentNo, f.SolLineNo!.Value))
                .ToHashSet();

            var detailRows = await _context.SalesOrderLineDetails.AsNoTracking()
                .Where(d => docNos.Contains(d.DocumentNo))
                .Select(d => new { d.DocumentNo, d.ItemLineNo, d.ItemNo, d.SizeNo, d.Quantity })
                .ToListAsync();

            var activeVersionIds = _context.MesProductionOrderVersions
                .Where(v => v.VersionStatus == "ACTIVE")
                .Select(v => v.Id);

            var mesItems = await _context.MesProductionOrderItems.AsNoTracking()
                .Where(i => activeVersionIds.Contains(i.VersionId)
                         && i.IsActive != false
                         && docNos.Contains(i.PoId))
                .Select(i => new { i.FactoryId, i.StyleId, i.PoId, i.ColorId, i.SizeId, i.PlannedQty })
                .ToListAsync();

            // SL đã phân theo size, tính trên tất cả nhà máy
            var allocatedBySize = mesItems
                .GroupBy(i => MatchKey(i.PoId, i.StyleId, i.ColorId, i.SizeId))
                .ToDictionary(g => g.Key, g => g.Sum(x => x.PlannedQty));

            // Khoá Style|PO|Color của nhà máy hiện tại (không tính size)
            var factoryDuplicateKeys = mesItems
                .Where(i => string.Equals(i.FactoryId, factoryId, StringComparison.OrdinalIgnoreCase))
                .Select(i => MatchKey(i.StyleId, i.PoId, i.ColorId))
                .ToHashSet();

            var result = new List<UnallocatedOrderLineSizesDto>();
            foreach (var r in refList)
            {
                var dto = new UnallocatedOrderLineSizesDto { DocumentNo = r.DocumentNo, LineNo = r.LineNo };

                if (!lineByKey.TryGetValue(LineKey(r.DocumentNo, r.LineNo), out var erp)
                    || !headerByNo.TryGetValue(r.DocumentNo, out var header))
                {
                    dto.UnavailableReason = "NOT_FOUND";
                    result.Add(dto);
                    continue;
                }

                dto.ItemNo = erp.ItemNo ?? string.Empty;
                dto.ColorNo = erp.ColorNo ?? string.Empty;
                dto.PoName = header.POName;
                dto.RequestedDeliveryDate = header.RequestedDeliveryDate.HasValue
                    ? DateOnly.FromDateTime(header.RequestedDeliveryDate.Value)
                    : null;

                if (ExcludedUnallocatedStatuses.Contains(header.Status))
                    dto.UnavailableReason = "STATUS_NOT_APPROVED";
                else if (allocatedLineKeys.Contains(LineKey(r.DocumentNo, r.LineNo)))
                    dto.UnavailableReason = "ALREADY_ALLOCATED";

                dto.IsAvailable = dto.UnavailableReason == null;
                dto.FactoryConflict = factoryDuplicateKeys.Contains(MatchKey(dto.ItemNo, dto.DocumentNo, dto.ColorNo));

                var erpSizes = detailRows
                    .Where(d => d.DocumentNo == r.DocumentNo && d.ItemLineNo == r.LineNo && d.ItemNo == erp.ItemNo)
                    .GroupBy(d => d.SizeNo)
                    .Select(g => new { SizeNo = g.Key, Qty = g.Sum(x => x.Quantity ?? 0) })
                    .OrderBy(s => s.SizeNo);

                foreach (var s in erpSizes)
                {
                    var allocated = allocatedBySize.TryGetValue(
                        MatchKey(dto.DocumentNo, dto.ItemNo, dto.ColorNo, s.SizeNo), out var a) ? a : 0;
                    dto.Sizes.Add(new UnallocatedOrderSizeQtyDto
                    {
                        SizeNo = s.SizeNo,
                        ErpQty = s.Qty,
                        AllocatedQty = allocated,
                        RemainingQty = Math.Max(0, s.Qty - allocated),
                    });
                }

                result.Add(dto);
            }

            return result;
        }

        [HttpPost("company-plan/unallocated-orders/sizes")]
        public async Task<ActionResult<object>> GetUnallocatedOrderSizes([FromBody] UnallocatedOrderSizesRequestDto request)
        {
            var factoryId = (request.FactoryId ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(factoryId))
                return BadRequest(new { success = false, message = "Thiếu mã nhà máy." });
            if (request.Lines == null || request.Lines.Count == 0)
                return BadRequest(new { success = false, message = "Chưa chọn đơn hàng nào." });

            var lines = await BuildUnallocatedLineSizesAsync(factoryId, request.Lines);
            return Ok(new { success = true, data = lines });
        }

        [HttpPost("company-plan/unallocated-orders/allocate")]
        public async Task<ActionResult<object>> AllocateUnallocatedOrders([FromBody] UnallocatedOrderAllocateRequestDto request)
        {
            var factoryId = (request.FactoryId ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(factoryId))
                return BadRequest(new { success = false, message = "Thiếu mã nhà máy." });
            if (request.Lines == null || request.Lines.Count == 0)
                return BadRequest(new { success = false, message = "Chưa chọn đơn hàng nào." });

            var createdBy = string.IsNullOrWhiteSpace(request.CreatedBy) ? "factory-user" : request.CreatedBy.Trim();
            if (createdBy.Length > 50) createdBy = createdBy[..50];

            var refs = request.Lines
                .Select(l => new UnallocatedOrderLineRefDto { DocumentNo = l.DocumentNo, LineNo = l.LineNo })
                .ToList();

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var lineInfos = await BuildUnallocatedLineSizesAsync(factoryId, refs);
                var now = DateTime.UtcNow;
                var added = new List<object>();
                var skipped = new List<object>();
                var acceptedKeys = new HashSet<string>();
                var toSave = new List<(UnallocatedOrderLineSizesDto Info, List<(string SizeNo, int Qty)> Sizes)>();

                for (var i = 0; i < request.Lines.Count; i++)
                {
                    var input = request.Lines[i];
                    var info = lineInfos[i];
                    string? reason = null;
                    string? detail = null;
                    var sizesToAdd = new List<(string SizeNo, int Qty)>();

                    if (!info.IsAvailable)
                        reason = info.UnavailableReason;
                    else if (info.FactoryConflict)
                        reason = "DUPLICATE_IN_FACTORY";
                    else if (input.Sizes.Any(s => s.Quantity < 0))
                        reason = "INVALID_QUANTITY";
                    else
                    {
                        sizesToAdd = input.Sizes
                            .Where(s => s.Quantity > 0)
                            .GroupBy(s => s.SizeNo)
                            .Select(g => (SizeNo: g.Key, Qty: g.Sum(x => x.Quantity)))
                            .ToList();

                        if (sizesToAdd.Count == 0)
                            reason = "NO_QUANTITY";
                        else
                        {
                            var over = sizesToAdd
                                .Select(q => (q.SizeNo, q.Qty, Remaining: info.Sizes.FirstOrDefault(x => x.SizeNo == q.SizeNo)?.RemainingQty ?? 0))
                                .Where(q => q.Qty > q.Remaining)
                                .ToList();

                            if (over.Count > 0)
                            {
                                reason = "EXCEEDS_REMAINING";
                                detail = string.Join(", ", over.Select(o => $"{o.SizeNo}: nhập {o.Qty}, còn {o.Remaining}"));
                            }
                            else if (!acceptedKeys.Add(MatchKey(info.ItemNo, info.DocumentNo, info.ColorNo)))
                                reason = "DUPLICATE_IN_REQUEST";
                        }
                    }

                    if (reason != null)
                    {
                        skipped.Add(new
                        {
                            documentNo = input.DocumentNo,
                            lineNo = input.LineNo,
                            itemNo = info.ItemNo,
                            colorNo = info.ColorNo,
                            reason,
                            message = detail != null
                                ? $"{AllocationSkipMessages[reason]} {detail}"
                                : AllocationSkipMessages.GetValueOrDefault(reason, "Không thể thêm dòng này."),
                        });
                        continue;
                    }

                    toSave.Add((info, sizesToAdd));
                }

                // Nhóm theo Style (ItemNo) giống quy ước plan_code của sync: {factoryId}|{ItemNo}
                foreach (var group in toSave.GroupBy(x => x.Info.ItemNo, StringComparer.OrdinalIgnoreCase))
                {
                    var planCode = $"{factoryId}|{group.Key}";
                    var order = await _context.MesProductionOrders
                        .FirstOrDefaultAsync(o => o.PlanCode == planCode);

                    if (order == null)
                    {
                        order = new MesProductionOrder
                        {
                            Id = Guid.NewGuid(),
                            PlanCode = planCode,
                            PlanName = planCode,
                            PlanPeriodFrom = null,
                            PlanPeriodTo = null,
                            Status = "APPROVED",
                            CurrentVersion = 0,
                            CreatedBy = createdBy,
                            CreatedAt = now,
                            UpdatedAt = now,
                            IsActive = true,
                        };
                        _context.MesProductionOrders.Add(order);
                    }

                    var version = await _context.MesProductionOrderVersions
                        .Where(v => v.ProductionOrderId == order.Id && v.VersionStatus == "ACTIVE")
                        .OrderByDescending(v => v.VersionNumber)
                        .FirstOrDefaultAsync();

                    if (version == null)
                    {
                        var maxVersion = await _context.MesProductionOrderVersions
                            .Where(v => v.ProductionOrderId == order.Id)
                            .MaxAsync(v => (int?)v.VersionNumber);

                        version = new MesProductionOrderVersion
                        {
                            Id = Guid.NewGuid(),
                            ProductionOrderId = order.Id,
                            VersionNumber = (maxVersion ?? 0) + 1,
                            VersionStatus = "ACTIVE",
                            TriggerType = "MANUAL",
                            SourcePoType = "TEMP",
                            ErpPoId = planCode,
                            DeltaSummary = null,
                            SupersededBy = null,
                            CreatedBy = createdBy,
                            CreatedAt = now,
                        };
                        _context.MesProductionOrderVersions.Add(version);
                    }

                    order.CurrentVersion = Math.Max(order.CurrentVersion, version.VersionNumber);
                    order.UpdatedAt = now;

                    foreach (var (info, sizes) in group)
                    {
                        var targetDate = info.RequestedDeliveryDate ?? DateOnly.FromDateTime(now);
                        var poName = info.PoName;
                        if (poName != null && poName.Length > 50) poName = poName[..50];

                        foreach (var s in sizes)
                        {
                            _context.MesProductionOrderItems.Add(new MesProductionOrderItem
                            {
                                Id = Guid.NewGuid(),
                                VersionId = version.Id,
                                FactoryId = factoryId,
                                StyleId = info.ItemNo,
                                PoId = info.DocumentNo,
                                ColorId = info.ColorNo,
                                ShadeId = null,
                                SizeId = s.SizeNo,
                                PlannedQty = s.Qty,
                                TargetDeliveryDate = targetDate,
                                POName = poName,
                                CreatedAt = now,
                                UpdatedAt = now,
                                UpdatedBy = createdBy,
                                IsActive = true,
                            });
                        }

                        added.Add(new
                        {
                            documentNo = info.DocumentNo,
                            lineNo = info.LineNo,
                            itemNo = info.ItemNo,
                            colorNo = info.ColorNo,
                            totalQty = sizes.Sum(s => s.Qty),
                        });
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new
                {
                    success = true,
                    message = $"Đã thêm {added.Count} dòng vào KHSX, bỏ qua {skipped.Count} dòng.",
                    data = new { added, skipped },
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { success = false, message = "Lỗi khi lưu dữ liệu: " + ex.Message });
            }
        }

        //[HttpPatch("production-plans/{planId}/pause")]
        //public async Task<IActionResult> PausePlan(Guid planId, [FromBody] PausePlanRequest request)
        //{
        //    var plan = await _context.MesFactoryPlans.FindAsync(planId);
        //    if (plan == null)
        //        return NotFound(new { success = false, message = "Không tìm thấy kế hoạch" });

        //    //if (plan.Status != "dang_san_xuat")
        //    //    return BadRequest(new { success = false, message = "Chỉ tạm dừng được kế hoạch đang sản xuất" });

        //    // Kiểm tra TH từ bảng QLCL — không cho tạm dừng đè lên ngày đã có sản lượng thật
        //    var hasActualAfter = await _context.qualityManagementSessions
        //        .AnyAsync(q => q.WorkShiftCode == plan.LineNumber
        //                     && q.SalesOrderNo == plan.PoId
        //                     && q.InspectionDate >= request.FromDate
        //                     && q.PassOutputQuantity > 0); // TODO: xác nhận tên cột số lượng thật

        //    if (hasActualAfter)
        //        return Conflict(new { success = false, message = "Đã có sản lượng thực tế từ ngày hiệu lực, không thể tạm dừng" });

        //    await using var tx = await _context.Database.BeginTransactionAsync();
        //    try
        //    {
        //        var futureDaily = await _context.MesFactoryPlanLines
        //            .Where(d => d.FactoryPlanId == planId && d.WorkDate >= request.FromDate)
        //            .ToListAsync();
        //        foreach (var d in futureDaily)
        //            d.PlannedQty = 0;

        //        plan.Status = "tam_dung";
        //        plan.UpdatedAt = DateTime.UtcNow;

        //        _context.FactoryPlanStatusHistory.Add(new FactoryPlanStatusHistory
        //        {
        //            PlanId = planId,
        //            Status = "tam_dung",
        //            EffectiveDate = DateOnly.FromDateTime(request.FromDate),
        //            Note = request.Reason,
        //            ChangedBy = request.UserId,
        //            ChangedAt = DateTime.UtcNow
        //        });

        //        await _context.SaveChangesAsync();
        //        await tx.CommitAsync();
        //        return Ok(new { success = true });
        //    }
        //    catch (Exception ex)
        //    {
        //        await tx.RollbackAsync();
        //        return StatusCode(500, new { success = false, message = ex.Message });
        //    }
        //}

        //[HttpGet("production-plans/{planId}/conflict-check")]
        //public async Task<IActionResult> CheckConflict(Guid planId, [FromQuery] DateTime effectiveDate)
        //{
        //    var plan = await _context.MesFactoryPlans.FindAsync(planId);
        //    if (plan == null)
        //        return NotFound(new { success = false, message = "Không tìm thấy kế hoạch" });

        //    var conflictPlan = await _context.MesFactoryPlans
        //        .FirstOrDefaultAsync(p => p.LineNumber == plan.LineNumber
        //                                && p.Status == "dang_san_xuat"
        //                                && p.Id != planId);

        //    return Ok(new
        //    {
        //        hasConflict = conflictPlan != null,
        //        conflictPlan = conflictPlan == null ? null : new { conflictPlan.Id, conflictPlan.PoId, conflictPlan.StyleId }
        //    });
        //}

        //[HttpPatch("production-plans/{planId}/activate")]
        //public async Task<IActionResult> ActivatePlan(Guid planId, [FromBody] ActivatePlanRequest request)
        //{
        //    var targetPlan = await _context.MesFactoryPlans.FindAsync(planId);
        //    if (targetPlan == null)
        //        return NotFound(new { success = false, message = "Không tìm thấy kế hoạch" });

        //    if (targetPlan.Status == "hoan_thanh")
        //        return BadRequest(new { success = false, message = "Kế hoạch đã hoàn thành, không thể kích hoạt lại" });

        //    //var conflictPlan = await _context.MesFactoryPlans
        //    //    .FirstOrDefaultAsync(p => p.LineNumber == targetPlan.LineNumber
        //    //                            && p.Status == "dang_san_xuat"
        //    //                            && p.Id != targetPlan.Id);
        //    //if (conflictPlan != null && !request.ConfirmedOverride)
        //    //{
        //    //    return Conflict(new
        //    //    {
        //    //        success = false,
        //    //        code = "NEED_CONFIRM_OVERRIDE",
        //    //        message = "Có kế hoạch khác đang chạy trên tổ này",
        //    //        conflictPlan = new { conflictPlan.Id, conflictPlan.PoId, conflictPlan.StyleId }
        //    //    });
        //    //}

        //    await using var tx = await _context.Database.BeginTransactionAsync();
        //    try
        //    {
        //        //if (conflictPlan != null)
        //        //{
        //        //    // TH check cho conflictPlan trước khi pause nó
        //        //    var hasActual = await _context.qualityManagementSessions
        //        //        .AnyAsync(q => q.WorkShiftCode == conflictPlan.LineNumber
        //        //                     && q.SalesOrderNo == conflictPlan.PoId
        //        //                     && q.InspectionDate >= request.FromDate
        //        //                     && q.PassOutputQuantity > 0);
        //        //    if (hasActual)
        //        //    {
        //        //        await tx.RollbackAsync();
        //        //        return Conflict(new { success = false, message = "Không thể tự động tạm dừng kế hoạch đang chạy do đã có sản lượng thực tế" });
        //        //    }

        //        //    var conflictFutureDaily = await _context.MesFactoryPlanLines
        //        //        .Where(d => d.FactoryPlanId == conflictPlan.Id && d.WorkDate >= request.FromDate)
        //        //        .ToListAsync();
        //        //    foreach (var d in conflictFutureDaily) d.PlannedQty = 0;

        //        //    conflictPlan.Status = "tam_dung";
        //        //    conflictPlan.UpdatedAt = DateTime.UtcNow;

        //        //    _context.FactoryPlanStatusHistory.Add(new FactoryPlanStatusHistory
        //        //    {
        //        //        PlanId = conflictPlan.Id,
        //        //        Status = "tam_dung",
        //        //        EffectiveDate = DateOnly.FromDateTime(request.FromDate),
        //        //        Note = $"Tự động tạm dừng do kích hoạt {targetPlan.PoId}",
        //        //        ChangedBy =currentUser,
        //        //        ChangedAt = DateTime.UtcNow
        //        //    });
        //        //}

        //        targetPlan.Status = "dang_san_xuat";
        //        if (targetPlan.ProductionStartDate == null)
        //            targetPlan.ProductionStartDate = DateOnly.FromDateTime(request.FromDate); 
        //        targetPlan.UpdatedAt = DateTime.UtcNow;

        //        _context.FactoryPlanStatusHistory.Add(new FactoryPlanStatusHistory
        //        {
        //            PlanId = planId,
        //            Status = "dang_san_xuat",
        //            EffectiveDate = DateOnly.FromDateTime(request.FromDate),
        //            ChangedBy = request.UserId,
        //            ChangedAt = DateTime.UtcNow
        //        });

        //        await _context.SaveChangesAsync();
        //        await tx.CommitAsync();
        //        return Ok(new { success = true });
        //    }
        //    catch (Exception ex)
        //    {
        //        await tx.RollbackAsync();
        //        return StatusCode(500, new { success = false, message = ex.Message });
        //    }
        //}


    }


}

