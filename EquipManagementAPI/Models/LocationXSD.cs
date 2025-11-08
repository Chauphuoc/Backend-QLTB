using System.ComponentModel.DataAnnotations.Schema;

namespace EquipManagementAPI.Models;

[Table("Location XSD")]
public class LocationXSD
{
    [Column("RowID")]
    public int Id { get; set; }

    public string Code { get; set; }

    [Column("Work Center Code")]
    public string? WorkCenterCode { get; set; }

    [Column("Work Shop Code")]
    public string? WorkShopCode { get; set; }

    public string? Name { get; set; }

    [Column("Department Code")]
    public string? DepartmentCode { get; set; }

    public int Check { get; set; }
}
