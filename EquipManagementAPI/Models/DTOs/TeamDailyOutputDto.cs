namespace EquipManagementAPI.Models.DTOs
{
    public class TeamDailyOutputDto
    {
        public int Day { get; set; }   // ngày trong tháng (1-31)
        public int ThuChiHien { get; set; }   // Thực hiện
        public int KeHoach { get; set; }   // Kế hoạch ngày
        public double PctHoanThanh { get; set; }   // % hoàn thành

    }
}
