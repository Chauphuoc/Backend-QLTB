using System.ComponentModel.DataAnnotations.Schema;

namespace EquipManagementAPI.Models
{
    [Table("QRCode Entry QLTB")]
    public class QRCodeEntryQLTB
    {
        [Column("RowID")]
        public int Id { get; set; }
        public string QRCode { get; set; }
        [Column("Equipment Code")]
        public string EquipmentCode { get; set; }
        [Column("Equipment Sub Code")]
        public string EquipmentSubCode { get; set; }
        [Column("Equipment Group Code")]
        public string EquipmentGroupCode { get; set; }
        [Column("Document No_")]
        public string DocumentNo { get; set; }
        [Column("Document Type")]
        public int DocumentType { get; set; }
        [Column("Manage Unit")]
        public string ManageUnit { get; set; }
        [Column("Using Unit")]
        public string UsingUnit { get; set; }
        [Column("Posting Date")]
        public DateTime PostingDate { get; set; }
        public string UserId { get; set; }
        [Column("Responsibility Center")]
        public string Respon {  get; set; }
        [Column("Source Code")]
        public string SourceCode { get; set; }
        public int Status { get; set; }
    }
}
