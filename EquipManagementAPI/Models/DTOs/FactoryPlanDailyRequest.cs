namespace EquipManagementAPI.Models.DTOs
{
    public class FactoryPlanDailyRequest
    {
        public DateOnly WorkDate { get; set; }
        public int PlannedQty { get; set; }
        public string? Note { get; set; }
    }
}
