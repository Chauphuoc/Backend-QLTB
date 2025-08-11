using EquipManagementAPI.Models;

namespace EquipManagementAPI.Helpers
{
    public class EquipmentMapper
    {
        

        public static string FormatDocumentNumber(string template, string number)
        {
            // Ví dụ: template = "99999", number = "22" => "000022"
            while (number.Length < template.Length)
            {
                number = "0" + number;
            }
            return number;
        }


        public static string GetStatusNameEquipLog(int docType)
        {
            return docType switch
            {
                0 => "Lưu kho",
                1 => "Sử dụng"
            };
        }
    }
}
