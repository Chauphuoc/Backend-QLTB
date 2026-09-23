namespace EquipManagementAPI.Models.DTOs
{
    public class TeamDailyRevenueDto
    {
        public int Day { get; set; }
        public decimal DoanhThuThucTe { get; set; } // Doanh thu thực hiện
        public decimal DoanhThuKeHoach { get; set; }  // Doanh thu kế hoạch
        public double PctHoanThanh { get; set; }       // % hoàn thành doanh thu
    }
}
