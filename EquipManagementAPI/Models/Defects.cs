using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EquipManagementAPI.Models
{

    [Table("QLCL_Defects")]
    public class Defects
    {
        public int RowID { get; set; }
        [Key]
        public string Code {  get; set; }
        public string Name { get; set; }
        public string NameEng {  get; set; }
        public int Phase {  get; set; } 
    }
}
