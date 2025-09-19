using EquipManagementAPI.Models;
using EquipManagementAPI.Models.DTOs;

namespace EquipManagementAPI.Services
{
    public interface IAssetService
    {
        Task<List<string>> ProcessQRCodeBatchAsync(QRCodeEntryBatchDTO request);
        Task<List<string>> ProcessScanQRCode_Type1(QRCodeEntryBatchDTO request);
        Task<bool> UpdateSerialNumber(string equipCode, string newSerial);
        Task<List<string>> ProcessScan_SuDungLuuKho(EquipScan01DTO request);
        Task<List<MaintenanceTypeDTO>> GetMaintenanceType();
        Task<List<string>> GetMaintenanceContent(MaintenanceRequest request);
        Task<List<string>> ProcessScan_BaoDuong(RequestScanMaintenance request);
        Task<List<string>> Process_BaoDuongSuaChua(RequestSuaChua request);
        Task<List<string>> Process_SuaChua(RequestSuaChua request);
        Task<InforRequestSC> GetInforEquipSC(string code);
        Task<InforRequestSC> GetInforEquipHTSC(string code);
        Task<List<string>> Process_BatDauSuaChua(RequestSuaChua request);
        Task<EquipmentDTO> GetInforEquipment(string code, HttpRequest request);
        Task<EquipmentDTO> GetInforEquipmentByCode(string code, HttpRequest request);
        Task<EquipmentDTO> GetInforEquipmentBySerial(string code, HttpRequest request);
        Task<List<ReasonTypeDTO>> GetReasonType();
        Task<List<string>> Process_HTSC(RequestHTSC request);
        Task<List<string>> GetRemainingTask(RequestRemaining request);
        Task<List<string>> Process_KhongSuaDuoc(RequestHTSC request);
        Task<List<XacnhanHT_DTO>> Process_GetListYeuCauXN();
        Task<List<RepairListDTO>> Process_GetListYeuCau();
        Task<List<string>> Process_XacNhanHoanThanh(XacnhanHT_DTO request);
        Task<List<string>> Process_KhongHTSC(XacnhanHT_DTO request);
        Task<(bool Success, string Message)> DeleteYeuCauSC(string code,string userId);
        Task<List<RepairHistoryListDTO>> Process_GetListRepairHistory(string code);
        Task<List<MaintenanceHistoryDTO>> Process_GetMaintenanceHistory(string code);
    }
}
