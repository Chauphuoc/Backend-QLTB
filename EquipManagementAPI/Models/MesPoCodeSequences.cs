using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EquipManagementAPI.Models
{
    [Table("po_code_sequence", Schema = "mes")]
    public class MesPoCodeSequences
    {
        [Key]
        [Column("factory_id")]
        public string? FactoryId { get; set; }

        [Required]
        [MaxLength(6)]
        [Column("period")]
        public string Period { get; set; } = string.Empty;

        [Column("last_seq")]
        public int LastSeq { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } 

    }
}
