using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EquipManagementAPI.Models
{
    [Table("Sales Order Line Factory Plan")]
    public class SalesOrderLineFactoryPlan
    {
        [Key]
        public int RowID { get; set; }

        [Column("Line No_")]
        public int? LineNo { get; set; }

        [Column("Sales Order No_")]
        public string? SalesOrderNo { get; set; }

        [Column("Item No_")]
        public string? ItemNo { get; set; }

        [Column("Work Center Code")]
        public string? WorkCenterCode { get; set; }

        [Column("Work Shop Code")]
        public string? WorkShopCode { get; set; }

        [Column("Work Shift Code")]
        public string? WorkShiftCode { get; set; }

        [Column("Color No_")]
        public string? ColorNo { get; set; }

        [Column("Size No_")]
        public string? SizeNo { get; set; }

        public string? Description { get; set; }

        public int? Status { get; set; }

        [Column("Plan Quantity")]
        public int? PlanQuantity { get; set; }

        [Column("Actual Quantity")]
        public int? ActualQuantity { get; set; }

        [Column("Plan Capacity Qty")]
        public int? PlanCapacityQty { get; set; }

        [Column("Starting Date")]
        public DateTime? StartingDate { get; set; }

        [Column("Ending Date")]
        public DateTime? EndingDate { get; set; }

        [Column("Shipping Date")]
        public DateTime? ShippingDate { get; set; }

        [Column("Currency Code")]
        public string? CurrencyCode { get; set; }

        [Column("Currency Factor")]
        public decimal? CurrencyFactor { get; set; }

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

        [Column("Document Date")]
        public DateTime? DocumentDate { get; set; }

        [Column("MF Order")]
        public int? MfOrder { get; set; }

        [Column("Customer No_")]
        public string? CustomerNo { get; set; }

        [Column("Customer Name")]
        public string? CustomerName { get; set; }

        [Column("PO Name")]
        public string? PoName { get; set; }

        [Column("Finish Name")]
        public string? FinishName { get; set; }

        [Column("Plan SF ID")]
        public int? PlanSfId { get; set; }
    }
}
