using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EquipManagementAPI.Models
{
    [Table("Item Size")]
    public class ItemSize
    {
        [Key]
        public int RowID { get; set; }

        [Column("No_")]
        public string No { get; set; } = string.Empty;

        [Column("Description")]
        public string? Description { get; set; }

        [Column("Ordinal Number")]
        public int OrdinalNumber { get; set; }
    }
}
