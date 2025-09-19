using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace EquipManagementAPI.Models
{
    [Table("Asset Item")]
    public class Equipment
    {
        [Column("RowID")]
        public int id { get; set; }
        [Key]
        [Column("Equipment Code")]
        public string EquipmentCode { get; set; }
        [Column("Line No")]
        public int? LineNo { get; set; }
        [Column("Manage Unit")]
        public string? ManageUnit { get; set; }
        [Column("Asset Type Code")]
        public string? AssetTypeCode { get; set; }
        [Column("Equipment Group Code")]
        public string? EquipmentGroupCode { get; set; }

        public string? Model { get; set; }
        [Column("Serial Number")]
        public string? SerialNumber { get; set; }
        public string? Brand { get; set; }
        [Column("Document No_")]
        public string? DocumentNo { get; set; }
        [Column("Document Type")]
        public int? DocumentType { get; set; }
        public int? Status { get; set; }
        public string? ManufacturingYear { get; set; }
        public string? QRCode { get; set; }
        public string? UserId { get; set; }
        public int? UsageYears { get; set; }
        [Column("Setup Date")]
        public DateTime? SetupDate { get; set; }
        public string? Description { get; set; }
        public string? Supplier { get; set; }
        public string? Image { get; set; }
        [Column("Unit Of Measure")]
        public string? UnitOfMeasure { get; set; }
        public decimal? Price { get; set; }
        [Column("Technical Document")]
        public string? TechnicalDoc { get; set; }
        public string? LogoCty { get; set; }
        [Column("Warranty Date")]
        public DateTime? WarrantyDate { get; set; }
        [Column("Next Service Date")]
        public DateTime? NextServiceDate { get; set; }
        public string? Url { get; set; }
        [Column("Source Code")]
        public string? SourceCode { get; set; }
        [Column("Responsibility Center")]
        public string? Responsibility { get; set; }
        [Column("Location Code")]
        public string? LocationCode { get; set; }
        public string? Name { get; set; }
        [Column("Equipment Sub Code")]
        public string? EquipmentSubCode { get; set; }
        [Column("Using Unit")]
        public string? UsingUnit { get; set; }
        [Column("Currency Code")]
        public string? CurrencyCode { get; set; }
        [Column("Year of equip import")]
        public string? YearOfImport { get; set; }
        [Column("Created Date")]
        public DateTime? CreatedDate { get; set; }
        [Column("Status Group")]
        public int? StatusGroup { get; set; }

    }
}
