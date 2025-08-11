using EquipManagementAPI.Data;
using EquipManagementAPI.Helpers;
using EquipManagementAPI.Models;
using EquipManagementAPI.Models.DTOs;
using Microsoft.EntityFrameworkCore;
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
        

        public async Task<List<string>> ProcessQRCodeBatchAsync(QRCodeEntryBatchDTO request)
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
                        case 5:
                            asset.Status = 10;
                            asset.StatusGroup = -1;
                            asset.LocationCode = "";
                            break;
                        case 8:
                        case 10:
                            asset.Status = 3;
                            asset.StatusGroup = -1;
                            asset.LocationCode = "";
                            break;
                      
                        case 12:
                            asset.Status = 4;
                            asset.StatusGroup = -1;
                            break;
                        case 13:
                            asset.Status = 5;
                            asset.StatusGroup = -1;
                            asset.LocationCode = "";
                            break;
                        case 15:
                            asset.Status = 6;
                            asset.StatusGroup = -1;
                            asset.LocationCode = "";
                            break;
                        case 3:
                            asset.Status = 7;
                            asset.StatusGroup = 0;
                            asset.LocationCode = "";
                            break;
                        case 16:
                            asset.Status = 8;   
                            asset.StatusGroup = -1;
                            asset.LocationCode = "";
                            break;
                        case 6:
                        case 7:
                            asset.Status = 9;
                            asset.StatusGroup = -1;
                            asset.LocationCode = "";
                            break;
                        case 11:
                            asset.Status = 11;
                            asset.StatusGroup = -1;
                            asset.LocationCode = "";
                            break;
                        case 14:
                            asset.Status = 12;
                            asset.StatusGroup = 1;
                            asset.LocationCode = "";
                            break;
                        case 2:
                            asset.Status = 13;
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
            
            results.Add($"✅ Quét nhập thành công");
            return results;
        }

        public async Task<List<string>> ProcessScanQRCode_Type1(QRCodeEntryBatchDTO request)
        {
            var results = new List<string>();
            var distinctQRCodes = request.QRCodes.Distinct().ToList();
            foreach (var qrCode in distinctQRCodes)
            {
                var equip= await _context.Equipment.FirstOrDefaultAsync(e => e.QRCode == qrCode);
                if (equip == null)
                {
                    results.Add($"Lỗi! QRCode:{qrCode} chưa được tạo!");
                    continue;
                }
                var docEntries = await _context.DocumentEntry.Where(e => e.DocumentNo != null && e.DocumentNo == request.DocumentNo).ToListAsync();
                if (!docEntries.Any(e => e.QRCode != null && e.QRCode == qrCode))
                {
                    results.Add($"Lỗi! QRCode: {qrCode} không thuộc phiếu nhập {request.DocumentNo}!");
                    continue;
                }
                var checkScan = await _context.QRCodeEntry.FirstOrDefaultAsync(e => e.QRCode == qrCode && e.DocumentNo == request.DocumentNo);
                if (checkScan != null)
                {
                    results.Add($"Lỗi! QRCode:{qrCode} đã được quét");
                    continue;
                }
                var dto = new QRCodeEntryQLTB
                {
                    QRCode = qrCode,
                    EquipmentCode = equip.EquipmentCode,
                    EquipmentSubCode = equip.EquipmentSubCode,
                    EquipmentGroupCode = equip.EquipmentGroupCode,
                    DocumentNo = request.DocumentNo,
                    DocumentType = request.DocumentType,
                    ManageUnit = request.Unit,
                    UsingUnit = request.Unit,
                    PostingDate = DateTime.Now,
                    UserId = request.UserId,
                    Respon = equip.Responsibility,
                    SourceCode = equip.SourceCode,

                };
                _context.QRCodeEntry.Add(dto);

                // Cập nhật LineNo nếu tìm thấy trong EquipmentLineNo
                var equipLineNo = await _context.equipmentLineNo
                    .FirstOrDefaultAsync(e => e.DepartmentCode == request.Unit);

                if (equipLineNo != null)
                {
                    int newNumber = equipLineNo.LastUsed + equipLineNo.Increment;
                    string seriesNo = EquipmentMapper.FormatDocumentNumber(
                        equipLineNo.EndingNo.ToString(),
                        newNumber.ToString());

                    equip.LineNo = Convert.ToInt16(seriesNo);

                    // Cập nhật LastUsed (CHỈ 1 LẦN)
                    equipLineNo.LastUsed = newNumber;
                }
                //update
                equip.ManageUnit = request.Unit;
                equip.UsingUnit = request.Unit;
                equip.DocumentNo = request.DocumentNo;
                equip.DocumentType = request.DocumentType;
                
                
            }
            await _context.SaveChangesAsync();

            var docEntry = await _context.DocumentEntry.CountAsync(e => e.DocumentNo == request.DocumentNo);
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

            results.Add($"✅ Quét xuất thành công");

            return results;
        }

        public async Task<List<string>> ProcessScan_SuDungLuuKho(EquipScan01DTO request)
        {
            var results = new List<string>();
            var distinctQRCodes = request.QRCodes.Distinct().ToList();
            foreach (var item in request.QRCodes)
            {
                try
                {
                    var check1 = await _context.Equipment.FirstOrDefaultAsync(e => e.QRCode == item && e.ManageUnit == request.Unit);
                    if (check1 == null)
                    {
                        results.Add($"QRCode {item} không thuộc nhà máy {request.Unit}");
                        continue;
                    }
                    var check2 = await _context.Equipment.FirstOrDefaultAsync(e => e.QRCode == item);
                    if (check2 == null)
                    {
                        results.Add($"QRCode {item} chưa được tạo!");
                        continue;
                    }
                    if(!(check2.DocumentType == 1 || check2.DocumentType == 7))
                    {
                        results.Add($"QRCode {item} chưa được nhập đơn vị quản lý");
                        continue;
                    }    
                    if (request.Status == 1)
                    {
                        check1.Status = 1;
                    }
                    else
                    {
                        check1.Status = 14;
                    }
                    check1.LocationCode = request.WorkShift;
                    check1.ManageUnit = request.Unit;
                    check1.UsingUnit = request.Unit;
                    check1.StatusGroup = 1;

                    var maintenanceTrack = await _context.maintenanceTrackings.FirstOrDefaultAsync(e => e.QRCode == item);
                    if (maintenanceTrack != null)
                    {
                        maintenanceTrack.LocationCode = request.WorkShift;
                    }
                    

                    var maintenanceTrackWeek = await _context.maintenanceTrackingsWeek.FirstOrDefaultAsync(e => e.QRCode == item);
                    if (maintenanceTrackWeek != null)
                    {
                        maintenanceTrackWeek.LocationCode = request.WorkShift;
                    }
                    

                    var logDto = new EquipmentStatusLog
                    {
                        QRCode = item,
                        CreatedDate = DateTime.Now,
                        Status = request.Status,
                        DepartmentCode = request.Unit,
                        WorkShiftCode = request.WorkShift,
                        UserID = request.UserID,

                    };
                    _context.EquipmentStatusLog.Add(logDto);
                    results.Add($"Quét QRCode thành công");
                }
                catch (Exception ex)
                {
                    results.Add($"{ex.Message}");
                }

            }

            await _context.SaveChangesAsync();

            return results;
        }


        public async Task<bool> UpdateSerialNumber(string equipCode,string newSerial)
        {
            var equipment = await _context.Equipment.FirstOrDefaultAsync(e=> e.EquipmentCode == equipCode);
            if (equipment == null)
            {
                return false;
            }
            equipment.SerialNumber = newSerial;
            await _context.SaveChangesAsync();
            return true;
        }
        public async Task<List<MaintenanceTypeDTO>> GetMaintenanceType ()
        {
            var result = await _context.maintenanceType.Select(e=> new MaintenanceTypeDTO
            {
                Value = e.Code,
                Label = e.Name
            }).ToListAsync();
            return result;
        }
        public async Task<List<string>> GetMaintenanceContent (MaintenanceRequest request)
        {
           
            var equip = await _context.Equipment.FirstOrDefaultAsync(e=>e.QRCode == request.QRCode);
            var equipGroup = await _context.equipmentGroup.Where(e => e.Code == equip.EquipmentGroupCode).Select(e=>e.Code).Distinct().FirstOrDefaultAsync();
            if (equipGroup == null)
            {
                return new List<string>();
            }

                
            return await _context.maintenanceContents.Where(e=> e.EquipGroupCode == equipGroup && e.MaintenanceType == request.MaintenanceType)
                .Select(e=>e.Task).ToListAsync();
        }

        public async Task<List<string>> ProcessScan_BaoDuong (RequestScanMaintenance request)
        {
            var results = new List<string>();
            var checkMainTrack = await _context.maintenanceTrackings
            .FirstOrDefaultAsync(e => e.QRCode == request.QRCode && e.MaintenanceType == request.type);
            
            var checkMainTrackWeek = await _context.maintenanceTrackingsWeek
                .FirstOrDefaultAsync(e => e.QRCode == request.QRCode && e.MaintenanceType == request.type);

            if (checkMainTrack == null && checkMainTrackWeek == null)
            {
                results.Add("Lỗi Không tìm thấy ghi nhận bảo dưỡng phù hợp.");
                return results;
                // Hoặc logic xử lý tạo mới, thông báo...
            }
            //Kiểm tra ngày quét có đúng theo KHBD không
            var now = DateTime.Now;
            var currentMonth = now.Month;
            var monthPlan = checkMainTrack.NextMaintenance.Value.Month;
            if (currentMonth < monthPlan)
            {
                results.Add("Lỗi: Thiết bị này chưa đến thời hạn bảo  dưỡng.");
                return results;
            }

            //Tạo lịch sử bd
            var equip = await _context.Equipment.FirstOrDefaultAsync(e => e.QRCode == request.QRCode);
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
                var equipGroup = checkMainTrackWeek?.EquipmentGroupCode;
                var nextNgayBD = checkMainTrackWeek?.NextMaintenance??DateTime.Now;
                var unit = checkMainTrackWeek?.ManageUnit;
                var status = checkMainTrackWeek?.Status;
                if (status == 1)
                {
                    results.Add($"Lỗi QRCode {request.QRCode} đã được bảo dưỡng");
                    return results;
                }
                var newHistory = new MaintenanceHistory
                {
                    EquipmentCode = equip.EquipmentCode,
                    EquipGroupCode = equip.EquipmentGroupCode,
                    QRCode = request.QRCode,
                    Serial = equip?.SerialNumber,
                    Brand = equip?.EquipmentCode,
                    PostingDate = DateTime.Now,
                    ManageUnit = equip?.ManageUnit,
                    UsingUnit = equip?.UsingUnit,
                    Location = equip?.LocationCode,
                    UserID = request.UserID,
                    MaintenanceType = request.type,
                    NextMaintenanceTime = checkMainTrackWeek.NextMaintenance
                };
                _context.maintenanceHistory.Add(newHistory);
                foreach (var item in request.Content)
                {
                    var newTask = new MaintenanceTask
                    {
                        QRCode = request.QRCode,
                        PostingDate = DateTime.Now,
                        Task = item,
                        Type = request.type,
                        
                    };
                    _context.maintenanceTasks.Add(newTask);
                }
                checkMainTrackWeek.LastMaintenanceTime = DateTime.Now;
                checkMainTrackWeek.NextMaintenance = nextNgayBD.AddDays(7);
                checkMainTrackWeek.MaintenanceType = request.type;
                checkMainTrackWeek.LocationCode = equip.LocationCode ?? "";
                await _context.SaveChangesAsync();
            }
            else
            {
                var equipGroup = checkMainTrack?.EquipmentGroupCode;
                var nextNgayBD = checkMainTrack?.NextMaintenance ?? DateTime.Now;
                var unit = checkMainTrack.ManageUnit;
                var status = checkMainTrack.Status;
                // Nếu thiết bị đã bảo dưỡng
                if (status == 1)
                {
                     results.Add($"Lỗi QRCode {request.QRCode} đã được bảo dưỡng");
                    return results;
                }
                                               // Tạo lịch sử bảo dưỡng mới
                var newHistory = new MaintenanceHistory
                {
                    EquipmentCode = equip.EquipmentCode,
                    EquipGroupCode = equip.EquipmentGroupCode,
                    QRCode = request.QRCode,
                    Serial = equip?.SerialNumber,
                    Brand = equip?.EquipmentCode,
                    PostingDate = DateTime.Now,
                    ManageUnit = equip?.ManageUnit,
                    UsingUnit = equip?.UsingUnit,
                    Location = equip?.LocationCode,
                    UserID = request.UserID,
                    MaintenanceType = request.type,
                    NextMaintenanceTime = checkMainTrack.NextMaintenance
                };

                _context.maintenanceHistory.Add(newHistory);
                foreach (var item in request.Content)
                {
                    var newTask = new MaintenanceTask
                    {
                        QRCode = request.QRCode,
                        PostingDate = DateTime.Now,
                        Task = item,
                        Type = request.type
                    };
                    _context.maintenanceTasks.Add(newTask);
                }
                

                // Cập nhật lại thông tin tracking
                checkMainTrack.LastMaintenanceTime = DateTime.Now;
                checkMainTrack.LocationCode = equip.LocationCode ?? "";
                checkMainTrack.MocBDGanNhat = checkMainTrack.NextMaintenance;

                switch (request.type)
                {
                    case "3M":
                        checkMainTrack.NextMaintenance = nextNgayBD.AddMonths(3) ;
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

                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateException ex)
                {
                    results.Add("Lỗi khi lưu tracking: " + ex.InnerException?.Message ?? ex.Message);
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
                var equip = await _context.Equipment.FirstOrDefaultAsync(e=>e.QRCode == request.QRCode);
                if (equip == null)
                {
                    results.Add($"Lỗi dữ liệu QRCode {request.QRCode} không được tìm thấy!");
                    return results;
                }
                if (equip.Status ==0)
                {
                    results.Add($"Lỗi thiết bị {request.QRCode} chưa được sử dụng!");
                    return results;
                }
                if (equip.Status == 2)
                {
                    results.Add($"Lỗi thiêt bị {request.QRCode} đang được sửa chữa!");
                    return results;
                }
                var NoSeriesLine = await _context.noSeriesLine.Where(e=>e.Code == "YCSC" && e.SeriesCode =="QLTB").FirstOrDefaultAsync();
                var EndNo = NoSeriesLine.EndingNo;
                var IncrementNo = NoSeriesLine.IncrementByNo;
                var LastNoUser = NoSeriesLine.LastNoUsed;
                var StartingNo = NoSeriesLine.StartingNo;
                var No = StartingNo + EquipmentMapper.FormatDocumentNumber(EndNo.ToString(),(IncrementNo+LastNoUser).ToString());
                var repairRequest = new RepairRequestList
                {
                    No = No,
                    EquipmentCode = equip.EquipmentCode,
                    EquipmentGroupCode = equip.EquipmentGroupCode,
                    Serial = equip.SerialNumber,
                    Brand = equip.Brand,
                    Model = equip.Model,
                    Reporter = request.UserID,
                    PostingDate = DateTime.Now,
                    QRCode = equip.QRCode,
                    LocationCode = equip.LocationCode
                };
                _context.repairRequests.Add(repairRequest);
                var repairHistory = new RepairHistory
                {
                    No = No,
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
                NoSeriesLine.LastNoUsed = IncrementNo + LastNoUser;
                await _context.SaveChangesAsync();
                results.Add($"Yêu cầu sửa chữa {request.QRCode} thành công!");
            }
            catch (Exception ex)
            {
                results.Add($"{ex.Message}");
            }
            return results;
        }

        public async Task<InforRequestSC> GetInforEquipSC(string qrCode)
        {
            
            var repairList = await _context.repairRequests.Where(e=>e.QRCode == qrCode && e.Status==0).OrderByDescending(r => r.PostingDate).FirstOrDefaultAsync();
            if (repairList == null)
            {
                return null;

            }
            
            var equipGroup = await _context.equipmentGroup.Where(e => e.Code == repairList.EquipmentGroupCode).FirstOrDefaultAsync();
            var workShift = await _context.locationXSDs.Where(e => e.Code == repairList.LocationCode).FirstOrDefaultAsync();
            var result = new InforRequestSC {
                QRCode = qrCode,
                EquipmentName = equipGroup.Name,
                Serial = repairList.Serial,
                Brand = repairList.Brand,
                Model = repairList.Model,
                WorkShift = workShift.Name,
                Status = 0
        };
            
            return result;
        }

        public async Task<List<string>> Process_BatDauSuaChua (RequestSuaChua request)
        {
            var result =new List<string>();
            try
            {
                var repairList = await _context.repairRequests.Where(e => e.QRCode == request.QRCode && e.Status == 0).OrderByDescending(r => r.PostingDate).FirstOrDefaultAsync();
                if (repairList == null)
                {
                    result.Add($"Lỗi không tìm thấy danh sách yêu cầu sữa chữa!");
                }
                else
                {
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
                        Status = 1
                    };
                    _context.repairHistory.Add(repairHistory);

                    repairList.FromDate = DateTime.Now;
                    repairList.Status = 1;

                    var equip = await _context.Equipment.FirstOrDefaultAsync(e => e.QRCode == repairList.QRCode);
                    if (equip == null)
                    {
                        result.Add($"Lỗi không tìm thấy thiết bị trong danh sách máy!");
                    }
                    else
                    {
                        equip.Status = 2;
                        equip.StatusGroup = -1;
                    }
                    
                    await _context.SaveChangesAsync();
                    result.Add($"Bắt đầu sửa chữa!");
                }
                

            }
            catch (Exception ex)
            {
                result.Add($"{ex.Message}");
            }
            return result;
        } 

        public async Task<EquipmentDTO> GetInforEquipment (string code)
        {
            var equipment = await _context.Equipment.FirstOrDefaultAsync(u=> u.QRCode == code);
            if (equipment == null)
            {
                return null;
            }
            var location = await _context.locationXSDs.Where(e => e.Code == equipment.LocationCode).Select(e => e.Name).FirstOrDefaultAsync();
            
            var result = new EquipmentDTO {
                EquipmentCode = equipment.EquipmentCode,
                ManageUnit = equipment.ManageUnit,
                EquipmentGroupCode = equipment.EquipmentGroupCode,
                Model = equipment.Model,
                SerialNumber = equipment.SerialNumber,
                Brand = equipment.Brand,
                QRCode = equipment.QRCode,
                Image = equipment.Image,
                LocationCode = equipment.LocationCode,
                LocationName = location,
                Status = equipment.Status,
                StatusGroup = equipment.StatusGroup,
            };
            return result;
        }

        public async Task<InforRequestSC> GetInforEquipHTSC(string qrCode)
        {

            var repairList = await _context.repairRequests.Where(e => e.QRCode == qrCode && e.Status == 1).OrderByDescending(r => r.PostingDate).FirstOrDefaultAsync();
            if (repairList == null)
            {
                return null;

            }

            var equipGroup = await _context.equipmentGroup.Where(e => e.Code == repairList.EquipmentGroupCode).FirstOrDefaultAsync();
            var workShift = await _context.locationXSDs.Where(e => e.Code == repairList.LocationCode).FirstOrDefaultAsync();

            var result = new InforRequestSC
            {
                QRCode = qrCode,
                EquipmentName = equipGroup.Name,
                Serial = repairList.Serial,
                Brand = repairList.Brand,
                Model = repairList.Model,
                WorkShift = workShift.Name,
               
                Status = 7
            };

            return result;
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
        public async Task<List<string>>  Process_HTSC (RequestHTSC request)
        {
            var result = new List<string>();
            try
            {
                var repairList = await _context.repairRequests.Where(e => e.QRCode == request.QRCode && e.Status == 1).OrderByDescending(r => r.PostingDate).FirstOrDefaultAsync();
                //var history = await _context.repairHistory.Where(e=>e.QRCode == request.QRCode && e.No == repairList.No && e.Status == 5 ).FirstOrDefaultAsync();
                if (repairList == null)
                {
                    return null;

                }
                var xacnhan = await _context.yeucauBQLCXacNhan.FirstOrDefaultAsync(e => e.DocNo == repairList.No && e.QRCode==request.QRCode && e.Status == 0);
                if (xacnhan != null) {
                    result.Add($"Yêu cầu đã được gửi!");
                    return result;
                }
                //if (history.Status == 5)
                //{
                //    result.Add($"Thiết bị {request.QRCode} đã được xác nhận không sửa được. Vui lòng thực hiện bắt đầu sửa chữa!");
                //    return result;
                //}
                else
                {
                    var newYeucau = new YeucauBQLCXacNhan
                    {
                        DocNo = repairList.No,
                        QRCode = request.QRCode,
                        EquipmentGroupCode = repairList.EquipmentGroupCode,
                        Requester = request.UserID,
                        LocationCode = repairList.LocationCode,
                        RepairType = request.ReasonType,
                        Description = request.Description,
                        PostingDate = DateTime.Now
                    };
                    _context.yeucauBQLCXacNhan.Add(newYeucau);

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
                        Status = 2,
                    };
                    _context.repairHistory.Add(newHistory);

                    await _context.SaveChangesAsync();
                    result.Add($"Gửi yêu cầu xác nhận hoàn thành sửa chữa thành công!");
                }
            }
            catch (Exception ex)
            {
                result.Add(ex.Message);
            }
            return result;
        }
        public async Task<List<string>> Process_KhongSuaDuoc (RequestHTSC request)
        {
            var result = new List<string>();
            try
            {
                var repairList = await _context.repairRequests.Where(e => e.QRCode == request.QRCode && e.Status == 1).OrderByDescending(r => r.PostingDate).FirstOrDefaultAsync();
                //var history = await _context.repairHistory.Where(e => e.QRCode == request.QRCode && e.No == repairList.No && e.Status==2).FirstOrDefaultAsync();
                if (repairList == null)
                {
                    return null;

                }
                //if (history != null)
                //{
                //    result.Add($"Thiết bị {repairList.QRCode} đã gửi yêu cầu xác nhận hoàn thành sửa chữa!");
                //    return result;
                //}
                else
                {
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
                        Status = 5
                    };
                    _context.repairHistory.Add(newHistory);

                    repairList.Status = 0;
                    await _context.SaveChangesAsync();

                    result.Add($"Xác nhận không sửa được thiết bị {repairList.QRCode}!");
                }
                
            }
            catch(Exception ex)
            {
                result.Add($"Lỗi {ex.Message}");
            }
            return result;
        }

        public async Task<List<XacnhanHT_DTO>> Process_GetListYeuCauXN()
        {
            var list = await _context.yeucauBQLCXacNhan.Where(e=>e.Status ==0).ToListAsync();
            var result = new List<XacnhanHT_DTO>();
            foreach (var item in list)
            {
                var equipGroup = await _context.equipmentGroup.Where(e => e.Code == item.EquipmentGroupCode).FirstOrDefaultAsync();
                var LocationXSD = await _context.locationXSDs.Where(e=>e.Code ==  item.LocationCode).FirstOrDefaultAsync();
                var repairType = await _context.repairType.Where(e=>e.Code == item.RepairType).FirstOrDefaultAsync();
                var order = new XacnhanHT_DTO
                {
                    RowID = item.RowID,
                    QRCode = item.QRCode,
                    EquipmentGroup = item.EquipmentGroupCode,
                    Requester = item.Requester,
                    LocationCode = item.LocationCode,
                    Location = LocationXSD.Name,
                    EquipmentName = equipGroup.Name,
                    RepairType = item.RepairType,
                    RepairTypeName = repairType.Name,
                    Description = item.Description,
                };
                result.Add(order);
            }
                              
            return result;
        }

        public async Task<List<RepairListDTO>> Process_GetListYeuCau()
        {
            var list = await _context.repairRequests.Where(e => e.Status == 0).ToListAsync();
            var result = new List<RepairListDTO>();
            foreach (var item in list)
            {
                var equipGroup = await _context.equipmentGroup.Where(e => e.Code == item.EquipmentGroupCode).FirstOrDefaultAsync();
                var LocationXSD = await _context.locationXSDs.Where(e => e.Code == item.LocationCode).FirstOrDefaultAsync();
                var order = new RepairListDTO
                {
                    No = item.No,
                    QRCode = item.QRCode,
                    EquipmentName = equipGroup.Name,
                    Location = LocationXSD.Name,
                    Model = item.Model,
                    Reporter = item.Reporter,
                };
                result.Add(order);
            }

            return result;
        }

        public async Task<List<string>> Process_XacNhanHoanThanh(XacnhanHT_DTO request)
        {
            var result = new List<string>();
            var yeucau = await _context.yeucauBQLCXacNhan.Where(e=>e.QRCode == request.QRCode).FirstOrDefaultAsync();
            var repairList = await _context.repairRequests.Where(e => e.No == yeucau.DocNo && e.QRCode == request.QRCode).FirstOrDefaultAsync();
            if (repairList.Status == 1)
            {
                var history = new RepairHistory
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
                _context.repairHistory.Add(history);
                var repairType = await _context.repairType.FirstOrDefaultAsync(e => e.Code == yeucau.RepairType);
                var content = new RepairContent
                {
                    DocNo = yeucau.DocNo,
                    RepairType = yeucau.RepairType,
                    Name = repairType.Name,
                    Detail = yeucau.Description
                };
                _context.repairContent.Add(content);

                repairList.Status = 2;
                repairList.ToDate = DateTime.Now;
                var fromDate =(DateTime) repairList.FromDate;
                var toDate = DateTime.Now;
                repairList.Duration =(decimal) Math.Round((toDate - fromDate).TotalMinutes, 0);

                var equip = await _context.Equipment.Where(e=>e.QRCode == request.QRCode).FirstOrDefaultAsync();
                equip.Status = 1;
                equip.StatusGroup = 1;

                yeucau.Status = 1;

                await _context.SaveChangesAsync();
                result.Add($"Xác nhận {request.QRCode} đã sửa chữa thành công!");
            }
            else
            {
                result.Add($"Lỗi: thiết bị {request.QRCode} chưa được sửa chữa!");
            }

            return result;
        }

        public async Task<List<string>> Process_KhongHTSC(XacnhanHT_DTO request)
        {
            var result = new List<string>();
            var yeucau = await _context.yeucauBQLCXacNhan.Where(e => e.QRCode == request.QRCode).FirstOrDefaultAsync();
            var repairList = await _context.repairRequests.Where(e => e.No == yeucau.DocNo && e.QRCode == request.QRCode).FirstOrDefaultAsync();
            if (repairList.Status == 1)
            {
                var history = new RepairHistory
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
                    Status = 4
                };

                yeucau.Status = 2;
                await _context.SaveChangesAsync();
                result.Add($"Xác nhận {request.QRCode} không sửa chữa thành công!");
            }
            else
            {
                result.Add($"Lỗi: thiết bị {request.QRCode} chưa được sửa chữa!");
            }
            
            return result;
        }
    }
}
