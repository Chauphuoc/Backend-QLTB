namespace EquipManagementAPI.Models.DTOs
{
    public class RegisterTokenRequest
    {
        public string UserID { get; set; }
        public string DeviceToken { get; set; }
        public string DeviceType { get; set; }
    }
}
