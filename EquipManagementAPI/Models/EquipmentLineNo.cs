using System.ComponentModel.DataAnnotations.Schema;

namespace EquipManagementAPI.Models
{
    [Table("Equipment Line No_")]
    public class EquipmentLineNo
    {
        [Column("RowID")]
        public int Id { get; set; }
        [Column("Department Code")]
        public string DepartmentCode { get; set; }
        [Column("Ending No_")]
        public int EndingNo { get; set; }
        [Column("Increment-by No_")]
        public int Increment { get; set; }
        [Column("Last No_ Used")]
        public int LastUsed { get; set; }
    }
}
