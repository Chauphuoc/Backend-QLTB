using System.ComponentModel.DataAnnotations;

namespace EquipManagementAPI.Models.DTOs
{
    public class SupplyRequestItemDto
    {
        [Required(ErrorMessage = "Tên bán thành phẩm là bắt buộc")]
        public string Name { get; set; } = string.Empty;

        [Range(1, int.MaxValue, ErrorMessage = "Số lượng phải >= 1")]
        public int Qty { get; set; }
    }
}
