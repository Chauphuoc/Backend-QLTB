namespace EquipManagementAPI.Models.DTOs
{
    public class TeamDefectBreakdownItemDto
    {
        public string Name { get; set; }
        public int Count { get; set; }
        public double Percentage { get; set; }
        public string Severity { get; set; } = "low"; // "high" | "medium" | "low"

    }
}
