using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace EquipManagementAPI.Controllers
{
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
                    return BadRequest("Path is empty");

                // Lấy thư mục từ cấu hình
                var uploadFolder = _configuration["UploadFolder"];
                if (string.IsNullOrWhiteSpace(uploadFolder))
                    return StatusCode(500, "UploadFolder chưa được cấu hình trong appsettings.json");

                // Ghép đường dẫn tuyệt đối
                var filePath = Path.Combine(uploadFolder, path);

                if (!System.IO.File.Exists(filePath))
                    return NotFound($"File not found at: {filePath}");

                var imageBytes = System.IO.File.ReadAllBytes(filePath);
                var extension = Path.GetExtension(filePath)?.ToLowerInvariant();

                var contentType = extension switch
                {
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".png" => "image/png",
                    ".gif" => "image/gif",
                    _ => "application/octet-stream"
                };

                return File(imageBytes, contentType);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in GetImage: " + ex.Message);
                return StatusCode(500, $"Lỗi khi đọc ảnh: {ex.Message}");
            }
        }
    }
}
