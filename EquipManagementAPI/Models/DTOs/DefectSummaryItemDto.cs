namespace EquipManagementAPI.Models.DTOs
{
    public class DefectSummaryItemDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int Count { get; set; }
        public double Percentage { get; set; }
        public string Trend { get; set; } = "stable"; // "up" | "down" | "stable"
        public string Severity { get; set; } = "low";    // "high" | "medium" | "low"
    }
}
