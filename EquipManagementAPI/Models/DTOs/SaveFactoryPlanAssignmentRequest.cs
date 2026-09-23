namespace EquipManagementAPI.Models.DTOs
{
    public class SaveFactoryPlanAssignmentRequest
    {
        public string FactoryId { get; set; } = null!;
        public int Year { get; set; }
        public int Month { get; set; }
        public List<PlanRowDto> Rows { get; set; } = new();
    }
    public class PlanRowDto
    {
        public Guid Id { get; set; }
        public string StyleId { get; set; } = null!;
        public string PoId { get; set; } = null!;
        public string ColorId { get; set; } = null!;
        public string LineNumber { get; set; } = null!;      // Tổ được gán
        public int TotalPlannedQty { get; set; }
        public string? SizeId { get; set; }                   // TODO xác nhận: null hay giá trị mặc định?
        public List<DailyQtyDto> DailyQuantities { get; set; } = new(); // 27 ngày công
    }

    public class DailyQtyDto
    {
        public DateOnly Date { get; set; }
        public int PlannedQty { get; set; }
    }
}
