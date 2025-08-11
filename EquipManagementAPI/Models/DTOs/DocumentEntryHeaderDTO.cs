namespace EquipManagementAPI.Models.DTOs
{
    public class DocumentEntryHeaderDTO
    {
        public string No { get; set; }
        public string? ReceivingUnit { get; set; }
        public string? ReceivedUnitName { get; set; }
        public string? ExportingUnit {  get; set; }
        public string? ExportingUnitName { get; set; }
        public int? DocumentType { get; set; }
        public string DocType { get; set; }
        public DateTime? PostingDate { get; set; }
        public DateTime? DocumentDate { get; set; }
        public string DocumentDateFormat { get; set; }
        public int? Status { get; set; }
        public string? StatusName { get; set; }
    }
}
