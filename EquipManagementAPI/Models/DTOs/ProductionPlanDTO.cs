namespace EquipManagementAPI.Models.DTOs
{
    public class ProductionPlanDTO
    {
        public string? WorkCenterName { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string ItemNo { get; set; }
        public string? ItemName { get; set; }
        public string? SalesOrderNo { get; set; }
        public string? POName { get; set; }
        public string? WorkShiftCode { get; set; }
        public string? WorkShiftName { get; set; }
        public string? ColorNo { get; set; }
        public string? SizeNo { get; set; }
        public int? PlanQty { get; set; }
        public int? RemainQty { get; set; }
        public int? TargetPerDay { get; set; }
        public Guid PlanID { get; set; }
    }
}
