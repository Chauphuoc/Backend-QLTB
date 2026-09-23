using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EquipManagementAPI.Models
{
    [Table("QLCL_QualityManagementSession")]
    public class QualityManagementSession
    {
        [Key]
        [Column("RowID")]
        public int Id { get; set; }
        [Column("No_")]
        public Guid No {  get; set; }
        public int TimeSlotId { get; set; }
        [Column("Operation Type")]
        public string OperationType { get; set; }
        [Column("Work Center Code")]
        public string WorkCenterCode { get; set; }
        [Column("Work Shift Code")]
        public string WorkShiftCode { get; set; }
        public string? CustomerNo_ { get; set; }
        [Column("Sales Order No_")]
        public string SalesOrderNo { get; set; }
        [Column("Item No_")]
        public string ItemNo { get; set; }
        [Column("ColorNo_")]
        public string ColorNo { get; set; }
        public int Status { get; set; } = 1;
        public DateTime InspectionDate { get; set; }
        [Column("Plan Output Quantity")]
        public int PlanOutputQuantity { get; set; }
        [Column("Pass Output Quantity")]
        public int PassOutputQuantity { get; set; }
        [Column("Checked Quantity")]
        public int CheckedQuantity { get; set; }
        [Column("Item Name")]
        public string? ItemName { get; set; } 
        public string Inspector { get; set; }
        public string Size { get; set; }
        public string? PoName { get; set; }
        public int DefectiveProductCount { get; set; }
        public int TargetQtyPerDate { get; set; }
        public ICollection<DefectDetail> Defects { get; set; } = new List<DefectDetail>();
        [Column("plan_id")]
        public Guid? PlanId { get; set; }
        
    }
}
