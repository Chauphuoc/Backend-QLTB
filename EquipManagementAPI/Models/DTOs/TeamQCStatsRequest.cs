namespace EquipManagementAPI.Models.DTOs
{
    public class TeamQCStatsRequest
    {
        public string? Date { get; set; }           // single day: "2026-03-24"
        public string? From { get; set; }           // range start
        public string? To { get; set; }             // range end
        public string? FactoryId { get; set; }      // optional, "all" or null = toàn nhà máy
        public string? OperationType { get; set; }  // "1"=Endline, "2"=Sau ủi, null/"all"=tất cả
    }
}
