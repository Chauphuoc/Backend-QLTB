namespace EquipManagementAPI.Models.DTOs
{
    public class GetTeamAllocationResult
    {
        public string Style { get; set; } = string.Empty;

        public string Po { get; set; } = string.Empty;

        public string Color { get; set; } = string.Empty;

        /// <summary>
        /// Tổng sản lượng của kế hoạch gốc
        /// </summary>
        public decimal TotalDemandQty { get; set; }

        /// <summary>
        /// Tổng sản lượng đã phân bổ cho các tổ
        /// </summary>
        public decimal AllocatedQty { get; set; }

        /// <summary>
        /// Sản lượng còn lại chưa phân bổ
        /// </summary>
        public decimal RemainingQty { get; set; }

        /// <summary>
        /// Danh sách các tổ đã được phân bổ
        /// </summary>
        public List<TeamAllocationItem> Allocations { get; set; } = new();
    }
}
