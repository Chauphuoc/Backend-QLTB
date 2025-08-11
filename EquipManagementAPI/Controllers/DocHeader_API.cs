using EquipManagementAPI.Data;
using EquipManagementAPI.Helpers;
using EquipManagementAPI.Models.DTOs;
using EquipManagementAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace EquipManagementAPI.Controllers
{
    [Route("api/DocHeader")]
    [ApiController]
    public class DocHeader_API : ControllerBase
    {
        private readonly IDocEntryService _service;

        public DocHeader_API(IDocEntryService service)
        {
            _service = service;
        }

        [HttpGet("type")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<IEnumerable<DocumentEntryHeaderDTO>>> GetDocumentHeaders([FromQuery] int type)
        {
            if(type < 0)
            {
                return BadRequest("Trạng thái không hợp lệ");
            }
            IEnumerable<DocumentEntryHeaderDTO> dtoList;
            if (type == 6 || type == 11 || type  == 12 || type==13 || type ==16)
            {
                dtoList = await _service.GetDocEntryHeader_other(type);
            }
            else
            {
                dtoList = await _service.GetDocEntryHeader(type);
            }
           
            return Ok(dtoList);
        }
    }
}
