using EquipManagementAPI.Models;
using EquipManagementAPI.Models.DTOs;

namespace EquipManagementAPI.Services
{
    public interface IAssetService
    {
        Task<List<string>> ProcessQRCodeBatchAsync(RequestEntryType0 request);

        Task<List<string>> ProcessScanQRCode_Type1(RequestEntryType1 request);

        Task<bool> UpdateSerialNumber(string equipCode, string newSerial, string user);

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

        Task<List<string>> Process_KhongSuaDuoc_Dieunguoi(RequestHTSC request);

        Task<List<string>> Process_KhongSuaDuoc_Dieumay(RequestDieuMay request);

        //Task<List<XacnhanHT_DTO>> Process_GetListYeuCau_User();

        Task<List<RepairListDTO>> Process_GetListYeuCau_User(string userId);
        Task<List<RepairListDTO>> Process_GetListYeuCau();
        Task<List<ItemRequireList>> GetOverView_Require();
        Task<List<ItemRequireList>> GetOverView_Repairing();
        Task<List<ItemRequireList>> GetOverView_Completed();
        Task<List<string>> Process_XacNhanHoanThanh(XacnhanHT_DTO request);

        Task<List<string>> Process_KhongHTSC(XacnhanHT_DTO request);

        Task<(bool Success, string Message)> DeleteYeuCauSC(string code, string userId);

        Task<List<RepairHistoryListDTO>> Process_GetListRepairHistory(string code);

        Task<List<MaintenanceHistoryDTO>> Process_GetMaintenanceHistory(string code);

        Task<string> GetUserRoleAsync(string userId);

        Task<InforRequestSC> GetInforEquipHTSC_Serial(string serial);
        Task<HoanthanhSC_Detail_DTO> Get_DetailContentRepair(string qrcode);
        Task<List<string>> ProcessScan_KiemKe(EquipScanKK_DTO request);
        Task<List<ItemRequireList>> GetOverView_Require_ByUser(string userId);
        Task<List<ItemRequireList>> GetOverView_Repairing_ByUser(string userId);
        Task<List<ItemRequireList>> GetOverView_Completed_ByUser(string userId);
        Task<List<Repair_Description_DTO>> Get_listDescription(string type);
        Task<List<string>> ProcessScan_KiemKe_External(EquipScanKK_External_DTO request);
    }
}
