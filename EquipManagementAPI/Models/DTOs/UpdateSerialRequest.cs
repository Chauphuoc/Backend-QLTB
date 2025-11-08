namespace EquipManagementAPI.Models.DTOs;

public class UpdateSerialRequest
{
    public string EquipmentCode { get; set; }

    public string SerialNumber { get; set; }

    public string User { get; set; }
}
