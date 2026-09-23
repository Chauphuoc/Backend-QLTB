namespace EquipManagementAPI.Models.DTOs
{
    public class SplitPoSizeInputDto
    {
        public string SizeId { get; set; } = string.Empty;
        public int PlannedQty { get; set; }
    }

    

    public class SplitPoAllocationItemDto
    {
        public string PoId { get; set; }
        public decimal PlannedQty { get; set; }
    }

    public class SplitPoEntryInputDto
    {
        public string PoId { get; set; } = string.Empty;
        public string LineNumber { get; set; } = string.Empty;
        public int PlannedQty { get; set; }
        public string? ShipDate { get; set; }
        public List<SplitPoSizeInputDto> SizeBreakdown { get; set; } = new();
    }

    public class SplitPoSaveRequestDto
    {
        public string FactoryId { get; set; } = default!;
        public string StyleId { get; set; } = default!;
        public string PoId { get; set; } = default!;      // PO gốc — áp dụng cho TẤT CẢ Màu của PO này
        public string CreatedBy { get; set; } = default!;

        // Danh sách PO con — DÙNG CHUNG cho mọi Màu (mỗi PO con chỉ khai báo 1 lần, không lặp theo Màu)
        public List<SplitPoChildDto> Children { get; set; } = new();

        // Breakdown theo TỪNG Màu
        public List<SplitPoColorInputDto> Colors { get; set; } = new();
    }

    public class SplitPoChildDto
    {
        public string PoId { get; set; } = default!;     // nhãn user đặt, VD "Split-01" — KHÔNG phải po_id thật
        public string? ShipDate { get; set; }               // "yyyy-MM-dd", áp dụng chung mọi Màu
    }

    public class SplitPoColorInputDto
    {
        public string ColorId { get; set; } = default!;
        public List<SplitPoSizeAllocationDto> SizeAllocations { get; set; } = new();

        // NULL nếu Màu này CHƯA phân tổ (API detail trả allocated=false cho Màu này).
        // Bắt buộc phải có nếu Màu này ĐÃ phân tổ (allocated=true).
        public List<SplitPoTeamAllocationDto>? TeamAllocations { get; set; }
    }

    public class SplitPoSizeAllocationDto
    {
        public string SizeId { get; set; } = default!;
        public List<SplitPoSizeSplitDto> Splits { get; set; } = new(); // chia SL của size này cho từng PO con
    }

    public class SplitPoSizeSplitDto
    {
        public string ChildPoId { get; set; } = default!; // khớp với SplitPoChildDto.PoName
        public int PlannedQty { get; set; }
    }

    public class SplitPoTeamAllocationDto
    {
        public string LineNumber { get; set; } = default!; // hoặc "__UNASSIGNED__"
        public bool IsUnallocated { get; set; }
        public List<SplitPoTeamSplitDto> Allocations { get; set; } = new();
    }

    public class SplitPoTeamSplitDto
    {
        public string PoId { get; set; } = default!; // khớp với SplitPoChildDto.PoName
        public int PlannedQty { get; set; }
    }
}
