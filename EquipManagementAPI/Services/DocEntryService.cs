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

        public async Task<List<DocumentEntryHeaderDTO>> GetDocEntryHeader(int type)
        {
            //Dùng Where khi trả về nhiều dòng
            var documents = await _context.DocumentEntryHeader.Where(a => a.DocumentType == type).OrderByDescending(e=>e.PostingDate).ToListAsync();
            var exportUnitCode = documents.Select(e => e.ExportingUnit).Distinct().ToList();
           var receiveUnitCode = documents.Select(e => e.ReceivingUnit).Where(code => !string.IsNullOrEmpty(code)).Distinct().ToList();
                
            var departments = await _context.Department.ToListAsync(); // lấy toàn bộ
            var receiveUnit = departments
            .Where(d => receiveUnitCode.Contains(d.Code))
            .ToDictionary(d => d.Code, d => d.Name);

            var exportUnit = departments
                .Where(d => exportUnitCode.Contains(d.Code))
                .ToDictionary(d => d.Code, d => d.Name);


            var result = documents.Select(doc => new DocumentEntryHeaderDTO
            {
                No = doc.No,
                ReceivingUnit = doc.ReceivingUnit,
                ReceivedUnitName = !string.IsNullOrEmpty(doc.ReceivingUnit) && receiveUnit.ContainsKey(doc.ReceivingUnit)
                ? receiveUnit[doc.ReceivingUnit]
                : "",
                ExportingUnit = doc.ExportingUnit,
                ExportingUnitName = !string.IsNullOrEmpty(doc.ExportingUnit) && exportUnit.ContainsKey(doc.ExportingUnit)
                ? exportUnit[doc.ExportingUnit]
                : "",
                DocumentType = doc.DocumentType,
                DocType = DocumentEntryHelper.GetDocTypeName((int)doc.DocumentType),
                PostingDate = doc.PostingDate,
                DocumentDate = doc.DocumentDate,
                DocumentDateFormat = doc.DocumentDate?.ToString("dd/MM/yyyy") ?? "",
                Status = doc.Status,
                StatusName = DocumentEntryHelper.GetStatusName((int)doc.Status)
            }).ToList() ;

            return result;
        }

        public async Task<List<DocumentEntryHeaderDTO>> GetDocEntryHeader_other(int type)
        {
            var documents = await _context.DocumentEntryHeader.Where(a => a.DocumentType == type && a.Status == 0).OrderByDescending(e=>e.PostingDate).ToListAsync();
            var departments = await _context.Department.ToListAsync(); // lấy toàn bộ
            var vendors = await _context.Vendor.ToListAsync();

            var receiveUnitCode = documents.Select(e => e.ReceivingUnit).Where(code => !string.IsNullOrEmpty(code)).Distinct().ToList();
            Dictionary<string,string> receiveUnit = new Dictionary<string, string>();
            var exportUnitCode = documents.Select(e => e.ExportingUnit).Distinct().ToList();
            Dictionary<string,string> exportUnit = new Dictionary<string,string>(); 
            if (type == 11 || type == 12 || type == 13 || type == 6 || type ==16)
            {
                receiveUnit = vendors.Where(e => receiveUnitCode.Contains(e.No)).ToDictionary(e => e.No, e => e.Name);
                exportUnit = departments.Where(e=>exportUnitCode.Contains(e.Code)).ToDictionary(e=>e.Code, e => e.Name);
            }    
            else
            {
                 receiveUnit = departments
                .Where(d => receiveUnitCode.Contains(d.Code))
                .ToDictionary(d => d.Code, d => d.Name);
                 exportUnit = vendors.Where(e => exportUnitCode.Contains(e.No)).ToDictionary(e => e.No, e => e.Name);
            }
                
            
            var result = documents.Select(doc => new DocumentEntryHeaderDTO
            {
                No = doc.No,
                ReceivingUnit = doc.ReceivingUnit,
                ReceivedUnitName = !string.IsNullOrEmpty(doc.ReceivingUnit) && receiveUnit.ContainsKey(doc.ReceivingUnit)
                ? receiveUnit[doc.ReceivingUnit]
                : "",
                ExportingUnit = doc.ExportingUnit,
                ExportingUnitName = !string.IsNullOrEmpty(doc.ExportingUnit) && exportUnit.ContainsKey(doc.ExportingUnit)
                ? exportUnit[doc.ExportingUnit]
                : "",
                DocumentType = doc.DocumentType,
                DocType = DocumentEntryHelper.GetDocTypeName((int)doc.DocumentType),
                PostingDate = doc.PostingDate,
                DocumentDate = doc.DocumentDate,
                DocumentDateFormat = doc.DocumentDate?.ToString("dd/MM/yyyy") ?? "",
                Status = doc.Status,
                StatusName = DocumentEntryHelper.GetStatusName((int)doc.Status)
            }).ToList();

            return result;
        }
    }
}
