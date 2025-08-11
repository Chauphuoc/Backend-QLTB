using System.ComponentModel.DataAnnotations.Schema;

namespace EquipManagementAPI.Models.DTOs
{
    public class MaintenanceRequest
    {
        
        public string QRCode { get; set; }
        public string MaintenanceType { get; set; }
    }
}
