namespace EquipManagementAPI.Models.DTOs
{
    public class AssignmentDto
    {
        public string EmployeeNo { get; set; }
        public string TeamCode { get; set; }
        public string TeamName { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public bool IsPermanent { get; set; }
        public string Reason { get; set; }
        public string AssignedByNo { get; set; }

        /// <summary>Loại kiểm: "Endline" hoặc "SauUi"</summary>
        public string? InspectionType { get; set; }
    }
}
