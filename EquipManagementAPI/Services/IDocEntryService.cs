using EquipManagementAPI.Models.DTOs;

namespace EquipManagementAPI.Services
{
    public interface  IDocEntryService
    {
        Task<List<DocumentEntryHeaderDTO>> GetDocEntryHeader(int type);

        Task<List<DocumentEntryHeaderDTO>> GetDocEntryHeader_other(int type);
    }
}
