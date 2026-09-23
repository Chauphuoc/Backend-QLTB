using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EquipManagementAPI.Models
{
    [Table("QLCL_TimeSlot")]
    public class TimeSlot
    {
        [Key]
        public int Id { get; set; }
        public int RowID { get; set; }  
        public string Name { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string? Sequence { get; set; }
    }
}
