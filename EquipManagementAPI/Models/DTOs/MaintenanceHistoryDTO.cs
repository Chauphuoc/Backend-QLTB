using System.Net.NetworkInformation;
namespace EquipManagementAPI.Models.DTOs
{
    public class MaintenanceHistoryDTO
    {
        public string id { get; set; }
        public string equipCode { get; set; }
        public string title { get; set; }
        public string postingDate { get; set; }
        public string assignee { get; set; }
        public string? content { get; set; }
        public string status { get; set; }

        public string? term { get; set; }
    }
}
