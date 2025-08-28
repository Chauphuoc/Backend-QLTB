using System.ComponentModel.DataAnnotations.Schema;

namespace EquipManagementAPI.Models
{
    [Table("Maintenance History")]
    public class MaintenanceHistory
    {
        [Column("RowID")]
        public int Id { get; set; }
        [Column("Equipment Code")]
        public string? EquipmentCode { get; set; }
        [Column("Equipment Group Code")]
        public string? EquipGroupCode { get; set; }
        [Column("Serial Number")]
        public string? Serial {  get; set; }
        public string? Brand { get; set; }
        public string? QRCode { get; set; }
        public DateTime? PostingDate { get; set; }
        [Column("Manage Unit")]
        public string? ManageUnit { get; set; }
        [Column("Using Unit")]
        public string? UsingUnit { get; set; }
        public string? Location { get; set; }
        public string? UserID { get; set; }
        [Column("Maintenance Type")]
        public string? MaintenanceType { get; set; }
        [Column("Next maintenance time")]
        public DateTime? NextMaintenanceTime { get; set;}
    }
}
