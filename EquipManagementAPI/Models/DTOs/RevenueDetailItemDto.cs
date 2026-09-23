namespace EquipManagementAPI.Models.DTOs
{
    public class RevenueDetailItemDto
    {
        public string Id { get; set; } = null!;
        public string ToMay { get; set; } = null!;
        public string? Customer { get; set; }
        public string? Style { get; set; }
        public decimal? UnitPrice { get; set; }
        // Công đoạn đã dùng để tra split_price khi Style có phân công đoạn (is_operation = true)
        public string? OperationCode { get; set; }
        public Dictionary<string, decimal>? Daily { get; set; }
    }
}
