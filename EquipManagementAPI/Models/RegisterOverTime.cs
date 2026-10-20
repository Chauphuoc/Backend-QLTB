using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EquipManagementAPI.Models
{
    [Table("Register OverTime")]
    public class RegisterOverTime
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int RowID { get; set; }

        [Required]
        public string EmpCode { get; set; }

        [Required]
        [Column("Posting Date")]
        public DateTime PostingDate { get; set; }

        [Required]
        [Column("Accept Over Time")]
        public int AcceptOverTime { get; set; }

        public int? Status { get; set; }

        [Column("Login ID")]
        public string? LoginID { get; set; }

        [Required]
        [Column("Work Shop Code")]
        public string WorkShopCode { get; set; }

        [Column("Work Shift Code")]
        public string? WorkShiftCode { get; set; }

        [Column("Work Shift Place")]
        public string? WorkShiftPlace { get; set; }

        public int? Type { get; set; }

        public decimal? OverTime { get; set; }

        public string? GioRa { get; set; }
    }
}
