using Azure.Core;
using EquipManagementAPI.Data;
using EquipManagementAPI.Models;
using EquipManagementAPI.Models.DTOs;
using Microsoft.AspNetCore.Connections;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System;
using System.Globalization;
using System.Runtime.InteropServices.JavaScript;

namespace EquipManagementAPI.Services
{
    public class QMService : IQMService
    {
        private readonly ApplicationDbContext _context;
        public QMService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<DefectsDTO>> GetListDefects(string phase)
        {
            if (string.IsNullOrWhiteSpace(phase))
            {
                return new List<DefectsDTO>();
            }

            var defects = await _context.Defects
                .AsNoTracking()
                .Where(x => x.Phase.ToString() == phase)
                .Select(x => new DefectsDTO
                {
                    Code = x.Code,
                    Name = x.Name,
                })
                .ToListAsync();

            return defects;
        }
        public async Task<List<DepartmentDTO>> GetWorkCenterAsync()
        {
            var data = await _context.workCenters.Where(e => e.No != null)
           .Select(e => new
           {
               e.No,
               e.Name
           }).Distinct().ToListAsync();

            return data.Select(e => new DepartmentDTO
            {
                Value = e.No,
                Label = e.Name
            }).ToList();
        }
        public async Task<List<LocationXsdDTO>> GetWorkShiftAsync(string workcenter)
        {
            var data = await _context.locationXSDs.Where(e => e.WorkCenterCode == workcenter)
           .Select(e => new
           {
               e.Code,

               e.Name
           }).Distinct().ToListAsync();

            return data.Select(e => new LocationXsdDTO
            {
                Value = e.Code,
                Label = e.Name
            }).ToList();
        }
        public async Task<List<ProductionPlanDTO>> GetStyleAsync(string workcenter, string workshift)
        {
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            var data = await (
                from line in _context.MesFactoryPlanLines
                join plan in _context.MesFactoryPlans on line.FactoryPlanId equals plan.Id
                join f in _context.Item on plan.StyleId equals f.No
                where plan.FactoryId == workcenter && plan.IsActive==true
                   && plan.LineNumber == workshift
                   && line.WorkDate >= today
                   && line.WorkDate < tomorrow
                orderby line.WorkDate descending
                select new
                {
                    ItemNo = plan.StyleId,
                    f.Name
                }
            ).Distinct().ToListAsync();

            return data.Select(e => new ProductionPlanDTO
            {
                ItemNo = e.ItemNo,
                ItemName = e.Name
            }).ToList();
        }
        public async Task<List<ProductionPlanDTO>> GetPOAsync(string workcenter, string workshift, string style)
        {
             var today = DateTime.Today;
            var tomorrow = today.AddDays(1);
            var data = await (
     from plan in _context.MesFactoryPlans

     join line in _context.MesFactoryPlanLines
         on plan.Id equals line.FactoryPlanId

     // LEFT JOIN SalesOrderHeader
     join soh in _context.SalesOrderHeader
         on plan.PoId equals soh.No into sohGroup
     from soh in sohGroup.DefaultIfEmpty()

         // LEFT JOIN MesProductionOrderItem
     join poi in _context.MesProductionOrderItems
         on plan.PoId equals poi.PoId into poiGroup
     from poi in poiGroup.DefaultIfEmpty()

     where plan.FactoryId == workcenter
        && plan.LineNumber == workshift && plan.IsActive == true
        && plan.StyleId == style
        && line.WorkDate >= today
        && line.WorkDate < tomorrow 

     select new ProductionPlanDTO
     {
         SalesOrderNo = plan.PoId,

         POName = soh != null && soh.POName != null
             ? soh.POName
             : poi != null
                 ? poi.POName
                 : null
     }
 )
 .Distinct()
 .ToListAsync();

            return data;
        }
        public async Task<List<ProductionPlanDTO>> GetSizeAsync(string workcenter, string workshift, string style)
        {
            var data = await _context.MesProductionOrderItems.AsNoTracking().Where(e=>e.StyleId == style&&e.FactoryId==workcenter).Select(e=>e.SizeId).Distinct().ToListAsync();
         
            return data.Select(size => new ProductionPlanDTO
            {
                SizeNo = size
            }).ToList();
        }
        public async Task<List<ProductionPlanDTO>> GetColorAsync(string workcenter, string workshift, string style, string son)
        {
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            var rows = await (
                from line in _context.MesFactoryPlanLines
                join plan in _context.MesFactoryPlans on line.FactoryPlanId equals plan.Id
                where plan.FactoryId == workcenter          // đúng tổ (workcenter)
                   && plan.LineNumber == workshift          // đúng ca/chuyền (workshift)
                   && plan.StyleId == style    
                   && plan.PoId == son
                   && line.WorkDate >= today                // đúng ngày hôm nay
                   && line.WorkDate < tomorrow && plan.IsActive == true
                select new { plan.Id, plan.ColorId, plan.TotalPlannedQty, line.PlannedQty }
            ).ToListAsync();

            // Tổng SL kế hoạch của cả style/ngày/tổ (không tách theo màu)
            var totalPlanQty = rows.Sum(x => x.PlannedQty);

            // Lấy danh sách màu kèm theo Id tương ứng (không trùng lặp theo ColorId)
            var colorList = rows
                 .GroupBy(x => x.ColorId)
                 .Select(g => new
                 {
                     Id = g.First().Id,
                     ColorId = g.Key,
                     PlanQty = g.Sum(x => x.PlannedQty),   // tổng riêng theo từng màu
                     TotalPlannedQty = g.First().TotalPlannedQty
                 })
                 .ToList();

            // Tổng SL đã thực hiện (PassOutputQuantity) theo PlanId trong QLCL
            var planIds = colorList.Select(x => x.Id).ToList();
            var passedByPlan = await _context.qualityManagementSessions
                .AsNoTracking()
                .Where(q => q.PlanId != null && planIds.Contains(q.PlanId.Value))
                .GroupBy(q => q.PlanId!.Value)
                .Select(g => new { PlanId = g.Key, Passed = g.Sum(x => x.PassOutputQuantity) })
                .ToDictionaryAsync(x => x.PlanId, x => x.Passed);

            return colorList.Select(x => new ProductionPlanDTO
            {
                PlanID = x.Id,
                ColorNo = x.ColorId,
                PlanQty = x.PlanQty,
                // Còn lại = total_planned_qty của plan - số đã thực hiện
                RemainQty = x.TotalPlannedQty - (passedByPlan.TryGetValue(x.Id, out var passed) ? passed : 0),
            }).ToList();
        }

        //public async Task<List<SalesOrderLineFactoryPlanQueueDTO>>GetPlanQueueScheduleAsync(GetPlanQueueDTO filter)
        //{
        //    var query = _context.SalesOrderLineFactoryPlanQueues
        //                        .Where(e => e.Status == 3);

        //    if (!string.IsNullOrEmpty(filter.WorkCenterName))
        //    {
        //        query = query.Where(e => e.WorkCenterCode == filter.WorkCenterName);
        //    }

        //    if (filter.FromDate.HasValue)
        //    {
        //        query = query.Where(x => x.ActualStartingDate >= filter.FromDate.Value.Date);
        //    }

        //    if (filter.ToDate.HasValue)
        //    {
        //        var toDate = filter.ToDate.Value.Date.AddDays(1);
        //        query = query.Where(x => x.ActualStartingDate < toDate);
        //    }

        //    if (!string.IsNullOrWhiteSpace(filter.ItemName))
        //    {
        //        query = query.Where(x => x.ItemName.Contains(filter.ItemName));
        //    }

        //    if (!string.IsNullOrWhiteSpace(filter.POName))
        //    {
        //        query = query.Where(x => x.POName.Contains(filter.POName));
        //    }

        //    if (!string.IsNullOrWhiteSpace(filter.WorkShiftName))
        //    {
        //        query = query.Where(x => x.WorkShiftCode == filter.WorkShiftName);
        //    }

        //    if (!string.IsNullOrWhiteSpace(filter.ColorNo))
        //    {
        //        query = query.Where(x => x.ColorNo == filter.ColorNo);
        //    }

        //    if (!string.IsNullOrWhiteSpace(filter.SizeNo))
        //    {
        //        query = query.Where(x => x.SizeNo == filter.SizeNo);
        //    }

        //    return await query
        //        .Select(e => new SalesOrderLineFactoryPlanQueueDTO
        //        {
        //            QueueID = e.QueueID,
        //            SalesOrderNo = e.SalesOrderNo,
        //            ItemName = e.ItemName,
        //            POName = e.POName,
        //            WorkShiftName = e.WorkShiftName,
        //            ColorNo = e.ColorNo
        //        })
        //        .ToListAsync();
        //}

        public async Task<List<UnitDTO>> GetUnitList()
        {
            var query = new List<UnitDTO>();
            query = await _context.Unit.Select(e => new UnitDTO
            {
                Id = e.Id,
                Name = e.Name,
                Quantity = e.Quantity,
            }).ToListAsync();
            return query;
        }
        private TimeSpan ParseTime(string time)
        {
            // "7h30" -> "7:30"
            var formatted = time.Replace("h", ":");
            return TimeSpan.Parse(formatted);
        }
        public static string FormatDocumentNumber(string template, string number)
        {
            // Ví dụ template = "99999", number = "22" → trả về "00022"
            for (int i = 0; i < template.Length - number.Length; i++)
            {
                number = "0" + number;
            }
            return number;
        }
        public async Task<object> Handle_XacNhanKiem(RequestCheckQlclDTO payload)
        {
            if (payload == null)
                throw new Exception("Payload không hợp lệ");
            try
            {
                //Check thời gian
                var now = DateTime.Now.TimeOfDay;
                var timeSlots = await _context.TimeSlots.ToListAsync();
                var currentSlot = timeSlots.FirstOrDefault(slot =>
                now >= slot.StartTime && now <= slot.EndTime
                );
                if (currentSlot == null)
                    throw new Exception("Không nằm trong khung giờ nào");
                //var erpPlan = await _context.ProductionPlans.FirstOrDefaultAsync(e => e.ErpPlanID == payload.erpPlanID.ToString());
                //if (erpPlan == null)
                //    throw new Exception($"Không tìm thấy ProductionPlan với ID = {payload.erpPlanID}");
                //var series = await _context.noSeriesLineQLCL.FirstOrDefaultAsync(e => e.SeriesCode == "QLCL" && e.Code == "QM");
                //if (series == null)
                //    throw new Exception("Không tìm thấy cấu hình số chứng từ (noSeriesLine)");
                //var lastNo = series.LastNoUsed ?? 0;
                //var increment = series.IncrementByNo ?? 1;

                //var newNo = (series.StartingNo ?? "") +
                //            FormatDocumentNumber(
                //                series.EndingNo?.ToString() ?? "0",
                //                (lastNo + increment).ToString()
                var newNo = Guid.NewGuid();
                var itemName = await _context.Item.Where(x => x.No == payload.style)
                .Select(x => x.Name)
                .FirstOrDefaultAsync();

                var nowCheck = DateTime.UtcNow;
                var checkPhanCong = await _context.EmployeeAssignmentHistory.Where(a => a.EmployeeNo == payload.inspector && (a.ToDate == null || a.ToDate >= nowCheck))
                    .FirstOrDefaultAsync();
                //Mở ở đây
                //if (checkPhanCong == null)
                //{
                //    throw new Exception("Bạn chưa được phân công!");
                //}

                // Nếu có sản phẩm lỗi thì phải có ít nhất một chi tiết lỗi với số lượng > 0
                if (payload.defectiveProductCount > 0 &&
                    (payload.defects == null || !payload.defects.Any(d => d.Quantity > 0)))
                {
                    throw new Exception("Khi có sản phẩm lỗi, phải nhập ít nhất một chi tiết lỗi với số lượng > 0");
                }
                // số lượng thoát chuyền > 0
                if (payload.passOutputQty <= 0 )
                {
                    throw new Exception("Số lượng thoát chuyền phải lớn hơn 0");
                }
                // Nếu không có sản phẩm lỗi thì không được có chi tiết lỗi với số lượng > 0
                if (payload.defectiveProductCount == 0 &&
                    payload.defects != null && payload.defects.Any(d => d.Quantity > 0))
                {
                    throw new Exception("Số sản phẩm lỗi đang là 0 nhưng có chi tiết lỗi với số lượng > 0. Vui lòng kiểm tra lại.");
                }

                //Kiểm tra số lượng nhập có vượt kế hoạch trong ngày không (planned_qty theo work_date = ngày kiểm)
                var todayStart = DateTime.Today;
                var tomorrowStart = todayStart.AddDays(1);

                if (payload.checkType == "1")
                {
                    var plannedQtyToday = await _context.MesFactoryPlanLines
                        .Where(l => l.FactoryPlanId == payload.planId &&
                                    l.WorkDate >= todayStart &&
                                    l.WorkDate < tomorrowStart)
                        .SumAsync(l => (int?)l.PlannedQty) ?? 0;

                    var checkedQtyToday = await _context.qualityManagementSessions
                        .Where(p => p.PlanId == payload.planId && p.Status == 1 && p.OperationType==payload.checkType &&
                                    p.InspectionDate >= todayStart &&
                                    p.InspectionDate < tomorrowStart)
                        .SumAsync(p => (int?)p.PassOutputQuantity) ?? 0;

                    var newCheckedQty = payload.passOutputQty * payload.unitQuantity;
                    var totalCheckedQtyToday = checkedQtyToday + newCheckedQty;

                    if (plannedQtyToday > 0 && totalCheckedQtyToday > plannedQtyToday && !payload.confirmOverPlan)
                    {
                        return new
                        {
                            Success = false,
                            RequireConfirm = true,
                            ConfirmType = "plan",
                            Message = $"Số lượng nhập ({totalCheckedQtyToday}) vượt quá kế hoạch trong ngày ({plannedQtyToday}). Bạn có muốn tiếp tục nhập không?",
                            PlannedQty = plannedQtyToday,
                            CheckedQty = totalCheckedQtyToday
                        };
                    }
                }

                //Kiểm tra tổng số lượng thoát chuyền của đơn hàng có vượt total_planned_qty (MesFactoryPlans) không
                var orderTotalPlannedQty = await _context.MesFactoryPlans
                    .Where(p => p.Id == payload.planId)
                    .Select(p => p.TotalPlannedQty)
                    .FirstOrDefaultAsync();

                if (orderTotalPlannedQty > 0 && !payload.confirmOverOrder)
                {
                    var passedQtyOfOrder = await _context.qualityManagementSessions
                        .Where(p => p.PlanId == payload.planId)
                        .SumAsync(p => (int?)p.PassOutputQuantity) ?? 0;

                    var newPassQty = payload.passOutputQty * payload.unitQuantity - payload.defectiveProductCount;
                    var totalPassQtyOfOrder = passedQtyOfOrder + newPassQty;

                    if (totalPassQtyOfOrder > orderTotalPlannedQty)
                    {
                        return new
                        {
                            Success = false,
                            RequireConfirm = true,
                            ConfirmType = "order",
                            Message = $"Số lượng nhập ({totalPassQtyOfOrder}) vượt quá số lượng của đơn hàng ({orderTotalPlannedQty}). Bạn có muốn tiếp tục nhập không?",
                            PlannedQty = orderTotalPlannedQty,
                            CheckedQty = totalPassQtyOfOrder
                        };
                    }
                }

                var inspectionDate = DateTime.UtcNow; // hoặc DateTime.Now nếu muốn theo local
                try
                {
                    // --- (A) Tạo QualityManagementSession và defect details như hiện tại ---
                    var data = new QualityManagementSession
                    {
                        No = newNo,
                        TimeSlotId = currentSlot.Id,
                        OperationType = payload.checkType,
                        WorkCenterCode = payload.factory,
                        WorkShiftCode = payload.productionTeam,
                        SalesOrderNo = payload.po,
                        ItemNo = payload.style,
                        ColorNo = payload.color,
                        InspectionDate = DateTime.Now,
                        PlanOutputQuantity = (int)payload.planOutputQuantity,
                        CheckedQuantity = payload.passOutputQty * payload.unitQuantity,
                        DefectiveProductCount = payload.defectiveProductCount,
                        PassOutputQuantity = payload.passOutputQty * payload.unitQuantity - payload.defectiveProductCount,
                        ItemName = itemName,
                        Inspector = payload.inspector,
                        Size = payload.size,
                        TargetQtyPerDate = payload.quantity,
                        PlanId = payload.planId
                    };
                    await _context.qualityManagementSessions.AddAsync(data);

                    if (payload.defects != null && payload.defects.Any())
                    {
                        var defectDetails = payload.defects.Select(i => new DefectDetail
                        {
                            QualityManagementCode = newNo,
                            DefectCode = i.Code ?? "",
                            DefectName = i.Name ?? "",
                            Quantity = i.Quantity,
                        }).ToList();
                        await _context.defectDetail.AddRangeAsync(defectDetails);
                    }

                    //series.LastNoUsed += series.IncrementByNo;
                    //_context.noSeriesLineQLCL.Update(series);

                    // Lưu để có data.Id và (nếu cần) reinspection.Id
                    await _context.SaveChangesAsync();
                    foreach (var i in payload.defects)
                    {
                        Console.WriteLine($"Code: {i.Code}, Name: {i.Name}, Qty: {i.Quantity}");
                    }
                    await _context.SaveChangesAsync();

                
                    // Dùng date-range thay vì DateDiffDay/UTC để tránh lệch timezone và lỗi translation
                    // (todayStart / tomorrowStart đã được tính ở bước kiểm tra kế hoạch phía trên)

                    // (a) Tổng từ sessions kiểm thường hôm nay
                    var sessionSums = await _context.qualityManagementSessions
                        .Where(p =>
                            p.WorkCenterCode == data.WorkCenterCode && p.Status==1 && data.OperationType==payload.checkType &&
                            p.WorkShiftCode  == data.WorkShiftCode &&
                            p.Inspector      == data.Inspector &&
                            p.ItemNo         == data.ItemNo &&
                            p.InspectionDate >= todayStart &&
                            p.InspectionDate <  tomorrowStart)
                        .GroupBy(p => 1)
                        .Select(g => new
                        {
                            CheckedQty = g.Sum(x => (int?)x.CheckedQuantity)       ?? 0,
                            PassQty    = g.Sum(x => (int?)x.PassOutputQuantity)     ?? 0,
                            DefectQty  = g.Sum(x => (int?)x.DefectiveProductCount)  ?? 0,
                        })
                        .FirstOrDefaultAsync();

                    // (b) Cộng kết quả reinspection hôm nay — KHÔNG sửa session gốc
                    // SpecifyKind(Unspecified) để tránh lỗi offset khi DateTime.Kind = Local
                    var todayStartOffset    = new DateTimeOffset(DateTime.SpecifyKind(todayStart,    DateTimeKind.Unspecified), TimeSpan.Zero);
                    var tomorrowStartOffset = new DateTimeOffset(DateTime.SpecifyKind(tomorrowStart, DateTimeKind.Unspecified), TimeSpan.Zero);
                    var reSums = await _context.ReinspectionItems
                        .Where(ri =>
                            ri.InspectorNo  == data.Inspector &&
                            ri.Style        == data.ItemNo &&
                            ri.InspectedAt  != null &&
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

                    var checkedTotal = sessionSums?.CheckedQty ?? 0;
                    //var passTotal    = (sessionSums?.PassQty   ?? 0) + (reSums?.RePassQty     ?? 0);
                    //var defectTotal  = Math.Max(0, (sessionSums?.DefectQty ?? 0) - (reSums?.ReResolvedQty ?? 0));
                    var passTotal = (sessionSums?.PassQty ?? 0);
                    var defectTotal = Math.Max(0, (sessionSums?.DefectQty ?? 0)) ;
                    return new
                    {
                        Success = true,
                        Message = "Xác nhận kiểm thành công",
                        DocumentNo = newNo,
                        CheckedQuantity = checkedTotal,
                        PassQuantity = passTotal,
                        DefectQuantity = defectTotal
                    };
                    
                }
                catch (Exception ex)
                {
                    throw new Exception($"Lỗi: {ex.InnerException?.Message ?? ex.Message}");
                }

            }
            catch (Exception ex)
            {
                throw new Exception($"Lỗi: {ex.InnerException?.Message ?? ex.Message}");
            }
        }

        public async Task<UserDTO> GetInforUser(string userID)
            {
                try
                {
                    //var result = await (
                    //    from e in _context.employee
                    //    join wc in _context.Department on e.WorkCenterCode equals wc.Code into wcg
                    //    from wc in wcg.DefaultIfEmpty()
                    //    join ws in _context.locationXSDs on e.WorkShiftCode equals ws.Code into wcf
                    //    from ws in wcf.DefaultIfEmpty()
                    //    where e.No == userID
                    //    select new UserDTO
                    //    {
                    //        Name = e.Name,
                    //        WorkCenterCode = e.WorkCenterCode,
                    //        WorkCenterName = wc.Name,
                    //        WorkShiftCode = e.WorkShiftCode,
                    //        WorkShiftName = ws.Name
                    //    }
                    //    ).FirstOrDefaultAsync();
                    //return result;
                    var user = await (
                        from e in _context.employee
                        join wc in _context.Department on e.WorkCenterCode equals wc.Code into wcg
                        from wc in wcg.DefaultIfEmpty()
                        join ws in _context.locationXSDs on e.WorkShiftCode equals ws.Code into wcf
                        from ws in wcf.DefaultIfEmpty()
                        where e.No == userID || e.LoginId == userID
                        select new UserDTO
                        {
                            Name = e.Name,
                            WorkCenterCode = e.WorkCenterCode,
                            WorkCenterName = wc.Name,
                            WorkShiftCode = e.WorkShiftCode,
                            WorkShiftName = ws.Name
                        }
                        ).FirstOrDefaultAsync();

                    if (user == null) return null;

                    var now = DateTime.UtcNow;

                    // 1) Lấy các delegation còn hiệu lực (Active)
                    var delegations = await _context.QualityInspectorDelegations
                        .Where(d => d.EmployeeNo == userID && d.Status == "Active"
                                //&& (d.IsPermanent
                                //    || (d.ValidFrom <= now && (d.ValidUntil == null || d.ValidUntil >= now)
                                //    ))
                                )
                        .Select(d => new DelegationDTO
                        {
                            Id = d.Id,
                            TargetTeamCode = d.TargetTeamCode,
                            TargetTeamName = d.TargetTeamName,
                            GrantedByNo = d.GrantedByNo,
                            GrantedByName = d.GrantedByName,
                            ValidFrom = d.ValidFrom,
                            ValidUntil = d.ValidUntil,
                            IsPermanent = d.IsPermanent,
                            Status = d.Status
                        })
                        .ToListAsync();

                    user.DelegatedTeams = delegations;

                    // 2) Tìm assigned inspection team ưu tiên:
                    // a) Kiểm tra EmployeeAssignmentHistory (nếu sử dụng)
                    var currentAssignment = await _context.EmployeeAssignmentHistory
                        .Where(a => a.EmployeeNo == userID && (a.ToDate == null || a.ToDate >= now))
                        .OrderByDescending(a => a.FromDate)
                        .Select(a => new { a.TeamCode, a.TeamName, a.InspectionType })
                        .FirstOrDefaultAsync();

                    if (currentAssignment != null)
                    {
                        user.AssignedInspectionTeamCode = currentAssignment.TeamCode;
                        user.AssignedInspectionTeamName = currentAssignment.TeamName;
                        user.AssignedInspectionType = currentAssignment.InspectionType;
                    }
                    else
                    {
                        // b) Nếu có delegation permanent thì lấy team đó
                        var perm = delegations.FirstOrDefault(d => d.IsPermanent);
                        if (perm != null)
                        {
                            user.AssignedInspectionTeamCode = perm.TargetTeamCode;
                            user.AssignedInspectionTeamName = perm.TargetTeamName;
                            user.AssignedInspectionType = perm.InspectionType;
                        }
                        else
                        {
                            user.AssignedInspectionTeamCode = null;
                            user.AssignedInspectionTeamName = null;
                            user.AssignedInspectionType = null;
                        }
                    }

                    return user;
                }
                catch (Exception ex) { throw new Exception($"Lỗi: {ex.InnerException?.Message ?? ex.Message}"); }
            }

            public async Task<List<TeamPlanSummaryDTO>> BaoCaoSLKeHoachVaThoatChuyen(DateOnly? date, DateOnly? from, DateOnly? to, string? factoryId)
            {
                try
                {
                    if (string.IsNullOrEmpty(factoryId))
                        throw new Exception("factoryId không được để trống");

                    // Xử lý thời gian
                    DateTime? fromDate = null;
                    DateTime? toDate = null;

                    if (date.HasValue)
                    {
                        fromDate = date.Value.ToDateTime(TimeOnly.MinValue);
                        toDate = fromDate.Value.AddDays(1);
                    }
                    else if (from.HasValue && to.HasValue)
                    {
                        fromDate = from.Value.ToDateTime(TimeOnly.MinValue);
                        toDate = to.Value.ToDateTime(TimeOnly.MinValue).AddDays(1);
                    }
                    else
                    {
                        throw new Exception("Phải truyền date hoặc from/to");
                    }


                    // 1) Tính tổng thực tế (Thoát chuyền) theo tổ (WorkShiftCode)
                    var actualsQuery = _context.qualityManagementSessions
                        .Where(q => q.InspectionDate >= fromDate && q.InspectionDate < toDate && q.OperationType=="1" && q.Status==1);

                    if (!string.IsNullOrEmpty(factoryId) && factoryId != "all")
                        actualsQuery = actualsQuery.Where(q => q.WorkCenterCode == factoryId);

                    var actuals = await actualsQuery
                        .GroupBy(q => q.WorkShiftCode)
                        .Select(g => new { TeamId = g.Key, ThoatChuyen = g.Sum(x => x.PassOutputQuantity) })
                        .ToListAsync();

                    var fromOffset = new DateTimeOffset(DateTime.SpecifyKind(fromDate.Value, DateTimeKind.Unspecified), TimeSpan.Zero);
                    var toOffset = new DateTimeOffset(DateTime.SpecifyKind(toDate.Value, DateTimeKind.Unspecified), TimeSpan.Zero);

                    var reinspectionActualsQuery =
                        from ri in _context.ReinspectionItems
                        join plan in _context.MesFactoryPlans on ri.PlanId equals plan.Id
                        where ri.Result == "Pass"
                           && ri.InspectedAt != null
                           && ri.InspectedAt.Value >= fromOffset
                           && ri.InspectedAt.Value < toOffset
                        select new { plan.FactoryId, plan.LineNumber, ri.Quantity };

                    if (!string.IsNullOrEmpty(factoryId) && factoryId != "all")
                        reinspectionActualsQuery = reinspectionActualsQuery.Where(x => x.FactoryId == factoryId);

                    var reinspectionActuals = await reinspectionActualsQuery
                        .GroupBy(x => x.LineNumber)
                        .Select(g => new { TeamId = g.Key, ThoatChuyen = g.Sum(x => x.Quantity) })
                        .ToListAsync();

                    var actualMap = actuals
                        .GroupBy(x => x.TeamId)
                        .ToDictionary(g => g.Key, g => g.Sum(x => x.ThoatChuyen));

                    foreach (var re in reinspectionActuals)
                    {
                        if (string.IsNullOrWhiteSpace(re.TeamId)) continue;
                        actualMap[re.TeamId] = (actualMap.TryGetValue(re.TeamId, out var current) ? current : 0) + re.ThoatChuyen;
                    }

                //var plansQuery = _context.ProductionPlans
                //    .Where(p => p.ActualStartingDate.HasValue
                //     && p.ActualStartingDate >= fromDate
                //     && p.ActualStartingDate < toDate);

                //if (!string.IsNullOrEmpty(factoryId) && factoryId != "all")
                //    plansQuery = plansQuery.Where(p => p.WorkCenterCode == factoryId);
                // ── Kế hoạch từ mes.factory_plans + mes.factory_plan_lines ──
                var plansQuery =
                    from line in _context.MesFactoryPlanLines
                    join plan in _context.MesFactoryPlans on line.FactoryPlanId equals plan.Id
                    where line.WorkDate >= fromDate && line.WorkDate < toDate && plan.IsActive == true
                    select new { plan.FactoryId, plan.LineNumber,line.PlannedQty };

                if (!string.IsNullOrEmpty(factoryId) && factoryId != "all")
                    plansQuery = plansQuery.Where(x => x.FactoryId == factoryId);

                // Sum(PlannedQty) trực tiếp theo tổ — không cần Max trung gian, không cần khử trùng
                var plans = await plansQuery
                    .GroupBy(x => x.LineNumber)
                    .Select(g => new { TeamId = g.Key, KeHoach = (int?)g.Sum(x => x.PlannedQty) })
                    .ToListAsync();
            
                //var intermediate = await plansQuery
                //    .GroupBy(p => new { p.LineNumber, p.StyleId })
                //    .Select(g => new
                //    {
                //        TeamId = g.Key.LineNumber,
                //        MaxQty = g.Max(x => x.TargetQuantityPerDay)
                //    })
                //    .ToListAsync();

                //// Bước 2 (in-memory): Sum các max của từng style → KeHoach của tổ
                //var plans = intermediate
                //    .GroupBy(x => x.TeamId)
                //    .Select(g => new { TeamId = g.Key, KeHoach = g.Sum(x => x.MaxQty) })
                //    .ToList();

                // 3) Lấy tên tổ từ LocationXSDs
                var teamCodes = actualMap.Keys
                    .Union(plans.Select(p => p.TeamId))
                    .Where(c => c != null)
                    .Distinct()
                    .ToList();
                    var teamNames = await _context.locationXSDs
                        .Where(ws => teamCodes.Contains(ws.Code))
                        .Select(ws => new { ws.Code, ws.Name })
                        .ToListAsync();

                    // 4) Kết hợp kết quả
                    var joined = teamCodes
                        .Select(code => new TeamPlanSummaryDTO
                        {
                            TeamId = code,
                            TeamName = teamNames.FirstOrDefault(t => t.Code == code)?.Name ?? string.Empty,
                            KeHoach = plans.FirstOrDefault(p => p.TeamId == code)?.KeHoach ?? 0,
                            ThoatChuyen = actualMap.TryGetValue(code, out var actualQty) ? actualQty : 0,
                        })
                        .Select(x =>
                        {
                            var ke = x.KeHoach ?? 0;
                            var thoat = x.ThoatChuyen ?? 0;
                            x.CompletionRate = ke == 0 ? 0 : Math.Round(thoat * 100.0m / ke, 2);
                            return x;
                        })
                        .OrderBy(x => x.TeamId)
                        .ToList();

                    return joined;
                }
                catch (Exception ex)
                {
                    throw new Exception($"Lỗi: {ex.InnerException?.Message ?? ex.Message}");
                }
            }
        public async Task<List<HourlyOutputDTO>> SanLuongThoatChuyenTheoGio(string teamId, DateOnly? date, DateOnly? from, DateOnly? to, string? factoryId)
            {
                DateTime fromDate, toDate;

                if (date.HasValue)
                {
                    fromDate = date.Value.ToDateTime(TimeOnly.MinValue);
                    toDate = fromDate.AddDays(1);
                }
                else
                {
                    fromDate = from!.Value.ToDateTime(TimeOnly.MinValue);
                    toDate = to!.Value.ToDateTime(TimeOnly.MinValue).AddDays(1);
                }

                var sql = @"
                SELECT 
                    ts.[Name]                        AS TimeSlot,
                    DATEPART(HOUR, ts.[StartTime])   AS Hour,
                    SUM(qm.[Pass Output Quantity])   AS ThoatChuyen
                FROM [QLCL_QualityManagementSession] qm
                INNER JOIN [QLCL_TimeSlot] ts ON qm.[TimeSlotId] = ts.[Id]
                WHERE qm.[Operation Type]=1 and
                    qm.[Work Shift Code] = @teamId
                    AND (@factoryId IS NULL OR qm.[Work Center Code] = @factoryId)
                    AND qm.[InspectionDate] >= @fromDate
                    AND qm.[InspectionDate] < @toDate 
                GROUP BY ts.[Id], ts.[Name], ts.[StartTime], ts.[Sequence]
                ORDER BY ts.[Sequence], ts.[StartTime]
            ";

                return await _context.Set<HourlyOutputDTO>()
                    .FromSqlRaw(sql,
                        new SqlParameter("@teamId", teamId),

                        new SqlParameter("@factoryId", string.IsNullOrEmpty(factoryId) ? (object)DBNull.Value : factoryId),
                        new SqlParameter("@fromDate", fromDate),
                        new SqlParameter("@toDate", toDate)
                    )
                    .ToListAsync();
            }
            public async Task<List<TeamQCStatsItem>> GetTeamQCStatsAsync(DateOnly dateFrom, DateOnly dateTo, string? factoryId, string? operationType = null)
            {
                // ── Bạn chỉnh query này theo bảng thực tế của mình ──
                var query = _context.qualityManagementSessions          // tên bảng của bạn
                    .Where(q => DateOnly.FromDateTime(q.InspectionDate) >= dateFrom && q.Status==1
                             && DateOnly.FromDateTime(q.InspectionDate) <= dateTo);

                if (!string.IsNullOrEmpty(factoryId) && factoryId != "all")
                    query = query.Where(q => q.WorkCenterCode == factoryId);

                if (!string.IsNullOrEmpty(operationType) && operationType != "all")
                    query = query.Where(q => q.OperationType == operationType);

                var result = await query
                    .Join(_context.locationXSDs, q => q.WorkShiftCode, ws => ws.Code, (q, ws) => new { q, ws })
                    .GroupBy(x => new { x.q.WorkShiftCode, x.ws.Name })
                    .Select(g => new TeamQCStatsItem
                    {
                        TeamId = g.Key.WorkShiftCode,
                        Team = g.Key.Name,
                        TotalInspected = g.Sum(e => e.q.CheckedQuantity),   // tổng kiểm
                        TotalDefects = g.Sum(e => e.q.DefectiveProductCount),       // sản phẩm lỗi
                        TotalEscaped = g.Sum(e => e.q.PassOutputQuantity),      // thoát chuyền
                    })
                    .OrderBy(x => x.TeamId)
                    .ToListAsync();

                return result;
            }

            public async Task<IEnumerable<DefectSummaryItemDto>> GetDefectSummaryAsync(DateTime dateFrom, DateTime dateTo, string? factoryId, string? teamId = null, string? operationType = null)
            {
                // --- current period counts per defect type ---
                var currentQ =
               from a in _context.defectDetail
               join b in _context.qualityManagementSessions
                   on a.QualityManagementCode equals b.No
               where b.InspectionDate >= dateFrom
                  && b.InspectionDate < dateTo.AddDays(1) && b.Status==1
               select new { a, b };

                if (!string.IsNullOrEmpty(factoryId))
                    currentQ = currentQ.Where(x => x.b.WorkCenterCode == factoryId);

                if (!string.IsNullOrEmpty(teamId) && teamId != "all")
                    currentQ = currentQ.Where(x => x.b.WorkShiftCode == teamId);

                if (!string.IsNullOrEmpty(operationType) && operationType != "all")
                    currentQ = currentQ.Where(x => x.b.OperationType == operationType);

                var current = await currentQ
                    .GroupBy(x => new { x.a.DefectCode, x.a.DefectName })
                    .Select(g => new
                    {
                        DefectTypeId = g.Key.DefectCode,
                        DefectTypeName = g.Key.DefectName,
                        Count = g.Sum(x => x.a.Quantity)
                    })
                    .OrderByDescending(g => g.Count)
                    .ToListAsync();

                if (current.Count == 0)
                    return Enumerable.Empty<DefectSummaryItemDto>();

                int totalCount = current.Sum(x => x.Count);
                int range = (dateTo - dateFrom).Days + 1;

                // --- previous period (same length, for trend) ---
                var prevFrom = dateFrom.AddDays(-range);
                var prevTo = dateFrom.AddDays(-1);

                var prevQ =
                    from a in _context.defectDetail
                    join b in _context.qualityManagementSessions
                        on a.QualityManagementCode equals b.No
                    where b.InspectionDate >= prevFrom
                       && b.InspectionDate < prevTo.AddDays(1) && b.Status==1
                    select new { a, b };

                if (!string.IsNullOrEmpty(factoryId))
                    prevQ = prevQ.Where(x => x.b.WorkCenterCode == factoryId);

                if (!string.IsNullOrEmpty(teamId) && teamId != "all")
                    prevQ = prevQ.Where(x => x.b.WorkShiftCode == teamId);

                if (!string.IsNullOrEmpty(operationType) && operationType != "all")
                    prevQ = prevQ.Where(x => x.b.OperationType == operationType);

                var prev = await prevQ
                    .GroupBy(x => x.a.DefectCode)
                    .Select(g => new { DefectCode = g.Key, Count = g.Sum(x => x.a.Quantity) })
                    .ToDictionaryAsync(g => g.DefectCode, g => g.Count);

                // --- assemble result (unchanged) ---
                return current.Select((item, idx) =>
                {
                    prev.TryGetValue(item.DefectTypeId, out int prevCount);
                    string trend = prevCount == 0 ? "stable"
                                 : item.Count > prevCount ? "up"
                                 : item.Count < prevCount ? "down"
                                 : "stable";

                    double pct = totalCount > 0
                                 ? Math.Round((double)item.Count / totalCount * 100, 1) : 0;
                    string sev = pct >= 20 ? "high" : pct >= 10 ? "medium" : "low";

                    return new DefectSummaryItemDto
                    {
                        Id = idx + 1,
                        Name = item.DefectTypeName,
                        Count = item.Count,
                        Percentage = pct,
                        Trend = trend,
                        Severity = sev,
                    };
                });
            }
            public async Task<IEnumerable<TeamHourlyQCItemDto>> GetTeamHourlyQCAsync(string teamId, DateTime dateFrom, DateTime dateTo, string? factoryId, string? operationType = null)
            {
                var sessionQuery = _context.qualityManagementSessions
                    .Where(b => b.WorkShiftCode == teamId
                             && b.InspectionDate >= dateFrom && b.Status==1
                             && b.InspectionDate < dateTo.AddDays(1));

                if (!string.IsNullOrEmpty(operationType) && operationType != "all")
                    sessionQuery = sessionQuery.Where(b => b.OperationType == operationType);

                var rows = await (
            from b in sessionQuery
            join ts in _context.TimeSlots on b.TimeSlotId equals ts.Id
            group new { b, ts } by new { ts.Id, ts.Name, ts.StartTime } into g
            orderby g.Key.StartTime
            select new TeamHourlyQCItemDto
            {
                Hour = g.Key.Name,                    // "7h30-8h30"
                Inspected = g.Sum(x => x.b.CheckedQuantity),
                Defects = g.Sum(x => x.b.DefectiveProductCount),
            }
        ).ToListAsync();
                return rows;
            }
            public async Task<IEnumerable<TeamDefectBreakdownItemDto>> GetTeamDefectBreakdownAsync(string teamId, DateTime dateFrom, DateTime dateTo, string? factoryId, string? operationType = null)
            {
                var sessionQuery = _context.qualityManagementSessions
                    .Where(b => b.WorkShiftCode == teamId
                             && b.InspectionDate >= dateFrom && b.Status==1
                             && b.InspectionDate < dateTo.AddDays(1));

                if (!string.IsNullOrEmpty(operationType) && operationType != "all")
                    sessionQuery = sessionQuery.Where(b => b.OperationType == operationType);

                var rows = await (
                    from a in _context.defectDetail
                    join b in sessionQuery on a.QualityManagementCode equals b.No
                    group a by new { a.DefectCode, a.DefectName } into g
                    orderby g.Sum(x => x.Quantity) descending
                    select new { g.Key.DefectName, Count = g.Sum(x => x.Quantity) }
                ).ToListAsync();

                if (rows.Count == 0) return Enumerable.Empty<TeamDefectBreakdownItemDto>();

                int total = rows.Sum(r => r.Count);
                return rows.Select(r =>
                {
                    double pct = total > 0 ? Math.Round((double)r.Count / total * 100, 1) : 0;
                    return new TeamDefectBreakdownItemDto
                    {
                        Name = r.DefectName,
                        Count = r.Count,
                        Percentage = pct,
                        Severity = pct >= 20 ? "high" : pct >= 10 ? "medium" : "low",
                    };
                });
            }

            /// <summary>
            /// Diễn biến kiểm/lỗi theo từng khung giờ — tổng hợp toàn nhà máy hoặc lọc theo tổ.
            /// Dùng cho chart "Diễn biến lỗi theo giờ" trên overview chất lượng.
            /// </summary>
            public async Task<IEnumerable<HourlyDefectTrendItemDto>> GetHourlyDefectTrendAsync(
                DateTime dateFrom, DateTime dateTo,
                string? factoryId = null, string? teamId = null, string? operationType = null)
            {
                var sessionQuery = _context.qualityManagementSessions
                    .Where(b => b.InspectionDate >= dateFrom && b.Status==1
                             && b.InspectionDate < dateTo.AddDays(1));

                if (!string.IsNullOrEmpty(factoryId) && factoryId != "all")
                    sessionQuery = sessionQuery.Where(b => b.WorkCenterCode == factoryId);

                if (!string.IsNullOrEmpty(teamId) && teamId != "all")
                    sessionQuery = sessionQuery.Where(b => b.WorkShiftCode == teamId);

                if (!string.IsNullOrEmpty(operationType) && operationType != "all")
                    sessionQuery = sessionQuery.Where(b => b.OperationType == operationType);

                var rows = await (
                    from b in sessionQuery
                    join ts in _context.TimeSlots on b.TimeSlotId equals ts.Id
                    group new { b, ts } by new { ts.Id, ts.Name, ts.StartTime } into g
                    orderby g.Key.StartTime
                    select new
                    {
                        Hour      = g.Key.Name,
                        Inspected = g.Sum(x => x.b.CheckedQuantity),
                        Defects   = g.Sum(x => x.b.DefectiveProductCount),
                    }
                ).ToListAsync();

                return rows.Select(r => new HourlyDefectTrendItemDto
                {
                    Hour       = r.Hour,
                    Inspected  = r.Inspected,
                    Defects    = r.Defects,
                    DefectRate = r.Inspected > 0
                        ? Math.Round((double)r.Defects / r.Inspected * 100, 2)
                        : 0,
                });
            }

            public async Task<IEnumerable<ReplacementItemDto>> GetReplacementItemsAsync()
            {
                var query = new List<ReplacementItemDto>();
                query = await _context.GarmentParts.Select(x => new ReplacementItemDto
                {
                    Code = x.PartID,
                    Name = x.PartName
                }).ToListAsync();
                return query;
            }
            public async Task<object> SendCuttingSupplyRequestAsync(CuttingSupplyRequestDto dto)
            {
                if (dto == null)
                    throw new ArgumentNullException(nameof(dto));
                var production = await _context.ProductionPlans.Where(e => e.ItemNo == dto.Style && e.SalesOrderNo == dto.Po).Select(e => new { e.ItemName, e.POName }).Distinct().FirstOrDefaultAsync();
                if (production == null)
                {
                    throw new Exception($"Không tìm thấy ProductionPlan với Style = {dto.Style} và PO = {dto.Po}");
                }
                var entity = new CuttingSupplyRequest
                {
                    WorkCenterCode = dto.Factory.Trim(),
                    WorkShiftCode = dto.ProductionTeam.Trim(),
                    ItemNo = dto.Style.Trim(),
                    Style = production.ItemName,
                    SalesOrderNo = dto.Po.Trim(),
                    PoName = production.POName,
                    Color = dto.Color.Trim(),
                    Size = string.IsNullOrWhiteSpace(dto.Size) ? null : dto.Size.Trim(),
                    UnreparableCount = dto.UnreparableCount,
                    Requester = dto.RequestedBy.Trim(),
                    SourceType     = string.IsNullOrWhiteSpace(dto.SourceType)  ? null : dto.SourceType.Trim(),
                    ReinspectionId = dto.ReinspectionId,
                    SessionNo      = string.IsNullOrWhiteSpace(dto.SessionNo)   ? null : dto.SessionNo.Trim(),
                    CreatedAt = DateTime.UtcNow,
                    Status = 0,
                    Items = dto.Items.Select(i => new CuttingSupplyRequestItems
                    {
                        Name = i.Name.Trim(),
                        Quantity = i.Qty
                    }).ToList(),
                    Defects = dto.DefectTypes.Select(x => new CuttingSupplyRequestDefects
                    {
                        DefectCode = x.DefectCode,
                        DefectName = x.DefectName,
                        Qty = x.Qty,
                    }).ToList()
                };

                _context.CuttingSupplyRequests.Add(entity);
                await _context.SaveChangesAsync();

                return new
                {
                    Success = true,
                    Message = "Gửi yêu cầu bổ sung thành công",
                }; ;
            }
            public async Task<IEnumerable<DefectsDTO>> GetDefectsNotRework()
            {
                var query = new List<DefectsDTO>();
                query = await _context.Defects.Select(x => new DefectsDTO
                {
                    Code = x.Code,
                    Name = x.Name
                }).ToListAsync();
                return query;
            }
            public async Task<List<CuttingSupplyRequestListItemDto>> GetCuttingSupplyRequestsAsync(int? status, DateTime? dateFrom, DateTime? dateTo)
            {
                var query = _context.CuttingSupplyRequests
                    .Include(r => r.Items)
                    .Include(r => r.Defects)
                    .AsQueryable();

                if (status.HasValue)
                    query = query.Where(r => r.Status == status.Value);

                if (dateFrom.HasValue)
                    query = query.Where(r => r.CreatedAt >= dateFrom.Value.Date);

                if (dateTo.HasValue)
                    query = query.Where(r => r.CreatedAt < dateTo.Value.Date.AddDays(1));

                return await query
                    .OrderByDescending(r => r.CreatedAt)
                    .Select(r => new CuttingSupplyRequestListItemDto
                    {
                        Id = r.RowID,
                        WorkCenterCode = r.WorkCenterCode,
                        WorkShiftCode = r.WorkShiftCode,
                        ItemNo = r.ItemNo,
                        Style = r.Style,
                        SalesOrderNo = r.SalesOrderNo,
                        PoName = r.PoName,
                        Color = r.Color,
                        Size = r.Size,
                        UnreparableCount = r.UnreparableCount,
                        Requester = r.Requester,
                        CreatedAt = r.CreatedAt,
                        UpdatedAt = r.UpdatedAt,
                        Status = r.Status,
                        ReceivedBy = r.ReceivedBy,
                        ReceivedAt = r.ReceivedAt,
                        Items = r.Items.Select(i => new CuttingSupplyRequestItemDto
                        {
                            Name = i.Name,
                            Qty = i.Quantity
                        }).ToList(),
                        DefectTypes = r.Defects.Select(d => new CuttingSupplyRequestDefectDto
                        {
                            DefectCode = d.DefectCode,
                            DefectName = d.DefectName,
                            Qty = d.Qty
                        }).ToList()
                    })
                    .ToListAsync();
            }
            public async Task<object> UpdateCuttingSupplyStatusAsync(int id, UpdateCuttingSupplyStatusDto dto)
            {
                var request = await _context.CuttingSupplyRequests
                    .Include(r => r.Items)
                    .FirstOrDefaultAsync(r => r.RowID == id);

                if (request == null)
                    return new { Success = false, Message = "Không tìm thấy yêu cầu" };

                // Kiểm tra chuyển trạng thái hợp lệ
                var validTransitions = new Dictionary<int, int[]>
    {
        { 0, new[] { 1, 3 } },   // Pending → Processing hoặc Rejected
        { 1, new[] { 2, 3 } },   // Processing → Done hoặc Rejected
    };

                if (!validTransitions.TryGetValue(request.Status, out var allowed) || !allowed.Contains(dto.Status))
                    return new { Success = false, Message = "Chuyển trạng thái không hợp lệ" };

                request.Status = dto.Status;
                request.Note = dto.Note;
                request.UpdatedAt = DateTime.Now;

                switch (dto.Status)
                {
                    case 1: // Processing — Tiếp nhận
                        request.ReceivedBy = dto.ActionBy;
                        request.ReceivedAt = DateTime.Now;
                        break;

                    case 2: // Done — Giao hàng
                        request.DeliveredBy = dto.ActionBy;
                        request.DeliveredAt = DateTime.Now;

                        if (dto.ActualItems?.Any() == true)
                        {
                            foreach (var actual in dto.ActualItems)
                            {
                                var item = request.Items.FirstOrDefault(i => i.Name == actual.Name);
                                if (item != null)
                                    item.Quantity = actual.ActualQty;
                            }
                        }
                        break;
                }

                await _context.SaveChangesAsync();
                return new { Success = true, Message = "Cập nhật trạng thái thành công" };
            }
            public async Task<List<StyleDefectSummaryDto>> GetStyleDefectSummaryAsync(string? date, string? from, string? to, string? factoryId, string? teamId, string? operationType = null)
            {
                // ── 1. Xử lý ngày ───────────────────────────────
                DateTime dateFrom, dateTo;

                if (!string.IsNullOrWhiteSpace(date))
                {
                    if (!DateTime.TryParse(date, out dateFrom))
                        throw new Exception("Tham số 'date' không hợp lệ (yyyy-MM-dd).");

                    dateTo = dateFrom;
                }
                else if (!string.IsNullOrWhiteSpace(from) && !string.IsNullOrWhiteSpace(to))
                {
                    if (!DateTime.TryParse(from, out dateFrom) ||
                        !DateTime.TryParse(to, out dateTo))
                        throw new Exception("Tham số 'from' hoặc 'to' không hợp lệ (yyyy-MM-dd).");

                    if (dateFrom > dateTo)
                        throw new Exception("'from' phải nhỏ hơn hoặc bằng 'to'.");
                }
                else
                {
                    dateFrom = dateTo = DateTime.Today;
                }

                var dateFromDate = dateFrom.Date;
                var dateToDate = dateTo.Date;

                // ── 2. Query ───────────────────────────────
                var query = _context.qualityManagementSessions
                    .Where(c =>
                        c.InspectionDate.Date >= dateFromDate && c.Status==1&
                        c.InspectionDate.Date <= dateToDate);

                if (!string.IsNullOrWhiteSpace(factoryId) &&
                    !string.Equals(factoryId, "all", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(c => c.WorkCenterCode == factoryId);
                }
                if (!string.IsNullOrWhiteSpace(teamId) &&
                    !string.Equals(teamId, "all", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(c => c.WorkShiftCode == teamId);
                }
                if (!string.IsNullOrWhiteSpace(operationType) &&
                    !string.Equals(operationType, "all", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(c => c.OperationType == operationType);
                }
                // ── 3. Group ───────────────────────────────
                var grouped = await query
                    .GroupBy(c => new { c.ItemNo, c.ItemName })
                    .Select(g => new
                    {
                        Style = g.Key.ItemNo,
                        StyleName = g.Key.ItemName,
                        TotalDefects = g.Sum(c => (int?)c.DefectiveProductCount) ?? 0,
                        TotalInspected = g.Sum(c => (int?)c.CheckedQuantity) ?? 0,
                    })
                    .Where(g => g.TotalInspected > 0)
                    .OrderByDescending(g => g.TotalDefects)
                    .ToListAsync();

                // ── 4. Map DTO ───────────────────────────────
                return grouped.Select(g => new StyleDefectSummaryDto
                {
                    Style = g.Style,
                    StyleName = g.StyleName,
                    TotalDefects = g.TotalDefects,
                    DefectRate = Math.Round(
                        (double)g.TotalDefects / g.TotalInspected * 100, 2)
                }).ToList();
            }
            public async Task<List<StyleDefectBreakdownDto>> GetStyleDefectBreakdownAsync(string styleCode, string? date, string? from, string? to, string? factoryId, string? teamId, string? operationType = null)
            {
                // ── 1. Validate styleCode ────────────────────────────────────────
                if (string.IsNullOrWhiteSpace(styleCode))
                    throw new Exception("Tham số 'styleCode' là bắt buộc.");

                // ── 2. Xác định khoảng ngày ──────────────────────────────────────
                DateTime dateFrom, dateTo;

                if (!string.IsNullOrWhiteSpace(date))
                {
                    if (!DateTime.TryParse(date, out dateFrom))
                        throw new Exception("Tham số 'date' không hợp lệ (yyyy-MM-dd).");
                    dateTo = dateFrom;
                }
                else if (!string.IsNullOrWhiteSpace(from) && !string.IsNullOrWhiteSpace(to))
                {
                    if (!DateTime.TryParse(from, out dateFrom) || !DateTime.TryParse(to, out dateTo))
                        throw new Exception("Tham số 'from' hoặc 'to' không hợp lệ (yyyy-MM-dd).");

                    if (dateFrom > dateTo)
                        throw new Exception("'from' phải nhỏ hơn hoặc bằng 'to'.");
                }
                else
                {
                    // Không truyền ngày → dùng hôm nay
                    dateFrom = dateTo = DateTime.Today;
                }

                var dateFromDate = dateFrom.Date;
                var dateToDate = dateTo.Date;

                // ── 3. Xây dựng query lọc bảng cha ──────────────────────────────
                var checksQuery = _context.qualityManagementSessions
                    .Where(c =>
                        c.ItemNo == styleCode && c.Status==1&&
                        c.InspectionDate.Date >= dateFromDate &&
                        c.InspectionDate.Date <= dateToDate);

                bool hasFactory = !string.IsNullOrWhiteSpace(factoryId) &&
                                  !string.Equals(factoryId, "all", StringComparison.OrdinalIgnoreCase);
                bool hasTeam = !string.IsNullOrWhiteSpace(teamId) &&
                                  !string.Equals(teamId, "all", StringComparison.OrdinalIgnoreCase);
                if (hasFactory)
                    checksQuery = checksQuery.Where(c => c.WorkCenterCode == factoryId);
                if (hasTeam)
                    checksQuery = checksQuery.Where(c => c.WorkShiftCode == teamId);
                if (!string.IsNullOrWhiteSpace(operationType) &&
                    !string.Equals(operationType, "all", StringComparison.OrdinalIgnoreCase))
                    checksQuery = checksQuery.Where(c => c.OperationType == operationType);
                // ── 4. Join với bảng con, group theo loại lỗi ───────────────────
                var defectRows = await checksQuery
                    .SelectMany(c => c.Defects)                    // navigation property
                    .GroupBy(d => new { d.DefectCode, d.DefectName })
                    .Select(g => new
                    {
                        DefectCode = g.Key.DefectCode,
                        DefectName = g.Key.DefectName,
                        Count = g.Sum(d => (int?)d.Quantity) ?? 0,
                    })
                    .Where(g => g.Count > 0)
                    .OrderByDescending(g => g.Count)
                    .ToListAsync();

                if (defectRows.Count == 0)
                    return new List<StyleDefectBreakdownDto>();

                // ── 5. Tính % và phân loại mức độ ────────────────────────────────
                int totalDefects = defectRows.Sum(d => d.Count);

                return defectRows.Select(d =>
                {
                    double pct = totalDefects > 0
                        ? Math.Round((double)d.Count / totalDefects * 100, 2)
                        : 0;

                    string severity = pct >= 30 ? "high"
                                    : pct >= 10 ? "medium"
                                    : "low";

                    return new StyleDefectBreakdownDto
                    {
                        DefectCode = d.DefectCode,
                        DefectName = d.DefectName,
                        Count = d.Count,
                        Percentage = pct,
                        Severity = severity
                    };
                }).ToList();
            }
            public async Task<IEnumerable<DailyOutputDto>> GetDailyOutputAsync(string dateFrom, string dateTo, string? factoryId = null)
            {
                var supportedFormats = new[] { "yyyy-MM-dd", "dd/MM/yyyy", "MM/dd/yyyy", "yyyy/MM/dd" };

                if (!DateOnly.TryParseExact(dateFrom, supportedFormats,
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out var from))
                    throw new ArgumentException($"Ngày không hợp lệ: '{dateFrom}'", nameof(dateFrom));

                if (!DateOnly.TryParseExact(dateTo, supportedFormats,
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out var to))
                    throw new ArgumentException($"Ngày không hợp lệ: '{dateTo}'", nameof(dateTo));

                if (from > to)
                    throw new ArgumentException("dateFrom không được lớn hơn dateTo.");

                var fromDt = new DateTime(from.Year, from.Month, from.Day, 0, 0, 0);
                var toDt = new DateTime(to.Year, to.Month, to.Day, 23, 59, 59);

                // 1) Tổng thực tế (thoát chuyền) theo ngày — chỉ lấy Endline (OperationType = 1)
                var actualQuery = _context.qualityManagementSessions
                    .Where(p => p.InspectionDate >= fromDt && p.InspectionDate <= toDt && p.Status==1
                             && p.OperationType == "1");

                if (!string.IsNullOrWhiteSpace(factoryId) && !string.Equals(factoryId, "all", StringComparison.OrdinalIgnoreCase))
                    actualQuery = actualQuery.Where(p => p.WorkCenterCode == factoryId);

                var actuals = await actualQuery
                    .GroupBy(p => p.InspectionDate.Date)
                    .Select(g => new { Date = g.Key, TotalOutput = g.Sum(p => p.PassOutputQuantity) })
                    .ToListAsync();

                var actualDict = actuals.ToDictionary(x => x.Date, x => x.TotalOutput);

                var fromOffset = new DateTimeOffset(DateTime.SpecifyKind(fromDt, DateTimeKind.Unspecified), TimeSpan.Zero);
                var toOffset = new DateTimeOffset(DateTime.SpecifyKind(toDt, DateTimeKind.Unspecified), TimeSpan.Zero);

                var reinspectionQuery =
                    from ri in _context.ReinspectionItems
                    join plan in _context.MesFactoryPlans on ri.PlanId equals plan.Id
                    where ri.Result == "Pass"
                       && ri.InspectedAt != null
                       && ri.InspectedAt.Value >= fromOffset
                       && ri.InspectedAt.Value <= toOffset
                    select new { plan.FactoryId, ri.InspectedAt, ri.Quantity };

                if (!string.IsNullOrWhiteSpace(factoryId) && !string.Equals(factoryId, "all", StringComparison.OrdinalIgnoreCase))
                    reinspectionQuery = reinspectionQuery.Where(x => x.FactoryId == factoryId);

                var reinspectionRows = await reinspectionQuery.ToListAsync();

                var reinspectionByDay = reinspectionRows
                    .GroupBy(x => x.InspectedAt!.Value.Date)
                    .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

                foreach (var (date, qty) in reinspectionByDay)
                    actualDict[date] = (actualDict.TryGetValue(date, out var current) ? current : 0) + qty;

            // 2) Kế hoạch lấy từ mes.factory_plans + mes.factory_plan_lines
            // factory_plans: factory_id (≈ WorkCenterCode), style_id (≈ ItemNo)
            // factory_plan_lines: planned_qty theo ngày (created_at ≈ ActualStartingDate), workcenter_plan_id -> factory_plans.id
            var planQuery =
                from line in _context.MesFactoryPlanLines
                join plan in _context.MesFactoryPlans on line.FactoryPlanId equals plan.Id
                where line.WorkDate.Date >= fromDt.Date
                   && line.WorkDate.Date <= toDt.Date && plan.IsActive == true
                select new { line, plan };

            if (!string.IsNullOrWhiteSpace(factoryId) && !string.Equals(factoryId, "all", StringComparison.OrdinalIgnoreCase))
                planQuery = planQuery.Where(x => x.plan.FactoryId == factoryId);

            // Sum(planned_qty) trực tiếp theo ngày — không cần khử trùng
            var plans = await planQuery
                .GroupBy(x => x.line.WorkDate.Date)
                .Select(g => new { Date = g.Key, TotalQty = g.Sum(x => x.line.PlannedQty) })
                .ToListAsync();

            var planDict = plans.ToDictionary(x => x.Date, x => (decimal?)x.TotalQty);

            // 3) Kết hợp các ngày trong khoảng (bao gồm ngày không có dữ liệu)
            var days = new List<DateTime>();
                for (var d = fromDt.Date; d <= toDt.Date; d = d.AddDays(1))
                {
                    days.Add(d);
                }

                var result = days
                    .Select(d => new DailyOutputDto
                    {
                        Date = d.ToString("yyyy-MM-dd"),
                        TotalOutput = actualDict.TryGetValue(d, out var outVal) ? outVal : 0,
                        KeHoach = (int)(planDict.TryGetValue(d, out var planVal) ? (planVal ?? 0) : 0)
                    })
                    .OrderBy(x => x.Date)
                    .ToList();

                return result;
            }
            public async Task<List<StyleOutputDto>> GetOutputByStyleAsync(string dateFrom, string dateTo, string? factoryId)
            {
                var from = DateTime.Parse(dateFrom);
                var to = DateTime.Parse(dateTo).AddDays(1).AddTicks(-1); // cuối ngày dateTo

                // Query tổng sản lượng theo style
                var query = _context.qualityManagementSessions                  // tên DbSet thực tế
                    .Where(o => o.InspectionDate >= from && o.InspectionDate <= to && o.Status==1);

                if (!string.IsNullOrEmpty(factoryId))
                    query = query.Where(o => o.WorkCenterCode == factoryId);

                var grouped = await query
                    .GroupBy(o => new { o.ItemNo, o.ItemName }) // cột thực tế trong DB
                    .Select(g => new
                    {
                        g.Key.ItemNo,
                        g.Key.ItemName,
                        Output = g.Sum(o => o.PassOutputQuantity),            // cột số lượng thực tế
                    })
                    .OrderByDescending(x => x.Output)
                    .ToListAsync();

                var fromOffset = new DateTimeOffset(DateTime.SpecifyKind(from, DateTimeKind.Unspecified), TimeSpan.Zero);
                var toOffset = new DateTimeOffset(DateTime.SpecifyKind(to, DateTimeKind.Unspecified), TimeSpan.Zero);

                var reinspectionQuery =
                    from ri in _context.ReinspectionItems
                    join plan in _context.MesFactoryPlans on ri.PlanId equals plan.Id
                    where ri.Result == "Pass"
                       && ri.InspectedAt != null
                       && ri.InspectedAt.Value >= fromOffset
                       && ri.InspectedAt.Value <= toOffset
                    select new { plan.FactoryId, StyleCode = ri.Style, ri.Quantity };

                if (!string.IsNullOrWhiteSpace(factoryId) && !string.Equals(factoryId, "all", StringComparison.OrdinalIgnoreCase))
                    reinspectionQuery = reinspectionQuery.Where(x => x.FactoryId == factoryId);

                var reinspectionByStyle = await reinspectionQuery
                    .Where(x => x.StyleCode != null && x.StyleCode != "")
                    .GroupBy(x => x.StyleCode!)
                    .Select(g => new { StyleCode = g.Key, Output = g.Sum(x => x.Quantity) })
                    .ToListAsync();

                var styleMap = grouped.ToDictionary(
                    x => x.ItemNo ?? string.Empty,
                    x => new StyleOutputDto
                    {
                        StyleCode = x.ItemNo,
                        StyleName = x.ItemName,
                        Output = x.Output,
                        Percentage = 0
                    },
                    StringComparer.OrdinalIgnoreCase);

                foreach (var re in reinspectionByStyle)
                {
                    if (!styleMap.TryGetValue(re.StyleCode, out var existing))
                    {
                        styleMap[re.StyleCode] = new StyleOutputDto
                        {
                            StyleCode = re.StyleCode,
                            StyleName = re.StyleCode,
                            Output = re.Output,
                            Percentage = 0
                        };
                        continue;
                    }

                    existing.Output += re.Output;
                }

                var merged = styleMap.Values
                    .OrderByDescending(x => x.Output)
                    .ToList();
                var totalOutput = merged.Sum(x => x.Output);

                return merged.Select(x => new StyleOutputDto
                {
                    StyleCode = x.StyleCode,
                    StyleName = x.StyleName,
                    Output = x.Output,
                    Percentage = totalOutput > 0
                        ? Math.Round((double)x.Output / totalOutput * 100, 2)
                        : 0,
                }).ToList();
            }

            public async Task<EmployeeDto> GetByUserNameAsync(string userName)
            {
                // TODO: thay bằng query thực tế theo DB của bạn
                var employee = await _context.employee
                    .Where(e => e.LoginId == userName)
                    .Select(e => new EmployeeDto
                    {
                        No_ = e.No,
                        Name = e.Name,
                        UserName = e.LoginId,
                        Department = e.WorkCenterCode,
                    })
                    .FirstOrDefaultAsync();

                return employee;
            }

        // ─────────────────────────────────────────────────────────────────────
        // Handle_KiemLai – xử lý phi\u1ebfn ki\u1ec3m l\u1ea1i đ\u1ed9c l\u1eadp t\u1eeb m\u00e0n h\u00ecnh KiemTraChatLuongKiemLai
        //
        // Logic:
        //   1. Validate payload
        //   2. Ki\u1ec3m tra ph\u00e2n c\u00f4ng
        //   3. Sinh ReinspectionNo t\u1eeb no-series "RE"
        //   4. T\u00ednh pass / fail t\u1eeb reinspectionQuantity (= passOutputQty) v\u00e0 reinspectionDefectiveCount (= soLoi)
        //   5. T\u1ea1o QLCL_Reinspection header (Status = "Finalized" ngay)
        //   6. T\u1ea1o QLCL_ReinspectionItem: 1 item "Pass" + 1 item "Fail" m\u1ed7i lo\u1ea1i l\u1ed7i
        //   7. T\u1ea1o QLCL_ReinspectionAudit
        // ─────────────────────────────────────────────────────────────────────
        public async Task<object> Handle_KiemLai(RequestCheckQlclDTO payload)
        {
            if (payload == null)
                throw new Exception("Payload kh\u00f4ng h\u1ee3p l\u1ec7");

            try
            {
                // ── 1. Validate c\u01a1 b\u1ea3n ───────────────────────────────────────────
                int checkedQty = payload.reinspectionQuantity * payload.unitQuantity;
                int defectQty  = payload.reinspectionDefectiveCount;
                int passedQty  = checkedQty - defectQty;

                if (checkedQty <= 0)
                    throw new Exception("S\u1ed1 l\u01b0\u1ee3ng ki\u1ec3m l\u1ea1i ph\u1ea3i l\u1edbn h\u01a1n 0.");
                if (defectQty < 0)
                    throw new Exception("S\u1ed1 l\u01b0\u1ee3ng l\u1ed7i ki\u1ec3m l\u1ea1i kh\u00f4ng h\u1ee3p l\u1ec7.");
                if (defectQty > checkedQty)
                    throw new Exception("S\u1ed1 l\u1ed7i ki\u1ec3m l\u1ea1i kh\u00f4ng th\u1ec3 l\u1edbn h\u01a1n s\u1ed1 l\u01b0\u1ee3ng ki\u1ec3m l\u1ea1i.");
                if (defectQty > 0 && (payload.defects == null || !payload.defects.Any(d => d.Quantity > 0)))
                    throw new Exception("Khi c\u00f3 s\u1ea3n ph\u1ea9m l\u1ed7i, ph\u1ea3i nh\u1eadp \u00edt nh\u1ea5t m\u1ed9t chi ti\u1ebft l\u1ed7i v\u1edbi s\u1ed1 l\u01b0\u1ee3ng > 0.");
                if (defectQty == 0 && payload.defects != null && payload.defects.Any(d => d.Quantity > 0))
                    throw new Exception("S\u1ed1 s\u1ea3n ph\u1ea9m l\u1ed7i \u0111ang l\u00e0 0 nh\u01b0ng c\u00f3 chi ti\u1ebft l\u1ed7i v\u1edbi s\u1ed1 l\u01b0\u1ee3ng > 0.");
                if (payload.planId == Guid.Empty)
                    throw new Exception("Thi\u1ebfu PlanId cho phi\u1ebfn ki\u1ec3m l\u1ea1i.");

                // ── 2. Ki\u1ec3m tra ph\u00e2n c\u00f4ng ────────────────────────────────────────
                var nowCheck = DateTime.UtcNow;
                var checkPhanCong = await _context.EmployeeAssignmentHistory
                    .Where(a => a.EmployeeNo == payload.inspector && (a.ToDate == null || a.ToDate >= nowCheck))
                    .FirstOrDefaultAsync();
                //if (checkPhanCong == null)
                //    throw new Exception("B\u1ea1n ch\u01b0a \u0111\u01b0\u1ee3c ph\u00e2n c\u00f4ng!");

                // ── 3. Sinh ReinspectionNo t\u1eeb no-series "RE" ──────────────────
                var reSeries = await _context.noSeriesLineQLCL
                    .FirstOrDefaultAsync(e => e.SeriesCode == "QLCL" && e.Code == "RE");
                string reinspectionNo;
                if (reSeries != null)
                {
                    var reLastNo  = reSeries.LastNoUsed ?? 0;
                    var reIncr    = reSeries.IncrementByNo ?? 1;
                    reinspectionNo = (reSeries.StartingNo ?? "") + FormatDocumentNumber(
                        reSeries.EndingNo?.ToString() ?? "0",
                        (reLastNo + reIncr).ToString());
                    reSeries.LastNoUsed = reLastNo + reIncr;
                    _context.noSeriesLineQLCL.Update(reSeries);
                }
                else
                {
                    // fallback n\u1ebfu ch\u01b0a c\u00f3 no-series "RE"
                    reinspectionNo = "RE" + DateTime.Now.ToString("yyyyMMddHHmmss");
                }

                // ── 4. Disposition ────────────────────────────────────────────
                string disposition = passedQty == checkedQty ? "Pass"
                                   : passedQty == 0          ? "Scrap"
                                   : "Partial";

                // ── 5. T\u1ea1o QLCL_Reinspection header (Finalized ngay) ─────────
                var reinspection = new QLCL_Reinspection
                {
                    OperationType = payload.checkType,
                    ReinspectionNo    = reinspectionNo,
                    OriginalSessionRowID = null,          // standalone, kh\u00f4ng link session c\u1ee5 th\u1ec3
                    CreatedBy         = payload.inspector,
                    CreatedAt         = DateTimeOffset.UtcNow,
                    FactoryCode       = payload.factory,
                    ProductionTeamCode = payload.productionTeam,
                    Disposition       = disposition,
                    Status            = "Finalized",
                    TotalQuantity     = checkedQty,
                    TotalPassed       = passedQty,
                    TotalScrapped     = defectQty,
                    FinalizedBy       = payload.inspector,
                    FinalizedAt       = DateTimeOffset.UtcNow,
                    Notes             = null,
                    PlanId            = payload.planId
                };
                await _context.Reinspections.AddAsync(reinspection);
                await _context.SaveChangesAsync(); // c\u1ea7n Id tr\u01b0\u1edbc khi t\u1ea1o items

                // ── 6. T\u1ea1o QLCL_ReinspectionItem ──────────────────────────────
                var reItems = new List<QLCL_ReinspectionItem>();

                // Item "Pass" (n\u1ebfu c\u00f3)
                if (passedQty > 0)
                {
                    reItems.Add(new QLCL_ReinspectionItem
                    {
                        ReinspectionId = reinspection.Id,
                        Style          = payload.style,
                        PO             = payload.po,
                        Color          = payload.color,
                        Size           = payload.size,
                        Quantity       = passedQty,
                        DefectCodes    = null,
                        Result         = "Pass",
                        InspectorNo    = payload.inspector,
                        InspectedAt    = DateTimeOffset.UtcNow,
                        Note           = null,
                        PlanId         = payload.planId
                    });
                }

                // Items "Fail" – m\u1ed9t item m\u1ed7i lo\u1ea1i l\u1ed7i
                if (payload.defects != null)
                {
                    foreach (var d in payload.defects.Where(d => d.Quantity > 0))
                    {
                        reItems.Add(new QLCL_ReinspectionItem
                        {
                            ReinspectionId = reinspection.Id,
                            Style          = payload.style,
                            PO             = payload.po,
                            Color          = payload.color,
                            Size           = payload.size,
                            Quantity       = d.Quantity,
                            DefectCodes    = JsonConvert.SerializeObject(new[] { d.Code }),
                            Result         = "Fail",
                            InspectorNo    = payload.inspector,
                            InspectedAt    = DateTimeOffset.UtcNow,
                            Note           = d.Name,
                            PlanId         = payload.planId
                        });
                    }
                }

                if (reItems.Any())
                    await _context.ReinspectionItems.AddRangeAsync(reItems);

                // ── 7. Audit ──────────────────────────────────────────────────
                var audit = new QLCL_ReinspectionAudit
                {
                    ReinspectionId = reinspection.Id,
                    Action         = "Create",
                    PerformedBy    = payload.inspector,
                    PerformedAt    = DateTimeOffset.UtcNow,
                    Payload        = JsonConvert.SerializeObject(new
                    {
                        reinspectionNo,
                        factory        = payload.factory,
                        productionTeam = payload.productionTeam,
                        style          = payload.style,
                        po             = payload.po,
                        color          = payload.color,
                        size           = payload.size,
                        checkedQty,
                        passedQty,
                        defectQty,
                        unit           = payload.unit,
                        unitQuantity   = payload.unitQuantity,
                        inspector      = payload.inspector,
                        planId         = payload.planId
                    })
                };
                await _context.ReinspectionAudits.AddAsync(audit);
                await _context.SaveChangesAsync();

                // ── 8. Tính lại tổng tổng hợp để trả về FE ─────────────────
                // KHÔNG cập nhật session gốc — session là bất biến.
                // Tổng đạt = session.Pass + reinspection Pass items hôm nay
                // Tổng lỗi = session.Defect - số lượng đã reinspect (đã xử lý xong)
                var today    = DateTime.Today;
                var tomorrow = today.AddDays(1);

                // (a) Tổng từ sessions kiểm thường
                //var sessionSums = await _context.qualityManagementSessions
                //    .Where(s =>
                //        s.Inspector      == payload.inspector &&
                //        s.WorkCenterCode == payload.factory &&
                //        s.WorkShiftCode  == payload.productionTeam &&
                //        s.ItemNo         == payload.style &&
                //        s.InspectionDate >= today &&
                //        s.InspectionDate <  tomorrow)
                //    .GroupBy(s => 1)
                //    .Select(g => new
                //    {
                //        CheckedQty = g.Sum(x => (int?)x.CheckedQuantity)       ?? 0,
                //        PassQty    = g.Sum(x => (int?)x.PassOutputQuantity)     ?? 0,
                //        DefectQty  = g.Sum(x => (int?)x.DefectiveProductCount)  ?? 0,
                //    })
                //    .FirstOrDefaultAsync();

                // (b) Tổng từ reinspection items hôm nay cho style này
                // SpecifyKind(Unspecified) để tránh lỗi offset khi DateTime.Kind = Local
                //var todayOffset    = new DateTimeOffset(DateTime.SpecifyKind(today,    DateTimeKind.Unspecified), TimeSpan.Zero);
                //var tomorrowOffset = new DateTimeOffset(DateTime.SpecifyKind(tomorrow, DateTimeKind.Unspecified), TimeSpan.Zero);
                //var reSums = await _context.ReinspectionItems
                //    .Where(ri =>
                //        ri.InspectorNo  == payload.inspector &&
                //        ri.Style        == payload.style &&
                //        ri.InspectedAt  != null &&
                //        ri.InspectedAt.Value >= todayOffset &&
                //        ri.InspectedAt.Value <  tomorrowOffset)
                //    .GroupBy(ri => 1)
                //    .Select(g => new
                //    {
                //        RePassQty    = g.Sum(ri => ri.Result == "Pass" ? (int?)ri.Quantity : 0) ?? 0,
                //        // Chỉ trừ những item đã Pass khỏi DefectiveProductCount
                //        // Item Result="Fail" vẫn còn là hàng lỗi tồn đọng
                //        ReResolvedQty = g.Sum(ri => ri.Result == "Pass" ? (int?)ri.Quantity : 0) ?? 0,
                //    })
                //    .FirstOrDefaultAsync();

                //var totalChecked = sessionSums?.CheckedQty ?? 0;
                //var totalPass    = (sessionSums?.PassQty   ?? 0) + (reSums?.RePassQty    ?? 0);
                //var totalDefect  = Math.Max(0, (sessionSums?.DefectQty ?? 0) - (reSums?.ReResolvedQty ?? 0));
                //var totalPass = (sessionSums?.PassQty ?? 0);
                //var totalDefect = (sessionSums?.DefectQty ?? 0);

                //Lấy tổng sl lỗi phát sinh theo kế hoạch
                var qrKiemLai = await _context.qualityManagementSessions
                    .Where(p =>
                        p.PlanId == payload.planId &&
                        p.Status == 1 &&
                        p.InspectionDate >= today &&
                        p.InspectionDate < tomorrow)
                    .GroupBy(x => 1)
                    .Select(g => new
                    {
                        PassOutputQty = g.Sum(x => x.PassOutputQuantity),
                        CheckedQty = g.Sum(x => x.CheckedQuantity)
                    })
                    .FirstOrDefaultAsync();
                var slKiemLai = (qrKiemLai?.CheckedQty ?? 0) - (qrKiemLai?.PassOutputQty ?? 0);
                var todayOffset = new DateTimeOffset(DateTime.SpecifyKind(today, DateTimeKind.Unspecified), TimeSpan.Zero);
                var tomorrowOffset = new DateTimeOffset(DateTime.SpecifyKind(tomorrow, DateTimeKind.Unspecified), TimeSpan.Zero);
                var totalPassed = await _context.ReinspectionItems
                    .Where(i =>
                        i.PlanId == payload.planId &&
                        i.Result == "Pass" &&
                        i.InspectedAt != null &&
                        i.InspectedAt.Value >= todayOffset &&
                        i.InspectedAt.Value < tomorrowOffset)
                    .SumAsync(i => (int?)i.Quantity) ?? 0;

                var totalDefect = Math.Max(0, slKiemLai - totalPassed);
                return new
                {
                    success         = true,
                    message         = "Tạo phiên kiểm lại thành công",
                    reinspectionId  = reinspection.Id,
                    reinspectionNo,
                    checkedQuantity = slKiemLai,
                    passQuantity    = totalPassed,
                    defectQuantity  = totalDefect
                };
            }
            catch (Exception ex)
            {
                throw new Exception($"L\u1ed7i: {ex.InnerException?.Message ?? ex.Message}");
            }
        }

        public async Task<FinalizeResultDto> FinalizeReinspectionAsync(int reinspectionId, string finalizedBy, string notes)
        {
            // assumes _context is your DbContext and caller already opened a transaction (or you open one here)
            // If caller passes transaction, ensure to use same _context and don't create nested tx.

            // Load header with a pessimistic lock hint using raw SQL to avoid concurrent finalize
            var reinspection = await _context.Set<QLCL_Reinspection>()
                .FromSqlRaw("SELECT * FROM mes.QLCL_Reinspection WITH (UPDLOCK, ROWLOCK) WHERE Id = {0}", reinspectionId)
                .AsNoTracking() // we'll attach and update below
                .FirstOrDefaultAsync();

            if (reinspection == null)
                throw new InvalidOperationException($"Reinspection {reinspectionId} not found.");

            if (string.Equals(reinspection.Status, "Finalized", StringComparison.OrdinalIgnoreCase))
            {
                // idempotent: return existing totals
                return new FinalizeResultDto
                {
                    Success = true,
                    Message = "Already finalized",
                    ReinspectionId = reinspection.Id,
                    ReinspectionNo = reinspection.ReinspectionNo,
                    TotalQuantity = reinspection.TotalQuantity,
                    TotalPassed = reinspection.TotalPassed,
                    TotalScrapped = reinspection.TotalScrapped
                };
            }

            if (string.Equals(reinspection.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Cannot finalize a cancelled reinspection.");

            // Get items
            var items = await _context.Set<QLCL_ReinspectionItem>()
                .Where(i => i.ReinspectionId == reinspectionId)
                .ToListAsync();

            var totalQty = items.Sum(i => i.Quantity);
            var totalPassed = items.Where(i => string.Equals(i.Result, "Pass", StringComparison.OrdinalIgnoreCase)).Sum(i => i.Quantity);
            var totalScrapped = items.Where(i => string.Equals(i.Result, "Scrap", StringComparison.OrdinalIgnoreCase)).Sum(i => i.Quantity);

            if (totalQty <= 0)
                throw new InvalidOperationException("Không có số lượng item để finalize.");

            // Update header fields (attach entity)
            reinspection.TotalQuantity = totalQty;
            reinspection.TotalPassed = totalPassed;
            reinspection.TotalScrapped = totalScrapped;
            reinspection.Status = "Finalized";
            reinspection.FinalizedBy = finalizedBy;
            reinspection.FinalizedAt = DateTimeOffset.UtcNow;
            if (!string.IsNullOrWhiteSpace(notes))
            {
                reinspection.Notes = string.IsNullOrWhiteSpace(reinspection.Notes) ? notes : reinspection.Notes + " | " + notes;
            }

            _context.Set<QLCL_Reinspection>().Update(reinspection);

            // Adjust related sessions
            var links = await _context.Set<QLCL_ReinspectionSessionLink>()
                .Where(l => l.ReinspectionId == reinspectionId)
                .ToListAsync();

            if (links.Any())
            {
                var linkTotal = links.Sum(l => l.QuantityFromSession);
                int remainingPassed = totalPassed;
                int remainingScrapped = totalScrapped;

                for (int idx = 0; idx < links.Count; idx++)
                {
                    var link = links[idx];
                    var sess = await _context.Set<QualityManagementSession>().FindAsync(link.SessionRowID);
                    if (sess == null) continue;

                    int allocPassed = (int)Math.Floor((decimal)totalPassed * link.QuantityFromSession / linkTotal);
                    int allocScrapped = (int)Math.Floor((decimal)totalScrapped * link.QuantityFromSession / linkTotal);

                    // for last link, give remainders
                    if (idx == links.Count - 1)
                    {
                        allocPassed = remainingPassed;
                        allocScrapped = remainingScrapped;
                    }
                    else
                    {
                        remainingPassed -= allocPassed;
                        remainingScrapped -= allocScrapped;
                    }

                    // update session safely
                    sess.PassOutputQuantity = (sess.PassOutputQuantity) + allocPassed;
                    var newDefect = Math.Max(0, (sess.DefectiveProductCount) - (allocPassed + allocScrapped));
                    sess.DefectiveProductCount = newDefect;

                    _context.Set<QualityManagementSession>().Update(sess);
                }
            }
            else if (reinspection.OriginalSessionRowID.HasValue)
            {
                var sess = await _context.Set<QualityManagementSession>().FindAsync(reinspection.OriginalSessionRowID.Value);
                if (sess != null)
                {
                    sess.PassOutputQuantity = (sess.PassOutputQuantity) + totalPassed;
                    var newDefect = Math.Max(0, (sess.DefectiveProductCount) - (totalPassed + totalScrapped));
                    sess.DefectiveProductCount = newDefect;
                    _context.Set<QualityManagementSession>().Update(sess);
                }
            }

            // Create audit entry: snapshot as JSON
            var snapshot = new
            {
                Header = reinspection,
                Items = items
            };
            var audit = new QLCL_ReinspectionAudit
            {
                ReinspectionId = reinspectionId,
                Action = "Finalize",
                PerformedBy = finalizedBy,
                PerformedAt = DateTimeOffset.UtcNow,
                Payload = JsonConvert.SerializeObject(snapshot)
            };
            await _context.Set<QLCL_ReinspectionAudit>().AddAsync(audit);

            // Persist all changes (caller should be in a transaction)
            await _context.SaveChangesAsync();

            // Return summary
            return new FinalizeResultDto
            {
                Success = true,
                Message = "Finalized",
                ReinspectionId = reinspectionId,
                ReinspectionNo = reinspection.ReinspectionNo,
                TotalQuantity = totalQty,
                TotalPassed = totalPassed,
                TotalScrapped = totalScrapped
            };
        }
    }
    

}
