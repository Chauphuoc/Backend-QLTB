namespace EquipManagementAPI.Models.DTOs
{
    public class LaborDayDetailDto
    {
        // Tổng công (Sum WorkDay) — bảng "HR Daily Working Hour", theo Work Shift Code + DateIn
        public decimal Cong { get; set; }
        // Tổng giờ tăng ca (Sum OverTime) — bảng "Register OverTime", theo Work Shift Code + Posting Date
        public decimal OverTimeHours { get; set; }
    }

    public class LaborDetailItemDto
    {
        public string Id { get; set; } = null!;
        public string ToMay { get; set; } = null!;
        public Dictionary<string, LaborDayDetailDto>? Daily { get; set; }
    }
}
