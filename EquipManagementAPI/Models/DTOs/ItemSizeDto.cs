namespace EquipManagementAPI.Models.DTOs
{
    /// <summary>Response item cho GET /api/production/item-sizes</summary>
    public class ItemSizeDto
    {
        public string SizeId { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int OrdinalNumber { get; set; }
    }
}
