using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EquipManagementAPI.Models
{
    [Table("production_order_versions", Schema = "mes")]
    public class MesProductionOrderVersion
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; }

        [Required]
        [Column("production_order_id")]
        public Guid ProductionOrderId { get; set; }

        [Required]
        [Column("version_number")]
        public int VersionNumber { get; set; }

        [Required]
        [MaxLength(20)]
        [Column("version_status")]
        public string VersionStatus { get; set; } = "ACTIVE";

        [Required]
        [MaxLength(20)]
        [Column("trigger_type")]
        public string TriggerType { get; set; } = "MANUAL";

        [Required]
        [MaxLength(10)]
        [Column("source_po_type")]
        public string SourcePoType { get; set; } = "TEMP";

        [MaxLength(50)]
        [Column("erp_po_id")]
        public string? ErpPoId { get; set; }

        [Column("delta_summary")]
        public string? DeltaSummary { get; set; }

        [Column("superseded_by")]
        public Guid? SupersededBy { get; set; }

        [Required]
        [MaxLength(50)]
        [Column("created_by")]
        public string CreatedBy { get; set; } = string.Empty;

        [Required]
        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }
}
