namespace EquipManagementAPI.Models.DTOs
{
    public class XacnhanHT_DTO
    {
        public int RowID { get; set; }
        public string QRCode { get; set; }
        public string? EquipmentGroup { get; set; }
        public string? EquipmentName { get; set; }
        public string Requester { get; set; }
        public string? LocationCode { get; set; }
        public string? Location {  get; set; }
        public string? RepairType { get; set; }
        public string? RepairTypeName { get; set; }
        public string? Description { get; set; }
        public string? Reason { get; set; }
    }
}
