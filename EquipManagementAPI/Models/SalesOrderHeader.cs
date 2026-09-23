using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EquipManagementAPI.Models
{
    [Table("Sales Order Header")]
    public class SalesOrderHeader
    {
        [Key]
        [Column("No_")]
        public string No { get; set; }

        [Column("PO Customer Name")]
        public string POName { get; set; } 

    }
}
