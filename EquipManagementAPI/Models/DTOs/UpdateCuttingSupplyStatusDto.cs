namespace EquipManagementAPI.Models.DTOs
{
    public class UpdateCuttingSupplyStatusDto
    {
        public int Status { get; set; }
        public string? Note { get; set; }
        public string ActionBy { get; set; }
        public List<ActualItemDto>? ActualItems { get; set; }
    }
}
