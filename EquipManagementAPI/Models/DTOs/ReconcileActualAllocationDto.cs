namespace EquipManagementAPI.Models.DTOs
{
    public class ReconcileActualAllocationDto
    {
        public string SplitPoId { get; set; } = string.Empty;   // PO con nhận phần Actual này
        public int Qty { get; set; }                              // SL Actual user nhập cho PO con đó
    }

    public class ReconcileActualTeamDto
    {
        public string TeamId { get; set; } = string.Empty;        // Work Center Code (Tổ)
        public List<ReconcileActualAllocationDto> Allocations { get; set; } = new();
    }

    public class ReconcileActualRequestDto
    {
        public string ParentPoId { get; set; } = string.Empty;    // PO gốc đã bị tách (Sales Order No_ cũ)
        //public string FactoryId { get; set; } = string.Empty;
        //public string StyleId { get; set; } = string.Empty;
        //public string ColorId { get; set; } = string.Empty;
        public List<ReconcileActualTeamDto> TeamAllocations { get; set; } = new();
    }
}
