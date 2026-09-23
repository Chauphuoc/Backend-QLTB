namespace EquipManagementAPI.Models.DTOs
{
    public class CuttingSupplyRequestItemDto
    {
        public string Name { get; set; }
        public int Qty { get; set; }
        public int? ActualQty { get; set; }
    }
}
