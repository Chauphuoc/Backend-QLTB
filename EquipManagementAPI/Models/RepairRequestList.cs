using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EquipManagementAPI.Models;

[Table("Repair Request List")]
public class RepairRequestList
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int RowID { get; set; }

    [Key]
    [Column("No_")]
    public string No { get; set; }

    [Column("Equipment Code")]
    public string EquipmentCode { get; set; }

    [Column("Equipment Group Code")]
    public string EquipmentGroupCode { get; set; }

    [Column("Serial Number")]
    public string Serial { get; set; }

    public string Brand { get; set; }

    public string Model { get; set; }

    public string Reporter { get; set; }

    [Column("Posting Date")]
    public DateTime PostingDate { get; set; }

    public DateTime? FromDate { get; set; }

    public DateTime? ToDate { get; set; }

    public int Status { get; set; }

    public string QRCode { get; set; }

    [Column("Location Code")]
    public string? LocationCode { get; set; }

    public int Approve { get; set; }

    public decimal Duration { get; set; }
    public string WorkCenterCode { get; set; }
}
