namespace EquipManagementAPI.Models.DTOs
{
    public class EquipScanKK_DTO
    {
        public string unit { get; set; }
        public string location { get; set; }
        public List<String> QRCodes { get; set; }
        public string status { get; set; }
        public string userId { get; set; }
    }
}
