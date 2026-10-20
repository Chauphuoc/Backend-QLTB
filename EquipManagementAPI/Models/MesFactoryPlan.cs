using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EquipManagementAPI.Models
{
    [Table("factory_plans", Schema = "mes")]
    public class MesFactoryPlan
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; }

        [Required]
        [MaxLength(150)]
        [Column("plan_code")]
        public string PlanCode { get; set; } = string.Empty;

        [Column("production_order_id")]
        public Guid ProductionOrderId { get; set; }

        [Required]
        [Column("version_id")]
        public Guid VersionId { get; set; }

        [Required]
        [MaxLength(50)]
        [Column("factory_id")]
        public string FactoryId { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        [Column("style_id")]
        public string StyleId { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        [Column("po_id")]
        public string PoId { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        [Column("color_id")]
        public string ColorId { get; set; } = string.Empty;

        [MaxLength(20)]
        [Column("line_number")]
        public string? LineNumber { get; set; } = string.Empty;

        [Column("production_start_date")]
        public DateOnly? ProductionStartDate { get; set; }

        [Column("production_end_date")]
        public DateOnly? ProductionEndDate { get; set; }

        [Required]
        [Column("buffer_days")]
        public int BufferDays { get; set; }

        [Column("btp_required_date")]
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public DateOnly? BtpRequiredDate { get; set; }

        [Required]
        [Column("total_planned_qty")]
        public int TotalPlannedQty { get; set; }

        [Required]
        [MaxLength(20)]
        [Column("status")]
        public string Status { get; set; } = string.Empty;

        [MaxLength(50)]
        [Column("approved_by")]
        public string? ApprovedBy { get; set; }

        [Column("approved_at")]
        public DateTime? ApprovedAt { get; set; }

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
        [Column("operation_code")]
        public string? OperationCode { get; set; }
        [Column("parent_plan_id")]
        public Guid? ParentPlanId { get; set; }
        [Column("is_group")]
        public bool IsGroup { get; set; }

        [Column("merged_at")]
        public DateTime? MergedAt { get; set; }

        [Column("merged_by")]
        public string? MergedBy { get; set; }
        // Navigation — self-reference cho quan h? PO Gom / PO con
        public MesFactoryPlan? Parent { get; set; }
        public ICollection<MesFactoryPlan> Children { get; set; } = new List<MesFactoryPlan>();
        public ICollection<MesFactoryPlanLine> Lines { get; set; } = new List<MesFactoryPlanLine>();
        [Column("updated_by")]
        public string? UpdatedBy { get; set; }
    }
}
