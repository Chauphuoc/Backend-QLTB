using EquipManagementAPI.Models.DTOs;
using Microsoft.EntityFrameworkCore;

namespace EquipManagementAPI.Services
{
    public interface IProductionPlanService
    {
        //Task<List<CmPriceDTOs>> GetSplitsAsync(int cmPriceId);

        Task<CmSplitBatchResponse> CreateCmSplitAsync(string styleId, CmSplitBatchRequest request, string? userId);

         //Task DeleteSplitsAsync(int cmPriceId);

         Task<List<CmPriceListItemDto>> GetCmPricesAsync(string factoryId, int year, int month,int toYear, int toMonth);

        Task<CmPriceCreateResponse> CreateCmPriceAsync(CmPriceCreateRequest request);

        Task<List<OperationResponse>> GetOperation();
        Task<List<OperationResponse>> GetOperationByStyle(string styleId);
        Task<bool> StyleHasCmSplitsAsync(string styleId);
        Task<List<OperationResponse>> GetCMPriceOperationAsync(string styleId);
        //Task<MergePreviewResponse> PreviewAsync(MergePreviewRequest request);
        Task<MergeExecuteResponse> ExecuteAsync(MergeExecuteRequest request);
        Task<MergedPoDetailResponse> GetMergedPoDetailAsync(string poId);
    }
}
