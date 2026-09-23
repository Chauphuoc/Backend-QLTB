using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EquipManagementAPI.Models
{
    [Table("QLCL_ReinspectionSessionLink", Schema = "mes")]
    public class QLCL_ReinspectionSessionLink
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ReinspectionId { get; set; }

        /// <summary>
        /// RowID của QLCL_QualityManagementSession
        /// </summary>
        [Required]
        public int SessionRowID { get; set; }

        /// <summary>
        /// Số lượng lấy từ session này
        /// </summary>
        [Required]
        public int QuantityFromSession { get; set; }

        // Navigation Property
        [ForeignKey(nameof(ReinspectionId))]
        public virtual QLCL_Reinspection Reinspection { get; set; }
    }
}
