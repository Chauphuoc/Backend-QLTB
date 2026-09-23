using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EquipManagementAPI.Models
{
    [Table("po_rename_history", Schema = "mes")]
    public class MesPoRenameHistory
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; }
        [Column("item_id")]
        // Id của MesProductionOrderItem bị đổi tên
        public Guid ItemId { get; set; }
        [Column("old_po_name")]
        public string? OldPoName { get; set; }
        [Column("new_po_name")]
        public string NewPoName { get; set; } = string.Empty;
        [Column("changed_by")]
        public string ChangedBy { get; set; } = string.Empty;
        [Column("changed_at")]
        public DateTime ChangedAt { get; set; }
    }
}
