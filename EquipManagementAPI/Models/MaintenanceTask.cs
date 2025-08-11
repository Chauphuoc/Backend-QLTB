using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EquipManagementAPI.Models
{
    [Table("MaintenanceTask")]
    public class MaintenanceTask
    {
        [Column("RowID")]
        public int Id { get; set; }
       
        public string QRCode { get; set; }
        public DateTime PostingDate { get; set; }
        public string Task {  get; set; }
        public string Type { get; set; }
    }
}
