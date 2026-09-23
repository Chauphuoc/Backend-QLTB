namespace EquipManagementAPI.Models.DTOs
{
    public class SplitActualBatchEntryItemDto
    {
        public DateTime WorkDate { get; set; }
        public int Quantity { get; set; }
    }

    public class SplitActualBatchRequestDto
    {
        public Guid ChildPlanId { get; set; }
        public List<SplitActualBatchEntryItemDto> Entries { get; set; } = new(); // toàn bộ danh sách hiện tại trong modal
        public string CreatedBy { get; set; } = default!;
    }
}
