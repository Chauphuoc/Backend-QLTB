using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EquipManagementAPI.Models;

[Table("QLTB_Roles")]
public class Roles
{
    [Key]
    public int Id { get; set; }

    public string Name { get; set; }

    public string Description { get; set; }

    public int Level { get; set; }

    public int isActive { get; set; }
}
