namespace EquipManagementAPI.Models.DTOs
{
    public class SalesOrderLineFactoryPlanQueueDTO
    {

        public int QueueID { get; set; }
        //public int LineNo { get; set; }
        public string? SalesOrderNo { get; set; }
        public string? ItemNo { get; set; }
        public string? WorkCenterCode { get; set; }
        //public string? WorkShopCode { get; set; }
        public string? WorkShiftCode { get; set; }
        //public int MfOrder { get; set; }
        //public string? Description { get; set; }
        public int Status { get; set; }

        public int TargetPerDay { get; set; }
        public int PlanQuantity { get; set; }
        public int RealQuantity { get; set; }

        public DateTime ActualStartingDate { get; set; }
        public DateTime ActualEndingDate { get; set; }
        public DateTime ActivedDate { get; set; }
        //public string? ActivedUser { get; set; }
        //public DateTime DocumentDate { get; set; }

        public string? ItemName { get; set; }
        public string? POName { get; set; }
        public string? WorkCenterName { get; set; }
        //public string? WorkShopName { get; set; }
        public string? WorkShiftName { get; set; }

        //public decimal UnitCost { get; set; }

        public string? ColorNo { get; set; }
        public string? SizeNo { get; set; }

        //public string? CustomerNo { get; set; }
        public string? CustomerName { get; set; }
        //public string? RoutingNo { get; set; }
    }
}
