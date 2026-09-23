namespace EquipManagementAPI.Models.DTOs
{
    public class TeamPlanSummaryDTO
    {
        public string? TeamId { get; set; }  // "to_cat_1"
        public string? TeamName { get; set; }  // "Tổ Cắt 1"
        public int? KeHoach { get; set; }  // kế hoạch ngày (SP)
        public int? ThoatChuyen { get; set; }  // thực tế thoát chuyền
        public decimal? CompletionRate { get; set; }  // ThoatChuyen/KeHoach*100, làm tròn 1 chữ số

    }
}
