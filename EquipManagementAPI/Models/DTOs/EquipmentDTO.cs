using System.ComponentModel.DataAnnotations;

namespace EquipManagementAPI.Models
{
    public class EquipmentDTO
    {

        [Required]
        public string EquipmentCode  {  get; set; }
        
        public string? ManageUnit { get; set; }
        public string? EquipmentGroupCode { get; set; }
        public string? Model { get; set; }
        public string SerialNumber { get; set; }
        public string? Brand { get; set; }
        public string? QRCode { get; set; }
        public string? Image {  get; set; }
        public string? LocationCode {  get; set; }
        public string? LocationName { get; set; }
        public int? Status { get; set;}
        
        public int? StatusGroup {  get; set; }
        public string? TrangThai {  get
            {
                return StatusGroup switch
                {
                    -1 => "Không sử dụng",
                    0 => "Chờ sử dụng",
                    1 => "Đang sử dụng",
                    _ => "Không xác định"
                };
            } }
        public string? LastMaintenanceTime { get; set; }
        public string? PlanTime { get; set; }
        public string? User { get; set;}
        public string? MaintenanceType { get; set; }
        public int? Check {  get; set; } 
        public string? NamSX { get; set; }
        public string? NamSD { get; set; }
        public int? SoNamSD { get; set; }

        public string MaintenanceCheck
        {
            get
            {
                return Check switch
                {
                    -1=>"Không có dữ liệu",
                    0 => "Chờ bảo dưỡng",
                    1 => "Đã bảo dưỡng",
                    2 => "Quá hạn bảo dưỡng",
                    3 => "Chuyển qua sửa chữa"
                };
            }
        }
    }
}
