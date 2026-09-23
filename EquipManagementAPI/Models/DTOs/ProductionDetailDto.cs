namespace EquipManagementAPI.Models.DTOs
{
    public class ProductionDetailDto
    {
        public string ToMay { get; set; }
        public string Style { get; set; } = "";
        public string Po { get; set; } = "";
        public string ColorNo { get; set; } = "";
        public string Size { get; set; } = "";
        public int SlKiem { get; set; }
        public int SlKeHoach { get; set; }
        public int SlThoatChuyen { get; set; }
        public double PctHoanThanh { get; set; }   // % làm tròn 2 chữ số

    }
}
