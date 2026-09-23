namespace EquipManagementAPI.Models.DTOs
{
    public class FactoryOrderSizeItem
    {
        public string SizeId { get; set; } = default!;
        public int PlannedQty { get; set; }
        public DateTime? TargetDeliveryDate { get; set; }
    }
    public class MergedPoChildDetail
    {
        public string PoId { get; set; } = default!;
        public string PoName { get; set; } = default!;
        public string? LineNumber { get; set; }
        public string? LineNumberName { get; set; }         // TODO: chưa có bảng tra tên Tổ, xem ghi chú bên dưới
        public string? OperationCode { get; set; }
        public string? OperationName { get; set; }
        public DateTime? TargetDeliveryDate { get; set; }
        public int TotalPlannedQty { get; set; }
        public List<FactoryOrderSizeItem> SizeIds { get; set; } = new();
    }
    public class MergedPoDetailResponse
    {
        public string VersionId { get; set; } = default!;
        public string StyleId { get; set; } = default!;
        public string StyleName { get; set; } = default!;   // TODO: xác nhận nguồn tên Style
        public string NewPoId { get; set; } = default!;
        public string NewPoName { get; set; } = default!;   // TODO: xác nhận nguồn — xem ghi chú bên dưới
        public string ColorId { get; set; } = default!;
        public string ColorName { get; set; } = default!;   // TODO: xác nhận nguồn tên Màu
        public string FactoryId { get; set; } = default!;
        public DateTime? MergedAt { get; set; }
        public List<MergedPoChildDetail> Children { get; set; } = new();
    }
}
