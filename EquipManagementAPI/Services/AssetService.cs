using Azure.Core;
using EquipManagementAPI.Data;
using EquipManagementAPI.Helpers;
using EquipManagementAPI.Models;
using EquipManagementAPI.Models.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace EquipManagementAPI.Services
{
    public class AssetService : IAssetService
    {
        private readonly ApplicationDbContext _context;
        public AssetService(ApplicationDbContext context)
        {
            _context = context;
        }
        

        public async Task<List<string>> ProcessQRCodeBatchAsync(RequestEntryType0 request)
        {
            var results = new List<string>();
            var distinctQRCodes = request.QRCodes.Distinct().ToList();
            foreach (var qrCode in distinctQRCodes)
            {
                try
                {
                    var asset = await _context.Equipment.FirstOrDefaultAsync(e=>e.QRCode == qrCode);
                    if (asset == null)
                    {
                        results.Add($"Lỗi! QRCode:{qrCode} chưa được tạo!");
                        continue;
                    }
                    var docEntries = await _context.DocumentEntry.Where(e=>e.DocumentNo !=null &&  e.DocumentNo==request.DocumentNo).ToListAsync();
                    if (!docEntries.Any(e =>e.QRCode!=null && e.QRCode == qrCode))
                    {
                        results.Add($"Lỗi! QRCode: {qrCode} không thuộc phiếu nhập {request.DocumentNo}!");
                        continue;
                    }
                    var checkScan = await _context.QRCodeEntry.FirstOrDefaultAsync(e=>e.QRCode == qrCode && e.DocumentNo == request.DocumentNo);
                    if (checkScan != null)
                    {
                        results.Add($"Lỗi! QRCode:{qrCode} đã được quét");
                        continue;
                    }
                    var dto = new QRCodeEntryQLTB
                    {
                        QRCode = qrCode,
                        EquipmentCode = asset.EquipmentCode,
                        EquipmentSubCode = asset.EquipmentSubCode,
                        EquipmentGroupCode = asset.EquipmentGroupCode,
                        DocumentNo = request.DocumentNo,
                        DocumentType = request.DocumentType,
                        ManageUnit = request.Unit,
                        UsingUnit = request.Unit,
                        PostingDate = DateTime.Now,
                        UserId = request.UserId,
                        Respon = asset.Responsibility,
                        SourceCode = asset.SourceCode

                    };
                    _context.QRCodeEntry.Add(dto);
                    //update
                    asset.ManageUnit = request.Unit;
                    asset.UsingUnit = request.Unit;
                    asset.DocumentNo = request.DocumentNo;
                    asset.DocumentType = request.DocumentType;
                    switch (request.DocumentType)
                    {
                        case 5: //Nhập thuê đơn vị ngoài
                            asset.Status = 10;
                            asset.StatusGroup = 0;
                            asset.LocationCode = "";
                            break;


                        case 12: //xuất trả thuê
                            //asset.Status = 4;
                            //asset.StatusGroup = -1;
                            asset.StatusGroup = -1;
                            asset.Status = -1;
                            break;
                        case 13: //Xuất trả NCC
                            //asset.Status = 5;
                            asset.Status = 4; //Hỏng
                            asset.StatusGroup = -1;
                            asset.LocationCode = "";
                            break;
                        case 15: //Xuất bảo hành
                            asset.Status = 3; //Bảo hành
                            asset.StatusGroup = -1;
                            asset.LocationCode = "";
                            break;
                        case 3: //Nhập sau khi sửa chữa BD từ DV ngoài
                            asset.Status = 14;
                            asset.StatusGroup = 0;
                            asset.LocationCode = "";
                            break;
                        case 16: //Xuất thanh lý
                            asset.Status = 5;
                            asset.StatusGroup = -1;
                            asset.LocationCode = "";
                            break;
                        case 6: //Nhập mượn đơn vị ngoài
                            asset.Status = 0;
                            asset.StatusGroup = 0;
                            asset.LocationCode = "";
                            break;
                        
                        case 11: //Xuất trả mượn ĐV ngoài
                            asset.Status = -1;
                            asset.StatusGroup = -1;
                            asset.LocationCode = "";
                            break;
                        case 14: //Xuất cho ĐV ngoài mượn
                            asset.Status = 6;
                            asset.StatusGroup = 1;
                            asset.LocationCode = "";
                            break;
                        case 2: // Nhập ĐV ngoài trả mượn
                            asset.Status = 14;
                            asset.StatusGroup = 0;
                            asset.LocationCode = "";
                            break;
                    }


                }
                catch (Exception ex)
                {
                    results.Add($"❌ {qrCode}: {ex.Message}");
                }
                
            }
            await _context.SaveChangesAsync();
            var docEntry = await _context.DocumentEntry.CountAsync(e => e.DocumentNo == request.DocumentNo);
            //var qrEntry = distinctQRCodes.Count();
            var qrEntry = await _context.QRCodeEntry
            .Where(e => e.DocumentNo == request.DocumentNo)
            .CountAsync();
            if (docEntry == qrEntry)
            {
                var docEntryHeader = await _context.DocumentEntryHeader.FirstOrDefaultAsync(e => e.No == request.DocumentNo);
                if (docEntryHeader != null)
                {
                    docEntryHeader.CheckQR = 1;
                    docEntryHeader.PostingDate = DateTime.Now;
                }
                await _context.SaveChangesAsync();
            }
            
            results.Add($"✅ Quét thành công");
            return results;
        }

        public async Task<List<string>> ProcessScanQRCode_Type1(RequestEntryType1 request)
        {
            var results = new List<string>();
            var distinctQRCodes = request.QRCodes.Distinct().ToList();

            foreach (var qrCode in distinctQRCodes)
            {
                var equip = await _context.Equipment.FirstOrDefaultAsync(e => e.QRCode == qrCode);
                if (equip == null)
                {
                    results.Add($"Lỗi! QRCode:{qrCode} chưa được tạo!");
                    continue;
                }

                var docEntries = await _context.DocumentEntry
                    .Where(e => e.DocumentNo != null && e.DocumentNo == request.DocumentNo)
                    .ToListAsync();

                if (!docEntries.Any(e => e.QRCode != null && e.QRCode == qrCode))
                {
                    results.Add($"Lỗi! QRCode: {qrCode} không thuộc phiếu nhập {request.DocumentNo}!");
                    continue;
                }

                var checkScan = await _context.QRCodeEntry
                    .FirstOrDefaultAsync(e => e.QRCode == qrCode && e.DocumentNo == request.DocumentNo);

                if (checkScan != null)
                {
                    results.Add($"Lỗi! QRCode:{qrCode} đã được quét");
                    continue;
                }

                string dtoManageUnit = request.ManageUnit;
                string dtoUsingUnit = request.UsingUnit;
                switch (request.DocumentType)
                {
                    case 7 : // Nhập mượn nội bộ
                        dtoManageUnit = request.ManageUnit;
                        dtoUsingUnit = request.UsingUnit;
                        break;

                    case 10: // Xuất hoàn trả nội bộ kho công ty
                        dtoManageUnit = request.UsingUnit;
                        dtoUsingUnit = request.UsingUnit;
                        break;

                    case 9: // Xuất cho mượn nội bộ
                        dtoManageUnit = request.ManageUnit;
                        dtoUsingUnit = request.UsingUnit;
                        break;

                    case 8: // Nhập hoàn trả nội bộ kho cty
                        dtoManageUnit = request.UsingUnit;
                        dtoUsingUnit = request.UsingUnit;
                        break;

                    case 17: // Xuất trả mượn NB
                        dtoManageUnit = request.ManageUnit;
                        dtoUsingUnit = request.ManageUnit;
                        break;
                    case 18: // Nhập trả mượn NB
                        dtoManageUnit = request.ManageUnit;
                        dtoUsingUnit = request.ManageUnit;
                        break;
                }


                var dto = new QRCodeEntryQLTB
                {
                    QRCode = qrCode,
                    EquipmentCode = equip.EquipmentCode,
                    EquipmentSubCode = equip.EquipmentSubCode,
                    EquipmentGroupCode = equip.EquipmentGroupCode,
                    DocumentNo = request.DocumentNo,
                    DocumentType = request.DocumentType,
                    ManageUnit = dtoManageUnit,
                    UsingUnit = dtoUsingUnit,
                    PostingDate = DateTime.Now,
                    UserId = request.UserId,
                    Respon = equip.Responsibility,
                    SourceCode = equip.SourceCode
                };


                _context.QRCodeEntry.Add(dto);

                var equipLineNo = await _context.equipmentLineNo
                    .FirstOrDefaultAsync(e => e.DepartmentCode == request.ManageUnit);

                if (equipLineNo != null)
                {
                    int newNumber = equipLineNo.LastUsed + equipLineNo.Increment;
                    string seriesNo = EquipmentMapper.FormatDocumentNumber(
                        equipLineNo.EndingNo.ToString(),
                        newNumber.ToString());

                    equip.LineNo = Convert.ToInt16(seriesNo);
                    equipLineNo.LastUsed = newNumber;
                }

                
                equip.DocumentNo = request.DocumentNo;
                equip.DocumentType = request.DocumentType;
                switch (request.DocumentType)
                {
                    case 7://Nhập mượn nội bộ
                        equip.Status = 14;
                        equip.StatusGroup = 0;
                        equip.LocationCode = "";
                        equip.ManageUnit = request.ManageUnit;
                        equip.UsingUnit = request.UsingUnit;
                        break;
                    case 10: //Xuất hoàn trả nội bộ kho công ty
                             //asset.Status = 3;
                             //asset.StatusGroup = -1;
                        equip.LocationCode = "";
                        equip.Status = 14;
                        equip.StatusGroup = 0;
                        equip.ManageUnit = request.UsingUnit;
                        equip.UsingUnit = request.UsingUnit;
                        break;
                    case 9://Xuất cho mượn nội bộ
                        equip.Status = 14;
                        equip.StatusGroup = 0;
                        equip.LocationCode = "";
                        equip.ManageUnit = request.ManageUnit;
                        equip.UsingUnit = request.UsingUnit;
                        break;
                    case 8: //Nhập hoàn trả nội bộ kho cty
                        equip.LocationCode = "";
                        equip.Status = 14;
                        equip.StatusGroup = 0;
                        equip.ManageUnit = request.UsingUnit;
                        equip.UsingUnit = request.UsingUnit;
                        break;
                    case 17: //Xuất trả mượn NB
                        equip.LocationCode = "";
                        equip.Status = 14;
                        equip.StatusGroup = 0;
                        equip.ManageUnit = request.ManageUnit;
                        equip.UsingUnit = request.ManageUnit;
                        break;
                    case 18: //Nhập trả mượn NB
                        equip.LocationCode = "";
                        equip.Status = 14;
                        equip.StatusGroup = 0;
                        equip.ManageUnit = request.ManageUnit;
                        equip.UsingUnit = request.ManageUnit;
                        break;
                };
            };
            await _context.SaveChangesAsync();

            int docEntryCount = await _context.DocumentEntry
                .CountAsync(e => e.DocumentNo == request.DocumentNo);

            int qrEntryCount = await _context.QRCodeEntry
                .Where(e => e.DocumentNo == request.DocumentNo)
                .CountAsync();

            if (docEntryCount == qrEntryCount)
            {
                var docEntryHeader = await _context.DocumentEntryHeader
                    .FirstOrDefaultAsync(e => e.No == request.DocumentNo);

                if (docEntryHeader != null)
                {
                    docEntryHeader.CheckQR = 1;
                    docEntryHeader.PostingDate = DateTime.Now;
                }

                await _context.SaveChangesAsync();
            }

            results.Add("✅ Quét thành công");
            return results;
        }


        public async Task<List<string>> ProcessScan_SuDungLuuKho(EquipScan01DTO request)
        {
            var results = new List<string>();

            // Loại bỏ trùng lặp QRCode
            var distinctQRCodes = request.QRCodes.Distinct().ToList();

            // Lấy thông tin đơn vị
            var unit = await _context.Department.FirstOrDefaultAsync(d => d.Code == request.Unit);
            if (unit == null)
            {
                results.Add("Lỗi: Không tìm thấy đơn vị quản lý " + request.Unit);
                return results;
            }

            foreach (var qr in distinctQRCodes)
            {
                try
                {
                    // Kiểm tra thiết bị có thuộc đơn vị này không
                    var equipInUnit = await _context.Equipment
                        .FirstOrDefaultAsync(e => e.QRCode == qr && e.UsingUnit == request.Unit);

                    if (equipInUnit == null)
                    {
                        results.Add($"Lỗi: Thiết bị {qr} không thuộc đơn vị {unit.Name}");
                        continue;
                    }

                    // Kiểm tra QRCode có tồn tại trong hệ thống
                    var equip = await _context.Equipment.FirstOrDefaultAsync(e => e.QRCode == qr);
                    if (equip == null)
                    {
                        results.Add($"Lỗi: QRCode {qr} chưa được tạo!");
                        continue;
                    }

                    // Kiểm tra tình trạng nhập kho (DocumentType 1 hoặc 7 là hợp lệ)
                    if (equip.DocumentType != 1 && equip.DocumentType != 7)
                    {
                        results.Add($"Lỗi: Thiết bị {qr} chưa được nhập đơn vị quản lý");
                        continue;
                    }

                    // Cập nhật trạng thái sử dụng hoặc lưu kho
                    if (request.Status == 0)
                    {
                        equipInUnit.Status = 1;         // Đang sử dụng
                        equipInUnit.StatusGroup = 1;
                    }
                    else
                    {
                        equipInUnit.Status = 14;        // Lưu kho
                        equipInUnit.StatusGroup = 0;
                    }

                    // Cập nhật vị trí và đơn vị
                    equipInUnit.LocationCode = request.WorkShift;
                    equipInUnit.ManageUnit = request.Unit;
                    equipInUnit.UsingUnit = request.Unit;

                    // Cập nhật vị trí trong các bảng liên quan
                    var maintenanceTrack = await _context.maintenanceTrackings
                        .FirstOrDefaultAsync(m => m.QRCode == qr);
                    if (maintenanceTrack != null)
                        maintenanceTrack.LocationCode = request.WorkShift;

                    var maintenanceTrackWeek = await _context.maintenanceTrackingsWeek
                        .FirstOrDefaultAsync(m => m.QRCode == qr);
                    if (maintenanceTrackWeek != null)
                        maintenanceTrackWeek.LocationCode = request.WorkShift;

                    var repairRequest = await _context.repairRequests
                        .FirstOrDefaultAsync(r => r.QRCode == qr);
                    if (repairRequest != null)
                        repairRequest.LocationCode = request.WorkShift;

                    // Ghi lại lịch sử trạng thái
                    var log = new EquipmentStatusLog
                    {
                        QRCode = qr,
                        CreatedDate = DateTime.Now,
                        Status = request.Status,
                        DepartmentCode = request.Unit,
                        WorkShiftCode = request.WorkShift,
                        UserID = request.UserID
                    };

                    _context.EquipmentStatusLog.Add(log);

                    results.Add($"Quét QRCode {qr} thành công");
                }
                catch (Exception ex)
                {
                    results.Add($"Lỗi xử lý {qr}: {ex.Message}");
                }
            }

            await _context.SaveChangesAsync();
            return results;
        }
        public async Task<List<string>> ProcessScan_KiemKe(EquipScanKK_DTO request)
        {
            var results = new List<string>();
            var distinctQRCodes = request.QRCodes.Distinct().ToList();

            var unit = await _context.Department.FirstOrDefaultAsync(d => d.Code == request.unit);
            if (unit == null)
            {
                results.Add("Lỗi: Không tìm thấy đơn vị quản lý " + request.unit);
                return results;
            }

            foreach (var qr in distinctQRCodes)
            {
                try
                {
                    // Kiểm tra thiết bị thuộc đơn vị
                    var equipInUnit = await _context.Equipment
                        .FirstOrDefaultAsync(e => e.QRCode == qr && e.UsingUnit == request.unit);

                    if (equipInUnit == null)
                    {
                        results.Add($"Lỗi: Thiết bị {qr} không thuộc đơn vị {unit.Name}");
                        continue;
                    }

                    // Kiểm tra QR đã được quét trong năm chưa
                    bool isExists = await _context.inventoryScans
                        .AnyAsync(x => x.QRCode == qr && x.Year == DateTime.Now.Year.ToString());

                    if (isExists)
                    {
                        results.Add($"Lỗi: QRCode {qr} đã được kiểm kê trong năm {DateTime.Now.Year}");
                        continue;   // BỎ QUA nhưng KHÔNG dừng vòng lặp
                    }
                    
                    var equipKK = new InventoryScan
                    {
                        QRCode = qr,
                        PostingDate = DateTime.Now,
                        Status = Convert.ToInt32(request.status),
                        WorkCenter = request.unit,
                        ManageUnit = equipInUnit.ManageUnit,
                        EquipmentGroupCode = equipInUnit.EquipmentGroupCode,
                        Model = equipInUnit.Model,
                        ScanLocation = request.location,
                        UserID = request.userId,
                        Year = DateTime.Now.Year.ToString(),
                        TinhTrang = equipInUnit.ManageUnit == request.unit ? 0 : 1
                      
                    };

                    _context.inventoryScans.Add(equipKK);

                    results.Add($"Quét QRCode {qr} thành công");
                }
                catch (Exception ex)
                {
                    results.Add($"Lỗi xử lý {qr}: {ex.Message}");
                }
            }

            await _context.SaveChangesAsync();
            return results;
        }


        public async Task<bool> UpdateSerialNumber(string equipmentCode, string newSerialNumber, string userId)
        {
            var equipment = await _context.Equipment
                .FirstOrDefaultAsync(e => e.EquipmentCode == equipmentCode);

            if (equipment == null)
            {
                return false;
            }
            equipment.SerialNumber = newSerialNumber;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<MaintenanceTypeDTO>> GetMaintenanceType()
        {
            var maintenanceTypes = await _context.maintenanceType
                .Select(e => new MaintenanceTypeDTO
                {
                    Value = e.Code,
                    Label = e.Name
                })
                .ToListAsync();

            return maintenanceTypes;
        }

        public async Task<List<string>> GetRemainingTask(RequestRemaining request)
        {
            var maintenanceHistory = await _context.maintenanceHistory
                .FirstOrDefaultAsync(e => e.QRCode == request.QRCode);

            if (maintenanceHistory == null)
                return new List<string>();

            var allTasks = await _context.maintenanceContents
                .Where(c => c.EquipGroupCode == maintenanceHistory.EquipGroupCode
                            && c.MaintenanceType == request.MaintenanceType)
                .Select(c => c.Task.Trim().ToLower())
                .ToListAsync();

            var completedTasks = await _context.maintenanceCheckList
                .Where(cl => cl.EquipCode == maintenanceHistory.EquipmentCode
                             && cl.MaintenanceType == request.MaintenanceType
                             && cl.MaintenanceTime == maintenanceHistory.NextMaintenanceTime)
                .Select(cl => cl.Task)
                .ToListAsync();

            var remainingTasks = allTasks.Except(completedTasks).ToList();

            return remainingTasks;
        }


        public async Task<List<string>> GetMaintenanceContent(MaintenanceRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.QRCode))
                return new List<string>();

            string qr = request.QRCode.Trim().ToLower();

            var equip = await _context.Equipment
                .FirstOrDefaultAsync(e =>
                    (e.QRCode != null && e.QRCode.ToLower() == qr) ||
                    (e.SerialNumber != null && e.SerialNumber.ToLower() == qr));

            if (equip == null)
                return new List<string>();

            var equipGroup = await _context.equipmentGroup
                .Where(e => e.Code == equip.EquipmentGroupCode)
                .Select(e => e.Code)
                .Distinct()
                .FirstOrDefaultAsync();

            if (equipGroup == null)
                return new List<string>();

            var contents = await _context.maintenanceContents
                .Where(e =>
                    e.EquipGroupCode == equipGroup &&
                    e.MaintenanceType == request.MaintenanceType)
                .Select(e => e.Task)
                .ToListAsync();

            return contents;
        }

        public async Task<List<string>> ProcessScan_BaoDuong(RequestScanMaintenance request)
        {
            var results = new List<string>();

            var checkMainTrack = await _context.maintenanceTrackings
                .FirstOrDefaultAsync(e => e.QRCode == request.QRCode && e.MaintenanceType == request.type);

            var checkEmployee = await _context.employee
                .FirstOrDefaultAsync(e => e.No == request.UserID);

            if (checkEmployee == null)
            {
                results.Add("Lỗi! Không tìm thấy thông tin nhân viên.");
                return results;
            }

            if (checkEmployee.WorkShiftCode != "01020121" && checkEmployee.WorkShiftCode != "01010124")
            {
                results.Add("Lỗi! Bạn không thuộc bộ phận bảo trì.");
                return results;
            }

            var checkEquip = await _context.Equipment
                .FirstOrDefaultAsync(e => e.QRCode == request.QRCode);

            if (checkEquip == null)
            {
                results.Add($"Lỗi! Không tìm thấy thiết bị có mã QR: {request.QRCode}.");
                return results;
            }

            var workShift = await _context.workShift
                .FirstOrDefaultAsync(e => e.Code == checkEmployee.WorkShiftCode);

            if (workShift == null)
            {
                results.Add("Lỗi! Không tìm thấy thông tin ca làm việc.");
                return results;
            }
            var unit1 = await _context.Department.FirstOrDefaultAsync(e => e.Code == checkEquip.UsingUnit);
            var unit2 = await _context.Department.FirstOrDefaultAsync(e => e.Code == workShift.WorkCenterCode);

            //if (!string.Equals(checkEmployee.WorkCenterCode, checkEquip.ManageUnit, StringComparison.OrdinalIgnoreCase))
            //{
            //    results.Add($"Lỗi! Thiết bị {request.QRCode} thuộc đơn vị {unit1?.Name}.");
            //    return results;
            //}
            if (!string.Equals(checkEquip.UsingUnit, workShift.WorkCenterCode, StringComparison.OrdinalIgnoreCase))
            {
                results.Add($"Lỗi! Thiết bị {request.QRCode} đang sử dụng ở đơn vị {unit1?.Name}, không phải ở {unit2?.Name}.");
                return results;
            }

            var checkMainTrackWeek = await _context.maintenanceTrackingsWeek
                .FirstOrDefaultAsync(e => e.QRCode == request.QRCode && e.MaintenanceType == request.type);

            if (checkMainTrack == null && checkMainTrackWeek == null)
            {
                results.Add("Lỗi Không tìm thấy ghi nhận bảo dưỡng phù hợp.");
                return results;
            }

            int currentMonth = DateTime.Now.Month;
            int? monthPlan = checkMainTrack?.NextMaintenance?.Month;

            if (monthPlan.HasValue && currentMonth < monthPlan.Value)
            {
                results.Add("Lỗi: Thiết bị này chưa đến thời hạn bảo dưỡng.");
                return results;
            }

            var equip = await _context.Equipment
                .FirstOrDefaultAsync(e => e.QRCode == request.QRCode);

            if (equip == null)
            {
                results.Add($"Lỗi Không tìm thấy thiết bị với QRCode {request.QRCode}");
                return results;
            }

            if (request.Content == null || !request.Content.Any())
            {
                results.Add("Lỗi Không có nội dung bảo dưỡng được gửi lên.");
                return results;
            }

            if (request.type == "1W")
            {
                string equipGroup = checkMainTrackWeek?.EquipmentGroupCode;
                DateTime nextNgayBD = checkMainTrackWeek?.NextMaintenance ?? DateTime.Now;

                if (checkMainTrackWeek?.Status == 1)
                {
                    results.Add($"Lỗi QRCode {request.QRCode} đã được bảo dưỡng");
                    return results;
                }

                var newHistory = new MaintenanceHistory
                {
                    EquipmentCode = equip.EquipmentCode,
                    EquipGroupCode = equip.EquipmentGroupCode,
                    QRCode = request.QRCode,
                    Serial = equip.SerialNumber,
                    Brand = equip.Brand,
                    PostingDate = DateTime.Now,
                    ManageUnit = equip.ManageUnit,
                    UsingUnit = equip.UsingUnit,
                    Location = equip.LocationCode,
                    UserID = request.UserID,
                    MaintenanceType = request.type,
                    NextMaintenanceTime = checkMainTrackWeek.NextMaintenance,
                    Status = 1
                };
                _context.maintenanceHistory.Add(newHistory);
                await _context.SaveChangesAsync();

                foreach (var item in request.Content)
                {
                    var newCheckList = new MaintenanceCheckList
                    {
                        HistoryID = newHistory.Id,
                        MaintenanceType = request.type,
                        EquipGroup = equip.EquipmentGroupCode,
                        Task = item,
                        MaintenanceTime = checkMainTrack?.NextMaintenance,
                        EquipCode = equip.EquipmentCode
                    };
                    _context.maintenanceCheckList.Add(newCheckList);
                }

                int total = await _context.maintenanceContents
                    .CountAsync(e => e.EquipGroupCode == equipGroup && e.MaintenanceType == request.type);
                int done = request.Content.Count();

                if (total > 0 && total == done)
                {
                    newHistory.Status = 0;
                    checkMainTrackWeek.LastMaintenanceTime = DateTime.Now;
                    checkMainTrackWeek.NextMaintenance = nextNgayBD.AddDays(7);
                    checkMainTrackWeek.MaintenanceType = request.type;
                    await _context.SaveChangesAsync();
                }
            }
            else
            {
                string equipGroup = checkMainTrack?.EquipmentGroupCode;
                DateTime nextNgayBD = checkMainTrack?.NextMaintenance ?? DateTime.Now;

                if (checkMainTrack?.Status == 1)
                {
                    results.Add($"Lỗi QRCode {request.QRCode} đã được bảo dưỡng");
                    return results;
                }

                var newHistory = new MaintenanceHistory
                {
                    EquipmentCode = equip.EquipmentCode,
                    EquipGroupCode = equip.EquipmentGroupCode,
                    QRCode = request.QRCode,
                    Serial = equip.SerialNumber,
                    Brand = equip.Brand,
                    PostingDate = DateTime.Now,
                    ManageUnit = equip.ManageUnit,
                    UsingUnit = equip.UsingUnit,
                    Location = equip.LocationCode,
                    UserID = request.UserID,
                    MaintenanceType = request.type,
                    NextMaintenanceTime = checkMainTrack?.NextMaintenance,
                    Status = 1
                };

                try
                {
                    _context.maintenanceHistory.Add(newHistory);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateException ex)
                {
                    results.Add($"Lỗi khi lưu MaintenanceHistory: {ex.InnerException?.Message ?? ex.Message}");
                    return results;
                }

                foreach (var item in request.Content)
                {
                    var newCheckList = new MaintenanceCheckList
                    {
                        HistoryID = newHistory.Id,
                        MaintenanceType = request.type,
                        EquipGroup = equip.EquipmentGroupCode,
                        Task = item,
                        MaintenanceTime = checkMainTrack?.NextMaintenance,
                        EquipCode = equip.EquipmentCode
                    };
                    _context.maintenanceCheckList.Add(newCheckList);
                }
                await _context.SaveChangesAsync();

                int total = await _context.maintenanceContents
                    .CountAsync(e => e.EquipGroupCode == equipGroup && e.MaintenanceType == request.type);
                DateTime? nextMaintenance = checkMainTrack?.NextMaintenance;
                int done = await _context.maintenanceCheckList
                    .Where(c => c.EquipCode == equip.EquipmentCode &&
                                c.MaintenanceType == request.type &&
                                c.MaintenanceTime == nextMaintenance)
                    .Select(c => c.Task)
                    .Distinct()
                    .CountAsync();

                if (total > 0 && total == done)
                {
                    newHistory.Status = 0;
                    checkMainTrack.LastMaintenanceTime = DateTime.Now;
                    checkMainTrack.MocBDGanNhat = checkMainTrack.NextMaintenance;

                    switch (request.type)
                    {
                        case "3M":
                            checkMainTrack.NextMaintenance = nextNgayBD.AddMonths(3);
                            checkMainTrack.MaintenanceType = "6M";
                            break;
                        case "6M":
                            checkMainTrack.NextMaintenance = nextNgayBD.AddMonths(3);
                            checkMainTrack.MaintenanceType = "3M";
                            break;
                        case "1M":
                            checkMainTrack.NextMaintenance = nextNgayBD.AddMonths(1);
                            checkMainTrack.MaintenanceType = "1M";
                            break;
                        case "1Y":
                            checkMainTrack.NextMaintenance = nextNgayBD.AddMonths(12);
                            checkMainTrack.MaintenanceType = "1Y";
                            break;
                    }
                }

                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateException ex)
                {
                    results.Add($"Lỗi khi lưu tracking: {ex.InnerException?.Message}");
                    return results;
                }

                results.Add("Đã ghi nhận bảo dưỡng thành công.");
            }

            return results;
        }


        public async Task<List<string>> Process_SuaChua(RequestSuaChua request)
        {
            var results = new List<string>();

            try
            {
                var equip = await _context.Equipment
                    .FirstOrDefaultAsync(e => e.QRCode == request.QRCode);

                if (equip == null)
                {
                    results.Add($"Lỗi: Dữ liệu QRCode {request.QRCode} không được tìm thấy!");
                    return results;
                }

                var checkEmployee = await _context.employee.FirstOrDefaultAsync(e => e.No == request.UserID);

                if (checkEmployee == null)
                {
                    results.Add("Lỗi! Không tìm thấy thông tin nhân viên.");
                    return results;
                }
                //var unit = await _context.Department.FirstOrDefaultAsync(e => e.Code == equip.ManageUnit);

                if (equip.Status == 2)
                {
                    results.Add($"Lỗi: Thiết bị {request.QRCode} đang được sửa chữa!");
                    return results;
                }

                // 3️⃣ Kiểm tra xem đã có yêu cầu sửa chữa nào đang mở không
                var existingRequest = await _context.repairRequests
                    .FirstOrDefaultAsync(r => r.QRCode == request.QRCode && r.Status == 0);

                if (existingRequest != null)
                {
                    results.Add($"Lỗi: Thiết bị {request.QRCode} đã có yêu cầu sửa chữa đang xử lý!");
                    return results;
                }

                // 4️⃣ Lấy thông tin số chứng từ từ NoSeriesLine
                var noSeriesLine = await _context.noSeriesLine
                    .FirstOrDefaultAsync(n => n.Code == "YCSC" && n.SeriesCode == "QLTB");

                if (noSeriesLine == null)
                {
                    results.Add("Lỗi: Không tìm thấy cấu hình đánh số (NoSeriesLine) cho YCSC - QLTB!");
                    return results;
                }

                int endNo = noSeriesLine.EndingNo;
                int incrementNo = noSeriesLine.IncrementByNo;
                int lastNoUsed = noSeriesLine.LastNoUsed;
                string startingNo = noSeriesLine.StartingNo;

                string no = startingNo + EquipmentMapper.FormatDocumentNumber(
                    endNo.ToString(),
                    (incrementNo + lastNoUsed).ToString()
                );

                // 5️⃣ Tạo mới yêu cầu sửa chữa
                var repairRequest = new RepairRequestList
                {
                    No = no,
                    EquipmentCode = equip.EquipmentCode,
                    EquipmentGroupCode = equip.EquipmentGroupCode,
                    Serial = equip.SerialNumber,
                    Brand = equip.Brand,
                    Model = equip.Model,
                    Reporter = request.UserID,
                    PostingDate = DateTime.Now,
                    QRCode = equip.QRCode,
                    LocationCode = equip.LocationCode,
                    WorkCenterCode = equip.UsingUnit,
                    Status = 0
                };
                _context.repairRequests.Add(repairRequest);

                // 6️⃣ Tạo mới lịch sử sửa chữa
                var repairHistory = new RepairHistory
                {
                    No = no,
                    EquipmentCode = equip.EquipmentCode,
                    EquipGroup = equip.EquipmentGroupCode,
                    Serial = equip.SerialNumber,
                    Brand = equip.Brand,
                    Model = equip.Model,
                    LocationCode = equip.LocationCode,
                    CreatedDate = DateTime.Now,
                    UserId = request.UserID,
                    QRCode = request.QRCode,
                    Status = 0
                };
                _context.repairHistory.Add(repairHistory);

                // 7️⃣ Cập nhật số chứng từ đã sử dụng
                noSeriesLine.LastNoUsed = incrementNo + lastNoUsed;

                // 8️⃣ Cập nhật trạng thái thiết bị
                equip.Status = 2; // Đang sửa chữa

                await _context.SaveChangesAsync();

                results.Add($"✅ Yêu cầu sửa chữa cho thiết bị {request.QRCode} đã được tạo thành công!");
            }
            catch (Exception ex)
            {
                results.Add($"Lỗi hệ thống: {ex.InnerException?.Message ?? ex.Message}");
            }

            return results;
        }

        public async Task<List<string>> Process_BaoDuongSuaChua(RequestSuaChua request)
        {
            var results = new List<string>();

            try
            {
                //var checkEmployee = await _context.employee.FirstOrDefaultAsync(e => e.No == request.UserID);

                //if (checkEmployee == null)
                //{
                //    results.Add("Lỗi! Không tìm thấy thông tin nhân viên.");
                //    return results;
                //}
               
                var equip = await _context.Equipment
                    .FirstOrDefaultAsync(e => e.QRCode == request.QRCode);

                if (equip == null)
                {
                    results.Add($"Lỗi: Không tìm thấy thiết bị có QRCode {request.QRCode}!");
                    return results;
                }
                //var unit = await _context.Department.FirstOrDefaultAsync(e => e.Code == equip.ManageUnit);
                //if (!string.Equals(checkEmployee.WorkCenterCode, equip.ManageUnit, StringComparison.OrdinalIgnoreCase))
                //{
                //    results.Add($"Lỗi! Thiết bị {request.QRCode} thuộc đơn vị {unit?.Name}.");
                //    return results;
                //}

                if (equip.Status == 2)
                {
                    results.Add($"Lỗi: Thiết bị {request.QRCode} đang được sửa chữa!");
                    return results;
                }

                var existingRepair = await _context.repairRequests
                    .FirstOrDefaultAsync(r => r.QRCode == request.QRCode && r.Status == 0);

                if (existingRepair != null)
                {
                    results.Add($"Lỗi: Thiết bị {request.QRCode} đã có yêu cầu sửa chữa đang xử lý!");
                    return results;
                }

                // 4️⃣ Lấy thông tin NoSeriesLine để sinh số chứng từ
                var noSeriesLine = await _context.noSeriesLine
                    .FirstOrDefaultAsync(n => n.Code == "YCSC" && n.SeriesCode == "QLTB");

                if (noSeriesLine == null)
                {
                    results.Add("Lỗi: Không tìm thấy cấu hình đánh số (NoSeriesLine) cho YCSC - QLTB!");
                    return results;
                }

                int endNo = noSeriesLine.EndingNo;
                int incrementNo = noSeriesLine.IncrementByNo;
                int lastNoUsed = noSeriesLine.LastNoUsed;
                string startingNo = noSeriesLine.StartingNo;

                string no = startingNo + EquipmentMapper.FormatDocumentNumber(
                    endNo.ToString(),
                    (incrementNo + lastNoUsed).ToString()
                );

                // 5️⃣ Tạo yêu cầu sửa chữa
                var repairRequest = new RepairRequestList
                {
                    No = no,
                    EquipmentCode = equip.EquipmentCode,
                    EquipmentGroupCode = equip.EquipmentGroupCode,
                    Serial = equip.SerialNumber,
                    Brand = equip.Brand,
                    Model = equip.Model,
                    Reporter = request.UserID,
                    PostingDate = DateTime.Now,
                    QRCode = equip.QRCode,
                    LocationCode = equip.LocationCode,
                    WorkCenterCode = equip.UsingUnit,
                    Status = 0
                };
                _context.repairRequests.Add(repairRequest);

                // 6️⃣ Tạo lịch sử sửa chữa
                var repairHistory = new RepairHistory
                {
                    No = no,
                    EquipmentCode = equip.EquipmentCode,
                    EquipGroup = equip.EquipmentGroupCode,
                    Serial = equip.SerialNumber,
                    Brand = equip.Brand,
                    Model = equip.Model,
                    LocationCode = equip.LocationCode,
                    CreatedDate = DateTime.Now,
                    UserId = request.UserID,
                    QRCode = request.QRCode,
                    Status = 0
                };
                _context.repairHistory.Add(repairHistory);

                // 7️⃣ Kiểm tra bảo dưỡng định kỳ
                var checkMainTrack = await _context.maintenanceTrackings
                    .FirstOrDefaultAsync(e => e.QRCode == request.QRCode);
                var checkMainTrackWeek = await _context.maintenanceTrackingsWeek
                    .FirstOrDefaultAsync(e => e.QRCode == request.QRCode);

                if (checkMainTrack == null && checkMainTrackWeek == null)
                {
                    results.Add("Lỗi: Không tìm thấy ghi nhận bảo dưỡng phù hợp.");
                    return results;
                }

                // Kiểm tra thời gian bảo dưỡng
                var nextMaintenance = checkMainTrack?.NextMaintenance ?? checkMainTrackWeek?.NextMaintenance;
                if (nextMaintenance != null && DateTime.Now.Month < nextMaintenance.Value.Month)
                {
                    results.Add("Lỗi: Thiết bị này chưa đến thời hạn bảo dưỡng.");
                    return results;
                }

                // 8️⃣ Nếu là bảo dưỡng tuần
                if (checkMainTrackWeek != null)
                {
                    if (checkMainTrackWeek.Status == 1)
                    {
                        results.Add($"Lỗi: QRCode {request.QRCode} đã được bảo dưỡng tuần.");
                        return results;
                    }

                    var newHistory = new MaintenanceHistory
                    {
                        EquipmentCode = equip.EquipmentCode,
                        EquipGroupCode = equip.EquipmentGroupCode,
                        QRCode = request.QRCode,
                        Serial = equip.SerialNumber,
                        Brand = equip.Brand,
                        PostingDate = DateTime.Now,
                        ManageUnit = equip.ManageUnit,
                        UsingUnit = equip.UsingUnit,
                        Location = equip.LocationCode,
                        UserID = request.UserID,
                        MaintenanceType = checkMainTrackWeek.MaintenanceType,
                        NextMaintenanceTime = checkMainTrackWeek.NextMaintenance,
                        Status = 2 // trạng thái kết hợp bảo dưỡng + sửa chữa
                    };
                    _context.maintenanceHistory.Add(newHistory);

                    await _context.SaveChangesAsync();

                    checkMainTrackWeek.LastMaintenanceTime = DateTime.Now;
                    checkMainTrackWeek.NextMaintenance = (checkMainTrackWeek.NextMaintenance ?? DateTime.Now).AddDays(7);
                    checkMainTrackWeek.Status = 2;
                    await _context.SaveChangesAsync();
                }

                // 9️⃣ Nếu là bảo dưỡng định kỳ (tháng, quý, năm)
                else if (checkMainTrack != null)
                {
                    if (checkMainTrack.Status == 1)
                    {
                        results.Add($"Lỗi: QRCode {request.QRCode} đã được bảo dưỡng định kỳ.");
                        return results;
                    }

                    var newHistory = new MaintenanceHistory
                    {
                        EquipmentCode = equip.EquipmentCode,
                        EquipGroupCode = equip.EquipmentGroupCode,
                        QRCode = request.QRCode,
                        Serial = equip.SerialNumber,
                        Brand = equip.Brand,
                        PostingDate = DateTime.Now,
                        ManageUnit = equip.ManageUnit,
                        UsingUnit = equip.UsingUnit,
                        Location = equip.LocationCode,
                        UserID = request.UserID,
                        MaintenanceType = checkMainTrack.MaintenanceType,
                        NextMaintenanceTime = checkMainTrack.NextMaintenance,
                        Status = 2
                    };

                    _context.maintenanceHistory.Add(newHistory);
                    await _context.SaveChangesAsync();

                    checkMainTrack.LastMaintenanceTime = DateTime.Now;
                    checkMainTrack.MocBDGanNhat = checkMainTrack.NextMaintenance;

                    DateTime nextNgayBD = checkMainTrack.NextMaintenance ?? DateTime.Now;
                    switch (checkMainTrack.MaintenanceType)
                    {
                        case "3M":
                            checkMainTrack.NextMaintenance = nextNgayBD.AddMonths(3);
                            checkMainTrack.MaintenanceType = "6M";
                            break;
                        case "6M":
                            checkMainTrack.NextMaintenance = nextNgayBD.AddMonths(3);
                            checkMainTrack.MaintenanceType = "3M";
                            break;
                        case "1M":
                            checkMainTrack.NextMaintenance = nextNgayBD.AddMonths(1);
                            checkMainTrack.MaintenanceType = "1M";
                            break;
                        case "1Y":
                            checkMainTrack.NextMaintenance = nextNgayBD.AddMonths(12);
                            checkMainTrack.MaintenanceType = "1Y";
                            break;
                    }

                    checkMainTrack.Status = 2;
                    await _context.SaveChangesAsync();
                }

                // 🔟 Cập nhật số chứng từ và trạng thái thiết bị
                noSeriesLine.LastNoUsed = incrementNo + lastNoUsed;
                equip.Status = 2; // đang bảo dưỡng/sửa chữa
                await _context.SaveChangesAsync();

                results.Add($"✅ Đã tạo yêu cầu sửa chữa và ghi nhận bảo dưỡng cho thiết bị {request.QRCode} thành công!");
            }
            catch (Exception ex)
            {
                results.Add($"Lỗi hệ thống: {ex.InnerException?.Message ?? ex.Message}");
            }

            return results;
        }


        public async Task<InforRequestSC?> GetInforEquipSC(string qrCode)
        {
            if (string.IsNullOrWhiteSpace(qrCode))
                return null;

            // Lấy yêu cầu sửa chữa mới nhất (Status = 0)
            var repairList = await _context.repairRequests
                .Where(r => r.QRCode == qrCode && r.Status == 0)
                .OrderByDescending(r => r.PostingDate)
                .FirstOrDefaultAsync();

            if (repairList == null)
                return null;

            // Lấy thông tin nhóm thiết bị
            var equipGroup = await _context.equipmentGroup
                .FirstOrDefaultAsync(e => e.Code == repairList.EquipmentGroupCode);

            // Lấy thông tin ca làm việc theo Location
            var workShift = await _context.locationXSDs
                .FirstOrDefaultAsync(e => e.Code == repairList.LocationCode);

            // Trả về DTO thông tin yêu cầu sửa chữa
            return new InforRequestSC
            {
                QRCode = qrCode,
                EquipmentName = equipGroup?.Name ?? "(Không xác định)",
                Serial = repairList.Serial,
                Brand = repairList.Brand,
                Model = repairList.Model,
                WorkShift = workShift?.Name ?? "(Không xác định)",
                Status = 0
            };
        }

        public async Task<List<string>> Process_BatDauSuaChua(RequestSuaChua request)
        {
            var result = new List<string>();

            try
            {
                // Tìm yêu cầu sửa chữa mới nhất (Status = 0)
                var repairList = await _context.repairRequests
                    .Where(r => r.QRCode == request.QRCode && r.Status == 0)
                    .OrderByDescending(r => r.PostingDate)
                    .FirstOrDefaultAsync();

                if (repairList == null)
                {
                    result.Add("Lỗi: Không tìm thấy danh sách yêu cầu sửa chữa!");
                    return result;
                }

                // Ghi lịch sử sửa chữa
                var repairHistory = new RepairHistory
                {
                    No = repairList.No,
                    EquipmentCode = repairList.EquipmentCode,
                    EquipGroup = repairList.EquipmentGroupCode,
                    Serial = repairList.Serial,
                    Brand = repairList.Brand,
                    Model = repairList.Model,
                    LocationCode = repairList.LocationCode,
                    CreatedDate = DateTime.Now,
                    UserId = request.UserID,
                    QRCode = request.QRCode,
                    Status = 1 // Bắt đầu sửa chữa
                };
                _context.repairHistory.Add(repairHistory);

                // Cập nhật trạng thái yêu cầu
                repairList.FromDate = DateTime.Now;
                repairList.Status = 1;

                // Cập nhật trạng thái thiết bị
                var equip = await _context.Equipment.FirstOrDefaultAsync(e => e.QRCode == repairList.QRCode);
                if (equip == null)
                {
                    result.Add("Lỗi: Không tìm thấy thiết bị trong danh sách máy!");
                    return result;
                }

                // Nếu thiết bị chưa ở trạng thái sửa chữa => cập nhật lại
                if (equip.Status != 2 || equip.StatusGroup != 0)
                {
                    equip.Status = 2;        // Đang sửa chữa
                    equip.StatusGroup = 1;   // Nhóm trạng thái sửa chữa
                }

                await _context.SaveChangesAsync();
                result.Add("Bắt đầu sửa chữa thành công!");
            }
            catch (Exception ex)
            {
                result.Add("Lỗi hệ thống: " + (ex.InnerException?.Message ?? ex.Message));
            }

            return result;
        }
        public async Task<EquipmentDTO> GetInforEquipment(string code, HttpRequest request)
        {
            Equipment equipment = await _context.Equipment.FirstOrDefaultAsync((Equipment u) => u.QRCode.ToLower() == code.ToLower());
            if (equipment == null)
            {
                return null;
            }
            string equipName = await (from e in _context.equipmentGroup
                                      where e.Code == equipment.EquipmentGroupCode
                                      select e.Name).FirstOrDefaultAsync();
            string location = await (from e in _context.locationXSDs
                                     where e.Code == equipment.LocationCode
                                     select e.Name).FirstOrDefaultAsync();
            string unitName = await (from e in _context.Department
                                     where e.Code == equipment.ManageUnit
                                     select e.Name).FirstOrDefaultAsync();
            string baseUrl = $"{request.Scheme}://{request.Host}";
            MaintenanceHistory history = await (from r in _context.maintenanceHistory
                                                where r.QRCode == code
                                                orderby r.PostingDate descending
                                                select r).FirstOrDefaultAsync();
            MaintenanceTracking tracking = await _context.maintenanceTrackings.Where((MaintenanceTracking e) => e.QRCode == code).FirstOrDefaultAsync();
            string employeeName = string.Empty;
            if (history != null && !string.IsNullOrEmpty(history.UserID))
            {
                employeeName = (await _context.employee.FirstOrDefaultAsync((Employee e) => e.No == history.UserID))?.Name ?? string.Empty;
            }
            return new EquipmentDTO
            {
                EquipmentCode = equipment.EquipmentCode,
                EquipmentName = equipName,
                ManageUnit = equipment.ManageUnit,
                ManageUnitName = unitName,
                EquipmentGroupCode = equipment.EquipmentGroupCode,
                Model = equipment.Model,
                SerialNumber = equipment.SerialNumber,
                Brand = equipment.Brand,
                QRCode = equipment.QRCode,
                NamSX = equipment?.ManufacturingYear,
                NamSD = equipment?.YearOfImport,
                SoNamSD = equipment?.UsageYears,
                Image = string.IsNullOrEmpty(equipment.Image) ? null: $"{baseUrl}/{equipment.Image.Replace("\\", "/")}",
                LocationCode = equipment.LocationCode,
                LocationName = location,
                Status = equipment.Status,
                StatusGroup = equipment.StatusGroup,
                LastMaintenanceTime = (history?.PostingDate?.ToString("dd/MM/yyyy") ?? string.Empty),
                PlanTime = history?.NextMaintenanceTime?.ToString("dd/MM/yyyy"),
                User = employeeName,
                MaintenanceType = history?.MaintenanceType,
                Check = (tracking?.Status ?? (-1))
            };
        }

        public async Task<EquipmentDTO> GetInforEquipmentByCode(string code, HttpRequest request)
        {
            if (string.IsNullOrWhiteSpace(code))
                return null;

            // Lấy thông tin thiết bị theo mã thiết bị (EquipmentCode)
            var equipment = await _context.Equipment
                .FirstOrDefaultAsync(u => u.EquipmentCode == code);

            if (equipment == null)
                return null;

            // Lấy các thông tin mô tả thiết bị
            var equipName = await _context.equipmentGroup
                .Where(e => e.Code == equipment.EquipmentGroupCode)
                .Select(e => e.Name)
                .FirstOrDefaultAsync();

            var locationName = await _context.locationXSDs
                .Where(e => e.Code == equipment.LocationCode)
                .Select(e => e.Name)
                .FirstOrDefaultAsync();

            var unitName = await _context.Department
                .Where(e => e.Code == equipment.ManageUnit)
                .Select(e => e.Name)
                .FirstOrDefaultAsync();

            // Base URL của server để hiển thị ảnh
            var baseUrl = $"{request.Scheme}://{request.Host}";

            // Lấy lịch sử bảo dưỡng gần nhất
            var history = await _context.maintenanceHistory
                .Where(r => r.QRCode == code)
                .OrderByDescending(r => r.PostingDate)
                .FirstOrDefaultAsync();

            // Lấy tracking bảo dưỡng hiện tại
            var tracking = await _context.maintenanceTrackings
                .FirstOrDefaultAsync(e => e.QRCode == code);

            // Lấy tên nhân viên thực hiện lần bảo dưỡng gần nhất (nếu có)
            string employeeName = string.Empty;
            if (history != null && !string.IsNullOrEmpty(history.UserID))
            {
                employeeName = await _context.employee
                    .Where(e => e.No == history.UserID)
                    .Select(e => e.Name)
                    .FirstOrDefaultAsync() ?? string.Empty;
            }

            // Trả về đối tượng DTO
            return new EquipmentDTO
            {
                EquipmentCode = equipment.EquipmentCode,
                EquipmentName = equipName,
                ManageUnit = equipment.ManageUnit,
                ManageUnitName = unitName,
                EquipmentGroupCode = equipment.EquipmentGroupCode,
                Model = equipment.Model,
                SerialNumber = equipment.SerialNumber,
                Brand = equipment.Brand,
                QRCode = equipment.QRCode,
                NamSX = equipment.ManufacturingYear,
                NamSD = equipment.YearOfImport,
                SoNamSD = equipment.UsageYears,
                Image = string.IsNullOrEmpty(equipment.Image) ? null : $"{baseUrl}/{equipment.Image}",
                LocationCode = equipment.LocationCode,
                LocationName = locationName,
                Status = equipment.Status,
                StatusGroup = equipment.StatusGroup,
                LastMaintenanceTime = history?.PostingDate?.ToString("dd/MM/yyyy") ?? string.Empty,
                PlanTime = history?.NextMaintenanceTime?.ToString("dd/MM/yyyy"),
                User = employeeName,
                MaintenanceType = history?.MaintenanceType,
                Check = tracking?.Status ?? -1
            };
        }

        public async Task<EquipmentDTO> GetInforEquipmentBySerial(string code, HttpRequest request)
        {
            if (string.IsNullOrWhiteSpace(code))
                return null;

            // Tìm thiết bị theo SerialNumber (không phân biệt hoa thường)
            var equipment = await _context.Equipment
                .FirstOrDefaultAsync(u => u.SerialNumber.ToLower() == code.ToLower());

            if (equipment == null)
                return null;

            // Lấy thông tin liên quan
            string equipName = await _context.equipmentGroup
                .Where(e => e.Code == equipment.EquipmentGroupCode)
                .Select(e => e.Name)
                .FirstOrDefaultAsync() ?? string.Empty;

            string location = await _context.locationXSDs
                .Where(e => e.Code == equipment.LocationCode)
                .Select(e => e.Name)
                .FirstOrDefaultAsync() ?? string.Empty;

            string unitName = await _context.Department
                .Where(e => e.Code == equipment.ManageUnit)
                .Select(e => e.Name)
                .FirstOrDefaultAsync() ?? string.Empty;

            string baseUrl = $"{request.Scheme}://{request.Host}";

            var history = await _context.maintenanceHistory
                .Where(r => r.QRCode == equipment.QRCode)
                .OrderByDescending(r => r.PostingDate)
                .FirstOrDefaultAsync();

            var tracking = await _context.maintenanceTrackings
                .FirstOrDefaultAsync(e => e.QRCode == equipment.QRCode);

            string employeeName = string.Empty;
            if (history != null && !string.IsNullOrEmpty(history.UserID))
            {
                employeeName = await _context.employee
                    .Where(e => e.No == history.UserID)
                    .Select(e => e.Name)
                    .FirstOrDefaultAsync() ?? string.Empty;
            }

            // Trả về DTO
            return new EquipmentDTO
            {
                EquipmentCode = equipment.EquipmentCode,
                EquipmentName = equipName,
                ManageUnit = equipment.ManageUnit,
                ManageUnitName = unitName,
                EquipmentGroupCode = equipment.EquipmentGroupCode,
                Model = equipment.Model,
                SerialNumber = equipment.SerialNumber,
                Brand = equipment.Brand,
                QRCode = equipment.QRCode,
                NamSX = equipment.ManufacturingYear,
                NamSD = equipment.YearOfImport,
                SoNamSD = equipment.UsageYears,
                Image = string.IsNullOrEmpty(equipment.Image) ? null : $"{baseUrl}/{equipment.Image}",
                LocationCode = equipment.LocationCode,
                LocationName = location,
                Status = equipment.Status,
                StatusGroup = equipment.StatusGroup,
                LastMaintenanceTime = history?.PostingDate?.ToString("dd/MM/yyyy") ?? string.Empty,
                PlanTime = history?.NextMaintenanceTime?.ToString("dd/MM/yyyy"),
                User = employeeName,
                MaintenanceType = history?.MaintenanceType,
                Check = tracking?.Status ?? -1
            };
        }

        public async Task<InforRequestSC> GetInforEquipHTSC(string qrCode)
        {
            if (string.IsNullOrWhiteSpace(qrCode))
                return null;

            // Tìm thiết bị theo QRCode (không phân biệt hoa thường)
            var equip = await _context.Equipment
                .FirstOrDefaultAsync(e => e.QRCode.ToLower() == qrCode.ToLower());

            if (equip == null)
                return null;

            // Lấy yêu cầu sửa chữa gần nhất còn mở (Status == 1)
            var repairList = await _context.repairRequests
                .Where(r => r.QRCode == equip.QRCode && r.Status == 1)
                .OrderByDescending(r => r.PostingDate)
                .FirstOrDefaultAsync();

            if (repairList == null)
                return null;

            // Lấy thông tin nhóm thiết bị
            var equipGroup = await _context.equipmentGroup
                .FirstOrDefaultAsync(e => e.Code == repairList.EquipmentGroupCode);

            // Lấy thông tin khu vực/làm việc
            var workShift = await _context.locationXSDs
                .FirstOrDefaultAsync(e => e.Code == equip.LocationCode);

            Department workCenter = null;
            if (!string.IsNullOrEmpty(workShift?.DepartmentCode))
            {
                workCenter = await _context.Department.FirstOrDefaultAsync(e => e.Code == workShift.DepartmentCode);
            }

            // Trả về DTO
            return new InforRequestSC
            {
                QRCode = qrCode,
                EquipmentName = equipGroup?.Name ?? string.Empty,
                Serial = repairList.Serial ?? string.Empty,
                Brand = repairList.Brand ?? string.Empty,
                Model = repairList.Model ?? string.Empty,
                WorkShiftCode = workShift?.Code ?? string.Empty,
                WorkShift = workShift?.Name ?? string.Empty,
                WorkCenter = workCenter?.Name ?? string.Empty,
                Status = 7 
            };
        }

        public async Task<InforRequestSC> GetInforEquipHTSC_Serial(string serial)
        {
            if (string.IsNullOrWhiteSpace(serial))
                return null;

            // Tìm thiết bị theo SerialNumber (không phân biệt hoa thường)
            var equip = await _context.Equipment
                .FirstOrDefaultAsync(e => e.SerialNumber.ToLower() == serial.ToLower());

            if (equip == null)
                return null;

            // Lấy yêu cầu sửa chữa gần nhất còn mở (Status == 1)
            var repairList = await _context.repairRequests
                .Where(r => r.QRCode == equip.QRCode && r.Status == 1)
                .OrderByDescending(r => r.PostingDate)
                .FirstOrDefaultAsync();

            if (repairList == null)
                return null;

            // Lấy thông tin nhóm thiết bị
            var equipGroup = await _context.equipmentGroup
                .FirstOrDefaultAsync(e => e.Code == repairList.EquipmentGroupCode);

            // Lấy thông tin khu vực/làm việc
            var workShift = await _context.locationXSDs
                .FirstOrDefaultAsync(e => e.Code == equip.LocationCode);

            Department workCenter = null;
            if (!string.IsNullOrEmpty(workShift?.DepartmentCode))
            {
                workCenter = await _context.Department
                    .FirstOrDefaultAsync(e => e.Code == workShift.DepartmentCode);
            }

            // Trả về thông tin tổng hợp
            return new InforRequestSC
            {
                QRCode = equip.QRCode ?? string.Empty,
                EquipmentName = equipGroup?.Name ?? string.Empty,
                Serial = repairList.Serial ?? string.Empty,
                Brand = repairList.Brand ?? string.Empty,
                Model = repairList.Model ?? string.Empty,
                WorkShiftCode = workShift?.Code ?? string.Empty,
                WorkShift = workShift?.Name ?? string.Empty,
                WorkCenter = workCenter?.Name ?? string.Empty,
                Status = 7 
            };
        }


        public async Task<List<ReasonTypeDTO>> GetReasonType()
        {
            var result = await _context.repairType.Select(e => new ReasonTypeDTO
            {
                Value = e.Code,
                Label = e.Name
            }).ToListAsync();
            return result;
        }
        public async Task<List<string>> Process_HTSC(RequestHTSC request)
        {
            var result = new List<string>();

            try
            {
                if (string.IsNullOrWhiteSpace(request?.QRCode))
                {
                    result.Add("Lỗi: QRCode không hợp lệ!");
                    return result;
                }

                // Lấy yêu cầu sửa chữa đang hoạt động (Status = 1)
                var repairList = await _context.repairRequests
                    .Where(r => r.QRCode == request.QRCode && r.Status == 1)
                    .OrderByDescending(r => r.PostingDate)
                    .FirstOrDefaultAsync();

                if (repairList == null)
                {
                    result.Add("Không tìm thấy yêu cầu sửa chữa đang hoạt động!");
                    return result;
                }

                // Ghi lịch sử sửa chữa
                var newHistory = new RepairHistory
                {
                    No = repairList.No,
                    EquipmentCode = repairList.EquipmentCode,
                    EquipGroup = repairList.EquipmentGroupCode,
                    Serial = repairList.Serial,
                    Brand = repairList.Brand,
                    Model = repairList.Model,
                    LocationCode = repairList.LocationCode,
                    CreatedDate = DateTime.Now,
                    UserId = request.UserID,
                    QRCode = repairList.QRCode,
                    Status = 2 // Hoàn thành
                };
                _context.repairHistory.Add(newHistory);

                // Lưu lý do sửa chữa
                var repairType = await _context.repairType
                    .FirstOrDefaultAsync(e => e.Code == request.ReasonType);

                var content = new RepairContent
                {
                    DocNo = repairList.No,
                    RepairType = request.ReasonType,
                    Name = repairType?.Name ?? "Không xác định",
                    Detail = request.Description ?? string.Empty
                };
                _context.repairContent.Add(content);

                // Cập nhật yêu cầu sửa chữa
                repairList.Status = 2; // Hoàn tất
                repairList.ToDate = DateTime.Now;

                if (repairList.FromDate.HasValue)
                {
                    var durationMinutes = (decimal)Math.Round(
                        (DateTime.Now - repairList.FromDate.Value).TotalMinutes, 0
                    );
                    repairList.Duration = durationMinutes;
                }

                // Cập nhật trạng thái thiết bị
                var equip = await _context.Equipment
                    .FirstOrDefaultAsync(e => e.QRCode == request.QRCode);

                if (equip != null)
                {
                    if (equip.StatusGroup == 0 && equip.Status == 2)
                    {
                        equip.Status = 14;
                    }
                    else
                    {
                        equip.Status = 1;
                        equip.StatusGroup = 1;
                    }
                }
                await _context.SaveChangesAsync();

                result.Add("Xác nhận hoàn thành sửa chữa thành công!");
            }
            catch (Exception ex)
            {
                result.Add($"Lỗi: {ex.Message}");
            }

            return result;
        }
        public async Task<List<string>> Process_KhongSuaDuoc_Dieunguoi(RequestHTSC request)
        {
            var result = new List<string>();

            if (request == null || string.IsNullOrWhiteSpace(request.QRCode))
            {
                result.Add("Yêu cầu không hợp lệ (thiếu thông tin QRCode).");
                return result;
            }

            try
            {
                var repairList = await _context.repairRequests
                    .Where(r => r.QRCode == request.QRCode && r.Status == 1)
                    .OrderByDescending(r => r.PostingDate)
                    .FirstOrDefaultAsync();

                if (repairList == null)
                {
                    result.Add($"Không tìm thấy yêu cầu sửa chữa đang xử lý cho QRCode: {request.QRCode}");
                    return result;
                }

                var newHistory = new RepairHistory
                {
                    No = repairList.No,
                    EquipmentCode = repairList.EquipmentCode,
                    EquipGroup = repairList.EquipmentGroupCode,
                    Serial = repairList.Serial,
                    Brand = repairList.Brand,
                    Model = repairList.Model,
                    LocationCode = repairList.LocationCode,
                    CreatedDate = DateTime.Now,
                    UserId = request.UserID,
                    QRCode = request.QRCode,
                    Status = 3
                };

                _context.repairHistory.Add(newHistory);
                repairList.Status = 0;

                await _context.SaveChangesAsync();

                result.Add($"✅ Xác nhận thay người sửa thiết bị {repairList.QRCode} thành công!");
            }
            catch (Exception ex)
            {
                result.Add($"❌ Lỗi: {ex.Message}");
            }

            return result;
        }
        public async Task<List<string>> Process_KhongSuaDuoc_Dieumay(RequestDieuMay request)
        {
            var result = new List<string>();

            if (request == null || string.IsNullOrWhiteSpace(request.OldQRCode) || string.IsNullOrWhiteSpace(request.NewQRCode))
            {
                result.Add("Yêu cầu không hợp lệ (thiếu QRCode cũ hoặc mới).");
                return result;
            }

            try
            {
                var repairList = await _context.repairRequests
                    .Where(r => r.QRCode == request.OldQRCode && r.Status == 1)
                    .OrderByDescending(r => r.PostingDate)
                    .FirstOrDefaultAsync();

                if (repairList == null)
                {
                    result.Add($"Không tìm thấy yêu cầu sửa chữa đang xử lý cho thiết bị {request.OldQRCode}");
                    return result;
                }

                var newEquip = await _context.Equipment.FirstOrDefaultAsync(e => e.QRCode == request.NewQRCode);
                if (newEquip == null)
                {
                    result.Add($"Thiết bị mới {request.NewQRCode} không tồn tại trong hệ thống.");
                    return result;
                }

                // Kiểm tra trạng thái máy mới
                if (newEquip.StatusGroup == 1)
                {
                    result.Add($"Lỗi: Thiết bị {request.NewQRCode} đang được sử dụng, không thể điều!");
                    return result;
                }
                if (newEquip.StatusGroup == -1)
                {
                    result.Add($"Lỗi: Thiết bị {request.NewQRCode} đang hỏng hoặc không khả dụng, không thể điều!");
                    return result;
                }

                var oldEquip = await _context.Equipment.FirstOrDefaultAsync(e => e.QRCode == request.OldQRCode);
                if (oldEquip == null)
                {
                    result.Add($"Thiết bị cũ {request.OldQRCode} không tồn tại trong hệ thống.");
                    return result;
                }

                // Ghi lịch sử
                var newHistory = new RepairHistory
                {
                    No = repairList.No,
                    EquipmentCode = repairList.EquipmentCode,
                    EquipGroup = repairList.EquipmentGroupCode,
                    Serial = repairList.Serial,
                    Brand = repairList.Brand,
                    Model = repairList.Model,
                    LocationCode = repairList.LocationCode,
                    CreatedDate = DateTime.Now,
                    UserId = request.UserID,
                    QRCode = request.OldQRCode,
                    Status = 4 // trạng thái: đã đổi máy
                };
                _context.repairHistory.Add(newHistory);

                // Cập nhật trạng thái yêu cầu và thiết bị
                repairList.Status = 0;
                oldEquip.StatusGroup = 0;
                oldEquip.Status = 0;

                var locationOldEquip = await _context.locationXSDs.FirstOrDefaultAsync(e => e.Code == request.WorkShiftCode);
                if (locationOldEquip != null)
                {
                    if (locationOldEquip.DepartmentCode == "0101")
                        oldEquip.LocationCode = "01010116";
                    else if (locationOldEquip.DepartmentCode == "0102")
                        oldEquip.LocationCode = "01020126";
                }

                // Cập nhật máy mới
                newEquip.LocationCode = request.WorkShiftCode;
                newEquip.Status = 1;
                newEquip.StatusGroup = 1;

                await _context.SaveChangesAsync();

                result.Add($"✅ Đã xác nhận thay thiết bị cũ {request.OldQRCode} bằng thiết bị mới {request.NewQRCode} thành công!");
            }
            catch (Exception ex)
            {
                result.Add($"❌ Lỗi: {ex.Message}");
            }

            return result;
        }


        //public async Task<List<XacnhanHT_DTO>> Process_GetListYeuCauXN()
        //{
        //    var result = new List<XacnhanHT_DTO>();

        //    try
        //    {
        //        // Lấy toàn bộ các yêu cầu có Status = 0 cùng các thông tin liên quan trong 1 truy vấn duy nhất
        //        var query = from yc in _context.yeucauBQLCXacNhan
        //                    where yc.Status == 0
        //                    join emp in _context.employee on yc.Requester equals emp.No into empJoin
        //                    from emp in empJoin.DefaultIfEmpty()
        //                    join eg in _context.equipmentGroup on yc.EquipmentGroupCode equals eg.Code into egJoin
        //                    from eg in egJoin.DefaultIfEmpty()
        //                    join loc in _context.locationXSDs on yc.LocationCode equals loc.Code into locJoin
        //                    from loc in locJoin.DefaultIfEmpty()
        //                    join rt in _context.repairType on yc.RepairType equals rt.Code into rtJoin
        //                    from rt in rtJoin.DefaultIfEmpty()
        //                    select new XacnhanHT_DTO
        //                    {
        //                        RowID = yc.RowID,
        //                        QRCode = yc.QRCode,
        //                        EquipmentGroup = yc.EquipmentGroupCode,
        //                        Requester = emp != null ? emp.Name : string.Empty,
        //                        LocationCode = yc.LocationCode,
        //                        Location = loc != null ? loc.Name : string.Empty,
        //                        EquipmentName = eg != null ? eg.Name : string.Empty,
        //                        RepairType = yc.RepairType,
        //                        RepairTypeName = rt != null ? rt.Name : string.Empty,
        //                        Description = yc.Description
        //                    };

        //        result = await query.ToListAsync();
        //    }
        //    catch (Exception ex)
        //    {
        //        // Nếu có lỗi, ghi nhận thông báo thay vì crash
        //        result.Add(new XacnhanHT_DTO
        //        {
        //            Description = "Lỗi khi lấy danh sách xác nhận hoàn thành: " + ex.Message
        //        });
        //    }

        //    return result;
        //}

        public async Task<List<RepairListDTO>> Process_GetListYeuCau_User(string userId)
        {
            try
            {
                var workcenter = await _context.employee.Where(e => e.No == userId).Select(e => e.WorkCenterCode).FirstOrDefaultAsync();
                // Nếu user không có WorkCenterCode thì trả rỗng
                if (string.IsNullOrEmpty(workcenter))
                    return new List<RepairListDTO>();
                var query = from r in _context.repairRequests
                                join emp in _context.employee on r.Reporter equals emp.No into empJoin
                                from emp in empJoin.DefaultIfEmpty()
                                join eg in _context.equipmentGroup on r.EquipmentGroupCode equals eg.Code into egJoin
                                from eg in egJoin.DefaultIfEmpty()
                                join loc in _context.locationXSDs on r.LocationCode equals loc.Code into locJoin
                                from loc in locJoin.DefaultIfEmpty()
                                where r.Status == 0 && r.WorkCenterCode == workcenter
                                orderby r.PostingDate descending
                                select new RepairListDTO
                                {
                                    No = r.No,
                                    QRCode = r.QRCode,
                                    EquipmentName = eg != null ? eg.Name : string.Empty,
                                    Location = loc != null ? loc.Name : string.Empty,
                                    Model = r.Model,
                                    Reporter = emp != null ? emp.Name : string.Empty,
                                    PostingDate = r.PostingDate
                                };
                
               

                return await query.ToListAsync();
            }
            catch (Exception ex)
            {
                // Trả danh sách trống và có thể log lỗi
                return new List<RepairListDTO>
        {
            new RepairListDTO
            {
                EquipmentName = "Lỗi khi lấy danh sách yêu cầu: " + ex.Message
            }
        };
            }
        }
        public async Task<List<RepairListDTO>> Process_GetListYeuCau()
        {
            try
            {
                  var  query = from r in _context.repairRequests
                            join emp in _context.employee on r.Reporter equals emp.No into empJoin
                            from emp in empJoin.DefaultIfEmpty()
                            join eg in _context.equipmentGroup on r.EquipmentGroupCode equals eg.Code into egJoin
                            from eg in egJoin.DefaultIfEmpty()
                            join loc in _context.locationXSDs on r.LocationCode equals loc.Code into locJoin
                            from loc in locJoin.DefaultIfEmpty()
                            where r.Status == 0
                            orderby r.PostingDate descending
                            select new RepairListDTO
                            {
                                No = r.No,
                                QRCode = r.QRCode,
                                EquipmentName = eg != null ? eg.Name : string.Empty,
                                Location = loc != null ? loc.Name : string.Empty,
                                Model = r.Model,
                                Reporter = emp != null ? emp.Name : string.Empty,
                                PostingDate = r.PostingDate
                            };
               
                return await query.ToListAsync();
            }
            catch (Exception ex)
            {
                // Trả danh sách trống và có thể log lỗi
                return new List<RepairListDTO>
        {
            new RepairListDTO
            {
                EquipmentName = "Lỗi khi lấy danh sách yêu cầu: " + ex.Message
            }
        };
            }
        }

        //public async Task<List<RepairListDTO>> Process_GetListYeuCau()
        //{
        //    try
        //    {

        //          var  query = from r in _context.repairRequests
        //                    join emp in _context.employee on r.Reporter equals emp.No into empJoin
        //                    from emp in empJoin.DefaultIfEmpty()
        //                    join eg in _context.equipmentGroup on r.EquipmentGroupCode equals eg.Code into egJoin
        //                    from eg in egJoin.DefaultIfEmpty()
        //                    join loc in _context.locationXSDs on r.LocationCode equals loc.Code into locJoin
        //                    from loc in locJoin.DefaultIfEmpty()
        //                    where r.Status == 0 
        //                    orderby r.PostingDate descending
        //                    select new RepairListDTO
        //                    {
        //                        No = r.No,
        //                        QRCode = r.QRCode,
        //                        EquipmentName = eg != null ? eg.Name : string.Empty,
        //                        Location = loc != null ? loc.Name : string.Empty,
        //                        Model = r.Model,
        //                        Reporter = emp != null ? emp.Name : string.Empty
        //                    };


        //        return await query.ToListAsync();
        //    }
        //    catch (Exception ex)
        //    {
        //        // Trả danh sách trống và có thể log lỗi
        //        return new List<RepairListDTO>
        //{
        //    new RepairListDTO
        //    {
        //        EquipmentName = "Lỗi khi lấy danh sách yêu cầu: " + ex.Message
        //    }
        //};
        //    }
        //}
        public async Task<List<string>> Process_XacNhanHoanThanh(XacnhanHT_DTO request)
        {
            var result = new List<string>();

            try
            {
                // 1️⃣ Tìm yêu cầu xác nhận mới nhất theo QRCode
                var yeucau = await _context.yeucauBQLCXacNhan
                    .Where(r => r.QRCode == request.QRCode && r.PostingDate != null)
                    .OrderByDescending(r => r.PostingDate)
                    .FirstOrDefaultAsync();

                if (yeucau == null)
                {
                    result.Add($"Không tìm thấy yêu cầu xác nhận cho QRCode {request.QRCode}!");
                    return result;
                }

                // 2️⃣ Lấy yêu cầu sửa chữa tương ứng
                var repairList = await _context.repairRequests
                    .FirstOrDefaultAsync(r => r.No == yeucau.DocNo && r.QRCode == request.QRCode);

                if (repairList == null)
                {
                    result.Add($"Không tìm thấy yêu cầu sửa chữa cho QRCode {request.QRCode}!");
                    return result;
                }

                // 3️⃣ Nếu thiết bị đang được sửa (Status = 1)
                if (repairList.Status == 1)
                {
                    // ➕ Lưu lịch sử sửa chữa
                    var newHistory = new RepairHistory
                    {
                        No = yeucau.DocNo,
                        EquipmentCode = repairList.EquipmentCode,
                        EquipGroup = yeucau.EquipmentGroupCode,
                        Serial = repairList.Serial,
                        Brand = repairList.Brand,
                        Model = repairList.Model,
                        LocationCode = yeucau.LocationCode,
                        CreatedDate = DateTime.Now,
                        UserId = request.Requester,
                        QRCode = request.QRCode,
                        Status = 3
                    };
                    _context.repairHistory.Add(newHistory);

                    // ➕ Lưu nội dung sửa chữa
                    var repairType = await _context.repairType.FirstOrDefaultAsync(t => t.Code == yeucau.RepairType);
                    var newContent = new RepairContent
                    {
                        DocNo = yeucau.DocNo,
                        RepairType = yeucau.RepairType,
                        Name = repairType?.Name ?? "Không xác định",
                        Detail = yeucau.Description
                    };
                    _context.repairContent.Add(newContent);

                    // 🔄 Cập nhật yêu cầu sửa
                    repairList.Status = 2; // hoàn tất
                    repairList.ToDate = DateTime.Now;

                    if (repairList.FromDate.HasValue)
                    {
                        var duration = (DateTime.Now - repairList.FromDate.Value).TotalMinutes;
                        repairList.Duration = (decimal)Math.Round(duration, 0);
                    }

                    // ⚙️ Cập nhật trạng thái thiết bị
                    var equip = await _context.Equipment.FirstOrDefaultAsync(e => e.QRCode == request.QRCode);
                    if (equip != null)
                    {
                        equip.Status = 1;       // hoạt động
                        equip.StatusGroup = 1;  // nhóm hoạt động
                    }

                    yeucau.Status = 1;

                    await _context.SaveChangesAsync();

                    result.Add($"Xác nhận thiết bị {request.QRCode} đã sửa chữa thành công!");
                }
                else
                {
                    result.Add($"Lỗi: thiết bị {request.QRCode} chưa được sửa chữa!");
                }
            }
            catch (Exception ex)
            {
                result.Add($"Lỗi hệ thống: {ex.Message}");
            }

            return result;
        }
        public async Task<List<string>> Process_KhongHTSC(XacnhanHT_DTO request)
        {
            var result = new List<string>();

            try
            {
                // 1️⃣ Tìm yêu cầu xác nhận theo QRCode
                var yeucau = await _context.yeucauBQLCXacNhan
                    .FirstOrDefaultAsync(y => y.QRCode == request.QRCode);

                if (yeucau == null)
                {
                    result.Add($"Không tìm thấy yêu cầu xác nhận cho QRCode {request.QRCode}!");
                    return result;
                }

                // 2️⃣ Tìm yêu cầu sửa chữa tương ứng
                var repairList = await _context.repairRequests
                    .FirstOrDefaultAsync(r => r.No == yeucau.DocNo && r.QRCode == request.QRCode);

                if (repairList == null)
                {
                    result.Add($"Không tìm thấy yêu cầu sửa chữa cho QRCode {request.QRCode}!");
                    return result;
                }

                // 3️⃣ Nếu thiết bị đang được sửa
                if (repairList.Status == 1)
                {
                    // ➕ Ghi lại lịch sử với trạng thái KHÔNG HOÀN THÀNH
                    var newHistory = new RepairHistory
                    {
                        No = yeucau.DocNo,
                        EquipmentCode = repairList.EquipmentCode,
                        EquipGroup = yeucau.EquipmentGroupCode,
                        Serial = repairList.Serial,
                        Brand = repairList.Brand,
                        Model = repairList.Model,
                        LocationCode = yeucau.LocationCode,
                        CreatedDate = DateTime.Now,
                        UserId = request.Requester,
                        QRCode = request.QRCode,
                        Reason = request.Reason,
                        Status = 4 // không sửa chữa thành công
                    };

                    _context.repairHistory.Add(newHistory);

                    // 🔄 Cập nhật trạng thái yêu cầu xác nhận
                    yeucau.Status = 2; // không hoàn thành

                    // ⚙️ Có thể cập nhật trạng thái thiết bị nếu cần (tùy business logic)
                    // var equip = await _context.Equipment.FirstOrDefaultAsync(e => e.QRCode == request.QRCode);
                    // if (equip != null)
                    // {
                    //     equip.Status = 0;       // thiết bị hỏng
                    //     equip.StatusGroup = 2;  // nhóm không hoạt động
                    // }

                    await _context.SaveChangesAsync();

                    result.Add($"Xác nhận thiết bị {request.QRCode} không sửa chữa thành công!");
                }
                else
                {
                    result.Add($"Lỗi: thiết bị {request.QRCode} chưa được sửa chữa hoặc không ở trạng thái đang sửa!");
                }
            }
            catch (Exception ex)
            {
                result.Add($"Lỗi hệ thống: {ex.Message}");
            }

            return result;
        }

        public async Task<(bool Success, string Message)> DeleteYeuCauSC(string code, string userId)
        {
            var repairRequest = await _context.repairRequests
                .FirstOrDefaultAsync(e => e.No == code && e.Reporter == userId);

            if (repairRequest == null)
            {
                return (false, $"Không tìm thấy yêu cầu sửa chữa");
            }

            var repairHis = await _context.repairHistory
                .FirstOrDefaultAsync(e => e.No == code && e.Status != 0);

            if (repairHis != null)
            {
                return (false, $"Không thể xoá yêu cầu vì đã có lịch sử sửa chữa liên quan.");
            }

            _context.repairRequests.Remove(repairRequest);
            await _context.SaveChangesAsync();

            return (true, $"Đã xoá thành công yêu cầu sửa chữa");
        }


        public async Task<List<RepairHistoryListDTO>> Process_GetListRepairHistory(string code)
        {
            var repairList = await (from r in _context.repairRequests
                                    where r.EquipmentCode == code
                                    orderby r.PostingDate descending
                                    select r).ToListAsync();

            var result = new List<RepairHistoryListDTO>();
            string status2 = default;

            foreach (var item in repairList)
            {
                var content = await _context.repairContent
                    .FirstOrDefaultAsync(e => e.DocNo == item.No);

                var repairHistoryListDTO = new RepairHistoryListDTO
                {
                    id = item.No,
                    equipCode = item.EquipmentCode,
                    postingDate = item.PostingDate.ToString("dd/MM/yyyy"),
                    title = content?.Name,
                    assignee = item.Reporter,
                    content = content?.Detail,
                    fromDate = item?.FromDate?.ToString("dd/MM/yyyy") ?? string.Empty,
                    toDate = item?.ToDate?.ToString("dd/MM/yyyy") ?? string.Empty
                };

                switch (item.Status)
                {
                    case 0:
                        status2 = "WAITING";
                        break;
                    case 1:
                        status2 = "IN_PROGRESS";
                        break;
                    case 2:
                        status2 = "DONE";
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(item.Status), "Trạng thái không hợp lệ");
                }

                repairHistoryListDTO.status = status2;

                result.Add(repairHistoryListDTO);
            }

            return result;
        }


        public async Task<List<MaintenanceHistoryDTO>> Process_GetMaintenanceHistory(string code)
        {
            var historyList = await (from r in _context.maintenanceHistory
                                     where r.EquipmentCode == code
                                     orderby r.Id descending
                                     select r).ToListAsync();

            var result = new List<MaintenanceHistoryDTO>();

            foreach (var item in historyList)
            {
                var content = await (from e in _context.maintenanceCheckList
                                     where e.EquipCode == item.EquipmentCode
                                        && e.MaintenanceType == item.MaintenanceType
                                        && e.MaintenanceTime == item.NextMaintenanceTime
                                        && e.HistoryID == (int?)item.Id
                                     select e.Task).ToListAsync();

                var title = await (from e in _context.maintenanceType
                                   where e.Code == item.MaintenanceType
                                   select e.Name).FirstOrDefaultAsync();

                var employeeName = (await _context.employee
                                    .FirstOrDefaultAsync(e => e.No == item.UserID))?.Name ?? string.Empty;

                var maintenanceHistoryDTO = new MaintenanceHistoryDTO
                {
                    id = item.Id.ToString(),
                    equipCode = item.EquipmentCode,
                    title = title,
                    postingDate = item.PostingDate?.ToString("dd/MM/yyyy"),
                    assignee = employeeName,
                    content = string.Join(" ", content),
                    status = item.Status switch
                    {
                        0 => "DONE",
                        1 => "IN_PROGRESS",
                        2 => "FAILED",
                        _ => "IN_PROGRESS"
                    },
                    term = item?.NextMaintenanceTime?.ToString("dd/MM/yyyy") ?? string.Empty
                };

                result.Add(maintenanceHistoryDTO);
            }

            return result;
        }

        public async Task<string> GetUserRoleAsync(string userId)
        {
            var user = await _context.employee
                .Where(e => e.No == userId)
                .FirstOrDefaultAsync();

            var roleId = await _context.userRoles
                .Where(e => e.UserID == user.LoginId)
                .Select(e => e.RoleID)
                .FirstOrDefaultAsync();

            var roleName = await _context.roles
                .Where(e => e.Id == roleId)
                .Select(e => e.Name)
                .FirstOrDefaultAsync();

            return roleName ?? "View";
        }

        public async Task<List<ItemRequireList>> GetOverView_Require()
        {
            try
            {
                var query = from r in _context.repairRequests
                            join emp in _context.employee on r.Reporter equals emp.No into empJoin
                            from emp in empJoin.DefaultIfEmpty()
                            join loc in _context.locationXSDs on r.LocationCode equals loc.Code into locJoin
                            from loc in locJoin.DefaultIfEmpty()
                            join unit in _context.Department on r.WorkCenterCode equals unit.Code into unitJoin
                            from unit in unitJoin.DefaultIfEmpty()
                            where r.Status == 0
                            orderby r.PostingDate descending
                            select new ItemRequireList
                            {
                                QRCode = r.QRCode,
                                Reporter = emp != null ? emp.Name : string.Empty,
                                CreatedDate = r.PostingDate.ToString("dd/MM/yyyy HH:mm"),
                                WorkCenter = unit.Name,
                                WorkShift = loc.Name
                            };

                return await query.ToListAsync();
            }
            catch (Exception ex)
            {
                // Trả danh sách trống và có thể log lỗi
                return new List<ItemRequireList>();
       
            }
        }
        public async Task<List<ItemRequireList>> GetOverView_Require_ByUser(string userId)
        {
            try
            {
                var workcenter = await _context.employee.Where(e=>e.No==userId).Select(e => e.WorkCenterCode).FirstOrDefaultAsync();
                // Nếu user không có WorkCenterCode thì trả rỗng
                if (string.IsNullOrEmpty(workcenter))
                    return new List<ItemRequireList>();
                var query = from r in _context.repairRequests
                            join emp in _context.employee on r.Reporter equals emp.No into empJoin
                            from emp in empJoin.DefaultIfEmpty()
                            join loc in _context.locationXSDs on r.LocationCode equals loc.Code into locJoin
                            from loc in locJoin.DefaultIfEmpty()
                            join unit in _context.Department on r.WorkCenterCode equals unit.Code into unitJoin
                            from unit in unitJoin.DefaultIfEmpty()
                            where r.Status == 0 && r.WorkCenterCode == workcenter
                            orderby r.PostingDate descending
                            select new ItemRequireList
                            {
                                QRCode = r.QRCode,
                                Reporter = emp != null ? emp.Name : string.Empty,
                                CreatedDate = r.PostingDate.ToString("dd/MM/yyyy HH:mm"),
                                WorkCenter = unit.Name,
                                WorkShift = loc.Name
                            };

                return await query.ToListAsync();
            }
            catch (Exception ex)
            {
                // Trả danh sách trống và có thể log lỗi
                return new List<ItemRequireList>();

            }
        }
        public async Task<List<ItemRequireList>> GetOverView_Repairing()
        {
            try
            {
                var query = from r in _context.repairRequests
                            join emp in _context.employee on r.Reporter equals emp.No into empJoin
                            from emp in empJoin.DefaultIfEmpty()
                            join loc in _context.locationXSDs on r.LocationCode equals loc.Code into locJoin
                            from loc in locJoin.DefaultIfEmpty()
                            join unit in _context.Department on r.WorkCenterCode equals unit.Code into unitJoin
                            from unit in unitJoin.DefaultIfEmpty()
                            where r.Status == 1
                            orderby r.PostingDate descending
                            select new ItemRequireList
                            {
                                QRCode = r.QRCode,
                                Reporter = emp != null ? emp.Name : string.Empty,
                                CreatedDate = r.PostingDate.ToString("dd/MM/yyyy"),
                                WorkCenter = unit.Name,
                                WorkShift = loc.Name
                            };

                return await query.ToListAsync();
            }
            catch (Exception ex)
            {
                // Trả danh sách trống và có thể log lỗi
                return new List<ItemRequireList>();

            }
        }
        public async Task<List<ItemRequireList>> GetOverView_Repairing_ByUser(string userId)
        {
            try
            {
                var workcenter = await _context.employee.Where(e => e.No == userId).Select(e => e.WorkCenterCode).FirstOrDefaultAsync();
                // Nếu user không có WorkCenterCode thì trả rỗng
                if (string.IsNullOrEmpty(workcenter))
                    return new List<ItemRequireList>();
                var query = from r in _context.repairRequests
                            join emp in _context.employee on r.Reporter equals emp.No into empJoin
                            from emp in empJoin.DefaultIfEmpty()
                            join loc in _context.locationXSDs on r.LocationCode equals loc.Code into locJoin
                            from loc in locJoin.DefaultIfEmpty()
                            join unit in _context.Department on r.WorkCenterCode equals unit.Code into unitJoin
                            from unit in unitJoin.DefaultIfEmpty()
                            where r.Status == 1 && r.WorkCenterCode == workcenter
                            orderby r.PostingDate descending
                            select new ItemRequireList
                            {
                                QRCode = r.QRCode,
                                Reporter = emp != null ? emp.Name : string.Empty,
                                CreatedDate = r.PostingDate.ToString("dd/MM/yyyy"),
                                WorkCenter = unit.Name,
                                WorkShift = loc.Name
                            };

                return await query.ToListAsync();
            }
            catch (Exception ex)
            {
                // Trả danh sách trống và có thể log lỗi
                return new List<ItemRequireList>();

            }
        }
        public async Task<List<ItemRequireList>> GetOverView_Completed()
        {
            try
            {
                var query = from r in _context.repairRequests
                            join emp in _context.employee on r.Reporter equals emp.No into empJoin
                            from emp in empJoin.DefaultIfEmpty()
                            join loc in _context.locationXSDs on r.LocationCode equals loc.Code into locJoin
                            from loc in locJoin.DefaultIfEmpty()
                            join unit in _context.Department on r.WorkCenterCode equals unit.Code into unitJoin
                            from unit in unitJoin.DefaultIfEmpty()
                            where r.Status == 2
                            orderby r.PostingDate descending
                            select new ItemRequireList
                            {
                                QRCode = r.QRCode,
                                Reporter = emp != null ? emp.Name : string.Empty,
                                CreatedDate = r.PostingDate.ToString("dd/MM/yyyy"),
                                WorkCenter = unit.Name,
                                WorkShift = loc.Name
                            };

                return await query.ToListAsync();
            }
            catch (Exception ex)
            {
                // Trả danh sách trống và có thể log lỗi
                return new List<ItemRequireList>();

            }
        }
        public async Task<List<ItemRequireList>> GetOverView_Completed_ByUser(string userId)
        {
            try
            {
                var workcenter = await _context.employee.Where(e => e.No == userId).Select(e => e.WorkCenterCode).FirstOrDefaultAsync();
                // Nếu user không có WorkCenterCode thì trả rỗng
                if (string.IsNullOrEmpty(workcenter))
                    return new List<ItemRequireList>();
                var query = from r in _context.repairRequests
                            join emp in _context.employee on r.Reporter equals emp.No into empJoin
                            from emp in empJoin.DefaultIfEmpty()
                            join loc in _context.locationXSDs on r.LocationCode equals loc.Code into locJoin
                            from loc in locJoin.DefaultIfEmpty()
                            join unit in _context.Department on r.WorkCenterCode equals unit.Code into unitJoin
                            from unit in unitJoin.DefaultIfEmpty()
                            where r.Status == 2 && r.WorkCenterCode == workcenter
                            orderby r.PostingDate descending
                            select new ItemRequireList
                            {
                                QRCode = r.QRCode,
                                Reporter = emp != null ? emp.Name : string.Empty,
                                CreatedDate = r.PostingDate.ToString("dd/MM/yyyy"),
                                WorkCenter = unit.Name,
                                WorkShift = loc.Name
                            };

                return await query.ToListAsync();
            }
            catch (Exception ex)
            {
                // Trả danh sách trống và có thể log lỗi
                return new List<ItemRequireList>();

            }
        }
        public async Task<HoanthanhSC_Detail_DTO> Get_DetailContentRepair(string qrCode)
        {
            
            var query = await (from r in _context.repairRequests
                               join e in _context.Equipment on r.QRCode equals e.QRCode into equipJoin
                               from e in equipJoin.DefaultIfEmpty()
                               join eg in _context.equipmentGroup on e.EquipmentGroupCode equals eg.Code into equipGroupJoin
                               from eg in equipGroupJoin.DefaultIfEmpty()
                               join lc in _context.locationXSDs on r.LocationCode equals lc.Code into locationJoin
                               from lc in locationJoin.DefaultIfEmpty()
                               join wc in _context.Department on r.WorkCenterCode equals wc.Code into workcenterJoin
                               from wc in workcenterJoin.DefaultIfEmpty()
                               join em in _context.employee on r.Reporter equals em.No into employeeJoin
                               from em in employeeJoin.DefaultIfEmpty()
                               join content in _context.repairContent on r.No equals content.DocNo into repairContentJoin
                               from content in repairContentJoin.DefaultIfEmpty()
                               join t in _context.repairType on content.RepairType equals t.Code into typeJoin
                               from t in typeJoin.DefaultIfEmpty()
                               where r.QRCode == qrCode
                               select new HoanthanhSC_Detail_DTO
                               {
                                   Name = eg.Name,
                                   Serial = e.SerialNumber,
                                   Brand = e.Brand,
                                   Model = e.Model,
                                   FromDate = r.FromDate.HasValue
                                       ? r.FromDate.Value.ToString("dd/MM/yyyy HH:mm") : string.Empty,
                                   ToDate = r.ToDate.HasValue
                                       ? r.ToDate.Value.ToString("dd/MM/yyyy HH:mm") : string.Empty,
                                   WorkShift = lc.Name,
                                   WorkCenter = wc.Name,
                                   TimeFinish = r.Duration != null? (r.Duration >= 60? $"{Math.Floor((decimal)r.Duration / 60)} giờ {(decimal)r.Duration % 60} phút"
                                    : $"{r.Duration} phút"): string.Empty,
                                   RepairType = t.Name,
                                   Content = content.Detail,
                                   Reporter = em.Name,
                                   PostingDate =  r.PostingDate.ToString("dd/MM/yyyy HH:mm") 
                               }).FirstOrDefaultAsync();

            return query ?? new HoanthanhSC_Detail_DTO();
        }

    }
}
