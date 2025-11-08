using System;
using System.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace EquipManagementAPI.Controllers;

[Route("api/Image")]
[ApiController]
public class ImageController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public ImageController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [HttpGet]
    public IActionResult GetImage([FromQuery] string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return BadRequest("Path is empty");
            }
            string uploadFolder = _configuration["UploadFolder"];
            if (string.IsNullOrWhiteSpace(uploadFolder))
            {
                return StatusCode(500, "UploadFolder chưa được cấu hình trong appsettings.json");
            }
            string filePath = Path.Combine(uploadFolder, path);
            if (!System.IO.File.Exists(filePath))
            {
                return NotFound("File not found at: " + filePath);
            }
            byte[] imageBytes = System.IO.File.ReadAllBytes(filePath);
            string text;
            switch (Path.GetExtension(filePath)?.ToLowerInvariant())
            {
                case ".jpg":
                case ".jpeg":
                    text = "image/jpeg";
                    break;
                case ".png":
                    text = "image/png";
                    break;
                case ".gif":
                    text = "image/gif";
                    break;
                default:
                    text = "application/octet-stream";
                    break;
            }
            string contentType = text;
            return File(imageBytes, contentType);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error in GetImage: " + ex.Message);
            return StatusCode(500, "Lỗi khi đọc ảnh: " + ex.Message);
        }
    }
}
