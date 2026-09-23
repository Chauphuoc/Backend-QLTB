namespace EquipManagementAPI.Models.DTOs
{
    public class TransferPlanRequest
    {
        public string TargetLineNumber { get; set; } = default!;
        public int TransferQty { get; set; }
        public DateOnly EffectiveDate { get; set; }
        public string UserId { get; set; }
    }
}
