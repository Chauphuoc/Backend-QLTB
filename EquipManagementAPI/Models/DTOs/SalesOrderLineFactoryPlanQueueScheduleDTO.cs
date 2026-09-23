namespace EquipManagementAPI.Models.DTOs
{
    public class SalesOrderLineFactoryPlanQueueScheduleDTO
    {
        public int RowID { get; set; }
        public int QueueID { get; set; }

        public string? SalesOrderNo { get; set; }
        public string? ItemNo { get; set; }
        public string? WorkCenterCode { get; set; }
        public string? WorkShopCode { get; set; }
        public string? WorkShiftCode { get; set; }
        //public string? RoutingNo { get; set; }

        public DateTime PostingDate { get; set; }
        //public int Status { get; set; }
        //public string? Description { get; set; }

        //public decimal UnitCost { get; set; }

        //public DateTime BeginTime { get; set; }
        //public DateTime EndTime { get; set; }

        public int InputQuantity { get; set; }
        public int PlanOutputQuantity { get; set; }
        public int OutputQuantity { get; set; }
        public int PassOutputQuantity { get; set; }
        public int ErrorOutputQuantity { get; set; }
    }
}
