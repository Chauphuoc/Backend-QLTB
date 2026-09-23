using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EquipManagementAPI.Models
{
    [Table("QLCL_ReinspectionItem", Schema = "mes")]
    public class QLCL_ReinspectionItem
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ReinspectionId { get; set; }

        [StringLength(50)]
        public string? Style { get; set; }

        [StringLength(50)]
        public string? PO { get; set; }

        [StringLength(50)]
        public string? Color { get; set; }

        [StringLength(20)]
        public string? Size { get; set; }

        /// <summary>
        /// Số lượng kiểm lại
        /// </summary>
        [Required]
        public int Quantity { get; set; }

        /// <summary>
        /// JSON hoặc CSV danh sách lỗi
        /// </summary>
        public string? DefectCodes { get; set; }

        /// <summary>
        /// Pass | Fail | Scrap | Rework
        /// </summary>
        [Required]
        [StringLength(20)]
        public string Result { get; set; }

        [StringLength(50)]
        public string? InspectorNo { get; set; }

        public DateTimeOffset? InspectedAt { get; set; }

        [StringLength(500)]
        public string? Note { get; set; }

        // Navigation Property
        [ForeignKey(nameof(ReinspectionId))]
        public virtual QLCL_Reinspection Reinspection { get; set; }
    }
}
