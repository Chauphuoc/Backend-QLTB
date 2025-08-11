using System.ComponentModel.DataAnnotations.Schema;

namespace EquipManagementAPI.Models
{
    [Table("Maintenance Type")]
    public class MaintenanceType
    {
        [Column("RowID")]
        public int Id { get; set; }
        public string? Code { get; set; }
       
        public string? Name { get; set; }
    }
}
