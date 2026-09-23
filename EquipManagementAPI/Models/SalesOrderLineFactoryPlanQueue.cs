using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EquipManagementAPI.Models
{
    [Table("Sales Order Line Factory Plan Queue")]
    public class SalesOrderLineFactoryPlanQueue
    {
        [Key]
        public int QueueID { get; set; }
        [Column("SOLFP Line No_")]
        public int LineNo { get; set; }
        [Column("Sales Order No_")]
        public string SalesOrderNo { get; set; }
        [Column("Item No_")]
        public string ItemNo { get; set; }
        [Column("Work Center Code")]
        public string WorkCenterCode { get; set; }
        [Column("Work Shop Code")]
        public string WorkShopCode { get; set;}
        [Column("Work Shift Code")]
        public string WorkShiftCode { get; set;}
        [Column("MF Order")]
        public int MfOrder {  get; set; }   
        public string Description { get; set; } 
        public int Status { get; set; }
        [Column("Target Quantity per Day")]
        public int TargetPerDay { get; set; }
        [Column("Plan Quantity")]
        public int PlanQuantity { get; set; }
        [Column("Real Quantity")]
        public int RealQuantity { get; set; }
        [Column("Actual Starting Date")]
        public DateTime ActualStartingDate { get; set; }
        [Column("Actual Ending Date")]
        public DateTime ActualEndingDate { get; set; }
        [Column("Actived Date")]
        public DateTime ActivedDate { get; set; }
        [Column("Actived User")]
        public string ActivedUser { get; set; }
        [Column("Document Date")]
        public DateTime DocumentDate { get; set; }
        [Column("Item Name")]
        public string ItemName { get; set; }
        [Column("PO Name")]
        public string POName { get; set; }
        [Column("Work Center Name")]
        public string WorkCenterName { get; set; }
        [Column("Work Shop Name")]
        public string WorkShopName { get; set; }
        [Column("Work Shift Name")]
        public string WorkShiftName { get; set; }
        [Column("Unit Cost")]
        public decimal UnitCost { get; set; }
        [Column("Color No_")]
        public string ColorNo { get; set; }
        [Column("Size No_")]
        public string SizeNo { get; set; }
        [Column("Customer No_")]
        public string CustomerNo { get; set; }
        [Column("Customer Name")]
        public string CustomerName { get; set; }
        [Column("Routing No_")]
        public string RoutingNo { get; set; }
    }
}
