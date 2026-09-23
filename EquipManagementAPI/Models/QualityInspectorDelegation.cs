using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace EquipManagementAPI.Models
{
    [Table("QLCL_QualityInspectorDelegation", Schema = "mes")]
        public class QualityInspectorDelegation
        {
            [Key]
            public int Id { get; set; }

            [Required]
            [StringLength(50)]
            public string EmployeeNo { get; set; }   // người được phân công/điều động

            [Required]
            [StringLength(50)]
            public string TargetTeamCode { get; set; }   // tổ được cấp quyền

            [StringLength(200)]
            public string? TargetTeamName { get; set; }

            [StringLength(50)]
            public string? GrantedByNo { get; set; }

            [StringLength(200)]
            public string? GrantedByName { get; set; }

            [Required]
            public DateTime ValidFrom { get; set; }

            public DateTime? ValidUntil { get; set; }

            [Required]
            [StringLength(20)]
            public string Status { get; set; } = "Active";

            [Required]
            public bool IsPermanent { get; set; } = false;

            [StringLength(500)]
            public string? Reason { get; set; }

            [Required]
            public DateTime CreatedAt { get; set; }

            public DateTime? RevokedAt { get; set; }

            [StringLength(50)]
            public string? RevokedBy { get; set; }

            /// <summary>Loại kiểm: "Endline" hoặc "SauUi"</summary>
            [StringLength(20)]
            public string? InspectionType { get; set; }
        }
    
}
