namespace EquipManagementAPI.Models.DTOs
{
    public class FactoryPlanLineRequest
    {
        public string SizeId { get; set; } = string.Empty;
        public int PlannedQty { get; set; }
    }
}
