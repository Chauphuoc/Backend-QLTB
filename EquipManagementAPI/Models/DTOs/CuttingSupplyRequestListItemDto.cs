namespace EquipManagementAPI.Models.DTOs
{
    public class CuttingSupplyRequestListItemDto
    {
        public int Id { get; set; }

        // Thông tin tổ / nhà máy
        public string WorkCenterCode { get; set; }
        public string WorkShiftCode { get; set; }
        //public string? SessionNo { get; set; }

        // Thông tin đơn hàng
        public string ItemNo { get; set; }
        public string Style { get; set; }
        public string SalesOrderNo { get; set; }
        public string PoName { get; set; }
        public string Color { get; set; }
        public string? Size { get; set; }

        // Thông tin yêu cầu
        public int UnreparableCount { get; set; }
        public string Requester { get; set; }
        //public string? RequestNote { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // Trạng thái & xử lý (0=Pending, 1=Processing, 2=Done, 3=Rejected)
        public int Status { get; set; }
        public string? ReceivedBy { get; set; }
        public DateTime? ReceivedAt { get; set; }
        public string? DeliveredBy { get; set; }
        public DateTime? DeliveredAt { get; set; }
        //public string? Note { get; set; }

        // Chi tiết BTP & lỗi
        public List<CuttingSupplyRequestItemDto> Items { get; set; } = new();
        public List<CuttingSupplyRequestDefectDto> DefectTypes { get; set; } = new();

    }
}
