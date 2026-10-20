namespace EquipManagementAPI.Models.DTOs
{
    public class UnallocatedOrdersQueryDto
    {
        public string? Status { get; set; }           // null/"all" = tất cả trạng thái (vẫn loại trừ -1,0,1)
        public DateOnly? PlanDateFrom { get; set; }
        public DateOnly? PlanDateTo { get; set; }
        public string? Keyword { get; set; }           // tìm theo No_ / Note / người tạo
        public string? SortBy { get; set; } = "PlanDate"; // "PlanDate" | "No" ...
        public string? SortDirection { get; set; } = "desc";
        public int? Page { get; set; } = 1;
        public int? PageSize { get; set; } = 20;
    }
}
