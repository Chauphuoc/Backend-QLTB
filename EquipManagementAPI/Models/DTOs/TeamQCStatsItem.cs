namespace EquipManagementAPI.Models.DTOs
{
    public class TeamQCStatsItem
    {
        public string TeamId { get; set; } = "";
        public string Team { get; set; } = "";
        public int TotalInspected { get; set; }
        public int TotalDefects { get; set; }
        public int TotalEscaped { get; set; }
    }
}
