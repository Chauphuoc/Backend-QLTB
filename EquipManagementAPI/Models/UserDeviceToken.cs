using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EquipManagementAPI.Models
{
    [Table("User Devices")]
    public class UserDeviceToken
    {
        [Key]
        public int Id { get; set; }
        [Column("User Id")]
        public string UserID { get; set; }      // = Employee.No_

        [Column("Device Token")]
        public string DeviceToken { get; set; } // FCM token từ app
        [Column("Device Type")]
        public string DeviceType { get; set; }  // "android" | "ios"
        [Column("Created At")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        [Column("Last Login")]
        public DateTime LastLogin { get; set; } =DateTime.UtcNow;
    }
}
