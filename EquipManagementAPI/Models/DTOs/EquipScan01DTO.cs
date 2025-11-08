using System.Collections.Generic;

namespace EquipManagementAPI.Models.DTOs;

public class EquipScan01DTO
{
	public List<string> QRCodes { get; set; }

	public int Status { get; set; }

	public string? Unit { get; set; }

	public string WorkShift { get; set; }

	public string UserID { get; set; }
}
