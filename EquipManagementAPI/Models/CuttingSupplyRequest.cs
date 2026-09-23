using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EquipManagementAPI.Models
{
    [Table("QLCL_CuttingSupplyRequest")]
    public class CuttingSupplyRequest
    {
        [Key]
        [Column("RowID")]
        public int RowID { get; set; }
        public string WorkCenterCode { get; set; }
        public string WorkShiftCode { get; set; }
        public string? ItemNo { get; set; }
        public string Style { get; set; }
        public string? SalesOrderNo { get; set; }
        public string PoName { get; set; }
        public string Color { get; set; }
        public string Size { get; set; }
        public int UnreparableCount { get; set; }
        public string  Requester { get; set; }
        public string? ReceivedBy { get; set; }
        public DateTime? ReceivedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public int Status { get; set; } //Pending / Processing / Done / Reject
        public DateTime? UpdatedAt { get; set; }
        public ICollection<CuttingSupplyRequestItems> Items { get; set; }
        public ICollection<CuttingSupplyRequestDefects> Defects { get; set; }
        public string? Note { get; set; }
        public string? DeliveredBy {  get; set; }
        public DateTime? DeliveredAt { get;set; }

        /// <summary>DirectInspection | AfterReinspection</summary>
        public string? SourceType { get; set; }

        /// <summary>Link đến QLCL_Reinspection.Id nếu SourceType = AfterReinspection</summary>
        public int? ReinspectionId { get; set; }

        /// <summary>Link đến QLCL_QualityManagementSession.No nếu SourceType = DirectInspection</summary>
        public string? SessionNo { get; set; }
    }
}
