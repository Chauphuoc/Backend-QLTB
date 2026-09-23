using System.ComponentModel.DataAnnotations;

namespace EquipManagementAPI.Models.DTOs
{
    public class CuttingSupplyRequestDto
    {
        [Required(ErrorMessage = "Factory là bắt buộc")]
        public string Factory { get; set; } = string.Empty;

        [Required(ErrorMessage = "ProductionTeam là bắt buộc")]
        public string ProductionTeam { get; set; } = string.Empty;

        [Required(ErrorMessage = "Style là bắt buộc")]
        public string Style { get; set; } = string.Empty;

        [Required(ErrorMessage = "PO là bắt buộc")]
        public string Po { get; set; } = string.Empty;

        [Required(ErrorMessage = "Color là bắt buộc")]
        public string Color { get; set; } = string.Empty;

        /// <summary>Size tùy chọn (XS / S / M / L / XL / XXL)</summary>
        public string? Size { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Số lượng không sửa được phải >= 1")]
        public int UnreparableCount { get; set; }

        [Required]
        [MinLength(1, ErrorMessage = "Phải có ít nhất 1 bán thành phẩm cần thay")]
        public List<SupplyRequestItemDto> Items { get; set; } = new();

        [Required(ErrorMessage = "RequestedBy là bắt buộc")]
        public string RequestedBy { get; set; } = string.Empty;
        public List<NotReworkDefects> DefectTypes { get; set; } = new();

        /// <summary>DirectInspection = phát hiện trực tiếp khi kiểm | AfterReinspection = sau kiểm lại</summary>
        public string? SourceType { get; set; }

        /// <summary>Id của phiên kiểm lại (nếu SourceType = AfterReinspection)</summary>
        public int? ReinspectionId { get; set; }

        /// <summary>No của session kiểm thường (nếu SourceType = DirectInspection)</summary>
        public string? SessionNo { get; set; }
    }
}
