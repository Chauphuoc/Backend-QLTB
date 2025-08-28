namespace EquipManagementAPI.Models.DTOs
{
    public class RepairListDTO
    {
        public string No { get; set; }
        public string QRCode { get; set; }
        public string EquipmentName { get; set; }
        public string Reporter {  get; set; }
        public string? Location { get; set; }
        public string Model { get; set; }
    }
}
