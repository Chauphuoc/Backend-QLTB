using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EquipManagementAPI.Models;

[Table("QLTB_UserRole")]
public class UserRole
{
    [Key]
    public int Id { get; set; }

    [Column("User ID")]
    public string UserID { get; set; }

    [Column("Role ID")]
    public int RoleID { get; set; }
}
