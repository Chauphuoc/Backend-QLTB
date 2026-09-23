using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EquipManagementAPI.Models
{
    [Table("QLCL_Reinspection", Schema = "mes")]
    public class QLCL_Reinspection
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string ReinspectionNo { get; set; }

        public int? OriginalSessionRowID { get; set; }

        [Required]
        [StringLength(50)]
        public string CreatedBy { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        [StringLength(50)]
        public string? FactoryCode { get; set; }

        [StringLength(50)]
        public string? ProductionTeamCode { get; set; }

        /// <summary>
        /// Rework | Pass | Scrap | Partial
        /// </summary>
        [Required]
        [StringLength(20)]
        public string Disposition { get; set; } = "Partial";

        /// <summary>
        /// Draft | Finalized | Cancelled
        /// </summary>
        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "Draft";

        [StringLength(1000)]
        public string? Notes { get; set; }

        public int TotalQuantity { get; set; }

        public int TotalPassed { get; set; }

        public int TotalScrapped { get; set; }

        [StringLength(50)]
        public string? FinalizedBy { get; set; }

        public DateTimeOffset? FinalizedAt { get; set; }
        public string? OperationType { get; set; }
    }
}
