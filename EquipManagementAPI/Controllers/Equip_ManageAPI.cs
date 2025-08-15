
using Azure.Core;
using EquipManagementAPI.Data;
using EquipManagementAPI.Helpers;
using EquipManagementAPI.Models;
using EquipManagementAPI.Models.DTOs;
using EquipManagementAPI.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EquipManagementAPI.Controllers
{
    [Route("api/Equipment")]
    [ApiController]
    public class Equip_ManageAPI : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        public Equip_ManageAPI(ApplicationDbContext context,IAssetService service)
        {
            _context = context;
            _service = service;
        }
        private readonly IAssetService _service;

        
        [HttpGet("{code}", Name = "GetQuip")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<EquipmentDTO>> GetEquipment(string code)
        {
            
            if (code == "")
            {
                return BadRequest();
            }
            var equip = await _service.GetInforEquipment(code);
           
            if (equip == null)
            {
                return NotFound($"Không tìm thấy mã thiết bị!");
            }
            return Ok(equip);
        }

        [HttpPost("scan")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> ScanQRCode0([FromBody] QRCodeEntryBatchDTO data)
        {
           try
            {
                var result = await _service.ProcessQRCodeBatchAsync(data);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("scanType1")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> ScanQRCode_XuatDVQL([FromBody] QRCodeEntryBatchDTO data)
        {
            try
            {
                var result = await _service.ProcessScanQRCode_Type1(data);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut("update-serial")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> UpdateEquip([FromBody] EquipmentDTO equipDTO)
        {
            if (equipDTO == null)
            {
                return BadRequest("Dữ liệu không hợp lệ.");
            }
            var result = await _service.UpdateSerialNumber(equipDTO.EquipmentCode, equipDTO.SerialNumber);
            if (!result)
            {
                return NotFound($"Không tìm thấy thiết bị với mã {equipDTO.EquipmentCode}");
            }
            return Ok($"Cập nhật Số serial thành công!");
        }

        [HttpPut("scan-sdlk")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> Scan_SudungLuuKho([FromBody] EquipScan01DTO equipDTO)
        {
            try
            {
                if (equipDTO == null)
                {
                    return BadRequest();
                }
                var result = await _service.ProcessScan_SuDungLuuKho(equipDTO);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("unit", Name = "GetUnit")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<IEnumerable<DepartmentDTO>>> GetUnit_SDLK()
        {
            try
            {
               var departments = await _context.Department.Select(e =>
               
                   new DepartmentDTO
                   {
                       Value = e.Code,
                       Label = e.Name,
                   }
               ).ToListAsync();
                return Ok(departments);
            }
            catch(Exception ex)
            {
                return BadRequest("Có lỗi xảy ra khi lấy danh sách đơn vị: " + ex.Message);
            }
            
            
        }
        [HttpGet("locationXSD", Name = "GetLocation")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<LocationXsdDTO>> GetLocationXSD([FromQuery] string unit)
        {
            try
            {
                var check = await _context.Department.FirstOrDefaultAsync(e => e.Code == unit);
                if (check == null)
                {
                    return BadRequest();
                }
                var Locations = await _context.locationXSDs.Where(e=>e.DepartmentCode == unit).Select(e =>
                new LocationXsdDTO
                {
                    Value = e.Code,
                    Label = e.Name,
                }
                ).ToListAsync();
                return Ok(Locations);
            }
            catch( Exception ex)
            {
                return BadRequest("Có lỗi xảy ra khi lấy danh sách vị trí: " + ex.Message);
            }
        }

        [HttpGet("typeSDLK")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> GetType_LKSD()
        {
            try
            {
                var result = new List<object>
               {
                   new {value="0", label = "Lưu kho"},
                   new {value="1", label = "Sử dụng"},
               };
                return Ok(result);
            }
            catch(Exception ex)
            {
                return BadRequest("Có lỗi xảy ra khi lấy loại quét: " + ex.Message);
            }
        }

        [HttpGet("maintenanceType")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<MaintenanceTypeDTO>> GetMaintenanceType()
        {
            try
            {
                var result = await _service.GetMaintenanceType();
                return Ok(result);
            }
            catch( Exception ex)
            {
                return BadRequest("Lỗi"+ex.Message);
            }
        }

        [HttpPost("maintenanceContent", Name = "GetMaintenanceContent")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<MaintenanceRequest>> GetMaintenanceContent([FromBody] MaintenanceRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.QRCode) || string.IsNullOrWhiteSpace(request.MaintenanceType))
                {
                    return BadRequest("Thiếu QRCode hoặc loại bảo dưỡng.");
                }
                var result = await _service.GetMaintenanceContent(request);
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
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> Scan_BaoDuong([FromBody] RequestScanMaintenance request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.QRCode) || request.Content.Count==0 )
                {
                    return BadRequest();
                }
                var result = await _service.ProcessScan_BaoDuong(request);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
        [HttpPost("requireSC")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> Request_SuaChua([FromBody] RequestSuaChua request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.QRCode))
                {
                    return BadRequest();
                }
                var result = await _service.Process_SuaChua(request);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("suachua/{code}", Name = "GetSuaChua")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<InforRequestSC>> GetEquipment_SuaChua(string code)
        {
            if (code == "")
            {
                return BadRequest();
            }
            var equip = await _service.GetInforEquipSC(code);
            if (equip == null)
            {
                return NotFound($"Lỗi {code} chưa được tạo yêu cầu sửa chữa!");
            }
            return Ok(equip);
        }

        [HttpPost("batdauSC")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> BatDauSuaChua([FromBody] RequestSuaChua  request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.QRCode))
            {
                return BadRequest();
            }
            var result = await _service.Process_BatDauSuaChua(request);
            return Ok(result);
            
        }

        [HttpGet("hoanthanhsc/{code}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<InforRequestSC>> GetEquipment_HoanThanhSC(string code)
        {
            if (code == "")
            {
                return BadRequest();
            }
            var equip = await _service.GetInforEquipHTSC(code);
            if (equip == null)
            {
                return NotFound($"Lỗi không tìm thấy thiết bị trong danh sách sửa chữa");
            }
            return Ok(equip);
        }

        [HttpGet("reason")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<MaintenanceTypeDTO>> GetReasonType()
        {
            try
            {
                var result = await _service.GetReasonType();
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest("Lỗi" + ex.Message);
            }
        }

        [HttpPost("hoanthanhSC")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> HoanThanhSuaChua([FromBody] RequestHTSC request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.QRCode))
            {
                return BadRequest();
            }
            var result = await _service.Process_HTSC(request);
            return Ok(result);

        }

        [HttpPost("khongsuaduoc")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> KhongSuaDuoc([FromBody] RequestHTSC request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.QRCode))
            {
                return BadRequest();
            }
            var result = await _service.Process_KhongSuaDuoc(request);
            return Ok(result);

        }

        [HttpGet("yeucauxacnhan")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<IEnumerable<XacnhanHT_DTO>>> GetList_YeuCauXacNhan()
        {
            IEnumerable<XacnhanHT_DTO> dtoList;
           
            dtoList = await _service.Process_GetListYeuCauXN();
           
            return Ok(dtoList);
        }

        [HttpGet("listyeucau")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<IEnumerable<RepairListDTO>>> GetList_YeuCau()
        {
            IEnumerable<RepairListDTO> dtoList;

            dtoList = await _service.Process_GetListYeuCau();

            return Ok(dtoList);
        }

        [HttpPost("xacnhanhtsc")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> XacNhanHoanThanhSC([FromBody] XacnhanHT_DTO request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.QRCode))
            {
                return BadRequest();
            }
            var result = await _service.Process_XacNhanHoanThanh(request);
            return Ok(result);

        }

        [HttpPost("khonghtsc")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> XacNhanKhongHoanThanhSC([FromBody] XacnhanHT_DTO request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.QRCode))
            {
                return BadRequest();
            }
            var result = await _service.Process_KhongHTSC(request);
            return Ok(result);

        }
    }
}
 