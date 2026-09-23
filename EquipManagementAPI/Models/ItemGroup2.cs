using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EquipManagementAPI.Models
{
    [Table("Item Group 2")]
    public class ItemGroup2
    {
        [Key]
        [Column("Code")]
        public string Code { get; set; }

        [Column("Description")]
        public string Description { get; set; }
    }
}
