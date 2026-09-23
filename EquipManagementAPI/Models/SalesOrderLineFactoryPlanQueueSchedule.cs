using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EquipManagementAPI.Models
{
    [Table("Sales Order Line Factory Plan Queue Schedule")]
    public class SalesOrderLineFactoryPlanQueueSchedule
    {
        
        public int RowID { get; set; }
        [Key]
        public int QueueID { get; set; }
        [Column("Sales Order No_")]
        public string SalesOrderNo { get; set; }
        [Column("Item No_")]
        public string ItemNo { get; set; }
        [Column("Work Center Code")]
        public string WorkCenterCode { get; set; }
        [Column("Work Shop Code")]
        public string WorkShopCode { get; set; }
        [Column("Work Shift Code")]
        public string WorkShiftCode { get; set; }
        [Column("Routing No_")]
        public string RoutingNo { get; set; }
        [Column("Posting Date")]
        public DateTime PostingDate { get; set; }
        public int Status { get; set; }
        public string Description { get; set; }
        [Column("Unit Cost")]
        public decimal UnitCost { get; set; }
        [Column("Begin Time")]
        public DateTime BeginTime { get; set; }
        [Column("End Time")]
        public DateTime EndTime { get; set; }
        [Column("Input Quantity")]
        public int InputQuantity { get; set; }
        [Column("Plan Output Quantity")]
        public int PlanOutputQuantity { get; set; }
        [Column("Output Quantity")]
        public int OutputQuantity { get; set; }
        [Column("Pass Output Quantity")]
        public int PassOutputQuantity { get; set; }
        [Column("Error Output Quantity")]
        public int ErrorOutputQuantity { get; set; }
    }
}
