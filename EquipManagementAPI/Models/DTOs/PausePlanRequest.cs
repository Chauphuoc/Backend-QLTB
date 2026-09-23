namespace EquipManagementAPI.Models.DTOs
{
    public class PausePlanRequest
    {
        /// <summary>
        /// Ngày hiệu lực tạm dừng — các ô planned_qty từ ngày này trở đi (work_date >= EffectiveDate)
        /// trong factory_plan_lines sẽ được set về 0.
        /// </summary>
        public DateTime FromDate { get; set; }

        /// <summary>
        /// Lý do tạm dừng (optional) — VD "Thiếu nguyên liệu", "Ưu tiên đơn khác".
        /// Lưu vào factory_plan_status_history.Note để tra cứu lịch sử sau này.
        /// </summary>
        public string? Reason { get; set; }
        public string? UserId { get; set; }
    }
    public class ActivatePlanRequest
    {
        /// <summary>
        /// Ngày hiệu lực bắt đầu/tiếp tục sản xuất.
        /// Dùng để set production_start_date (nếu đang null) và làm mốc effectiveDate
        /// khi tự động pause plan xung đột trên cùng tổ.
        /// </summary>
        public DateTime FromDate { get; set; }

        /// <summary>
        /// FE set = true sau khi user đã xác nhận popup cảnh báo 
        /// "Kế hoạch [X] đang chạy sẽ bị tạm dừng, tiếp tục?".
        /// Nếu false và có conflict, BE trả lỗi 409 NEED_CONFIRM_OVERRIDE để FE mở popup.
        /// </summary>
        public bool ConfirmedOverride { get; set; }
        public string? UserId { get; set; }
    }

    public class UpdateDailyBatchRequest
    {
        public List<DailyPlanItem> Items { get; set; } = new();
    }

    public class DailyPlanItem
    {
        /// <summary>Ngày kế hoạch, khớp factory_plan_lines.work_date</summary>
        public DateTime WorkDate { get; set; }

        /// <summary>Số lượng kế hoạch của ngày đó (>= 0)</summary>
        public int PlannedQty { get; set; }
    }
}
