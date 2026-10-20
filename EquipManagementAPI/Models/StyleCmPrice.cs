using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EquipManagementAPI.Models
{
    [Table("style_cm_prices", Schema = "mes")]
    public class StyleCmPrice
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [Column("style_id")]
        [MaxLength(50)]
        public string StyleId { get; set; } = string.Empty;

        [Required]
        [Column("cm_price", TypeName = "decimal(12,4)")]
        public decimal CmPrice { get; set; }

        [Required]
        [Column("currency")]
        [MaxLength(10)]
        public string Currency { get; set; } = "USD";

        [Column("entry_method")]
        [MaxLength(20)]
        public string? EntryMethod { get; set;}

        [Required]
        [Column("effective_date")]
        public DateTime EffectiveDate { get; set; }

        [Column("expired_date")]
        public DateTime? ExpiredDate { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_by")]
        [MaxLength(50)]
        public string? CreatedBy { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("note")]
        [MaxLength(500)]
        public string? Note { get; set; }

        [Column("is_operation")]
        public bool IsOperation {  get; set; }
        // Navigation Property
        public virtual ICollection<StyleCmSplit> StyleCmSplits { get; set; }
            = new List<StyleCmSplit>();
    }
}
