namespace EquipManagementAPI.Models.DTOs
{
    /// <summary>
    /// Kết quả tổng hợp thoát chuyền lũy kế so với kế hoạch
    /// GET /api/QualityManagement/output-summary
    /// </summary>
    public class OutputSummaryResultDto
    {
        public string Factory { get; set; } = string.Empty;
        public string ProductionTeam { get; set; } = string.Empty;
        public string Style { get; set; } = string.Empty;
        public string Po { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;

        /// <summary>Kế hoạch tổng (toàn bộ đơn hàng, đơn vị: pcs)</summary>
        public int PlanQty { get; set; }

        /// <summary>
        /// Kế hoạch lũy kế đến thời điểm hiện tại trong ca.
        /// Công thức: PlanQty × (phút_đã_trôi_qua / tổng_phút_ca)
        /// </summary>
        public int CumulativePlan { get; set; }

        /// <summary>Tổng sản phẩm thoát chuyền lũy kế trong ngày (pcs)</summary>
        public int TotalPassOutput { get; set; }

        /// <summary>Số lần kiểm tra trong ngày khớp với bộ lọc</summary>
        public int CheckCount { get; set; }

        /// <summary>Ngày kiểm tra (yyyy-MM-dd)</summary>
        public string CheckDate { get; set; } = string.Empty;
    }
}
