using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EquipManagementAPI.Models;

[Table("Employee")]
public class Employee
{
    [Key]
    [Column("No_")]
    public string No { get; set; }

    [Column("No_ 2")]
    public string No2 { get; set; }

    public string Name { get; set; }

    [Column("Work Shift Code")]
    public string WorkShiftCode { get; set; }

    [Column("Work Center Code")]
    public string WorkCenterCode { get; set; }

    [Column("Login ID")]
    public string LoginId { get; set; }
}
