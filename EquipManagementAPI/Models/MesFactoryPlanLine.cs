using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EquipManagementAPI.Models
{
    [Table("factory_plan_lines", Schema = "mes")]
    public class MesFactoryPlanLine
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; }

        [Required]
        [Column("workcenter_plan_id")]
        public Guid FactoryPlanId { get; set; }

        [Required]
        [MaxLength(50)]
        [Column("size_id")]
        public string SizeId { get; set; } = string.Empty;

        [Required]
        [Column("planned_qty")]
        public int PlannedQty { get; set; }

        [Required]
        [Column("demand_created_qty")]
        public int DemandCreatedQty { get; set; }

        [Column("remaining_qty")]
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public int? RemainingQty { get; set; }

        [Required]
        [Column("work_date")]
        public DateTime WorkDate { get; set; }

        [Required]
        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
        [ForeignKey(nameof(FactoryPlanId))]
        public MesFactoryPlan Plan { get; set; } = default!;
    }
}
