using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EquipManagementAPI.Models
{
    [Table("production_order_items", Schema = "mes")]
    public class MesProductionOrderItem
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; }

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

        [MaxLength(50)]
        [Column("shade_id")]
        public string? ShadeId { get; set; }

        [Required]
        [MaxLength(50)]
        [Column("size_id")]
        public string SizeId { get; set; } = string.Empty;

        [Required]
        [Column("planned_qty")]
        public int PlannedQty { get; set; }

        [Required]
        [Column("target_delivery_date")]
        public DateOnly TargetDeliveryDate { get; set; }

        [MaxLength(20)]
        [Column("disposal_action")]
        public string? DisposalAction { get; set; }

        [Column("disposal_notes")]
        public string? DisposalNotes { get; set; }

        [Required]
        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [MaxLength(50)]
        [Column("PO Name")]
        public string? POName { get; set; }

        [MaxLength(50)]
        [Column("Finish Name")]
        public string? FinishName { get; set; }
        [Column("parent_item_id")]
        public Guid? ParentItemId { get; set; }
        public string? Status { get; set; }
        [Column("is_active")]
        public bool? IsActive { get; set; } = true;
        [Column("merged_at")]
        public DateTime? MergedAt { get; set; }
        [Column("merged_by")]
        public string? MergedBy { get; set; }
        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }
        [Column("updated_by")]
        public string? UpdatedBy { get; set; }
    }
}
