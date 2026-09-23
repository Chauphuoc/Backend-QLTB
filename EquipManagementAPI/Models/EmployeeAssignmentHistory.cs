using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EquipManagementAPI.Models
{
    [Table("QLCL_EmployeeAssignmentHistory", Schema = "mes")]
    public class EmployeeAssignmentHistory
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string EmployeeNo { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string TeamCode { get; set; } = string.Empty;

        [StringLength(200)]
        public string? TeamName { get; set; }

        [StringLength(50)]
        public string? AssignedByNo { get; set; }

        [StringLength(200)]
        public string? AssignedByName { get; set; }

        [Required]
        public DateTime FromDate { get; set; }

        public DateTime? ToDate { get; set; }

        [StringLength(500)]
        public string? Reason { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; }

        /// <summary>Loại kiểm: "Endline" hoặc "SauUi"</summary>
        [StringLength(20)]
        public string? InspectionType { get; set; }
    }
}
