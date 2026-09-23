namespace EquipManagementAPI.Models.DTOs
{
    public class RenamePoRequest
    {
        public string NewPoName { get; set; } = string.Empty;
        public string? UserId { get; set; }
    }
}
