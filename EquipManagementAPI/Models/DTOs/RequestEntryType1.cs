namespace EquipManagementAPI.Models.DTOs
{
    public class RequestEntryType1
    {
        public List<string> QRCodes { get; set; }
        public string DocumentNo { get; set; }
        public int DocumentType { get; set; }
        public string ManageUnit { get; set; }
        public string UsingUnit { get; set; }
        public string UserId { get; set; }
        public int Status { get; set; }
    }
}
