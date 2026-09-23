using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EquipManagementAPI.Models
{
    [Table("Item")]
    public class Item
    {
        [Key]
        public int RowID { get; set; }

        [Column("No_")]
        public string No { get; set; }

        [Column("Name")]
        public string Name { get; set; }
    }
}
