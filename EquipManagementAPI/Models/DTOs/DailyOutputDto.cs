namespace EquipManagementAPI.Models.DTOs
{
    public class DailyOutputDto
    {
        public string Date { get; set; }       // "2026-04-04" (ISO)
        public int TotalOutput { get; set; }
        public int KeHoach { get; set; }
    }
}
