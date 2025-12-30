using System.ComponentModel.DataAnnotations.Schema;

namespace EquipManagementAPI.Models
{
    [Table("Repair_Description")]
    public class Repair_Description
    {
        [Column("RowID")]
        public int Id { get; set; }

        public string RepairTypeNo_ { get; set; }
        public string Name { get; set; }
    }
}
