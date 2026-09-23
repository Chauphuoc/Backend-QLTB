namespace EquipManagementAPI.Models.DTOs
{
    public class BatchUpdateDailyItem
    {
        public Guid FactoryPlanId { get; set; }
        public int Day { get; set; }
        public int Month { get; set; }
        public int Year { get; set; }
        public int PlannedQty { get; set; }
    }
}
