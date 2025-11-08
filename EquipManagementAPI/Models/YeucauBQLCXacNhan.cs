using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EquipManagementAPI.Models;

[Table("Yeucau_BQLC_Xacnhan")]
public class YeucauBQLCXacNhan
{
    [Key]
    public int RowID { get; set; }

    [Column("Document No_")]
    public string DocNo { get; set; }

    public string QRCode { get; set; }

    [Column("Equipment Group Code")]
    public string EquipmentGroupCode { get; set; }

    public string Requester { get; set; }

    [Column("Location Code")]
    public string? LocationCode { get; set; }

    public int Status { get; set; }

    [Column("Repair Type")]
    public string RepairType { get; set; }

    public string Description { get; set; }

    public DateTime? PostingDate { get; set; }
}
