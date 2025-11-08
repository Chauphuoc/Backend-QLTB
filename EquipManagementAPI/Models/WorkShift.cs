using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EquipManagementAPI.Models;

[Table("Work Shift")]
public class WorkShift
{
    [Key]
    public int RowID { get; set; }

    public string Code { get; set; }

    [Column("Work Center Code")]
    public string WorkCenterCode { get; set; }

    [Column("Work Shop Code")]
    public string WorkShopCode { get; set; }

    public string Name { get; set; }
}
