using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EquipManagementAPI.Models
{
    [Table("factory_plan_daily", Schema = "mes")]
    public class MesFactoryPlanDaily
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; }

        [Required]
        [Column("factory_plan_id")]
        public Guid FactoryPlanId { get; set; }

        [Required]
        [Column("work_date")]
        public DateOnly WorkDate { get; set; }

        [Required]
        [Column("planned_qty")]
        public int PlannedQty { get; set; }

        [Column("actual_qty")]
        public int? ActualQty { get; set; }

        [MaxLength(200)]
        [Column("note")]
        public string? Note { get; set; }

        [Required]
        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Required]
        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }
}
