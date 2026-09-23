using System.ComponentModel.DataAnnotations.Schema;

namespace EquipManagementAPI.Models.DTOs
{
    public class MaintenanceHisOverview
    {
        public int RowID { get; set; }

        public string? EquipmentCode { get; set; }

        public string? EquipGroupCode { get; set; }

        public string? Serial { get; set; }

        public string? Brand { get; set; }

        public string? QRCode { get; set; }

        public DateTime? PostingDate { get; set; }

        public string? ManageUnit { get; set; }

        public string? UsingUnit { get; set; }

        public string? Location { get; set; }

        public string? UserID { get; set; }

        public string? MaintenanceType { get; set; }

        public int? Status { get; set; }
        public DateTime? NextMaintenanceTime { get; set; }
        public int? Id { get; set; }
    }
}
