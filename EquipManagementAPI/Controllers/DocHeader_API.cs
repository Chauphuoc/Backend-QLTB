using System.Collections.Generic;
using System.Threading.Tasks;
using EquipManagementAPI.Helpers;
using EquipManagementAPI.Models.DTOs;
using EquipManagementAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace EquipManagementAPI.Controllers;

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
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(400)]
    [MaintenanceAuthorize("Admin", "Manager", "Khocty", "Baotri1", "Baotri2")]
    public async Task<ActionResult<IEnumerable<DocumentEntryHeaderDTO>>> GetDocumentHeaders([FromQuery] int type)
    {
        if (type < 0)
        {
            return BadRequest("Trạng thái không hợp lệ");
        }
        //Nhập mượn ĐV ngoài , Xuất trả mượn ĐV ngoài, Xuất trả thuê,Xuất trả Nhầ CC
        IEnumerable<DocumentEntryHeaderDTO> dtoList = ((type != 6 && type != 11 && type != 12 && type != 13 &&  type != 16) ?
            (await _service.GetDocEntryHeader(type)) : (await _service.GetDocEntryHeader_other(type)));
        return Ok(dtoList);
    }
}
