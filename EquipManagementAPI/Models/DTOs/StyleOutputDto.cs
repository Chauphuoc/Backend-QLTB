namespace EquipManagementAPI.Models.DTOs
{
    public class StyleOutputDto
    {
        public string StyleCode { get; set; }
        public string? StyleName { get; set; }
        public int Output { get; set; }
        public double Percentage { get; set; }  // đã làm tròn 2 chữ số

    }
}
