using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("QLCL_GarmentParts")]
public class GarmentParts
{
    [Key] // ✅ FIX
    public int RowID { get; set; }

    public int PartID { get; set; }
    public string PartName { get; set; }
}