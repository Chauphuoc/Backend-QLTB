using EquipManagementAPI.Models;
using EquipManagementAPI.Models.DTOs;

namespace EquipManagementAPI.Helpers
{
    public class DocumentEntryHelper
    {
        public static string GetDocTypeName(int docType)
        {
            return docType switch
            {
                0 => "Nhập mua mới",
                1 => "Nhập điều chuyển ĐVQL",
                2 => "Nhập ĐV ngoài trả mượn",
                3 => "Nhập sau khi sửa chữa BD",
                4 => "Xuất điều chuyển ĐVQL",
                5 => "Nhập thuê",
                6 => "Nhập mượn ĐV ngoài",
                7 => "Nhập mượn NB",
                8 => "Nhập trả kho công ty",
                9 => "Xuất cho mượn NB",
                10 => "Xuất trả kho Cty",
                11 => "Xuất trả mượn ĐV ngoài",
                12 => "Xuất trả thuê",
                13 => "Xuất trả nhà CC",
                16 => "Xuất thanh lý",
                14 => "Xuất cho ĐV ngoài mượn"
            };
        }
        public static string GetStatusName(int status)
        {
            return status switch
            {
                0 => "Mới",
                1 => "Mở lại",
                2 => "Xác nhận"
            };
        }



    }
}
