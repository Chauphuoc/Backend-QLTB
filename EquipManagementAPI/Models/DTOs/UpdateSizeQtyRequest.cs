namespace EquipManagementAPI.Models.DTOs
{
    public class UpdateSizeQtyRequest
    {
        public Guid VersionId { get; set; } = default!;
        public string StyleId { get; set; } = default!;
        public string PoId { get; set; } = default!;
        public string ColorId { get; set; } = default!;
        public List<SizeQtyItem> SizeIds { get; set; } = new();
    }
    public class SizeQtyItem
    {
        public string SizeId { get; set; } = default!;
        public int PlannedQty { get; set; }
    }
}
