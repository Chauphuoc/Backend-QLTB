using EquipManagementAPI.Helpers;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace EquipManagementAPI.Models.DTOs
{
    public class RequestCheckQlclDTO
    {
        [Required(ErrorMessage = "Chưa chọn loại kiểm")]
        public string checkType { get; set; }
        [Required(ErrorMessage = "Chưa chọn màu")]
        public string color { get; set; }
        public int defectiveProductCount { get; set; }
        // Danh sách các chi tiết lỗi
        public List<RequestDefectsDTO> defects { get; set; } = new List<RequestDefectsDTO>();
        [Required(ErrorMessage = "Chưa chọn nhà máy")]
        public string factory { get; set; }
        public string inspector { get; set; }
        [Required(ErrorMessage = "Chưa chọn PO")]
        public string po { get; set; }
        [Required(ErrorMessage = "Chưa chọn tổ")]
        public string productionTeam { get; set; }
        public int quantity { get; set; }
        public string size { get; set; }
        [Required(ErrorMessage = "Chưa chọn style")]
        public string style { get; set; }
        [JsonConverter(typeof(StringOrNumberConverter))]
        public string? erpPlanID { get; set; }
        [Required(ErrorMessage = "Chưa chọn đơn vị")]
        public string unit { get; set; }
        [Range(1, int.MaxValue, ErrorMessage = "Số cái / đơn vị phải > 0")]
        public int unitQuantity { get; set; }
        [Range(1, int.MaxValue, ErrorMessage = "Số lượng kiểm phải > 0")]
        public int passOutputQty { get; set; }
        public int planOutputQuantity { get; set; }
        public bool isReinspection { get; set; }

        public int reinspectionQuantity { get; set; }

        public int reinspectionDefectiveCount { get; set; }

        public Guid planId { get; set; }

        // Xác nhận của người dùng khi số lượng nhập vượt kế hoạch trong ngày (true = vẫn muốn lưu)
        public bool confirmOverPlan { get; set; } = false;

        // Xác nhận khi tổng số lượng đã thoát chuyền vượt total_planned_qty của đơn hàng (MesFactoryPlans)
        public bool confirmOverOrder { get; set; } = false;
    }
}
