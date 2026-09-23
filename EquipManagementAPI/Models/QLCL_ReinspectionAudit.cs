using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EquipManagementAPI.Models
{
    [Table("QLCL_ReinspectionAudit", Schema = "mes")]
    public class QLCL_ReinspectionAudit
    {
        [Key]
        public int Id { get; set; }

        public int? ReinspectionId { get; set; }

        /// <summary>
        /// Create | Update | Finalize | Cancel
        /// </summary>
        [Required]
        [StringLength(50)]
        public string Action { get; set; }

        [StringLength(50)]
        public string? PerformedBy { get; set; }

        public DateTimeOffset PerformedAt { get; set; }

        /// <summary>
        /// JSON snapshot hoặc chi tiết thay đổi
        /// </summary>
        public string? Payload { get; set; }

        // Navigation Property
        [ForeignKey(nameof(ReinspectionId))]
        public virtual QLCL_Reinspection? Reinspection { get; set; }
    }
}
