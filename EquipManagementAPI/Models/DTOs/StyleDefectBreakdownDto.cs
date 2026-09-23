namespace EquipManagementAPI.Models.DTOs
{
    public class StyleDefectBreakdownDto
    { /// <summary>Mã loại lỗi (DefectCode)</summary>
        public string DefectCode { get; set; } = string.Empty;

        /// <summary>Tên loại lỗi (DefectName)</summary>
        public string DefectName { get; set; } = string.Empty;

        /// <summary>Số lượng lỗi của loại này</summary>
        public int Count { get; set; }

        /// <summary>% trên tổng lỗi của style (làm tròn 2 chữ số thập phân)</summary>
        public double Percentage { get; set; }

        /// <summary>Mức độ nghiêm trọng: "high" (>=30%), "medium" (>=10%), "low" (<10%)</summary>
        public string Severity { get; set; } = string.Empty;
    }
}
