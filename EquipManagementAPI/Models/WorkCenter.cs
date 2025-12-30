using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EquipManagementAPI.Models
{
    [Table("Work Center")]
    public class WorkCenter
    {
        [Key]
        public int RowID { get; set; }
        [Column("No_")]
        public string? No { get; set; }
        [Column("Search Name")]
        public string? Name { get; set; }
        public int Type { get; set; }
    }
}
