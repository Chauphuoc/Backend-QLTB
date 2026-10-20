using System.Text.Json.Serialization;

namespace EquipManagementAPI.Models.DTOs
{
    public class ImportPoNameRequest
    {
        public List<ImportPoNameRow> Items { get; set; } = new();
        public string? CreatedBy { get; set; }
    }

    public class ImportPoNameRow
    {
        // Tên Style (Item.Name) và Tên PO cũ từ file Excel; Tên PO mới là giá trị đổi sang
        public string StyleName { get; set; } = default!;
        public string OldPoName { get; set; } = default!;
        public string NewPoName { get; set; } = default!;

        // Mã style / mã PO do server suy ra từ tên, không nhận từ client
        [JsonIgnore]
        public string StyleId { get; set; } = string.Empty;
        [JsonIgnore]
        public string PoId { get; set; } = string.Empty;
    }
}
