using System.ComponentModel.DataAnnotations.Schema;

namespace EquipManagementAPI.Models.DTOs
{
    public class QRCodeEntryDTO
    {
        public int Id { get; set; }
        public string QRCode { get; set; }
        public string EquipmentCode { get; set; }
        public string EquipmentSubCode { get; set; }
        public string EquipmentGroupCode { get; set; }
        public string DocumentNo { get; set; }
        public int DocumentType { get; set; }
        public string ManageUnit { get; set; }
        public string UsingUnit { get; set; }
        public DateTime PostingDate { get; set; }
        public string UserId { get; set; }
        public string Respon { get; set; }
        public string SourceCode { get; set; }
        public int Status { get; set; }
    }
}
