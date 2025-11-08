using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EquipManagementAPI.Models;

[Table("Document Entry QLTB")]
public class DocumentEntry
{
    [Column("RowID")]
    public int Id { get; set; }

    [Column("Document No_")]
    [Required]
    public string DocumentNo { get; set; }

    [Column("Equipment Code")]
    public string EquipmentCode { get; set; }

    [Column("Equipment Group Code")]
    public string EquipmentGroupCode { get; set; }

    [Column("Manage Unit")]
    public string? ManageUnit { get; set; }

    [Column("Using Unit")]
    public string? UsingUnit { get; set; }

    [Column("Unit Of Measure")]
    public string? UnitOfMeasure { get; set; }

    public int? Quantity { get; set; }

    public int? Status { get; set; }

    [Column("Document Type")]
    public int? DocumentType { get; set; }

    public string QRCode { get; set; }

    [Column("Posting Date")]
    public DateTime? PostingDate { get; set; }

    public string? Brand { get; set; }

    public string? Model { get; set; }

    [Column("Serial Number")]
    public string? SerialNumber { get; set; }

    [Column("Responsibility Center")]
    public string? Respon { get; set; }

    [Column("Source Code")]
    public string? SourceCode { get; set; }

    [Column("Department Code")]
    public string? DepartmentCode { get; set; }
}
