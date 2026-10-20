namespace EquipManagementAPI.Models.DTOs
{
    public class UnallocatedOrderLineRefDto
    {
        public string DocumentNo { get; set; } = string.Empty;
        public int LineNo { get; set; }
    }

    public class UnallocatedOrderSizesRequestDto
    {
        public string FactoryId { get; set; } = string.Empty;
        public List<UnallocatedOrderLineRefDto> Lines { get; set; } = new();
    }

    public class UnallocatedOrderSizeQtyDto
    {
        public string SizeNo { get; set; } = string.Empty;
        public int ErpQty { get; set; }
        public int AllocatedQty { get; set; }
        public int RemainingQty { get; set; }
    }

    public class UnallocatedOrderLineSizesDto
    {
        public string DocumentNo { get; set; } = string.Empty;
        public int LineNo { get; set; }
        public string ItemNo { get; set; } = string.Empty;
        public string ColorNo { get; set; } = string.Empty;
        public string? PoName { get; set; }
        public DateOnly? RequestedDeliveryDate { get; set; }
        public bool IsAvailable { get; set; }
        public string? UnavailableReason { get; set; }
        public bool FactoryConflict { get; set; }
        public List<UnallocatedOrderSizeQtyDto> Sizes { get; set; } = new();
    }

    public class UnallocatedOrderSizeInputDto
    {
        public string SizeNo { get; set; } = string.Empty;
        public int Quantity { get; set; }
    }

    public class UnallocatedOrderAllocateLineDto
    {
        public string DocumentNo { get; set; } = string.Empty;
        public int LineNo { get; set; }
        public List<UnallocatedOrderSizeInputDto> Sizes { get; set; } = new();
    }

    public class UnallocatedOrderAllocateRequestDto
    {
        public string FactoryId { get; set; } = string.Empty;
        public string? CreatedBy { get; set; }
        public List<UnallocatedOrderAllocateLineDto> Lines { get; set; } = new();
    }
}
