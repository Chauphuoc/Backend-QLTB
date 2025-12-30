using System.ComponentModel.DataAnnotations;

namespace EquipManagementAPI.Models;

public class EquipmentDTO
{
    [Required]
    public string EquipmentCode { get; set; }

    public string EquipmentName { get; set; }

    public string? ManageUnit { get; set; }
    public string? UsingUnit { get; set; }
    public string? LineNo { get; set; }
        public string? ManageUnitName { get; set; }

    public string? EquipmentGroupCode { get; set; }

    public string? Model { get; set; }

    public string SerialNumber { get; set; }

    public string? Brand { get; set; }

    public string? QRCode { get; set; }

    public string? Image { get; set; }

    public string? LocationCode { get; set; }

    public string? LocationName { get; set; }

    public int? Status { get; set; }

    public string? TinhTrang
    {
        get
        {
            int? status = Status;
            switch (status)
            {
                case -1:
                    return "Huỷ";
                case 0:
                    return "Mới";
                case 1:
                    return "Đang sử dụng";
                case 2:
                    return "Đang sửa chữa";
                case 3:
                    return "Hoàn trả kho Cty";
                case 4:
                    return "Trả thuê";
                case 5:
                    return "Trả nhà CC";
                case 6:
                    return "Bảo hành hãng";
                case 7:
                    return "Nhập bảo hành";
                case 8:
                    return "Thanh lý";
                case 9:
                    return "Nhập mượn";
                case 10:
                    return "Nhập thuê";
                case 11:
                    return "Trả mượn";
                case 12:
                    return "Cho mượn";
                case 13:
                    return "Nhập ĐV ngoài trả mượn";
                case 14:
                    return "Lưu kho";
                default:
                    {
                        return "Không tìm thấy";
                    }
            }
        }
    }

    public int? StatusGroup { get; set; }

    public string? TrangThai => StatusGroup switch
    {
        -1 => "Không sử dụng",
        0 => "Lưu kho",
        1 => "Sử dụng",
        _ => "Không xác định",
    };

    public string? LastMaintenanceTime { get; set; }

    public string? PlanTime { get; set; }

    public string? User { get; set; }

    public string? MaintenanceType { get; set; }

    public int? Check { get; set; }

    public string? NamSX { get; set; }

    public string? NamSD { get; set; }

    public int? SoNamSD { get; set; }

    public string MaintenanceCheck => Check switch
    {
        0 => "Chờ bảo dưỡng",
        1 => "Đã bảo dưỡng",
        2 => "Quá hạn bảo dưỡng",
        3 => "Chuyển qua sửa chữa",
        _ => "Không có dữ liệu",
    };
}
