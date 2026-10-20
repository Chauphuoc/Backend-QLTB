using Microsoft.AspNetCore.Http.HttpResults;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EquipManagementAPI.Models
{
    [Table("User Setup Mobile")]
    public class UserSetupMobile
    {
        [Key]
        [Column("User ID")]
        public string UserID { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;

        public string? Email { get; set; }

        public string? RefreshToken { get; set; }

        public DateTime? RefreshTokenExpiryTime { get; set; }
        [Column("Created At")]
        public DateTime? CreatedAt { get; set; }

        [Column("Last Login")]
        public DateTime? LastLogin { get; set; }

        public int? Status { get; set; }

        public string? Role { get; set; }

        public int RowID { get; set; }
    }
}