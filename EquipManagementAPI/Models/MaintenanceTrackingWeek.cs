using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EquipManagementAPI.Models
{
    [Table("Maintenance Tracking Week")]
    public class MaintenanceTrackingWeek
    {
        [Column("RowID")]
        public int id { get; set; }
        [Required]
        [Column("Equipment Code")]
        public string EquipmentCode { get; set; }
        [Column("Equipment Group Code")]
        public string EquipmentGroupCode { get; set; }
        [Column("Asset Type Code")]
        public string AssetType { get; set; }
        public string Model { get; set; }
        [Column("Serial Number")]
        public string Serial { get; set; }

        [Column("Last Maintenance time Weekly")]
        public DateTime? LastMaintenanceTime { get; set; }
        [Column("Using Unit")]
        public string? UsingUnit { get; set; }
        [Column("Manage Unit")]
        public string? ManageUnit { get; set; }
        [Column("Location Code")]
        public string? LocationCode { get; set; }
        public string? UserID { get; set; }
        [Column("Next maintenance time Weekly")]
        public DateTime? NextMaintenance { get; set; }
        public int? Status { get; set; }
        [Column("Maintenance Type")]
        public string? MaintenanceType { get; set; }
        public string? QRCode { get; set; }
        public string? Brand { get; set; }
        [Column("Week No")]
        public int? WeekNo { get; set; }
    }
}
