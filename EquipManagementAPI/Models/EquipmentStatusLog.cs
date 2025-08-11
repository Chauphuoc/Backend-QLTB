using System.ComponentModel.DataAnnotations.Schema;

namespace EquipManagementAPI.Models
{
    [Table("EquipmentStatusLog")]
    public class EquipmentStatusLog
    {
        [Column("RowID")]
        public int Id { get; set; }
        public string QRCode { get; set; }
        [Column("Created Date")]
        public DateTime CreatedDate { get; set; }
        public int Status { get; set; }
        [Column("Department Code")]
        public string DepartmentCode { get; set; }
        public string WorkShiftCode {  get; set; }
        public string UserID { get; set; }
    }
}
