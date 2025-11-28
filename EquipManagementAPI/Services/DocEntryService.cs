using EquipManagementAPI.Data;
using EquipManagementAPI.Helpers;
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
            var documents = await (from e in _context.DocumentEntryHeader
                                   where e.DocumentType == (int?)type && e.CheckQR == (int?)0 && e.Status == (int?)0
                                   orderby e.PostingDate descending
                                   select e).ToListAsync();

            var exportUnitCode = documents.Select(e => e.ExportingUnit).Distinct().ToList();

            var receiveUnitCode = documents
                .Select(e => e.ReceivingUnit)
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
                No = doc.No,
                ReceivingUnit = doc.ReceivingUnit,
                ReceivedUnitName = !string.IsNullOrEmpty(doc.ReceivingUnit) && receiveUnit.ContainsKey(doc.ReceivingUnit)
                    ? receiveUnit[doc.ReceivingUnit]
                    : string.Empty,
                ExportingUnit = doc.ExportingUnit,
                ExportingUnitName = !string.IsNullOrEmpty(doc.ExportingUnit) && exportUnit.ContainsKey(doc.ExportingUnit)
                    ? exportUnit[doc.ExportingUnit]
                    : string.Empty,
                DocumentType = doc.DocumentType,
                DocType = DocumentEntryHelper.GetDocTypeName(doc.DocumentType.Value),
                PostingDate = doc.PostingDate,
                DocumentDate = doc.DocumentDate,
                DocumentDateFormat = doc.DocumentDate?.ToString("dd/MM/yyyy") ?? string.Empty,
                Status = doc.Status,
                StatusName = DocumentEntryHelper.GetStatusName(doc.Status.Value)
            }).ToList();
        }

        public async Task<List<DocumentEntryHeaderDTO>> GetDocEntryHeader_other(int type) //Đơn vị ngoài
        {
            var documents = await (from e in _context.DocumentEntryHeader
                                   where e.DocumentType == (int?)type && e.CheckQR == (int?)0 && e.Status == (int?)0
                                   orderby e.PostingDate descending
                                   select e).ToListAsync();

            var departments = await _context.Department.ToListAsync();
            var vendors = await _context.Vendor.ToListAsync();

            var receiveUnitCode = documents
                .Select(e => e.ReceivingUnit)
                .Where(code => !string.IsNullOrEmpty(code))
                .Distinct()
                .ToList();

            var exportUnitCode = documents.Select(e => e.ExportingUnit).Distinct().ToList();

            Dictionary<string, string> receiveUnit;
            Dictionary<string, string> exportUnit;

            if (type == 11 || type == 12 || type == 13 || type == 6 || type == 16)
            {
                receiveUnit = vendors
                    .Where(e => receiveUnitCode.Contains(e.No))
                    .ToDictionary(e => e.No, e => e.Name);

                exportUnit = departments
                    .Where(d => exportUnitCode.Contains(d.Code))
                    .ToDictionary(d => d.Code, d => d.Name);
            }
            else
            {
                receiveUnit = departments
                    .Where(d => receiveUnitCode.Contains(d.Code))
                    .ToDictionary(d => d.Code, d => d.Name);

                exportUnit = vendors
                    .Where(e => exportUnitCode.Contains(e.No))
                    .ToDictionary(e => e.No, e => e.Name);
            }

            return documents.Select(doc => new DocumentEntryHeaderDTO
            {
                No = doc.No,
                ReceivingUnit = doc.ReceivingUnit,
                ReceivedUnitName = !string.IsNullOrEmpty(doc.ReceivingUnit) && receiveUnit.ContainsKey(doc.ReceivingUnit)
                    ? receiveUnit[doc.ReceivingUnit]
                    : string.Empty,
                ExportingUnit = doc.ExportingUnit,
                ExportingUnitName = !string.IsNullOrEmpty(doc.ExportingUnit) && exportUnit.ContainsKey(doc.ExportingUnit)
                    ? exportUnit[doc.ExportingUnit]
                    : string.Empty,
                DocumentType = doc.DocumentType,
                DocType = DocumentEntryHelper.GetDocTypeName(doc.DocumentType.Value),
                PostingDate = doc.PostingDate,
                DocumentDate = doc.DocumentDate,
                DocumentDateFormat = doc.DocumentDate?.ToString("dd/MM/yyyy") ?? string.Empty,
                Status = doc.Status,
                StatusName = DocumentEntryHelper.GetStatusName(doc.Status.Value)
            }).ToList();
        }

    }
}
