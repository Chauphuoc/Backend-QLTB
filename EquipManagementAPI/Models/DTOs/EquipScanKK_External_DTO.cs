namespace EquipManagementAPI.Models.DTOs
{
    public class EquipScanKK_External_DTO
    {
        public string unit { get; set; }
        public string borrowingUnit  { get; set; }
        public List<String> QRCodes { get; set; }
        public string userId { get; set; }
    }
}
