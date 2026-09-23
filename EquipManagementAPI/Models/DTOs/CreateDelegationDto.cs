namespace EquipManagementAPI.Models.DTOs
{
    public class CreateDelegationDto
    {
        public string EmployeeNo { get; set; }            // người được phân công (required)
        public string TargetTeamCode { get; set; }        // required
        public string? TargetTeamName { get; set; }
        public DateTime ValidFrom { get; set; }           // required
        public DateTime? ValidUntil { get; set; }         // nullable
        public bool IsPermanent { get; set; } = false;
        public string? Reason { get; set; }
        // Don't trust client for GrantedBy; optional if you want compatibility:
        public string? GrantedByNo { get; set; }

        /// <summary>Loại kiểm: "Endline" hoặc "SauUi"</summary>
        public string? InspectionType { get; set; }
    }
}
