using System.Text.Json.Serialization;

namespace EquipManagementAPI.Models.DTOs
{
    public class ImportOrderSizeRequest
    {
        public List<ImportOrderSizeRow> Items { get; set; } = new();
        public string? CreatedBy { get; set; }
    }

    public class ImportOrderSizeRow
    {
        // Tên Style (Item.Name) từ file Excel
        public string StyleName { get; set; } = default!;
        // Tên PO từ file Excel (SalesOrderHeader."PO Customer Name", fallback production_order_items.POName)
        public string PoName { get; set; } = default!;
        public string ColorId { get; set; } = default!;
        // Chỉ cần truyền khi cùng style + po + color tồn tại ở nhiều version
        public Guid? VersionId { get; set; }
        public List<SizeQtyItem> SizeBreakdown { get; set; } = new();

        // Mã style / mã PO do server suy ra từ StyleName / PoName, không nhận từ client
        [JsonIgnore]
        public string StyleId { get; set; } = string.Empty;
        [JsonIgnore]
        public string PoId { get; set; } = string.Empty;
    }
}
