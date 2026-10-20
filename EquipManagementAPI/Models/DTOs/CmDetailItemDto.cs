namespace EquipManagementAPI.Models.DTOs
{
    // Doanh thu CM (USD) theo Tổ + theo ngày = Doanh thu (USD) / (Tổng công + Tổng giờ tăng ca / 9)
    public class CmDayDetailDto
    {
        public decimal RevenueUsd { get; set; }
        public decimal Labor { get; set; }        // Tổng công + Tổng giờ tăng ca / 9
        public decimal Cm { get; set; }           // RevenueUsd / Labor (0 nếu Labor = 0)
    }

    public class CmDetailItemDto
    {
        public string Id { get; set; } = null!;
        public string ToMay { get; set; } = null!;
        public Dictionary<string, CmDayDetailDto>? Daily { get; set; }
    }
}
