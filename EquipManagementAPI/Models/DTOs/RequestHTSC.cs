namespace EquipManagementAPI.Models.DTOs
{
    public class RequestHTSC
    {
        public string QRCode { get; set; }
        public string UserID { get; set; }
        public string ReasonType { get; set; }
        public List<RepairTypeDetailRequest> RepairTypeDetails { get; set; }
    }
}
