using System.Text.Json.Serialization;

namespace EquipManagementAPI.Models.DTOs
{
    public class ImportDeliveryDateRequest
    {
        public List<ImportDeliveryDateRow> Items { get; set; } = new();
        public string? CreatedBy { get; set; }
    }

    public class ImportDeliveryDateRow
    {
        // Tên Style (Item.Name) và Tên PO từ file Excel
        public string StyleName { get; set; } = default!;
        public string PoName { get; set; } = default!;
        public DateOnly NewDeliveryDate { get; set; }

        // Mã style / mã PO do server suy ra từ tên, không nhận từ client
        [JsonIgnore]
        public string StyleId { get; set; } = string.Empty;
        [JsonIgnore]
        public string PoId { get; set; } = string.Empty;
    }
}
