namespace EquipManagementAPI.Models.DTOs
{
    /// <summary>Response item cho GET /api/mes/cm-prices</summary>
    public class CmPriceListItemDto
    {
        public string StyleId { get; set; } = string.Empty;
        public string StyleName { get; set; } = string.Empty;
        public decimal CmPrice { get; set; }
        public string Currency { get; set; } = string.Empty;
        public DateTime EffectiveDate { get; set; }
        public bool IsActive { get; set; }
        public List<CmSplitDto> Splits { get; set; } = new();
    }

    /// <summary>Body của POST /api/mes/cm-prices</summary>
    public class CmPriceCreateRequest
    {
        public string StyleId { get; set; } = string.Empty;
        public decimal CmPrice { get; set; }
        public string Currency { get; set; } = "USD";
        public string UserId { get; set; }
    }

    /// <summary>Response của POST /api/mes/cm-prices</summary>
    public class CmPriceCreateResponse
    {
        public string StyleId { get; set; } = string.Empty;
        public decimal CmPrice { get; set; }
        public string Currency { get; set; } = string.Empty;
        public DateTime EffectiveDate { get; set; }
    }

    /// <summary>Body của PUT /api/mes/cm-prices/:id</summary>
    public class CmPriceUpdateRequest
    {
        public decimal CmPrice { get; set; }
        public string Currency { get; set; } = "USD";
        public string? Note { get; set; }
    }

    /// <summary>Response của PUT /api/mes/cm-prices/:id</summary>
    public class CmPriceUpdateResponse
    {
        public int Id { get; set; }
        public string StyleId { get; set; } = string.Empty;
        public decimal CmPrice { get; set; }
        public string Currency { get; set; } = string.Empty;
        public string? Note { get; set; }
        public DateTime EffectiveDate { get; set; }
        public bool IsActive { get; set; }
    }
    public class OperationResponse
    {
        public string OperationCode { get; set; }
        public string Name { get; set; }
        public string Price { get; set; }
    }
    
}
