namespace EquipManagementAPI.Models.DTOs
{
    public class StyleTeamDefectDto
    {
        /// <summary>Mã chuyền / tổ (WorkShiftCode)</summary>
        public string TeamId { get; set; } = string.Empty;

        /// <summary>Tên chuyền / tổ (WorkShiftName)</summary>
        public string TeamName { get; set; } = string.Empty;

        /// <summary>Tổng số sản phẩm đã kiểm</summary>
        public int TotalInspected { get; set; }

        /// <summary>Tổng số sản phẩm lỗi</summary>
        public int TotalDefects { get; set; }

        /// <summary>Tỷ lệ lỗi % = TotalDefects / TotalInspected × 100 (làm tròn 2 chữ số)</summary>
        public double DefectRate { get; set; }
    }
}
