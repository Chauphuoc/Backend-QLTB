using System.ComponentModel.DataAnnotations.Schema;

namespace EquipManagementAPI.Models
{
    [Table("Vendor QLTB")]
    public class Vendor
    {
        [Column("RowID")]
        public int Id { get; set; }

        [Column("No_")]
        public string? No { get; set; }

        public string? Name { get; set; }

        [Column("Search Name")]
        public string? SearchName { get; set; }

        

    }
}
