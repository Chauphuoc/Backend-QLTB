namespace EquipManagementAPI.Models.DTOs
{
    public class CmSplitDto
    {
        public int Id { get; set; }
        public string OperationCode { get; set; } = string.Empty;
        public string? OperationName { get; set; }
        public decimal SplitPrice { get; set; }
        public string? DefaultLineId { get; set; }
    }
    /// <summary>Một item trong body của POST batch</summary>
    public class CmSplitUpsertItem
    {
        public string OperationCode { get; set; } = string.Empty;
        public string? OperationName { get; set; }
        public decimal SplitPrice { get; set; }
        public int? DefaultLineId { get; set; }
    }

    public class CmSplitBatchRequest
    {
        public List<CmSplitUpsertItem> Splits { get; set; } = new();
    }

    public class CmSplitBatchResponse
    {
        public List<CmSplitDto> Splits { get; set; } = new();
        public bool Validated { get; set; }
    }

    /// <summary>Response chuẩn cho lỗi validate (400)</summary>
    public class ApiErrorResponse
    {
        public string Message { get; set; } = string.Empty;
        public string? Detail { get; set; }
    }
}
