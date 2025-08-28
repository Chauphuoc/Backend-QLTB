using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EquipManagementAPI.Models
{
    [Table("Vw_Employee")]
    public class Employee
    {
        [Key]
        [Column("No_")]
        public string No { get; set; }
        [Column("No_ 2")]
        public string No2 { get; set; }
        public string Name { get; set; }
    }
}
