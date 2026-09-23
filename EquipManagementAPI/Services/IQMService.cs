using EquipManagementAPI.Models.DTOs;

namespace EquipManagementAPI.Services
{
    public interface IQMService
    {
        //Task<List<SalesOrderLineFactoryPlanQueueDTO>>GetPlanQueueScheduleAsync(GetPlanQueueDTO filter);
        Task<List<DepartmentDTO>> GetWorkCenterAsync();
        Task<List<LocationXsdDTO>> GetWorkShiftAsync(string workcenter);
        Task<UserDTO> GetInforUser(string userID);
        Task<List<ProductionPlanDTO>> GetStyleAsync(string workcenter,string workshift);
        Task<List<ProductionPlanDTO>> GetSizeAsync(string workcenter, string workshift, string style);
        Task<List<ProductionPlanDTO>> GetPOAsync(string workcenter, string workshift, string style);
        Task<List<ProductionPlanDTO>> GetColorAsync(string workcenter, string workshift, string style, string so);
        Task<List<DefectsDTO>> GetListDefects(string phase);
        Task<List<UnitDTO>> GetUnitList();
        Task<object> Handle_XacNhanKiem(RequestCheckQlclDTO payload);
        Task<object> Handle_KiemLai(RequestCheckQlclDTO payload);
        Task<List<TeamPlanSummaryDTO>> BaoCaoSLKeHoachVaThoatChuyen(DateOnly? date,DateOnly? from,DateOnly? to,string? factoryId);
        Task<List<HourlyOutputDTO>> SanLuongThoatChuyenTheoGio(string teamId, DateOnly? date, DateOnly? from, DateOnly? to, string? factoryId);
        Task<List<TeamQCStatsItem>> GetTeamQCStatsAsync(DateOnly dateFrom, DateOnly dateTo, string? factoryId, string? operationType = null);
        Task<IEnumerable<DefectSummaryItemDto>> GetDefectSummaryAsync(DateTime dateFrom, DateTime dateTo, string? factoryId, string? teamId = null, string? operationType = null);
        Task<IEnumerable<TeamHourlyQCItemDto>> GetTeamHourlyQCAsync(string teamId, DateTime dateFrom, DateTime dateTo, string? factoryId, string? operationType = null);
        Task<IEnumerable<TeamDefectBreakdownItemDto>> GetTeamDefectBreakdownAsync(string teamId, DateTime dateFrom, DateTime dateTo, string? factoryId, string? operationType = null);
        Task<IEnumerable<HourlyDefectTrendItemDto>> GetHourlyDefectTrendAsync(DateTime dateFrom, DateTime dateTo, string? factoryId, string? teamId = null, string? operationType = null);
        Task<IEnumerable<ReplacementItemDto>> GetReplacementItemsAsync();
        Task<object> SendCuttingSupplyRequestAsync(CuttingSupplyRequestDto dto);
        Task<IEnumerable<DefectsDTO>> GetDefectsNotRework();
        Task<List<CuttingSupplyRequestListItemDto>> GetCuttingSupplyRequestsAsync(int? status, DateTime? dateFrom, DateTime? dateTo);
        Task<object> UpdateCuttingSupplyStatusAsync(int id, UpdateCuttingSupplyStatusDto dto);
        Task<List<StyleDefectSummaryDto>> GetStyleDefectSummaryAsync(string? date, string? from, string? to, string? factoryId, string? teamId, string? operationType = null);
        Task<List<StyleDefectBreakdownDto>> GetStyleDefectBreakdownAsync(string styleCode, string? date, string? from, string? to, string? factoryId, string? teamId, string? operationType = null);
        Task<IEnumerable<DailyOutputDto>> GetDailyOutputAsync(string dateFrom,string dateTo,string? factoryId = null);
        Task<List<StyleOutputDto>> GetOutputByStyleAsync(string dateFrom, string dateTo, string? factoryId);
        Task<EmployeeDto> GetByUserNameAsync(string userName);

    }

}
