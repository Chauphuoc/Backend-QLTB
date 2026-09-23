using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EquipManagementAPI.Models
{
    [Table("Sales Order Line Factory")]
    public class SalesOrderLineFactory
    {
        [Key]
        public int RowID { get; set; }

        [Column("Line No_")]
        public int? LineNo { get; set; }

        [Column("Document No_")]
        public string? DocumentNo { get; set; }

        [Column("SOL Line No_")]
        public int? SolLineNo { get; set; }

        [Column("Work Center Code")]
        public string? WorkCenterCode { get; set; }

        [Column("Item No_")]
        public string? ItemNo { get; set; }

        [Column("Customer No_")]
        public string? CustomerNo { get; set; }

        public int? Status { get; set; }

        public string? Description { get; set; }

        public int? Quantity { get; set; }

        [Column("Starting Plan Date")]
        public DateTime? StartingPlanDate { get; set; }

        [Column("Ending Plan Date")]
        public DateTime? EndingPlanDate { get; set; }

        [Column("Currency Code")]
        public string? CurrencyCode { get; set; }

        [Column("Currency Factor")]
        public decimal? CurrencyFactor { get; set; }

        [Column("Material Sync Date")]
        public DateTime? MaterialSyncDate { get; set; }

        [Column("Plan Final Date")]
        public DateTime? PlanFinalDate { get; set; }

        [Column("Actual Final Date")]
        public DateTime? ActualFinalDate { get; set; }

        [Column("Plan Export Date")]
        public DateTime? PlanExportDate { get; set; }

        [Column("Actual Export Date")]
        public DateTime? ActualExportDate { get; set; }

        [Column("CM Cost")]
        public decimal? CmCost { get; set; }

        [Column("CM Cost Plus")]
        public decimal? CmCostPlus { get; set; }

        [Column("PT Cost")]
        public decimal? PtCost { get; set; }

        [Column("PT Cost Plus")]
        public decimal? PtCostPlus { get; set; }

        [Column("P Cost")]
        public decimal? PCost { get; set; }

        [Column("P Cost Plus")]
        public decimal? PCostPlus { get; set; }

        [Column("T Cost")]
        public decimal? TCost { get; set; }

        [Column("T Cost Plus")]
        public decimal? TCostPlus { get; set; }

        public int? Visible { get; set; }

        [Column("Modified Date")]
        public DateTime? ModifiedDate { get; set; }

        [Column("Modified User")]
        public string? ModifiedUser { get; set; }

        [Column("Unit of Measure")]
        public string? UnitOfMeasure { get; set; }

        [Column("Qty_ per Unit of Measure")]
        public decimal? QtyPerUnitOfMeasure { get; set; }

        public int? Planned { get; set; }

        public int? GetData { get; set; }
    }
}
