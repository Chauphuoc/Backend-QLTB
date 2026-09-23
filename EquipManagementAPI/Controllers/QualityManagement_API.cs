using EquipManagementAPI.Data;
using EquipManagementAPI.Helpers;
using EquipManagementAPI.Models;
using EquipManagementAPI.Models.DTOs;
using EquipManagementAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace EquipManagementAPI.Controllers
{
    [Route("api/QualityManagement")]
    [ApiController]
    public class QualityManagement_API : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IQMService  _service;
        public QualityManagement_API(ApplicationDbContext context, IQMService service)
        {
            _context = context;
            _service = service;
        }


        [HttpGet("workcenter")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<List<DepartmentDTO>>> GetWorkCenter()
        {
            try
            {
                var result = await _service.GetWorkCenterAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest("Có lỗi xảy ra khi lấy danh sách đơn vị: " + ex.Message);
            }
        }
        [HttpGet("workshift")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<List<LocationXsdDTO>>> GetWorkShift([FromQuery] string? workcenter)
        {
            try
            {
                var result = await _service.GetWorkShiftAsync(workcenter);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest("Có lỗi xảy ra khi lấy danh sách đơn vị: " + ex.Message);
            }

        }

        [HttpGet("style")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<List<ProductionPlanDTO>>> GetStyle([FromQuery] string workcenter,string workshift)
        {
            try
            {
                var result = await _service.GetStyleAsync(workcenter,workshift);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest("Có lỗi xảy ra khi lấy danh sách đơn vị: " + ex.Message);
            }
        }
        [HttpGet("size")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<List<ProductionPlanDTO>>> GetSizes([FromQuery] string workcenter, string workshift, string style)
        {
            try
            {
                var result = await _service.GetSizeAsync(workcenter, workshift, style);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest("Có lỗi xảy ra khi lấy danh sách đơn vị: " + ex.Message);
            }
        }
        [HttpGet("po")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<List<ProductionPlanDTO>>> GetPO([FromQuery] string workcenter, string workshift, string style)
        {
            try
            {
                var result = await _service.GetPOAsync(workcenter, workshift,style);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest("Có lỗi xảy ra khi lấy danh sách đơn vị: " + ex.Message);
            }
        }
        [HttpGet("color")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<List<ProductionPlanDTO>>> GetColor([FromQuery] string workcenter, string workshift, string style, string son)
        {
            try
            {
               var result = await _service.GetColorAsync(workcenter, workshift, style,son);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest("Có lỗi xảy ra khi lấy danh sách đơn vị: " + ex.Message);
            }
        }
       
        //[HttpPost("planschedule")]
        //[ProducesResponseType(200)]
        //[ProducesResponseType(400)]
        //public async Task<ActionResult<List<SalesOrderLineFactoryPlanQueueDTO>>> GetPlanQueueSchedule([FromBody] GetPlanQueueDTO filter)
        //{
        //    try
        //    {
        //       var result = await _service.GetPlanQueueScheduleAsync(filter);
        //        return Ok(result);
        //    }
        //    catch (Exception ex) {
        //        return BadRequest("Có lỗi xảy ra khi lấy danh sách đơn vị: " + ex.Message);
        //    }   

        //}

        [HttpGet("defects")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<List<DefectsDTO>>> GetEquipmentByEquipCode(string phase)
        {
            if (phase == "")
            {
                return BadRequest();
            }
            var result = await _service.GetListDefects(phase);
            if (result == null)
            {
                return NotFound("Không tìm thấy danh sách lỗi!");
            }
            return Ok(result);
        }
        [HttpGet("unit")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<List<UnitDTO>>> UnitOfMeasure()
        {
            var result = await _service.GetUnitList();
            if (result == null)
            {
                return NotFound("Không tìm thấy danh sách lỗi!");
            }
            return Ok(result);
        }
        [HttpGet("user-work-info")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<UserDTO>> GetInforUser([FromQuery] string userID)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(userID))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "userID không hợp lệ"
                    });
                }
                var result = await _service.GetInforUser(userID);
                if (result == null)
                {
                    // Nhân viên không có trong bảng employee nhưng đây là trạng thái hợp lệ
                    // (nhân viên QC có thể chưa có đầy đủ dữ liệu) → trả empty thay vì 404
                    return Ok(new
                    {
                        success = true,
                        data = (object?)null
                    });
                }
                return Ok(new
                {
                    success = true,
                    data = result
                });
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
       

        [HttpPost("checks")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        public async Task<ActionResult> XacNhan_Kiem([FromBody] JsonElement body)
        {
            var payloadElement = body.ValueKind == JsonValueKind.Object && body.TryGetProperty("payload", out var p) ? p : body;
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var payload = JsonSerializer.Deserialize<RequestCheckQlclDTO>(payloadElement.GetRawText(), options);
            if (payload == null) return BadRequest(new { success = false, message = "Invalid payload" });

            // Validate model manually
            if (!TryValidateModel(payload))
            {
                var firstError = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).FirstOrDefault();
                return BadRequest(new { success = false, message = firstError });
            }

            try
            {
                var result = await _service.Handle_XacNhanKiem(payload);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Có lỗi xảy ra khi xác nhận kiểm",
                    detail = ex.Message
                });
            }

        }
        // GET /api/QualityManagement/replacement-items
        // QualityManagementController.cs

        [HttpGet("replacement-items")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<List<ReplacementItemDto>>> GetReplacementItems()
        {
            var result = await _service.GetReplacementItemsAsync();
            
            return Ok(result);
        }
        [HttpGet("unrepairable-defects")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<List<ReplacementItemDto>>> GetDefectsNotRework()
        {
            var result = await _service.GetDefectsNotRework();


            return Ok(result);
        }
        // POST /api/quality/cutting-supply-request
        [HttpPost("cutting-supply-requests")]
        [MaintenanceAuthorize("Admin")]
        public async Task<IActionResult> SendCuttingSupplyRequest([FromBody] CuttingSupplyRequestDto dto)
        {
            var result = await _service.SendCuttingSupplyRequestAsync(dto);

            return Ok(result);
        }
        [HttpGet("cutting-supply-requests")]
        public async Task<IActionResult> GetCuttingSupplyRequests([FromQuery] int? status,[FromQuery] DateTime? dateFrom,[FromQuery] DateTime? dateTo)
        {
            var result = await _service.GetCuttingSupplyRequestsAsync(status, dateFrom, dateTo);
            return Ok(result);
        }

        // Controllers/QualityManagementController.cs
        [HttpPut("cutting-supply-requests/{id}/status")]
        [MaintenanceAuthorize("Admin")]
        public async Task<IActionResult> UpdateCuttingSupplyStatus(
            int id,
            [FromBody] UpdateCuttingSupplyStatusDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { Success = false, Message = "Dữ liệu không hợp lệ" });

            var result = await _service.UpdateCuttingSupplyStatusAsync(id, dto);
            return Ok(result);
        }
        // ──────────────────────────────────────────────────────────────────────
        // GET /api/QualityManagement/output-summary
        //
        // Query params:
        //   factory        (bắt buộc) – WorkCenterCode
        //   productionTeam (bắt buộc) – WorkShiftCode
        //   style          (bắt buộc) – ItemNo
        //   po             (bắt buộc) – SalesOrderNo
        //   color          (bắt buộc) – ColorNo
        //   erpPlanID      (tuỳ chọn) – nếu FE truyền lên để tránh join thêm
        //   date           (tuỳ chọn) – yyyy-MM-dd, mặc định hôm nay
        // ──────────────────────────────────────────────────────────────────────
        [HttpGet("output-summary")]
        [Authorize]
        public async Task<IActionResult> GetOutputSummary(
            [FromQuery] string factory,
            [FromQuery] string productionTeam,
            [FromQuery] string style,
            [FromQuery] string po,
            [FromQuery] string color,
            [FromQuery] int? erpPlanID = null,
            [FromQuery] string? date = null)
        {
            // ── 1. Validate đầu vào ──────────────────────────────────────────
            if (string.IsNullOrWhiteSpace(factory) ||
                string.IsNullOrWhiteSpace(productionTeam) ||
                string.IsNullOrWhiteSpace(style) ||
                string.IsNullOrWhiteSpace(po) ||
                string.IsNullOrWhiteSpace(color))
            {
                return BadRequest(new { message = "Cần truyền đầy đủ: factory, productionTeam, style, po, color." });
            }

            var checkDate = string.IsNullOrWhiteSpace(date)
                ? DateTime.Today
                : DateTime.Parse(date).Date;

            // ── 2. Lấy kế hoạch tổng (planQty) ──────────────────────────────
            // Bảng: QLCL_ErpPlan (hoặc tên bảng tương đương trong DB của bạn)
            // Có thể dùng erpPlanID nếu FE truyền lên để truy vấn nhanh hơn
            int planQty = 0;

            if (erpPlanID.HasValue && erpPlanID.Value > 0)
            {
                planQty = await _context.ProductionPlans
                    .Where(p => p.ErpPlanID == erpPlanID.Value.ToString())
                    .Select(p => (int?)p.PlanQuantity)
                    .FirstOrDefaultAsync() ?? 0;
            }
            else
            {
                // Fallback: tìm theo (WorkCenter + WorkShift + ItemNo + SalesOrderNo + ColorNo)
                planQty = await _context.ProductionPlans
                    .Where(p =>
                        p.WorkCenterCode == factory &&
                        p.WorkShiftCode == productionTeam &&
                        p.ItemNo == style &&
                        p.SalesOrderNo == po &&
                        p.ColorNo == color)
                    .SumAsync(p => (int?)p.TargetQuantityPerDay) ?? 0;
            }

            // ── 3. Tính kế hoạch lũy kế theo tiến độ ca ─────────────────────
            // Ca làm việc mặc định: 7h30 – 17h00 (570 phút)
            // Điều chỉnh theo lịch ca thực tế nếu cần (có thể lấy từ bảng ca)
            //const int shiftStartMinutes = 7 * 60 + 30;  // 450
            //const int shiftEndMinutes = 17 * 60 + 30;       // 1020

            //var now = DateTime.Now;
            //int currentMinutes = now.Hour * 60 + now.Minute;

            //int elapsed = Math.Max(0,
            //    Math.Min(shiftEndMinutes, currentMinutes) - shiftStartMinutes);
            //int shiftDuration = shiftEndMinutes - shiftStartMinutes; // 570

            //double progressRatio = (double)elapsed / shiftDuration;
            //progressRatio = Math.Min(1.0, progressRatio);

            //int cumulativePlan = (int)Math.Round(planQty * progressRatio);

            // ── 4. Tổng hợp thoát chuyền từ bảng quality checks ─────────────
            // Bảng: QLCL_QualityCheck
            // Cột PassOutputQty: số lượng thoát chuyền của từng lần kiểm
            var checksQuery = _context.qualityManagementSessions
                .Where(c =>
                    c.WorkCenterCode == factory &&
                    c.WorkShiftCode == productionTeam &&
                    c.ItemNo == style &&
                    c.SalesOrderNo == po &&
                    c.ColorNo == color &&
                    c.InspectionDate.Date <= checkDate && c.Status==1);

            var aggregated = await checksQuery
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    TotalPassOutput = g.Sum(c => (int?)c.PassOutputQuantity) ?? 0,
                    CheckCount = g.Count(),
                })
                .FirstOrDefaultAsync();

            int totalPassOutput = aggregated?.TotalPassOutput ?? 0;
            int checkCount = aggregated?.CheckCount ?? 0;

            var cumulativePlan = await _context.qualityManagementSessions
            .Where(c =>
                c.WorkCenterCode == factory && c.Status==1&&
                c.WorkShiftCode == productionTeam &&
                c.ItemNo == style &&
                c.SalesOrderNo == po &&
                c.ColorNo == color)
            .SumAsync(x => (int?)x.TargetQtyPerDate) ?? 0;
            //int cumulativePlan = qrTargetQty?.TotalTargetPerDay ?? 0;

            // ── 5. Trả kết quả ───────────────────────────────────────────────
            var result = new OutputSummaryResultDto
            {
                Factory = factory,
                ProductionTeam = productionTeam,
                Style = style,
                Po = po,
                Color = color,
                PlanQty = planQty,
                CumulativePlan = cumulativePlan,
                TotalPassOutput = totalPassOutput,
                CheckCount = checkCount,
                CheckDate = checkDate.ToString("yyyy-MM-dd"),
            };

            return Ok(result);
        }

        [HttpGet("output-summary-user")]
        public async Task<IActionResult> GetOutputSummaryByUser(
    [FromQuery] string userID,
    [FromQuery] string style,
    [FromQuery] string workshiftcode,
    [FromQuery] string? date = null
     )
        {
            var checkDate = string.IsNullOrWhiteSpace(date)
                ? DateTime.Today
                : DateTime.Parse(date).Date; // recommend TryParse with invariant culture in production

            var user = await (
                from e in _context.employee
                join wc in _context.Department on e.WorkCenterCode equals wc.Code into wcg
                from wc in wcg.DefaultIfEmpty()
                join ws in _context.locationXSDs on e.WorkShiftCode equals ws.Code into wcf
                from ws in wcf.DefaultIfEmpty()
                where e.No == userID
                select new UserDTO
                {
                    Id = e.LoginId,
                    Name = e.Name,
                    WorkCenterCode = e.WorkCenterCode,
                    WorkCenterName = wc.Name,
                    WorkShiftCode = e.WorkShiftCode,
                    WorkShiftName = ws.Name
                }).FirstOrDefaultAsync();

            if (user == null) return BadRequest("User not found");

            // Aggregate in one DB roundtrip: sum checked, pass, defect
            // Dùng date-range thay vì DateDiffDay (EF Core không translate được đúng trên SQL Server)
            // Dùng ItemNo thay vì ItemName (style param là mã ItemNo, không phải tên sản phẩm)
            // Dùng userID (No_ của employee) thay vì user.Id (LoginId) vì session lưu Inspector = payload.inspector = No_
            var todayStart    = checkDate;
            var tomorrowStart = checkDate.AddDays(1);

            var sums = await _context.qualityManagementSessions
                .Where(p =>
                    p.WorkCenterCode == user.WorkCenterCode &&
                    p.WorkShiftCode  == workshiftcode &&
                    p.Inspector      == userID &&
                    p.ItemNo         == style && p.Status==1&&
                    p.InspectionDate >= todayStart &&
                    p.InspectionDate <  tomorrowStart)
                .GroupBy(p => 1)
                .Select(g => new
                {
                    CheckedQty = g.Sum(x => (int?)x.CheckedQuantity)        ?? 0,
                    PassQty    = g.Sum(x => (int?)x.PassOutputQuantity)      ?? 0,
                    DefectQty  = g.Sum(x => (int?)x.DefectiveProductCount)   ?? 0,
                })
                .FirstOrDefaultAsync();

            var sessionChecked = sums?.CheckedQty ?? 0;
            var sessionPass    = sums?.PassQty    ?? 0;
            var sessionDefect  = sums?.DefectQty  ?? 0;

            // Cộng thêm kết quả từ reinspection items hôm nay cho style này
            // → KHÔNG sửa session gốc; thay vào đó cộng/trừ từ bảng reinspection
            // SpecifyKind(Unspecified) để tránh lỗi offset khi DateTime.Kind = Local
            var todayStartOffset    = new DateTimeOffset(DateTime.SpecifyKind(todayStart,    DateTimeKind.Unspecified), TimeSpan.Zero);
            var tomorrowStartOffset = new DateTimeOffset(DateTime.SpecifyKind(tomorrowStart, DateTimeKind.Unspecified), TimeSpan.Zero);
            var reSums = await _context.ReinspectionItems
                .Where(ri =>
                    ri.InspectorNo == userID &&
                    ri.Style       == style &&
                    ri.InspectedAt != null &&
                    ri.InspectedAt.Value >= todayStartOffset &&
                    ri.InspectedAt.Value <  tomorrowStartOffset)
                .GroupBy(ri => 1)
                .Select(g => new
                {
                    RePassQty     = g.Sum(ri => ri.Result == "Pass" ? (int?)ri.Quantity : 0) ?? 0,
                    // Chỉ trừ những item đã Pass khỏi DefectiveProductCount
                    // Item Result="Fail" vẫn còn là hàng lỗi tồn đọng
                    ReResolvedQty = g.Sum(ri => ri.Result == "Pass" ? (int?)ri.Quantity : 0) ?? 0,
                })
                .FirstOrDefaultAsync();

            var checkedQty = sessionChecked;
            //var passQty    = sessionPass   + (reSums?.RePassQty     ?? 0);
            //var defectQty  = Math.Max(0, sessionDefect - (reSums?.ReResolvedQty ?? 0));
            var passQty = sessionPass;
            var defectQty =  sessionDefect;
            var result = new OutputSummaryByUserDto
            {
                CheckedQuantity = checkedQty,
                PassQuantity = passQty,
                DefectQuantity = defectQty,
                //WorkCenterName = user.WorkCenterName,
                //WorkShiftName = user.WorkShiftName,
                //Date = checkDate.ToString("yyyy-MM-dd")
            };

            return Ok(result);
        }

        [HttpGet("output-summary-kiemlai")]
        public async Task<IActionResult> GetOutputSummary_KiemLai(
    [FromQuery] string userID,
    [FromQuery] string style,
    [FromQuery] string workshiftcode,
    [FromQuery] string? date = null
     )
        {
            var checkDate = string.IsNullOrWhiteSpace(date)
                ? DateTime.Today
                : DateTime.Parse(date).Date; // recommend TryParse with invariant culture in production

            var user = await (
                from e in _context.employee
                join wc in _context.Department on e.WorkCenterCode equals wc.Code into wcg
                from wc in wcg.DefaultIfEmpty()
                join ws in _context.locationXSDs on e.WorkShiftCode equals ws.Code into wcf
                from ws in wcf.DefaultIfEmpty()
                where e.No == userID
                select new UserDTO
                {
                    Id = e.LoginId,
                    Name = e.Name,
                    WorkCenterCode = e.WorkCenterCode,
                    WorkCenterName = wc.Name,
                    WorkShiftCode = e.WorkShiftCode,
                    WorkShiftName = ws.Name
                }).FirstOrDefaultAsync();

            if (user == null) return BadRequest("User not found");
            var todayStart = checkDate;
            var tomorrowStart = checkDate.AddDays(1);

            //Lấy tổng sl kiểm lại
            var qrKiemLai = await _context.qualityManagementSessions
                .Where(p => p.ItemNo == style && p.WorkShiftCode == workshiftcode && p.Status==1 )
                .GroupBy(x => 1)
                .Select(g => new
                {
                    PassOutputQty = g.Sum(x => x.PassOutputQuantity),
                    CheckedQty = g.Sum(x => x.CheckedQuantity)
                })
                .FirstOrDefaultAsync();
            //Lấy tổng sl kiểm đạt
            var slKiemLai = (qrKiemLai?.CheckedQty ?? 0) - (qrKiemLai?.PassOutputQty ?? 0);
            var totalPassed = await _context.Reinspections
            .Where(a => a.ProductionTeamCode == workshiftcode
                && _context.ReinspectionItems.Any(b => b.ReinspectionId == a.Id && b.Style == style))
            .SumAsync(a => a.TotalPassed);

            var defectQty = slKiemLai - totalPassed;
                    
            var result = new OutputSummaryByUserDto
            {
                CheckedQuantity = slKiemLai,
                PassQuantity = totalPassed,
                DefectQuantity = defectQty,
            };

            return Ok(result);
        }


        // GET /api/quality/style-defect-summary
        //
        // Query params:
        //   date      (tuỳ chọn) – nếu dateFrom == dateTo, FE gửi 1 param này
        //   from      (tuỳ chọn) – ngày bắt đầu khoảng (yyyy-MM-dd)
        //   to        (tuỳ chọn) – ngày kết thúc khoảng (yyyy-MM-dd)
        //   factoryId (tuỳ chọn) – WorkCenterCode; bỏ qua nếu "all" hoặc không có
        //
        // Trả về: StyleDefectSummaryDto[]
        //   – Mỗi phần tử là 1 style với tổng lỗi + tỷ lệ lỗi trong khoảng thời gian
        //   – Chỉ trả những style có ít nhất 1 lần kiểm
        //   – Sắp xếp giảm dần theo TotalDefects

        // ──────────────────────────────────────────────────────────────────────
        [HttpGet("style-defect-summary")]
        [Authorize]
        public async Task<IActionResult> GetStyleDefectSummary(
            [FromQuery] string? date = null,
            [FromQuery] string? from = null,
            [FromQuery] string? to = null,
            [FromQuery] string? factoryId = null,
            [FromQuery] string? teamId = null,
            [FromQuery] string? operationType = null)
        {
            try
            {
                var result = await _service.GetStyleDefectSummaryAsync(date, from, to, factoryId, teamId, operationType);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
        [HttpGet("style-defect-breakdown")]
        public async Task<IActionResult> GetStyleDefectBreakdown(
            [FromQuery] string styleCode,
            [FromQuery] string? date = null,
            [FromQuery] string? from = null,
            [FromQuery] string? to = null,
            [FromQuery] string? factoryId = null,
            [FromQuery] string? teamId = null,
            [FromQuery] string? operationType = null)
        {
            try
            {
                var result = await _service.GetStyleDefectBreakdownAsync(
                    styleCode, date, from, to, factoryId, teamId, operationType);

                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
        // GET /api/QualityManagement/style-defect-by-team
        //
        // Mục đích:
        //   Cho thấy tỷ lệ lỗi của CÙNG MỘT Style nhưng chia theo từng Chuyền.
        //   → Nếu tất cả Chuyền đều cao: lỗi do Style khó (đặc thù mã hàng).
        //   → Nếu chỉ 1-2 Chuyền cao hơn hẳn: lỗi do năng lực Chuyền đó.
        //
        // Query params:
        //   styleCode  (bắt buộc) – ItemNo của style cần phân tích
        //   date       (tuỳ chọn) – ngày đơn (yyyy-MM-dd), dùng khi dateFrom == dateTo
        //   from       (tuỳ chọn) – ngày bắt đầu khoảng (yyyy-MM-dd)
        //   to         (tuỳ chọn) – ngày kết thúc khoảng (yyyy-MM-dd)
        //   factoryId  (tuỳ chọn) – WorkCenterCode; bỏ qua nếu "all" hoặc không có
        //
        // Trả về: StyleTeamDefectDto[]
        //   – Mỗi phần tử là 1 Chuyền với: TeamId, TeamName, TotalInspected,
        //     TotalDefects, DefectRate
        //   – Chỉ trả những Chuyền có ít nhất 1 lần kiểm style đó
        //   – Sắp xếp giảm dần theo DefectRate
               // ──────────────────────────────────────────────────────────────────────
        [HttpGet("style-defect-by-team")]
        [Authorize]
        public async Task<IActionResult> GetStyleDefectByTeam(
            [FromQuery] string styleCode,
            [FromQuery] string? date = null,
            [FromQuery] string? from = null,
            [FromQuery] string? to = null,
            [FromQuery] string? factoryId = null,
            [FromQuery] string? operationType = null)
        {
            // ── 1. Validate styleCode ────────────────────────────────────────
            if (string.IsNullOrWhiteSpace(styleCode))
                return BadRequest(new { message = "Tham số 'styleCode' là bắt buộc." });

            // ── 2. Xác định khoảng ngày ──────────────────────────────────────
            DateTime dateFrom, dateTo;

            if (!string.IsNullOrWhiteSpace(date))
            {
                if (!DateTime.TryParse(date, out dateFrom))
                    return BadRequest(new { message = "Tham số 'date' không hợp lệ (yyyy-MM-dd)." });
                dateTo = dateFrom;
            }
            else if (!string.IsNullOrWhiteSpace(from) && !string.IsNullOrWhiteSpace(to))
            {
                if (!DateTime.TryParse(from, out dateFrom) || !DateTime.TryParse(to, out dateTo))
                    return BadRequest(new { message = "Tham số 'from' hoặc 'to' không hợp lệ (yyyy-MM-dd)." });

                if (dateFrom > dateTo)
                    return BadRequest(new { message = "'from' phải nhỏ hơn hoặc bằng 'to'." });
            }
            else
            {
                dateFrom = dateTo = DateTime.Today;
            }

            var dateFromDate = dateFrom.Date;
            var dateToDate = dateTo.Date;

            // ── 3. Query lọc theo style + ngày (+ nhà máy tuỳ chọn) ─────────
            var query = _context.qualityManagementSessions
                .Join(_context.locationXSDs, q=> q.WorkShiftCode,ws => ws.Code, (q, ws) => new {q,ws})
                .Where(c =>
                    c.q.ItemNo == styleCode &&
                    c.q.InspectionDate.Date >= dateFromDate &&
                    c.q.InspectionDate.Date <= dateToDate);

            bool hasFactory = !string.IsNullOrWhiteSpace(factoryId) &&
                              !string.Equals(factoryId, "all", StringComparison.OrdinalIgnoreCase);
            if (hasFactory)
                query = query.Where(c => c.q.WorkCenterCode == factoryId);

            bool hasOperationType = !string.IsNullOrWhiteSpace(operationType) &&
                                    !string.Equals(operationType, "all", StringComparison.OrdinalIgnoreCase);
            if (hasOperationType)
                query = query.Where(c => c.q.OperationType == operationType);

            // ── 4. Group theo Chuyền, tính tổng kiểm & lỗi ──────────────────
            //
            // Nếu bảng QLCL_QualityCheck có cột WorkShiftName trực tiếp,
            // dùng: g.Key.WorkShiftName
            //
            // Nếu tên chuyền cần join sang bảng khác (e.g. QLCL_WorkShift),
            // thay đổi phần Select bên dưới để join trước khi GroupBy.
            // ─────────────────────────────────────────────────────────────────
            var grouped = await query
                .GroupBy(c => new { c.q.WorkShiftCode, c.ws.Name})
                .Select(g => new
                {
                    TeamId = g.Key.WorkShiftCode,
                    TeamName = g.Key.Name,
                    TotalInspected = g.Sum(c => (int?)c.q.CheckedQuantity) ?? 0,
                    TotalDefects = g.Sum(c => (int?)c.q.DefectiveProductCount) ?? 0,
                })
                .Where(g => g.TotalInspected > 0)
                .OrderByDescending(g =>
                    g.TotalInspected > 0
                        ? (double)g.TotalDefects / g.TotalInspected
                        : 0)
                .ToListAsync();

            if (grouped.Count == 0)
                return Ok(Array.Empty<StyleTeamDefectDto>());

            // ── 5. Tính DefectRate & map sang DTO ────────────────────────────
            var result = grouped.Select(g => new StyleTeamDefectDto
            {
                TeamId = g.TeamId,
                TeamName = g.TeamName ?? g.TeamId,   // fallback nếu tên null
                TotalInspected = g.TotalInspected,
                TotalDefects = g.TotalDefects,
                DefectRate = g.TotalInspected > 0
                    ? Math.Round((double)g.TotalDefects / g.TotalInspected * 100, 2)
                    : 0,
            }).ToList();

            return Ok(result);
        }

        /// <summary>
        /// Lấy thông tin nhân viên theo username đăng nhập
        /// GET api/Employee/user/{userName}
        /// </summary>
        [HttpGet("user/{userName}")]
        public async Task<IActionResult> GetByUserName(string userName)
        {
            if (string.IsNullOrWhiteSpace(userName))
                return BadRequest(new { message = "userName không được để trống." });

            var employee = await _service.GetByUserNameAsync(userName);
            if (employee == null)
                return NotFound(new { message = $"Không tìm thấy nhân viên với userName: {userName}" });

            return Ok(employee);
        }
        [HttpGet("qc-users")]
        public async Task<IActionResult> GetQcUsers([FromQuery] string? workcenter)
        {
            // Lưu ý: cách xác định "QC user" phụ thuộc vào dữ liệu hiện có.
            // Ví dụ: cột e.IsQc, hoặc bảng role liên kết. Thay điều kiện lọc phù hợp.

            var query =
                  from e in _context.employee
                  join ws in _context.workShift on e.WorkShiftCode equals ws.Code into wsg
                  from ws in wsg.DefaultIfEmpty()
                  where e.Status != 2
                        &&
                        (
                            (workcenter == "0102" &&
                             (e.WorkShiftCode == "01020122" || e.WorkShiftCode == "01020130"))
                            ||
                            (workcenter == "0101" &&
                             e.WorkShiftCode == "01010120")
                        )
                  select new QcUserDto
                  {
                      No = e.No,
                      No2 = e.No2,
                      Name = e.Name,
                      QcTeamCode = e.WorkShiftCode,
                      QcTeamName = ws.Name
                  };

            var list = await query.OrderBy(x => x.Name).ToListAsync();
            return Ok(list);
        }
        [HttpPost("assignments")]
        //[MaintenanceAuthorize("Admin", "Manager",  "Baotri1", "Baotri2")]
        public async Task<IActionResult> CreateAssignment([FromBody] AssignmentDto dto)
        {
            if (dto == null) return BadRequest();
            if (string.IsNullOrEmpty(dto.EmployeeNo) || string.IsNullOrEmpty(dto.TeamCode))
                return BadRequest("Missing employeeNo or teamCode");

            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                var now = DateTime.UtcNow;
                var from = dto.FromDate == default ? now : dto.FromDate;

                // 1) Close previous active primary assignment(s) for this employee
                var activeAssignments = await _context.EmployeeAssignmentHistory
                    .Where(a => a.EmployeeNo == dto.EmployeeNo && (a.ToDate == null || a.ToDate >= from))
                    .ToListAsync();

                foreach (var a in activeAssignments)
                {
                    // set ToDate to just before new assignment start (or now)
                    a.ToDate = from.AddMilliseconds(-1);
                    _context.EmployeeAssignmentHistory.Update(a);
                }

                // 2) Insert new assignment record
                var newAssign = new EmployeeAssignmentHistory
                {
                    EmployeeNo = dto.EmployeeNo,
                    TeamCode = dto.TeamCode,
                    TeamName = dto.TeamName,
                    AssignedByNo = dto.AssignedByNo ?? User?.Identity?.Name,
                    FromDate = from,
                    ToDate = dto.IsPermanent ? (DateTime?)null : dto.ToDate,
                    Reason = dto.Reason,
                    CreatedAt = now,
                    InspectionType = dto.InspectionType
                };
                _context.EmployeeAssignmentHistory.Add(newAssign);

                await _context.SaveChangesAsync();

                // Optionally: if you maintain employee table for quick lookup, update it here
                // var emp = await _context.employee.FirstOrDefaultAsync(e => e.No == dto.EmployeeNo);
                // if (emp != null && dto.IsPermanent) { emp.WorkAssignmentTeamCode = dto.TeamCode; _context.employee.Update(emp); await _context.SaveChangesAsync(); }

                await tx.CommitAsync();

                var result = new
                {
                    id = newAssign.Id,
                    employeeNo = newAssign.EmployeeNo,
                    teamCode = newAssign.TeamCode,
                    teamName = newAssign.TeamName,
                    fromDate = newAssign.FromDate,
                    toDate = newAssign.ToDate,
                    isPermanent = newAssign.ToDate == null,
                    inspectionType = newAssign.InspectionType
                };

                return CreatedAtAction(nameof(GetAssignmentById), new { id = newAssign.Id }, result);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                // log error
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpGet("assignments/{id}")]
        public async Task<IActionResult> GetAssignmentById(int id)
        {
            var a = await _context.EmployeeAssignmentHistory.FindAsync(id);
            if (a == null) return NotFound();
            return Ok(a);
        }
        [HttpPost("delegations")]
        [MaintenanceAuthorize("Admin", "Manager", "Baotri1", "Baotri2")]
        public async Task<IActionResult> CreateDelegation([FromBody] CreateDelegationDto dto)
        {
            if (dto == null) return BadRequest("Missing payload");
            if (string.IsNullOrWhiteSpace(dto.EmployeeNo) || string.IsNullOrWhiteSpace(dto.TargetTeamCode) || dto.ValidFrom == default)
                return BadRequest("employeeNo, targetTeamCode and validFrom are required.");

            // Get caller identity (prefer claim that holds employee no, e.g. "no_" or NameIdentifier)
            var callerNo = User?.FindFirst("no_")?.Value
                           ?? User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                           ?? User?.Identity?.Name;
            if (string.IsNullOrEmpty(callerNo) && string.IsNullOrEmpty(dto.GrantedByNo))
                return Unauthorized("Granting user not found.");

            var grantedByNo = dto.GrantedByNo ?? callerNo;
            var grantedByName = User?.FindFirst("name")?.Value ?? User?.Identity?.Name;

            // Validate date logic
            if (!dto.IsPermanent && dto.ValidUntil.HasValue && dto.ValidUntil < dto.ValidFrom)
                return BadRequest("validUntil must be >= validFrom");

            // Optional: validate employee exists
            var granteeExists = await _context.employee.AnyAsync(e => e.No == dto.EmployeeNo);
            if (!granteeExists) return BadRequest("Employee (grantee) not found.");

            // Optional: validate caller has permission to grant for target team
            var hasScope = true; // TODO: implement actual permission check
            if (!hasScope) return Forbid();

            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                var now = DateTime.UtcNow;
                var entity = new QualityInspectorDelegation
                {
                    EmployeeNo = dto.EmployeeNo,
                    TargetTeamCode = dto.TargetTeamCode,
                    TargetTeamName = dto.TargetTeamName,
                    GrantedByNo = grantedByNo,
                    GrantedByName = grantedByName,
                    ValidFrom = dto.ValidFrom,
                    ValidUntil = dto.IsPermanent ? null : dto.ValidUntil,
                    IsPermanent = dto.IsPermanent,
                    Reason = dto.Reason,
                    Status = "Active",
                    CreatedAt = now,
                    InspectionType = dto.InspectionType
                };

                _context.QualityInspectorDelegations.Add(entity);
                await _context.SaveChangesAsync();

                await tx.CommitAsync();

                var result = new
                {
                    id = entity.Id,
                    employeeNo = entity.EmployeeNo,
                    targetTeamCode = entity.TargetTeamCode,
                    targetTeamName = entity.TargetTeamName,
                    validFrom = entity.ValidFrom,
                    validUntil = entity.ValidUntil,
                    isPermanent = entity.IsPermanent,
                    status = entity.Status,
                    inspectionType = entity.InspectionType
                };
                return CreatedAtAction(nameof(GetDelegationById), new { id = entity.Id }, result);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                // log exception
                return StatusCode(500, new { message = ex.Message });
            }
        }
        [HttpPatch("delegations/{id}/revoke")]
        [MaintenanceAuthorize("Admin", "Manager", "Baotri1", "Baotri2")]
        public async Task<IActionResult> RevokeDelegation(int id)
        {
            var delegation = await _context.QualityInspectorDelegations.FindAsync(id);
            if (delegation == null) return NotFound();

            // Permission: only grantor or manager can revoke
            var callerNo = User?.FindFirst("no_")?.Value ?? User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (delegation.GrantedByNo != callerNo)
            {
                var callerHasScope = true; // implement scope/role check
                if (!callerHasScope) return Forbid();
            }

            delegation.Status = "Revoked";
            delegation.RevokedAt = DateTime.UtcNow;
            delegation.RevokedBy = callerNo;
            _context.QualityInspectorDelegations.Update(delegation);
            await _context.SaveChangesAsync();

            return NoContent();
        }
        [HttpGet("delegations/{id}")]
        public async Task<IActionResult> GetDelegationById(int id)
        {
            var d = await _context.QualityInspectorDelegations
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (d == null) return NotFound();

            // Lấy caller id (ưu tiên claim chứa mã nhân viên, ví dụ "no_")
            var callerNo = User?.FindFirst("no_")?.Value
                           ?? User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                           ?? User?.Identity?.Name;

            // Quyền truy cập: grantor hoặc grantee hoặc admin hoặc manager-of-team
            var isAdmin = User?.IsInRole("Admin") ?? false;
            var isGrantorOrGrantee = !string.IsNullOrEmpty(callerNo) && (d.GrantedByNo == callerNo || d.EmployeeNo == callerNo);

            var isManagerOfTeam = false;
            // TODO: implement actual manager-scope check, ví dụ:
            // isManagerOfTeam = await _yourService.IsManagerOfTeamAsync(callerNo, d.TargetTeamCode);

            //if (!isGrantorOrGrantee && !isAdmin && !isManagerOfTeam)
            //    return Forbid();

            var result = new
            {
                id = d.Id,
                employeeNo = d.EmployeeNo,
                targetTeamCode = d.TargetTeamCode,
                targetTeamName = d.TargetTeamName,
                grantedByNo = d.GrantedByNo,
                grantedByName = d.GrantedByName,
                validFrom = d.ValidFrom,
                validUntil = d.ValidUntil,
                isPermanent = d.IsPermanent,
                reason = d.Reason,
                status = d.Status,
                createdAt = d.CreatedAt,
                revokedAt = d.RevokedAt,
                revokedBy = d.RevokedBy
            };

            return Ok(result);
        }

        // ─────────────────────────────────────────────────────────────────────
        // POST /api/QualityManagement/reinspections
        //
        // Xử lý "Kiểm lại": nhân viên QC hoàn tất phiên kiểm lại độc lập.
        // Payload dùng chung RequestCheckQlclDTO; các field quan trọng:
        //   passOutputQty           – số lượng kiểm (theo đơn vị)
        //   unitQuantity            – số cái / đơn vị
        //   defectiveProductCount   – số sản phẩm lỗi (pcs)
        //   reinspectionQuantity    – = passOutputQty (FE tự map)
        //   reinspectionDefectiveCount – = defectiveProductCount (FE tự map)
        //   defects                 – danh sách chi tiết lỗi
        // ─────────────────────────────────────────────────────────────────────
        [HttpPost("reinspections")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        public async Task<ActionResult> TaoPhienKiemLai([FromBody] JsonElement body)
        {
            var payloadElement = body.ValueKind == JsonValueKind.Object && body.TryGetProperty("payload", out var p) ? p : body;
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var payload = JsonSerializer.Deserialize<RequestCheckQlclDTO>(payloadElement.GetRawText(), options);
            if (payload == null) return BadRequest(new { success = false, message = "Invalid payload" });

            if (!TryValidateModel(payload))
            {
                var firstError = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).FirstOrDefault();
                return BadRequest(new { success = false, message = firstError });
            }

            try
            {
                var result = await _service.Handle_KiemLai(payload);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Có lỗi xảy ra khi tạo phiên kiểm lại",
                    detail = ex.Message
                });
            }
        }

        // POST /api/QualityManagement/assign-inspection-type
        // Cập nhật loại kiểm (Endline / SauUi) cho bản ghi assignment đang active của nhân viên.
        [HttpPost("assign-inspection-type")]
        [MaintenanceAuthorize("Admin", "Manager", "Baotri1", "Baotri2")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> AssignInspectionType([FromBody] JsonElement body)
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            string? employeeNo = null;
            string? inspectionType = null;
            string? assignedByNo = null;

            try
            {
                if (body.TryGetProperty("employeeNo", out var en)) employeeNo = en.GetString();
                if (body.TryGetProperty("inspectionType", out var it)) inspectionType = it.GetString();
                if (body.TryGetProperty("assignedByNo", out var ab)) assignedByNo = ab.GetString();
            }
            catch
            {
                return BadRequest(new { success = false, message = "Payload không hợp lệ" });
            }

            if (string.IsNullOrWhiteSpace(employeeNo))
                return BadRequest(new { success = false, message = "employeeNo là bắt buộc" });

            var allowed = new[] { "Endline", "SauUi" };
            if (string.IsNullOrWhiteSpace(inspectionType) || !allowed.Contains(inspectionType))
                return BadRequest(new { success = false, message = "inspectionType phải là 'Endline' hoặc 'SauUi'" });

            var callerNo = assignedByNo ?? User?.Identity?.Name;
            var now = DateTime.UtcNow;

            // Tìm bản ghi assignment đang active
            var active = await _context.EmployeeAssignmentHistory
                .Where(a => a.EmployeeNo == employeeNo && (a.ToDate == null || a.ToDate >= now))
                .OrderByDescending(a => a.FromDate)
                .FirstOrDefaultAsync();

            if (active != null)
            {
                // Cập nhật InspectionType vào bản ghi hiện tại
                active.InspectionType = inspectionType;
                _context.EmployeeAssignmentHistory.Update(active);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        active.Id,
                        active.EmployeeNo,
                        active.TeamCode,
                        active.TeamName,
                        active.InspectionType
                    }
                });
            }
            else
            {
                // Không có assignment active — tạo mới với TeamCode rỗng (placeholder)
                var record = new EmployeeAssignmentHistory
                {
                    EmployeeNo = employeeNo,
                    TeamCode = "-",
                    InspectionType = inspectionType,
                    AssignedByNo = callerNo,
                    FromDate = now,
                    ToDate = null,
                    CreatedAt = now
                };
                _context.EmployeeAssignmentHistory.Add(record);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        record.Id,
                        record.EmployeeNo,
                        record.TeamCode,
                        record.TeamName,
                        record.InspectionType
                    }
                });
            }
        }
    }
}
