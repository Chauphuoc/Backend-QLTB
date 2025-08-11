using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EquipManagementAPI.Models
{
    [Table("Repair Content")]
    public class RepairContent

    {
        [Key]
        public int RowID { get; set; }
        [Column("Document No_")]
        public string DocNo { get; set; }
        public string Name { get; set; }
        public string Detail {  get; set; }
        [Column("Repair Type")]
        public string RepairType { get; set; }
    }
}
