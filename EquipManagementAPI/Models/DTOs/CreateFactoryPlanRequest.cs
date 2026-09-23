namespace EquipManagementAPI.Models.DTOs
{
    public class CreateFactoryPlanRequest
    {
        public Guid ProductionOrderId { get; set; }
        public Guid VersionId { get; set; }
        public string FactoryId { get; set; } = string.Empty;
        public string StyleId { get; set; } = string.Empty;
        public string PoId { get; set; } = string.Empty;
        public string ColorId { get; set; } = string.Empty;
        public string LineNumber { get; set; } = string.Empty;
        public DateOnly ProductionStartDate { get; set; }
        public int BufferDays { get; set; }
        public DateOnly? BtpRequiredDate { get; set; }
        public int TotalPlannedQty { get; set; }
        public string? CreatedBy { get; set; }
        public List<FactoryPlanLineRequest>? Lines { get; set; }
        public List<FactoryPlanDailyRequest>? Daily { get; set; }
    }
}
