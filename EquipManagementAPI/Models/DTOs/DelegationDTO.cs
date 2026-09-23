namespace EquipManagementAPI.Models.DTOs
{
    public class DelegationDTO
    {
        public int Id { get; set; }
        public string TargetTeamCode { get; set; }
        public string TargetTeamName { get; set; }
        public string GrantedByNo { get; set; }
        public string GrantedByName { get; set; }
        public DateTime ValidFrom { get; set; }
        public DateTime? ValidUntil { get; set; }
        public bool IsPermanent { get; set; }
        public string Status { get; set; }
        public string? InspectionType { get; set; }
    }
}
