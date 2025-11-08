namespace EquipManagementAPI.Models.DTOs;

public class RequestDieuMay
{
    public string OldQRCode { get; set; }

    public string UserID { get; set; }

    public string Unit { get; set; }

    public string WorkShift { get; set; }

    public string WorkShiftCode { get; set; }

    public string NewQRCode { get; set; }
}
