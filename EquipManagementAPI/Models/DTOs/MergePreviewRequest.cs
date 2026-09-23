namespace EquipManagementAPI.Models.DTOs
{
    
        // ===== Preview =====
        public class MergePreviewRequest
        {
            public string StyleId { get; set; }
            public string Color { get; set; }
            public List<string> PoIds { get; set; } = new();
            public string FactoryId { get; set; } = default!;
        }

        public class TeamBreakdownDto
        {
            public string? LineNumber { get; set; }   // null = "Chưa phân tổ"
            public string? LineNumberName { get; set; }
            public string? OperationCode { get; set; } // chỉ có giá trị khi Style có phân công đoạn
            public int AssignedQty { get; set; }
            public int ActualQty { get; set; }
            public int RemainingQty { get; set; }
        }

        public class CmPriceDto
        {
            public string PoId { get; set; } = default!;
            public decimal? CmPrice { get; set; }
        }

        public class OperationActualDto
        {
            public string OperationCode { get; set; } = default!;
            public string? OperationName { get; set; }
            public int ActualQty { get; set; }
            public int RemainingQty { get; set; }
        }

        public class MergePreviewResponse
        {
            public string StyleId { get; set; } = default!;
            public string ColorId { get; set; } = default!;
            public int TotalNewPlanQty { get; set; }

            // null khi IsOperation = true — vì Actual khác nhau theo từng công đoạn,
            // gộp thành 1 số duy nhất sẽ gây hiểu sai. Dùng OperationBreakdown thay thế.
            public int? TotalActual { get; set; }
            public int? TotalRemaining { get; set; }

            public bool IsOperation { get; set; }
            public List<OperationActualDto> OperationBreakdown { get; set; } = new(); // chỉ có dữ liệu khi IsOperation = true

            public decimal CmPrice { get; set; }
            public List<CmPriceDetailDto> CmPriceDetails { get; set; } = new();
            public List<TeamBreakdownDto> TeamBreakdown { get; set; } = new();
            //public List<string> MissingPoIds { get; set; } = new();
            //public List<string> PoIds { get; set; } = new();
    }

        // ===== Execute =====
        public class MergeExecuteRequest
        {
        public List<string> PoIds { get; set; } = new();  
        public string FactoryId { get; set; } = default!;
        public string StyleId { get; set; } = default!;     
        public string NewPoName { get; set; } = default!;   
        public DateTime ProductionStartDate { get; set; }   // ô "Ngày giao hàng" trong modal — xem lưu ý bên dưới

        public int BufferDays { get; set; } = 0;
        public string CreatedBy { get; set; } = default!;
        public string UserId { get; set; } = default!;
        public decimal? FallbackCmPrice { get; set; }       
                                                             //public string StyleId { get; set; }
                                                             //public string Color { get; set; }
                                                             //public List<string> PoIds { get; set; } = new();
                                                             //public string FactoryId { get; set; } = default!;
                                                             //public string NewPoName { get; set; } = default!;
                                                             //public decimal CmPrice { get; set; }
                                                             //public DateTime ProductionStartDate { get; set; }
                                                             //public int BufferDays { get; set; } = 0;
                                                             //public string? OperationCode { get; set; }
                                                             //public string CreatedBy { get; set; } = default!;
                                                             //public decimal? FallbackCmPrice { get; set; } // dùng khi style chưa có StyleCmPrice
                                                             //public string? UserId { get; set; }

    }

        public class CreatedPlanDto
        {
             public Guid Id { get; set; }
             public string PlanCode { get; set; } = default!;
             public string PoId { get; set; } = default!;
             public string? LineNumber { get; set; }
             public string? OperationCode { get; set; }
             public int TotalPlannedQty { get; set; }
             public string Status { get; set; } = default!;
             public decimal? CmPrice { get; set; }
             public string ColorId { get; set; } = default!;
    }

    public class MergeExecuteResponse
        {
            public bool Success { get; set; } = true;
            public string NewPoId { get; set; } = default!;
            public List<CreatedPlanDto> CreatedPlans { get; set; } = new();
        }

        // ===== Get children =====
        public class PlanHeaderDto
        {
            public Guid Id { get; set; }
            public string PlanCode { get; set; } = default!;
            public string PoId { get; set; } = default!;
            public string StyleId { get; set; } = default!;
            public string ColorId { get; set; } = default!;
            public string? LineNumber { get; set; }
            public int TotalPlannedQty { get; set; }
            public string Status { get; set; } = default!;
            public decimal? CmPrice { get; set; }
            public bool IsGroup { get; set; }
            public DateTime? ProductionStartDate { get; set; }
            public DateTime? ProductionEndDate { get; set; }
        }

        public class PlanChildDto
        {
            public Guid Id { get; set; }
            public string PlanCode { get; set; } = default!;
            public string PoId { get; set; } = default!;
            public string? LineNumber { get; set; }
            public int AssignedQty { get; set; }
            public int ActualQty { get; set; }
            public int RemainingQtyAtMerge { get; set; }
            public decimal? OriginalCmPrice { get; set; }
            public DateTime? ProductionStartDate { get; set; }
            public DateTime? ProductionEndDate { get; set; }
            public DateTime? MergedAt { get; set; }
            public string? MergedBy { get; set; }
        }

        public class DailyHistoryDto
        {
            public Guid FactoryPlanId { get; set; }
            public string PoId { get; set; } = default!;
            public string? LineNumber { get; set; }
            public string SizeId { get; set; } = default!;
            public DateTime WorkDate { get; set; }
            public int PlannedQty { get; set; }
            public int ActualPassQty { get; set; }
        }

        public class PlanChildrenResponse
        {
            public PlanHeaderDto Header { get; set; } = default!;
            public List<PlanChildDto> Children { get; set; } = new();
            public List<DailyHistoryDto> DailyHistory { get; set; } = new();
        }
        public class CmPriceDetailDto
        {
            public string Name { get; set; }
            public string OperationCode { get; set; }
            public decimal? SplitPrice { get; set; }
        }
    
}
