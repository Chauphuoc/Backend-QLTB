namespace EquipManagementAPI.Models.DTOs
{
    public class UserRoleDTO
    {
        public string Name { get; set; }
        public string WorkCenterCode { get; set; }
        public string WorkCenterName { get; set; }
        public string WorkShiftCode { get; set; }
        public string WorkShiftName { get; set; }

        public List<string> Roles { get; set; } = new();   // thay cho Role
        public bool IsAdmin { get; set; }                  // FE dùng để mở/khoá dropdown
    }
}
