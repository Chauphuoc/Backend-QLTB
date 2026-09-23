namespace EquipManagementAPI.Models.DTOs
{
    /// <summary>
    /// Body của POST /api/production/team-allocations
    /// Xác nhận phân bổ tổ sản xuất cho một dòng Kế hoạch (Style/PO/Màu).
    /// </summary>
    public class SaveTeamAllocationRequest
    {
     
        public string FactoryId { get; set; } = null!;

        public string Style { get; set; } = null!;

        public string Po { get; set; } = null!;

        public string Color { get; set; } = null!;
        public List<TeamAllocationItem> Allocations { get; set; } = new();
    }

    /// <summary>Một dòng phân bổ: tổ sản xuất + số lượng KH tổ</summary>
    public class TeamAllocationItem
    {
        public string? Id { get; set; }
        /// <summary>Mã tổ sản xuất (line_number trong factory_plans)</summary>
        public string ToId { get; set; } = null!;

        /// <summary>Số lượng KH gán cho tổ này</summary>
        public int Qty { get; set; }
        public string? OperationCode { get; set; } = null!; // thêm mới
    }

    /// <summary>Response của POST /api/production/team-allocations</summary>
    public class SaveTeamAllocationResult
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public List<TeamAllocationPlanRow> Data { get; set; } = new();
    }

    /// <summary>Một dòng factory_plan trong response (tương tự FactoryPlanViewRow)</summary>
    public class TeamAllocationPlanRow
    {
        public Guid Id { get; set; }
        public string PlanCode { get; set; } = string.Empty;
        public string FactoryId { get; set; } = string.Empty;
        public string? LineNumber { get; set; }
        public string StyleId { get; set; } = string.Empty;
        public string StyleName { get; set; } = string.Empty;
        public string PoId { get; set; } = string.Empty;
        public string PoName { get; set; } = string.Empty;
        public string ColorId { get; set; } = string.Empty;
        public string ColorName { get; set; } = string.Empty;
        public DateOnly? ProductionStartDate { get; set; }
        public DateOnly? ProductionEndDate { get; set; }
        public DateOnly? TargetDeliveryDate { get; set; }
        public int TotalPlannedQty { get; set; }
        /// <summary>Phân bổ theo ngày { "yyyy-MM-dd": qty } (rỗng nếu chưa nhập)</summary>
        public Dictionary<string, int> TeamTotalQty { get; set; } = new();
        public int MonthlyPlannedQty { get; set; }
        /// <summary>Đơn giá CM gốc và các splits công đoạn</summary>
        public TeamAllocationCmPrice? CmPrice { get; set; }
    }

    public class TeamAllocationCmPrice
    {
        public string StyleId { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string Currency { get; set; } = string.Empty;
        public DateTime EffectiveDate { get; set; }
        public List<TeamAllocationCmSplit> Splits { get; set; } = new();
    }

    public class TeamAllocationCmSplit
    {
        public int Id { get; set; }
        public string OperationCode { get; set; } = string.Empty;
        public string OperationName { get; set; } = string.Empty;
        public decimal SplitRatio { get; set; }
        public decimal SplitPrice { get; set; }
        public string? DefaultLineId { get; set; }
    }
}
