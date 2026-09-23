using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EquipManagementAPI.Models
{
    [Table("QLCL_ProductionPlan")]
    public class ProductionPlan
    {
        [Key]
        public int RowID { get; set; }

        [Column("Document No_")]
        public string? DocumentNo { get; set; }

        [Column("Work Center Code")]
        public string? WorkCenterCode { get; set; }

        [Column("Item No_")]
        public string? ItemNo { get; set; }

        [Column("Customer No_")]
        public string? CustomerNo { get; set; }

        public int? Status { get; set; }

        [Column("Color No_")]
        public string? ColorNo { get; set; }

        [Column("Size No_")]
        public string? SizeNo { get; set; }

        [Column("Work Shift Code")]
        public string? WorkShiftCode { get; set; }

        [Column("Plan Quantity")]
        public int? PlanQuantity { get; set; }

        [Column("Target Quantity Per Day")]
        public int? TargetQuantityPerDay { get; set; }

        [Column("Plan Capacity")]
        public int? PlanCapacity { get; set; }

        [Column("Unit Of Measure")]
        public string? UnitOfMeasure { get; set; }

        [Column("Actual Starting Date",TypeName ="date")]
        public DateTime? ActualStartingDate { get; set; }

        [Column("Real Quantity")]
        public int? RealQuantity { get; set; }

        [Column("User ID")]
        public string? UserID { get; set; }

        [Column("Document Date")]
        public DateTime? DocumentDate { get; set; }
        [Column("Sales Order No_")]
        public string? SalesOrderNo { get; set;}

        [Column("PO Name")]
        public string? POName { get; set; }

        [Column("Item Name")]
        public string? ItemName { get; set; }

        [Column("Work Center Name")]
        public string? WorkCenterName { get; set; }

        [Column("Work Shift Name")]
        public string? WorkShiftName { get; set; }

        [Column("Parent No_")]
        public string? ParentNo { get; set; }
        public string ErpPlanID { get; set; }
    }
}