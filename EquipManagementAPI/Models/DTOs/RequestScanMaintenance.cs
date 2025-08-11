namespace EquipManagementAPI.Models.DTOs
{
    public class RequestScanMaintenance
    {
        public string QRCode { get; set; }
        public string type { get; set; }
        public List<string> Content { get; set; }
        public string UserID { get; set; }
    }
}
