namespace EquipManagementAPI.Models.DTOs
{
    public class FinalizeResultDto
    {
        public bool Success { get; set; }

        public string Message { get; set; }

        public int ReinspectionId { get; set; }

        public string ReinspectionNo { get; set; }

        public int TotalQuantity { get; set; }

        public int TotalPassed { get; set; }

        public int TotalScrapped { get; set; }
    }
}
