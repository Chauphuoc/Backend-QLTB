namespace EquipManagementAPI.Models.DTOs
{
    public class InforRequestSC
    {
        public string QRCode { get; set; }
        public string EquipmentName { get; set; }
        public string Serial { get; set; }
        public string Brand { get; set; }
        public string Model { get; set; }
        public string? WorkShift { get; set; }
        public string? WorkShiftCode { get; set; }
        public string? WorkCenter { get; set; }
        public int Status { get; set; }

        public string? StatusName
        {
            get
            {
                switch (Status)
                {
                    case 0:
                        return "Đã tạo yêu cầu";
                    case 1:
                        return "Bắt đầu sửa";
                    case 2:
                        return "Chờ BQLC xác nhận";
                    case 3:
                        return "Sửa lại";
                    case 4:
                        return "BQLC xác nhận";
                    case 5:
                        return "Hoàn thành";
                    case 6:
                        return "Không sửa được";
                    case 7:
                        return "Đang sửa chữa";
                    default:
                        return null;
                }
            }
        }
    }
}
