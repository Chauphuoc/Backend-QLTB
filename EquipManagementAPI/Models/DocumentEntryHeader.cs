using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace EquipManagementAPI.Models;

[Table("Document Entry Header QLTB")]
public class DocumentEntryHeader
{
    [Column("RowID")]
    public int Id { get; set; }

    [Column("No_")]
    public string No { get; set; }

    [Column("Receiving Unit")]
    public string? ReceivingUnit { get; set; }

    [Column("Exporting Unit")]
    public string? ExportingUnit { get; set; }

    [Column("Document Type")]
    public int? DocumentType { get; set; }

    [Column("Posting Date")]
    public DateTime? PostingDate { get; set; }

    [Column("Document Date")]
    public DateTime? DocumentDate { get; set; }

    public string Creater { get; set; }

    public string? Approver { get; set; }

    public int? Status { get; set; }

    [Column("Check QR")]
    public int? CheckQR { get; set; }

    [Column("Responsibility Center")]
    public string? Responsibility { get; set; }

    [Column("Source Code")]
    public string? SourceCode { get; set; }

    public string? Description { get; set; }

    [Column("Due Date")]
    public DateTime? DueDate { get; set; }
    [Column("Source No_")]
    public string? SourceNo { get; set; }
}
