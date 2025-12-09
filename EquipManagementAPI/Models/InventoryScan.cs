using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
namespace EquipManagementAPI.Models
{
    [Table("InventoryScan")]
    public class InventoryScan
    {
        [Key]
        [Column("RowID")]
        public int RowId { get; set; }
        public string? EquipmentGroupCode { get; set; }
        public string? QRCode { get; set; }
        public DateTime? PostingDate { get; set; }
        public string? Model { get; set; }
        public string? UserID { get; set; }
        public string? ManageUnit { get; set; }
        public string? WorkCenter { get; set; }
        public string? ScanLocation { get; set; }
        public string? Year { get; set; }
        public int? Status { get; set; }
        public int? TinhTrang {  get; set; }
    }
}
