using System.ComponentModel.DataAnnotations;

namespace EquipManagementAPI.Models.DTOs
{
    public class ActualItemDto
    {
        [Required]
        public string Name { get; set; }
        [Range(0, int.MaxValue)]
        public int ActualQty { get; set; }
    }
}
