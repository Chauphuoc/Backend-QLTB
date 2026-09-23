using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EquipManagementAPI.Models
{
    [Table("factory_plan_status_history", Schema = "mes")]
    public class FactoryPlanStatusHistory
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; }

        [Column("plan_id")]
        public Guid PlanId { get; set; }

        [Required]
        [Column("status")]
        [StringLength(50)]
        public string Status { get; set; } = string.Empty;

        [Column("changed_at")]
        public DateTime ChangedAt { get; set; }

        [Column("changed_by")]
        public string? ChangedBy { get; set; }

        [Column("note")]
        [StringLength(50)]
        public string? Note { get; set; }

        [Column("effectiveDate")]
        public DateOnly EffectiveDate { get; set; }
    }
}