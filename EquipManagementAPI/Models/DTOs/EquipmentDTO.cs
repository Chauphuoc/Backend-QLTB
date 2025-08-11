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
                    -1 =>"Huy",
                    0 => "Không sử dụng",
                    1 => "Đang sử dụng",
                    _ => "Không xác định"
                };
            } }
    }
}
