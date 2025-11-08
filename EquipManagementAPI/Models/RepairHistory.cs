using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EquipManagementAPI.Models;

[Table("Repair History")]
public class RepairHistory
{
    [Key]
    public int RowID { get; set; }

    [Column("No_")]
    public string No { get; set; }

    [Column("Equipment Code")]
    public string EquipmentCode { get; set; }

    [Column("Equipment Group Code")]
    public string EquipGroup { get; set; }

    [Column("Serial Number")]
    public string Serial { get; set; }

    public string Brand { get; set; }

    public string Model { get; set; }

    [Column("Location Code")]
    public string LocationCode { get; set; }

    [Column("Created Date")]
    public DateTime CreatedDate { get; set; }

    [Column("User ID")]
    public string UserId { get; set; }

    public string QRCode { get; set; }

    public int Status { get; set; }

    public string? Reason { get; set; }
}
