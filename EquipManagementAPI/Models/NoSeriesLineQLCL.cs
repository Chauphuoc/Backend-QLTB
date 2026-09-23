using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EquipManagementAPI.Models;

[Table("No_ Series Line")]
public class NoSeriesLineQLCL
{

    [Key]
    public int RowID { get; set; }
    [Column("Series Code")]
    public string? SeriesCode { get; set; }

    public string? Code { get; set; }

    [Column("Starting No_")]
    public string? StartingNo { get; set; }

    [Column("Ending No_")]
    public int? EndingNo { get; set; }

    [Column("Warning No_")]
    public int? WarningNo { get; set; }

    [Column("Increment-by No_")]
    public int? IncrementByNo { get; set; }

    [Column("Last No_ Used")]
    public int? LastNoUsed { get; set; }


    [Column("Last Date Used")]
    public DateTime? LastDateUsed { get; set; }

    [Column("Ext_ Last No_ Used")]
    public int? ExtLastNoUsed { get; set; }

    [Column("Ext_ Starting No_")]
    public string? ExtStartingNo { get; set; }

    [Column("Ext_ Increment-by No_")]
    public int? ExtIncrementByNo { get; set; }

    [Column("Source Code")]
    public string? SourceCode { get; set; }

    public string? Description { get; set; }

    [Column("Responsibility Center")]
    public string? ResponsibilityCenter { get; set; }

    [Column("Location Code")]
    public string? LocationCode { get; set; }
       
        
}
