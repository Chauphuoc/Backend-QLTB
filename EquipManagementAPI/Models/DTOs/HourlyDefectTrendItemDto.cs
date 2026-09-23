namespace EquipManagementAPI.Models.DTOs
{
    /// <summary>
    /// Dữ liệu kiểm/lỗi theo từng khung giờ — dùng cho biểu đồ
    /// "Diễn biến lỗi theo giờ" ở tab Quản lý chất lượng (overview)
    /// </summary>
    public class HourlyDefectTrendItemDto
    {
        public string Hour { get; set; } = "";          // tên khung giờ, e.g. "7h30-8h30"
        public int Inspected { get; set; }              // tổng kiểm
        public int Defects { get; set; }                // tổng lỗi
        public double DefectRate { get; set; }          // % lỗi = Defects/Inspected*100
    }
}
