using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EquipManagementAPI.Models
{
    [Table("production_orders", Schema = "mes")]
    public class MesProductionOrder
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; }

        [Required]
        [MaxLength(30)]
        [Column("plan_code")]
        public string PlanCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        [Column("plan_name")]
        public string PlanName { get; set; } = string.Empty;

        [Column("plan_period_from")]
        public DateOnly? PlanPeriodFrom { get; set; }

        [Column("plan_period_to")]
        public DateOnly? PlanPeriodTo { get; set; }

        [Required]
        [MaxLength(20)]
        [Column("status")]
        public string Status { get; set; } = string.Empty;

        [Required]
        [Column("current_version")]
        public int CurrentVersion { get; set; }

        [MaxLength(50)]
        [Column("approved_by")]
        public string? ApprovedBy { get; set; }

        [Column("approved_at")]
        public DateTime? ApprovedAt { get; set; }

        [Column("notes")]
        public string? Notes { get; set; }

        [Required]
        [MaxLength(50)]
        [Column("created_by")]
        public string CreatedBy { get; set; } = string.Empty;

        [Required]
        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Required]
        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }

        [Required]
        [Column("is_active")]
        public bool IsActive { get; set; }
    }
}
