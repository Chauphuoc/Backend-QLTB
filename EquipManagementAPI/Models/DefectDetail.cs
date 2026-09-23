using System.ComponentModel.DataAnnotations.Schema;

namespace EquipManagementAPI.Models
{
    [Table("QLCL_DefectDetail")]
    public class DefectDetail
    {
        [Column("RowID")]
        public int Id { get; set; }
        public Guid QualityManagementCode { get; set; }
        public string DefectCode { get; set; }  
        public int Quantity { get; set; }    
        public string DefectName { get; set; }
        public QualityManagementSession Session { get; set; }
    }
}
