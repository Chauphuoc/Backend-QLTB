namespace EquipManagementAPI.Models.DTOs
{
    // 1 dòng của báo cáo "Tổng hợp" doanh thu — tổng theo Tổ/Khách hàng/PO/Style cho cả kỳ báo cáo (không tách theo ngày)
    public class RevenueSummaryItemDto
    {
        public string Id { get; set; } = null!;       // Mã Tổ (Work Shift Code)
        public string ToMay { get; set; } = null!;
        public string? Customer { get; set; }        // Khách hàng — CustomerNo_
        public string? SalesOrderNo { get; set; }     // Mã đơn hàng
        public string? PoName { get; set; }           // PO Name
        public string? ItemNo { get; set; }           // Mã sản phẩm
        public string? Style { get; set; }            // Style — Item Name
        public decimal UnitPrice { get; set; }        // Đơn giá (USD)
        public string? OperationCode { get; set; }
        public int Quantity { get; set; }             // Số lượng hoàn thành
        public decimal RevenueUsd { get; set; }        // Doanh thu (USD)
        public decimal RevenueVnd { get; set; }        // Doanh thu (VNĐ)
    }
}
