using Microsoft.AspNetCore.Http;

namespace EquipManagementAPI.Models.DTOs
{
    public class InforRequestSC
    {
        public string QRCode { get; set; }
        public string EquipmentName { get; set; }
        public string Serial {  get; set; }
        public string Brand { get; set; }
        public string Model { get; set; }
        public string WorkShift { get; set; }
        public int Status {  get; set; }
        public string? StatusName
        { get
            {
                return Status switch
                {
                    0 => "Đã tạo yêu cầu",
                    1 => "Bắt đầu sửa",
                    2 => "Chờ BQLC xác nhận",
                    3 =>  "Sửa lại",
                    4 => "BQLC xác nhận",
                    5 => "Hoàn thành",
                    6 => "Không sửa được",
                    7 => "Đang sửa chữa"
                };
            }
            
        }
    }
}
