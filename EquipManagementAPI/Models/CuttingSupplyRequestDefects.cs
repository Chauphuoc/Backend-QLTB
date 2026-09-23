using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EquipManagementAPI.Models
{
    [Table("QLCL_CuttingSupplyRequestDefects")]
    public class CuttingSupplyRequestDefects
    {
        [Key]
        public int RowID { get; set; }
        [Column("RequestId")]
        public int CuttingSupplyRequestId { get; set; }
        public string DefectCode { get; set; }
        public string DefectName { get; set; }
        public int Qty { get; set; }
    }
}
