namespace EquipManagementAPI.Models.DTOs
{
    public class TeamHourlyQCItemDto
    {
        public string Hour { get; set; } = ""; // "7:00", "8:00", ...
        public int Inspected { get; set; }
        public int Defects { get; set; }
    }
}
