namespace EquipManagementAPI.Models.DTOs
{
    public class StyleDefectSummaryDto
    {
        public string Style { get; set; } = string.Empty;

        public string StyleName { get; set; } = string.Empty;

        /// <summary>Tổng số sản phẩm lỗi của style này trong khoảng thời gian</summary>
        public int TotalDefects { get; set; }

        /// Tỷ lệ lỗi (%) = TotalDefects / TotalInspected × 100, làm tròn 2 chữ số.
        /// TotalInspected là tổng lần kiểm của style đó (không phải toàn nhà máy).
        public double DefectRate { get; set; }
    }
}
