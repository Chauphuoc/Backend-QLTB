namespace EquipManagementAPI.Models.DTOs
{
    public class BatchUpdateDailyRequest
    {
        public List<BatchUpdateDailyItem> Updates { get; set; } = new();
    }
}
