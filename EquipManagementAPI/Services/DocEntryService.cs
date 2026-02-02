using EquipManagementAPI.Data;
using EquipManagementAPI.Helpers;
using EquipManagementAPI.Models;
using EquipManagementAPI.Models.DTOs;
using Microsoft.EntityFrameworkCore;
using System.Reflection.Metadata;

namespace EquipManagementAPI.Services
{
    public class DocEntryService : IDocEntryService
    {
        private readonly ApplicationDbContext _context;

        public DocEntryService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<DocumentEntryHeaderDTO>> GetDocEntryHeader(int type) //Nội bộ
        {
           
               var  documents = await (from e in _context.DocumentEntryHeader
                                       join d in _context.DocumentEntry on e.No equals d.DocumentNo into entryGroup
                                       where e.DocumentType == (int?)type && e.CheckQR == (int?)0 && e.Status != (int?)-1 
                                       orderby e.PostingDate descending
                                       select new
                                       {
                                           Header = e,
                                           Entries = entryGroup
                                       }).ToListAsync();
            
            var exportUnitCode = documents.Select(e => e.Header.ExportingUnit).Distinct().ToList();

            var receiveUnitCode = documents
                .Select(e => e.Header.ReceivingUnit)
                .Where(code => !string.IsNullOrEmpty(code))
                .Distinct()
                .ToList();

            var departments = await _context.Department.ToListAsync();

            var receiveUnit = departments
                .Where(d => receiveUnitCode.Contains(d.Code))
                .ToDictionary(d => d.Code, d => d.Name);

            var exportUnit = departments
                .Where(d => exportUnitCode.Contains(d.Code))
                .ToDictionary(d => d.Code, d => d.Name);
                
            return documents.Select(doc => new DocumentEntryHeaderDTO
            {
                No = doc.Header.No,
                ReceivingUnit = doc.Header.ReceivingUnit,
                ReceivedUnitName = !string.IsNullOrEmpty(doc.Header.ReceivingUnit) && receiveUnit.ContainsKey(doc.Header.ReceivingUnit)
                    ? receiveUnit[doc.Header.ReceivingUnit]
                    : string.Empty,
                ExportingUnit = doc.Header.ExportingUnit,
                ExportingUnitName = !string.IsNullOrEmpty(doc.Header.ExportingUnit) && exportUnit.ContainsKey(doc.Header.ExportingUnit)
                    ? exportUnit[doc.Header.ExportingUnit]
                    : string.Empty,
                DocumentType = doc.Header.DocumentType,
                DocType = DocumentEntryHelper.GetDocTypeName(doc.Header.DocumentType.Value),
                PostingDate = doc.Header.PostingDate,
                DocumentDate = doc.Header.DocumentDate,
                DocumentDateFormat = doc.Header.DocumentDate?.ToString("dd/MM/yyyy") ?? string.Empty,
                Status = doc.Header.Status,
                StatusName = DocumentEntryHelper.GetStatusName(doc.Header.Status.Value),
                QRCodes = doc.Entries.Where(e => !string.IsNullOrEmpty(e.QRCode)).Select(e => e.QRCode).Distinct().ToList()
            }).ToList();
        }

        public async Task<List<DocumentEntryHeaderDTO>> GetDocEntryHeader_other(int type) //Đơn vị ngoài
        {
            var documents = await (from e in _context.DocumentEntryHeader join d in _context.DocumentEntry on e.No equals d.DocumentNo into entryGroup
                                   where e.DocumentType == (int?)type && e.CheckQR == (int?)0 
                                   orderby e.PostingDate descending 
                                   select new
                                   {
                                       Header = e,
                                       Entries = entryGroup
                                   }).ToListAsync();

            var departments = await _context.Department.ToListAsync();

            var receiveUnitCode = documents
                .Select(e => e.Header.ReceivingUnit)
                .Where(code => !string.IsNullOrEmpty(code))
                .Distinct()
                .ToList();

            var exportUnitCode = documents.Select(e => e.Header.ExportingUnit).Distinct().ToList();

            Dictionary<string, string> receiveUnit;
            Dictionary<string, string> exportUnit;

            if (type == 11 || type == 12 || type == 13 || type == 6 || type == 16)
            {
                receiveUnit = departments
                    .Where(e => receiveUnitCode.Contains(e.Code))
                    .ToDictionary(e => e.Code, e => e.Name);

                exportUnit = departments
                    .Where(d => exportUnitCode.Contains(d.Code))
                    .ToDictionary(d => d.Code, d => d.Name);
            }
            else
            {
                receiveUnit = departments
                    .Where(d => receiveUnitCode.Contains(d.Code))
                    .ToDictionary(d => d.Code, d => d.Name);

                exportUnit = departments
                    .Where(e => exportUnitCode.Contains(e.Code))
                    .ToDictionary(e => e.Code, e => e.Name);
            }

            return documents.Select(doc => new DocumentEntryHeaderDTO
            {
                No = doc.Header.No,
                ReceivingUnit = doc.Header.ReceivingUnit,
                ReceivedUnitName = !string.IsNullOrEmpty(doc.Header.ReceivingUnit) && receiveUnit.ContainsKey(doc.Header.ReceivingUnit)
                    ? receiveUnit[doc.Header.ReceivingUnit]
                    : string.Empty,
                ExportingUnit = doc.Header.ExportingUnit,
                ExportingUnitName = !string.IsNullOrEmpty(doc.Header.ExportingUnit) && exportUnit.ContainsKey(doc.Header.ExportingUnit)
                    ? exportUnit[doc.Header.ExportingUnit]
                    : string.Empty,
                DocumentType = doc.Header.DocumentType,
                DocType = DocumentEntryHelper.GetDocTypeName(doc.Header.DocumentType.Value),
                PostingDate = doc.Header.PostingDate,
                DocumentDate = doc.Header.DocumentDate,
                DocumentDateFormat = doc.Header.DocumentDate?.ToString("dd/MM/yyyy") ?? string.Empty,
                Status = doc.Header .Status,
                StatusName = DocumentEntryHelper.GetStatusName(doc.Header.Status.Value),
                QRCodes = doc.Entries.Where(e => !string.IsNullOrEmpty(e.QRCode)).Select(e => e.QRCode).Distinct().ToList(),
            }).ToList();
        }

    }
}
