namespace EquipManagementAPI.Models.DTOs
{
    public class ActualTransferEntryDto
    {
        public DateTime WorkDate { get; set; }
        public int MoveQty { get; set; }
    }

    public class ActualTransferRequestDto
    {
        public Guid SourcePlanId { get; set; }
        public string TargetPoId { get; set; } = default!;   // MÃ PO đích (string) — khớp đúng những gì dropdown trả về
        public List<ActualTransferEntryDto> Entries { get; set; } = new();
        public string CreatedBy { get; set; } = default!;
    }
}
