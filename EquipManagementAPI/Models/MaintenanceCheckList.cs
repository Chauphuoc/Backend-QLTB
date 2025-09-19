using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EquipManagementAPI.Models
{
    [Table("MaintenanceCheckList")]
    public class MaintenanceCheckList
    {
        [Key]
        [Column("RowID")]
        public int RowID { get; set; }
        public int? HistoryID { get; set; }
        public string? MaintenanceType { get; set; }
        [Column("Equip Group")]
        public string? EquipGroup { get; set; }
        public string? Task { get; set; }
        public string Note { get; set; }
        [Column("Maintenance Time")]
        public DateTime? MaintenanceTime { get; set; }
        public string EquipCode { get; set; }
    }
}
