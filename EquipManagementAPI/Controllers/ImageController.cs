using Microsoft.AspNetCore.Mvc;

namespace EquipManagementAPI.Controllers
{
    [Route("api/Image")]
    [ApiController]
    public class ImageController : ControllerBase
    {
        [HttpGet]
        public IActionResult GetImage([FromQuery] string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path))
                    return BadRequest("Path is empty");

                if (!System.IO.File.Exists(path))
                    return NotFound($"File not found at: {path}");

                var imageBytes = System.IO.File.ReadAllBytes(path);
                var extension = Path.GetExtension(path)?.ToLowerInvariant();
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
