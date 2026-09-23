using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EquipManagementAPI.Models
{
    [Table("style_cm_splits", Schema = "mes")]
    public class StyleCmSplit
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Column("style_cm_price_id")]
        public Guid StyleCmPriceId { get; set; }

        [Required]
        [Column("operation_code")]
        [MaxLength(30)]
        public string OperationCode { get; set; } = string.Empty;

        [Required]
        [Column("operation_name")]
        public string OperationName { get; set; } = string.Empty;

        [Required]
        [Column("split_ratio", TypeName = "decimal(5,2)")]
        public decimal SplitRatio { get; set; }

        [Required]
        [Column("split_price", TypeName = "decimal(12,2)")]
        public decimal SplitPrice { get; set; }

        [Column("effective_from")]
        public DateTime EffectiveFrom { get; set; }

        [Column("created_by")]
        [MaxLength(50)]
        public string? CreatedBy { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("note")]
        [MaxLength(500)]
        public string? Note { get; set; }

        [Column("default_line_id")]
        public string? DefaultLineId { get; set; }

        // Navigation Property
        [ForeignKey(nameof(StyleCmPriceId))]
        public virtual StyleCmPrice? StyleCmPrice { get; set; }
    }
}
