using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EquipManagementAPI.Models;

[Table("No_ Series Line QLTB")]
public class NoSeriesLine
{
    [Column("Series Code")]
    public string SeriesCode { get; set; }

    public string Code { get; set; }

    [Column("Starting No_")]
    public string StartingNo { get; set; }

    [Column("Ending No_")]
    public int EndingNo { get; set; }

    [Column("Warning No_")]
    public int WarningNo { get; set; }

    [Column("Increment-by No_")]
    public int IncrementByNo { get; set; }

    [Column("Last No_ Used")]
    public int LastNoUsed { get; set; }

    [Column("Proposal No_")]
    public string ProposalNo { get; set; }

    public int Open { get; set; }

    [Column("Last Date Used")]
    public DateTime LastDateUsed { get; set; }

    [Column("Ext_ Last No_ Used")]
    public int ExtLastNoUsed { get; set; }

    [Column("Ext_ Starting No_")]
    public string ExtStartingNo { get; set; }

    [Column("Ext_ Increment-by No_")]
    public int ExtIncrementByNo { get; set; }

    [Column("Source Code")]
    public string SourceCode { get; set; }

    public string Description { get; set; }

    [Column("Responsibility Center")]
    public string ResponsibilityCenter { get; set; }

    [Column("Location Code")]
    public string LocationCode { get; set; }

    [Column("Default Customer")]
    public string DefaultCustomer { get; set; }

    public string UserID { get; set; }

    [Column("From Date")]
    public DateTime? FromDate { get; set; }

    [Column("To Date")]
    public DateTime? ToDate { get; set; }

    public int? Type { get; set; }

    public int? AllowEdit { get; set; }

    [Key]
    public int RowID { get; set; }

    [Column("Bank Account")]
    public string? BankAccount { get; set; }

    [Column("Category Code")]
    public string? CategoryCode { get; set; }

    public decimal? Diameter { get; set; }

    public decimal? Height { get; set; }

    public decimal? Length { get; set; }

    [Column("Sub Group Code")]
    public string? SubGroupCode { get; set; }

    public decimal? Surface { get; set; }

    public decimal? Width { get; set; }

    [Column("Default Bank")]
    public string? DefaultBank { get; set; }

    [Column("Setup Type")]
    public int? SetupType { get; set; }

    [Column("Ext_ Ending No_")]
    public int? ExtEndingNo { get; set; }

    public int? Status { get; set; }

    public int? Blocked { get; set; }
}
