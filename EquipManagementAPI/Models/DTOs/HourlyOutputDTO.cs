namespace EquipManagementAPI.Models.DTOs
{
    public class HourlyOutputDTO
    {
        public string TimeSlot { get; set; }  // "7h30-8h30"
        public int Hour { get; set; }  // 7  (dùng để sort nếu cần)
        public int ThoatChuyen { get; set; }
    }
}
