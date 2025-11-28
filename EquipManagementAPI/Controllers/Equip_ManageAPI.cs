using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EquipManagementAPI.Data;
using EquipManagementAPI.Helpers;
using EquipManagementAPI.Models;
using EquipManagementAPI.Models.DTOs;
using EquipManagementAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EquipManagementAPI.Controllers;

[Route("api/Equipment")]
[ApiController]
public class Equip_ManageAPI : ControllerBase
{
    private readonly ApplicationDbContext _context;

    private readonly IAssetService _service;

    public Equip_ManageAPI(ApplicationDbContext context, IAssetService service)
    {
        _context = context;
        _service = service;
    }

    [HttpGet("{code}", Name = "GetQuip")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [MaintenanceAuthorize("Admin", "Manager", "Khocty", "Baotri1", "Baotri2", "Chuyen1", "Chuyen2", "View")]
    public async Task<ActionResult<EquipmentDTO>> GetEquipment(string code)
    {
        if (code == "")
        {
            return BadRequest();
        }
        EquipmentDTO equip = await _service.GetInforEquipment(code, base.Request);
        if (equip == null)
        {
            return NotFound("Không tìm thấy mã thiết bị!");
        }
        return Ok(equip);
    }

    [HttpGet("serial/{serial}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [MaintenanceAuthorize( "Admin", "Manager", "Khocty", "Baotri1", "Baotri2", "Chuyen1", "Chuyen2", "View" )]
    public async Task<ActionResult<EquipmentDTO>> GetEquipmentBySerial(string serial)
    {
        if (serial == "")
        {
            return BadRequest();
        }
        EquipmentDTO equip = await _service.GetInforEquipmentBySerial(serial, base.Request);
        if (equip == null)
        {
            return NotFound("Không tìm thấy mã thiết bị!");
        }
        return Ok(equip);
    }

    [HttpGet("code/{equipCode}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [MaintenanceAuthorize("Admin", "Manager", "Khocty", "Baotri1", "Baotri2", "Chuyen1", "Chuyen2", "View")]
    public async Task<ActionResult<EquipmentDTO>> GetEquipmentByEquipCode(string equipCode)
    {
        if (equipCode == "")
        {
            return BadRequest();
        }
        EquipmentDTO equip = await _service.GetInforEquipmentByCode(equipCode, base.Request);
        if (equip == null)
        {
            return NotFound("Không tìm thấy mã thiết bị!");
        }
        return Ok(equip);
    }

    [HttpPost("scan")]
    [ProducesResponseType(201)]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    [MaintenanceAuthorize("Admin", "Manager", "Khocty", "Baotri1", "Baotri2")]
    public async Task<ActionResult> ScanQRCode0([FromBody] RequestEntryType0 data)
    {
        try
        {
            return Ok(await _service.ProcessQRCodeBatchAsync(data));
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("scanType1")]
    [ProducesResponseType(201)]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    [MaintenanceAuthorize( "Admin", "Manager", "Khocty", "Baotri1", "Baotri2" )]
    public async Task<ActionResult> ScanQRCode_XuatDVQL([FromBody] RequestEntryType1 data)
    {
        try
        {
            return Ok(await _service.ProcessScanQRCode_Type1(data));
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("update-serial")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [MaintenanceAuthorize("Admin", "Manager", "Khocty", "Baotri1", "Baotri2")]
    public async Task<ActionResult> UpdateEquip([FromBody] UpdateSerialRequest request)
    {
        if (request == null)
        {
            return BadRequest("Dữ liệu không hợp lệ.");
        }
        if (!(await _service.UpdateSerialNumber(request.EquipmentCode, request.SerialNumber, request.User)))
        {
            return NotFound("Không tìm thấy thiết bị với mã " + request.EquipmentCode);
        }
        return Ok("Cập nhật Số serial thành công!");
    }

    [HttpPut("scan-sdlk")]
    [ProducesResponseType(204)]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [MaintenanceAuthorize("Admin", "Manager", "Baotri1", "Baotri2")]
    public async Task<ActionResult> Scan_SudungLuuKho([FromBody] EquipScan01DTO equipDTO)
    {
        try
        {
            if (equipDTO == null)
            {
                return BadRequest();
            }
            return Ok(await _service.ProcessScan_SuDungLuuKho(equipDTO));
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("unit", Name = "GetUnit")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [MaintenanceAuthorize("Admin", "Manager", "Baotri1", "Baotri2")]
    public async Task<ActionResult<DepartmentDTO>> GetUnit_SDLK([FromQuery] string userId)
    {
        try
        {
            if (string.IsNullOrEmpty(userId))
            {
                return BadRequest("Thiếu thông tin userId.");
            }
            DepartmentDTO departmentDto = await (from e in _context.employee
                                                 join ws in _context.workShift on e.WorkShiftCode equals ws.Code
                                                 join d in _context.Department on ws.WorkCenterCode equals d.Code
                                                 where e.No == userId
                                                 select new DepartmentDTO
                                                 {
                                                     Value = d.Code,
                                                     Label = d.Name
                                                 }).FirstOrDefaultAsync();
            if (departmentDto == null)
            {
                return NotFound("Không tìm thấy đơn vị cho user này.");
            }
            return Ok(departmentDto);
        }
        catch (Exception ex)
        {
            return BadRequest("Có lỗi xảy ra khi lấy danh sách đơn vị: " + ex.Message);
        }
    }

    [HttpGet("units")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [MaintenanceAuthorize("Admin")]
    public async Task<ActionResult<IEnumerable<DepartmentDTO>>> GetUnits()
    {
        try
        {
            List<DepartmentDTO> departmentDto = await _context.Department.Select((Department e) => new DepartmentDTO
            {
                Value = e.Code,
                Label = e.Name
            }).ToListAsync();
            if (departmentDto == null)
            {
                return NotFound("Không tìm thấy đơn vị cho user này.");
            }
            return Ok(departmentDto);
        }
        catch (Exception ex)
        {
            return BadRequest("Có lỗi xảy ra khi lấy danh sách đơn vị: " + ex.Message);
        }
    }

    [HttpGet("user-role")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [MaintenanceAuthorize("Admin", "Manager", "Baotri1", "Baotri2", "Chuyen1", "Chuyen2")]
    public async Task<ActionResult<string>> GetUserRole([FromQuery] string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return BadRequest("UserId is required");
        }
        return await _service.GetUserRoleAsync(userId);
    }

    [HttpGet("locationXSD", Name = "GetLocation")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [MaintenanceAuthorize("Admin", "Manager", "Baotri1", "Baotri2")]
    public async Task<ActionResult<LocationXsdDTO>> GetLocationXSD([FromQuery] string unit, [FromQuery] string type)
    {
        try
        {
            if (await _context.Department.FirstOrDefaultAsync((Department e) => e.Code == unit) == null)
            {
                return BadRequest();
            }
            if (string.IsNullOrEmpty(type))
            {
                return BadRequest("Thiếu tham số type.");
            }
            new List<LocationXsdDTO>();
            List<LocationXsdDTO> result;
            if (type.Equals("0", StringComparison.OrdinalIgnoreCase))
            {
                result = await (from e in _context.locationXSDs
                                where e.DepartmentCode == unit && e.Check == 0
                                select new LocationXsdDTO
                                {
                                    Value = e.Code,
                                    Label = e.Name
                                }).ToListAsync();
            }
            else
            {
                if (!type.Equals("1", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest("Loại type không hợp lệ. Chỉ chấp nhận 'use' hoặc 'store'.");
                }
                result = await (from e in _context.locationXSDs
                                where e.DepartmentCode == unit && e.Check == 1
                                select new LocationXsdDTO
                                {
                                    Value = e.Code,
                                    Label = e.Name
                                }).ToListAsync();
            }
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest("Có lỗi xảy ra khi lấy danh sách vị trí: " + ex.Message);
        }
    }

    [HttpGet("typeSDLK")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [MaintenanceAuthorize(new string[] { "Admin", "Manager", "Baotri1", "Baotri2" })]
    public async Task<ActionResult> GetType_LKSD()
    {
        try
        {
            List<object> result = new List<object>
            {
                new
                {
                    value = "1",
                    label = "Lưu kho"
                },
                new
                {
                    value = "0",
                    label = "Sử dụng"
                }
            };
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest("Có lỗi xảy ra khi lấy loại quét: " + ex.Message);
        }
    }

    [HttpGet("maintenanceType")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [MaintenanceAuthorize(new string[] { "Admin", "Manager", "Baotri1", "Baotri2" })]
    public async Task<ActionResult<MaintenanceTypeDTO>> GetMaintenanceType()
    {
        try
        {
            return Ok(await _service.GetMaintenanceType());
        }
        catch (Exception ex)
        {
            return BadRequest("Lỗi" + ex.Message);
        }
    }

    [HttpPost("maintenanceContent")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [MaintenanceAuthorize(new string[] { "Admin", "Manager", "Baotri1", "Baotri2" })]
    public async Task<ActionResult<MaintenanceRequest>> GetMaintenanceContent([FromBody] MaintenanceRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.QRCode) || string.IsNullOrWhiteSpace(request.MaintenanceType))
            {
                return BadRequest("Thiếu QRCode hoặc loại bảo dưỡng.");
            }
            List<string> result = await _service.GetMaintenanceContent(request);
            if (result == null)
            {
                return NotFound("Không tìm thấy nội dung bảo dưỡng.");
            }
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest("Lỗi" + ex.Message);
        }
    }

    [HttpPost("scan-bd")]
    [ProducesResponseType(204)]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [MaintenanceAuthorize(new string[] { "Admin", "Manager", "Baotri1", "Baotri2" })]
    public async Task<ActionResult> Scan_BaoDuong([FromBody] RequestScanMaintenance request)
    {
        try
        {
            if (request == null || string.IsNullOrWhiteSpace(request.QRCode) || request.Content.Count == 0)
            {
                return BadRequest();
            }
            return Ok(await _service.ProcessScan_BaoDuong(request));
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("get-remaining")]
    [ProducesResponseType(204)]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [MaintenanceAuthorize(new string[] { "Admin", "Manager", "Baotri1", "Baotri2" })]
    public async Task<IActionResult> GetRemainingTasks([FromBody] RequestRemaining request)
    {
        if (string.IsNullOrWhiteSpace(request.QRCode) || string.IsNullOrWhiteSpace(request.MaintenanceType))
        {
            return BadRequest("Thiếu QRCode hoặc loại bảo dưỡng.");
        }
        List<string> result = await _service.GetRemainingTask(request);
        if (result == null)
        {
            return NotFound("Không tìm thấy nội dung bảo dưỡng.");
        }
        return Ok(result);
    }

    [HttpGet("MaintenanceHistory/{code}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [MaintenanceAuthorize(new string[] { "Admin", "Manager", "Khocty", "Baotri1", "Baotri2", "Chuyen1", "Chuyen2", "View" })]
    public async Task<ActionResult<MaintenanceHistoryDTO>> GetMaintenanceHistory(string code)
    {
        IEnumerable<MaintenanceHistoryDTO> dtoList = await _service.Process_GetMaintenanceHistory(code);
        if (dtoList == null)
        {
            return NotFound("Không có lịch sử");
        }
        return Ok(dtoList);
    }

    [HttpPost("requireSC")]
    [ProducesResponseType(204)]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [MaintenanceAuthorize(new string[] { "Admin", "Manager", "Baotri1", "Baotri2", "Chuyen1", "Chuyen2" })]
    public async Task<ActionResult> Request_SuaChua([FromBody] RequestSuaChua request)
    {
        try
        {
            if (request == null || string.IsNullOrWhiteSpace(request.QRCode))
            {
                return BadRequest();
            }
            return Ok(await _service.Process_SuaChua(request));
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("baoduong/requireSC")]
    [ProducesResponseType(204)]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [MaintenanceAuthorize(new string[] { "Admin", "Manager", "Baotri1", "Baotri2", "Chuyen1", "Chuyen2" })]
    public async Task<ActionResult> BaoDuong_RequestSuaChua([FromBody] RequestSuaChua request)
    {
        try
        {
            if (request == null || string.IsNullOrWhiteSpace(request.QRCode))
            {
                return BadRequest();
            }
            return Ok(await _service.Process_BaoDuongSuaChua(request));
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("suachua/{code}", Name = "GetSuaChua")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [MaintenanceAuthorize(new string[] { "Admin", "Manager", "Baotri1", "Baotri2" })]
    public async Task<ActionResult<InforRequestSC>> GetEquipment_SuaChua(string code)
    {
        if (code == "")
        {
            return BadRequest();
        }
        InforRequestSC equip = await _service.GetInforEquipSC(code);
        if (equip == null)
        {
            return NotFound("Lỗi " + code + " chưa được tạo yêu cầu sửa chữa!");
        }
        return Ok(equip);
    }

    [HttpPost("batdauSC")]
    [ProducesResponseType(204)]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [MaintenanceAuthorize(new string[] { "Admin", "Manager", "Baotri1", "Baotri2" })]
    public async Task<ActionResult> BatDauSuaChua([FromBody] RequestSuaChua request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.QRCode))
        {
            return BadRequest();
        }
        return Ok(await _service.Process_BatDauSuaChua(request));
    }

    [HttpGet("hoanthanhsc/{code}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [MaintenanceAuthorize(new string[] { "Admin", "Manager", "Baotri1", "Baotri2" })]
    public async Task<ActionResult<InforRequestSC>> GetEquipment_HoanThanhSC(string code)
    {
        if (code == "")
        {
            return BadRequest();
        }
        InforRequestSC equip = await _service.GetInforEquipHTSC(code);
        if (equip == null)
        {
            return NotFound("Lỗi không tìm thấy thiết bị trong danh sách sửa chữa");
        }
        return Ok(equip);
    }

    [HttpGet("hoanthanhsc/serial/{code}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [MaintenanceAuthorize(new string[] { "Admin", "Manager", "Baotri1", "Baotri2" })]
    public async Task<ActionResult<InforRequestSC>> GetEquipment_Serial_HoanThanhSC(string code)
    {
        if (code == "")
        {
            return BadRequest();
        }
        InforRequestSC equip = await _service.GetInforEquipHTSC_Serial(code);
        if (equip == null)
        {
            return NotFound("Lỗi không tìm thấy thiết bị trong danh sách sửa chữa");
        }
        return Ok(equip);
    }

    [HttpGet("reason")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [MaintenanceAuthorize(new string[] { "Admin", "Manager", "Baotri1", "Baotri2" })]
    public async Task<ActionResult<MaintenanceTypeDTO>> GetReasonType()
    {
        try
        {
            return Ok(await _service.GetReasonType());
        }
        catch (Exception ex)
        {
            return BadRequest("Lỗi" + ex.Message);
        }
    }

    [HttpPost("hoanthanhSC")]
    [ProducesResponseType(204)]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [MaintenanceAuthorize(new string[] { "Admin", "Manager", "Baotri1", "Baotri2" })]
    public async Task<ActionResult> HoanThanhSuaChua([FromBody] RequestHTSC request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.QRCode))
        {
            return BadRequest();
        }
        return Ok(await _service.Process_HTSC(request));
    }

    [HttpPost("suachua/dieunguoi")]
    [ProducesResponseType(204)]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [MaintenanceAuthorize(new string[] { "Admin", "Manager", "Baotri1", "Baotri2" })]
    public async Task<ActionResult> KhongSuaDuoc([FromBody] RequestHTSC request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.QRCode))
        {
            return BadRequest();
        }
        return Ok(await _service.Process_KhongSuaDuoc_Dieunguoi(request));
    }

    [HttpPost("suachua/dieumay")]
    [ProducesResponseType(204)]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [MaintenanceAuthorize(new string[] { "Admin", "Manager", "Baotri1", "Baotri2" })]
    public async Task<ActionResult> KhongSuaDuoc_DieuMay([FromBody] RequestDieuMay request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.NewQRCode))
        {
            return BadRequest();
        }
        if (request.OldQRCode == request.NewQRCode)
        {
            return BadRequest();
        }
        return Ok(await _service.Process_KhongSuaDuoc_Dieumay(request));
    }

    [HttpGet("yeucauxacnhan")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [MaintenanceAuthorize(new string[] { "Admin", "Manager", "Baotri1", "Baotri2" })]
    public async Task<ActionResult<IEnumerable<XacnhanHT_DTO>>> GetList_YeuCauXacNhan()
    {
        return Ok(await _service.Process_GetListYeuCauXN());
    }

    [HttpGet("listyeucau")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [MaintenanceAuthorize(new string[] { "Admin", "Manager", "Baotri1", "Baotri2" })]
    public async Task<ActionResult<IEnumerable<RepairListDTO>>> GetList_YeuCau([FromQuery] string userId)
    {
        return Ok(await _service.Process_GetListYeuCau(userId));
    }

    [HttpPost("xacnhanhtsc")]
    [ProducesResponseType(204)]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [MaintenanceAuthorize(new string[] { "Admin", "Manager", "Baotri1", "Baotri2" })]
    public async Task<ActionResult> XacNhanHoanThanhSC([FromBody] XacnhanHT_DTO request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.QRCode))
        {
            return BadRequest();
        }
        return Ok(await _service.Process_XacNhanHoanThanh(request));
    }

    [HttpPost("khonghtsc")]
    [ProducesResponseType(204)]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [MaintenanceAuthorize(new string[] { "Admin", "Manager", "Baotri1", "Baotri2" })]
    public async Task<ActionResult> XacNhanKhongHoanThanhSC([FromBody] XacnhanHT_DTO request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.QRCode))
        {
            return BadRequest();
        }
        return Ok(await _service.Process_KhongHTSC(request));
    }

    [HttpDelete("yeucauSC/{*code}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [MaintenanceAuthorize(new string[] { "Admin", "Manager", "Baotri1", "Baotri2" })]
    public async Task<ActionResult> XoaYeucauSC(string code, [FromQuery] string userID)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return BadRequest(new
            {
                message = "Mã yêu cầu không được để trống."
            });
        }
        (bool, string) result = await _service.DeleteYeuCauSC(code, userID);
        if (!result.Item1)
        {
            if (result.Item2.Contains("không tìm thấy"))
            {
                return NotFound(new
                {
                    message = result.Item2
                });
            }
            if (result.Item2.Contains("lịch sử sửa chữa"))
            {
                return Conflict(new
                {
                    message = result.Item2
                });
            }
            return BadRequest(new
            {
                message = result.Item2
            });
        }
        return Ok(new
        {
            message = result.Item2
        });
    }

    [HttpGet("repairHistory/{code}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [MaintenanceAuthorize(new string[] { "Admin", "Manager", "Khocty", "Baotri1", "Baotri2", "Chuyen1", "Chuyen2", "View" })]
    public async Task<ActionResult<IEnumerable<RepairHistoryListDTO>>> GetList_RepairHistory(string code)
    {
        return Ok(await _service.Process_GetListRepairHistory(code));
    }

    [HttpGet("yeucau/count")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [MaintenanceAuthorize(new string[] { "Admin", "Manager", "Baotri1", "Baotri2" })]
    public async Task<ActionResult<int>> GetCountYeucau()
    {
        return Ok(await _context.repairRequests.Where((RepairRequestList e) => e.Status == 0).CountAsync());
    }

    [HttpGet("xacnhan/count")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [MaintenanceAuthorize(new string[] { "Admin", "Manager", "Baotri1", "Baotri2" })]
    public async Task<ActionResult<int>> GetCountXacNhan()
    {
        return Ok(await _context.yeucauBQLCXacNhan.Where((YeucauBQLCXacNhan e) => e.Status == 0).CountAsync());
    }

    [HttpGet("validate-device")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [MaintenanceAuthorize(new string[] { "Admin", "Manager", "Baotri1", "Baotri2" })]
    public async Task<ActionResult<object>> ValidateInputType([FromQuery] string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return BadRequest(new
            {
                message = "Giá trị input không được để trống."
            });
        }
        Equipment equip = await _context.Equipment.FirstOrDefaultAsync((Equipment e) => e.QRCode.ToLower() == input.ToLower() || e.SerialNumber.ToLower() == input.ToLower());
        if (equip == null)
        {
            return BadRequest(new
            {
                message = "Không tìm thấy thiết bị."
            });
        }
        return Ok(equip.QRCode);
    }

    [HttpGet("tonghopyeucau")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [MaintenanceAuthorize(new string[] { "Admin", "Manager", "Baotri1", "Baotri2" })]
    public async Task<ActionResult<IEnumerable<ItemRequireList>>> Overview_listRequireSC()
    {
        return Ok(await _service.GetOverView_Require());
    }
    [HttpGet("repairing")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [MaintenanceAuthorize(new string[] { "Admin", "Manager", "Baotri1", "Baotri2" })]
    public async Task<ActionResult<IEnumerable<ItemRequireList>>> Overview_listRepairing()
    {
        return Ok(await _service.GetOverView_Repairing());
    }
    [HttpGet("completed")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [MaintenanceAuthorize(new string[] { "Admin", "Manager", "Baotri1", "Baotri2" })]
    public async Task<ActionResult<IEnumerable<ItemRequireList>>> Overview_listCompleted()
    {
        return Ok(await _service.GetOverView_Completed());
    }
    [HttpGet("hoanthanhsc/details")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [MaintenanceAuthorize(new string[] { "Admin", "Manager", "Baotri1", "Baotri2" })]
    public async Task<ActionResult<HoanthanhSC_Detail_DTO>> Detail_Content_Repair([FromQuery] string qrcode)
    {
        return Ok(await _service.Get_DetailContentRepair(qrcode));
    }

}
