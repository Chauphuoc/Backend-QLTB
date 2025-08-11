using System.ComponentModel.DataAnnotations.Schema;

namespace EquipManagementAPI.Models
{
    [Table("Maintenance Content")]
    public class MaintenanceContent
    {
        [Column("RowID")]
        public int Id { get; set; }
        [Column("Equipment Group Code")]
        public string? EquipGroupCode { get; set; }
        [Column("Maintenance Type")]
        public string MaintenanceType { get; set; }
        public string Task {  get; set; }
    }
}
