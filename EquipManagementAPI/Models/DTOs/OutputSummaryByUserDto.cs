namespace EquipManagementAPI.Models.DTOs
{
    public class OutputSummaryByUserDto
    {
        public int CheckedQuantity { get; set; }
        public int PassQuantity { get; set; }
        public int DefectQuantity { get; set; }
        public string WorkCenterName { get; set; }

        public string WorkShiftName { get; set;}
        public string Date { get; set;}
    }
}
