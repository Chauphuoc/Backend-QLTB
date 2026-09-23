using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EquipManagementAPI.Models
{
    [Table("QLCL_CuttingSupplyRequestItem")]
    public class CuttingSupplyRequestItems
    {
        [Key]
        public int RowID { get; set; }
        [Column("RequestId")]
        public int CuttingSupplyRequestId { get; set; }

        // 👇 Navigation
        public CuttingSupplyRequest CuttingSupplyRequest { get; set; }
        public string Name { get; set; }
        public int Quantity { get; set; }
    }
}
