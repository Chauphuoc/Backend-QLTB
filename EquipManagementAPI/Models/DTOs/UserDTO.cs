namespace EquipManagementAPI.Models.DTOs
{
    public class UserDTO
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string WorkCenterCode { get; set; }
        public string WorkCenterName { get; set; }
        public string WorkShiftCode { get; set; }
        public string WorkShiftName { get; set;}

        public string AssignedInspectionTeamCode { get; set; }    // tổ thực sự được phân công để kiểm (ưu tiên dùng)
        public string AssignedInspectionTeamName { get; set; }
        public string? AssignedInspectionType { get; set; }       // loại kiểm của phân công chính: "Endline" / "SauUi"
        public List<DelegationDTO> DelegatedTeams { get; set; } = new List<DelegationDTO>();

    }
}
