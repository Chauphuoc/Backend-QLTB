using EquipManagementAPI.Data;
using EquipManagementAPI.Exceptions;
using EquipManagementAPI.Models;
using EquipManagementAPI.Models.DTOs;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Data;

namespace EquipManagementAPI.Services
{
    public class ProductionPlanService:IProductionPlanService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ProductionPlanService> _logger;
        public ProductionPlanService(ApplicationDbContext context, ILogger<ProductionPlanService> logger)
        {
            _context = context;
            _logger = logger;
        }
        // Sai số cho phép khi so sánh số thực (đúng theo spec đề bài)
        private const decimal RatioTolerance = 0.01m;
        private const decimal PriceTolerance = 0.0001m;

        public async Task<CmSplitBatchResponse> CreateCmSplitAsync(string styleId,CmSplitBatchRequest request,string? userId)
        {
            // ── 1. Validate ──────────────────────────────────────────────────────
            if (string.IsNullOrWhiteSpace(styleId))
                throw new BusinessValidationException("styleId là bắt buộc");

            if (request.Splits is null)
                throw new BusinessValidationException("Splits không được null");

            if (request.Splits.Any())
            {
                foreach (var (split, i) in request.Splits.Select((s, i) => (s, i)))
                {
                    if (string.IsNullOrWhiteSpace(split.OperationCode))
                        throw new BusinessValidationException(
                            $"splits[{i}].OperationCode là bắt buộc");

                    if (string.IsNullOrWhiteSpace(split.OperationName))
                        throw new BusinessValidationException(
                            $"splits[{i}].OperationName là bắt buộc");

                    if (split.SplitPrice <= 0)
                        throw new BusinessValidationException(
                            $"splits[{i}].SplitPrice phải lớn hơn 0");
                }

                var dupCode = request.Splits
                    .GroupBy(s => s.OperationCode.Trim().ToUpper())
                    .FirstOrDefault(g => g.Count() > 1)?.Key;

                if (dupCode is not null)
                    throw new BusinessValidationException(
                        $"Mã công đoạn \"{dupCode}\" bị trùng");
            }

            // ── 2. Transaction ───────────────────────────────────────────────────
            await using var transaction = await _context.Database
                .BeginTransactionAsync();
            try
            {
                // ── 3. Lấy giá CM active ─────────────────────────────────────────
                var cmPrice = await _context.StyleCmPrice
                    .Where(p => p.StyleId == styleId && p.IsActive)
                    .OrderByDescending(p => p.EffectiveDate)
                    .FirstOrDefaultAsync()
                    ?? throw new BusinessValidationException(
                        $"Style \"{styleId}\" chưa có giá CM. " +
                        "Vui lòng nhập giá CM trước khi phân bổ công đoạn.");

                // ── 4. Validate tổng SplitPrice = CmPrice ────────────────────────
                if (request.Splits.Any())
                {
                    var total = request.Splits.Sum(s => s.SplitPrice);
                    var diff = Math.Abs(total - cmPrice.CmPrice);
                    if (diff > 0.0001m)
                        throw new BusinessValidationException(
                            $"Tổng giá phân bổ ({total:F4}) " +
                            $"≠ giá CM gốc ({cmPrice.CmPrice:F4}). " +
                            $"Chênh lệch: {diff:F4}");
                }

                // ── 5. Xoá splits cũ ─────────────────────────────────────────────
                var oldSplits = await _context.StyleCmSplits
                    .Where(s => s.StyleCmPriceId == cmPrice.Id)
                    .ToListAsync();

                _context.StyleCmSplits.RemoveRange(oldSplits);

                // ── 6. Insert splits mới ──────────────────────────────────────────
                var savedEntities = new List<StyleCmSplit>();

                if (request.Splits.Any())
                {
                    var now = DateTime.UtcNow;

                    for (int i = 0; i < request.Splits.Count; i++)
                    {
                        var item = request.Splits[i];
                        var ratio = Math.Round(
                            item.SplitPrice / cmPrice.CmPrice * 100, 2);

                        savedEntities.Add(new StyleCmSplit
                        {
                            StyleCmPriceId = cmPrice.Id,
                            OperationCode = item.OperationCode.Trim().ToUpper(),
                            OperationName = item.OperationName?.Trim(),
                            SplitPrice = item.SplitPrice,
                            SplitRatio = ratio,
                            DefaultLineId = item.DefaultLineId?.ToString(),
                            EffectiveFrom = now,
                            CreatedAt = now,
                            CreatedBy = userId,
                        });
                    }

                    // Điều chỉnh item cuối — tổng SplitRatio = 100.00 chính xác
                    var ratioSum = savedEntities.Sum(s => s.SplitRatio);
                    var gap = Math.Round(100m - ratioSum, 2);
                    if (Math.Abs(gap) > 0.01m)
                        savedEntities[^1].SplitRatio =
                            Math.Round(savedEntities[^1].SplitRatio + gap, 2);

                    _context.StyleCmSplits.AddRange(savedEntities);
                }

                // ── 7. Cập nhật OperationCode trong MesFactoryPlan ───────────────
                // Với các splits có DefaultLineId → tự động gán operation_code
                // cho các plan đang chạy style đó trên tổ tương ứng
                var splitsWithDefault = savedEntities
                    .Where(s => !string.IsNullOrEmpty(s.DefaultLineId))
                    .ToList();

                if (splitsWithDefault.Any())
                {
                    // Lấy tất cả plan active của style này
                    var plans = await _context.MesFactoryPlans
                        .Where(p => p.StyleId == styleId
                                 && p.IsActive
                                 && p.LineNumber != null)
                        .ToListAsync();

                    foreach (var plan in plans)
                    {
                        // Tìm split có DefaultLineId khớp với LineNumber của plan
                        var matchedSplit = splitsWithDefault
                            .FirstOrDefault(s => s.DefaultLineId == plan.LineNumber);

                        if (matchedSplit is not null)
                        {
                            plan.OperationCode = matchedSplit.OperationCode;
                            plan.UpdatedAt = DateTime.UtcNow;
                        }
                        // Nếu không match → giữ nguyên OperationCode cũ
                        // (không reset về null, tránh mất cấu hình đã gán thủ công)
                    }
                }

                // ── 8. Cảnh báo nếu có splits đổi OperationCode ──────────────────
                // Các plan đang dùng operation_code cũ không còn trong splits mới
                // → log warning, không tự reset (để user xử lý thủ công)
                var newCodes = savedEntities.Select(s => s.OperationCode).ToList();
                var affectedPlans = await _context.MesFactoryPlans
                    .Where(p => p.StyleId == styleId
                             && p.IsActive
                             && p.OperationCode != null
                             && !newCodes.Contains(p.OperationCode!))
                    .CountAsync();

                if (affectedPlans > 0)
                    _logger.LogWarning(
                        "[CreateCmSplitAsync] {Count} factory_plan của style {StyleId} " +
                        "đang dùng operation_code không còn tồn tại sau khi đổi splits. " +
                        "Cần user gán lại thủ công.",
                        affectedPlans, styleId);
                cmPrice.IsOperation = true;
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // ── 9. Build response ─────────────────────────────────────────────
                return new CmSplitBatchResponse
                {
                    Validated = true,
                    Splits = savedEntities.Select(s => new CmSplitDto
                    {
                        Id = s.Id,
                        OperationCode = s.OperationCode,
                        OperationName = s.OperationName,
                        SplitPrice = s.SplitPrice,
                        DefaultLineId = s.DefaultLineId,
                    }).ToList(),
                };
            }
            catch (BusinessValidationException)
            {
                await transaction.RollbackAsync();
                throw;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex,
                    "[CreateCmSplitAsync] styleId={StyleId}", styleId);
                throw;
            }
        }


        //public Task DeleteSplitsAsync(int cmPriceId)
        //{
        //    throw new NotImplementedException();
        //}

        //public Task<List<CmPriceDtos>> GetSplitsAsync(int cmPriceId)
        //{
        //    throw new NotImplementedException();
        //}

        // ---------------------------------------------------------------
        // GET /api/mes/cm-prices?factoryId=&year=&month=
        // ---------------------------------------------------------------
        public async Task<List<CmPriceListItemDto>> GetCmPricesAsync(string factoryId, int year, int month, int toYear, int toMonth)
        {
            if (month < 1 || month > 12)
                throw new BusinessValidationException("month phải nằm trong khoảng 1-12");

            var periodStart = new DateOnly(year, month, 1);
            var toMonthEnd = new DateOnly(toYear, toMonth, 1).AddMonths(1).AddDays(-1);

            // 1) Lấy danh sách styleId có kế hoạch tại factory trong kỳ
            var styleIds = await _context.MesProductionOrderItems
                .Where(fp => fp.FactoryId == factoryId
                             && fp.TargetDeliveryDate >= periodStart
                             && fp.TargetDeliveryDate < toMonthEnd)
                .Select(fp => fp.StyleId)
                .Distinct()
                .ToListAsync();

            if (styleIds.Count == 0)
                return new List<CmPriceListItemDto>();

            // 2) Lấy các bản ghi giá còn hiệu lực trong kỳ (đúng như comment mô tả)
            var periodStartDt = periodStart.ToDateTime(TimeOnly.MinValue);
            var toMonthEndDt = toMonthEnd.ToDateTime(TimeOnly.MaxValue);
            // dùng MaxValue để bao trọn cả ngày cuối kỳ (vì EffectiveDate có thể có giờ/phút)

            var cmPricesRaw = await _context.StyleCmPrice
                .Where(p => styleIds.Contains(p.StyleId)
                            && p.EffectiveDate <= toMonthEndDt
                            && (p.ExpiredDate == null || p.ExpiredDate > periodStartDt))
                .Include(p => p.StyleCmSplits)
                .AsNoTracking()
                .ToListAsync();

            // Nếu 1 style có nhiều version hiệu lực trong kỳ -> lấy version có EffectiveDate mới nhất
            var cmPriceByStyle = cmPricesRaw
                .GroupBy(p => p.StyleId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.EffectiveDate).First());

            // 3) Lấy thông tin style
            var styles = await _context.Item
                .Where(s => styleIds.Contains(s.No))
                .AsNoTracking()
                .ToDictionaryAsync(s => s.No, s => s);

            // 4) Duyệt theo TOÀN BỘ styleIds, không phải theo cmPrices
            var result = styleIds
                .OrderBy(id => id)
                .Select(styleId =>
                {
                    styles.TryGetValue(styleId, out var style);
                    cmPriceByStyle.TryGetValue(styleId, out var price);

                    return new CmPriceListItemDto
                    {
                        StyleId = styleId,
                        StyleName = style?.Name ?? string.Empty,
                        CmPrice = price?.CmPrice ?? 0,        // null nếu chưa có giá
                        Currency = price?.Currency,
                        Splits = price?.StyleCmSplits
                            .OrderBy(s => s.OperationCode)
                            .Select(s => new CmSplitDto
                            {
                                Id = s.Id,
                                OperationCode = s.OperationCode,
                                OperationName = s.OperationName,
                                SplitPrice = s.SplitPrice,
                                DefaultLineId = s.DefaultLineId
                            })
                            .ToList() ?? new List<CmSplitDto>()
                    };
                })
                .ToList();

            return result;
        }

        public async Task<List<OperationResponse>> GetOperation()
        {
            var result = await _context.itemGroup2
                .Select(p => new OperationResponse
                {
                    OperationCode = p.Code,
                    Name = p.Description
                })
                .ToListAsync();

            return result;
        }

        public async Task<List<OperationResponse>> GetOperationByStyle(string styleId)
        {
            var result = await _context.StyleCmPrice
                .Where(p => p.StyleId == styleId && p.IsActive == true) // lọc price đang active nếu cần
                .Join(_context.StyleCmSplits,
                      price => price.Id,
                      split => split.StyleCmPriceId,
                      (price, split) => new OperationResponse
                      {
                          OperationCode = split.OperationCode,
                          Name = split.OperationName
                      })
                .ToListAsync();

            return result;
        }

        public async Task<List<OperationResponse>> GetCMPriceOperationAsync(string styleId)
        {
            var result = await _context.StyleCmPrice
                .Where(p => p.StyleId == styleId && p.IsActive == true) // lọc price đang active nếu cần
                .Join(_context.StyleCmSplits,
                      price => price.Id,
                      split => split.StyleCmPriceId,
                      (price, split) => new OperationResponse
                      {
                          OperationCode = split.OperationCode,
                          Name = split.OperationName,
                          Price = split.SplitPrice.ToString()
                      })
                .ToListAsync();

            return result;
        }
        // ---------------------------------------------------------------
        // POST /api/mes/cm-prices
        // ---------------------------------------------------------------
        public async Task<CmPriceCreateResponse> CreateCmPriceAsync(
            CmPriceCreateRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.StyleId))
                throw new BusinessValidationException("styleId không được để trống");

            if (request.CmPrice <= 0)
                throw new BusinessValidationException("cmPrice phải lớn hơn 0");

            if (string.IsNullOrWhiteSpace(request.Currency))
                throw new BusinessValidationException("currency không được để trống");

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var today = DateTime.UtcNow.Date;

                // Auto expire các version đang active của style này
                var activeOldVersions = await _context.StyleCmPrice
                    .Where(p => p.StyleId == request.StyleId && p.IsActive)
                    .ToListAsync();

                foreach (var old in activeOldVersions)
                {
                    old.IsActive = false;
                    old.ExpiredDate = today;
                }

                var newVersion = new StyleCmPrice
                {
                    StyleId = request.StyleId,
                    CmPrice = request.CmPrice,
                    Currency = request.Currency,
                    EntryMethod = null,
                    EffectiveDate =  today ,
                    ExpiredDate = null,
                    IsActive = true,
                    CreatedBy = request.UserId,
                    CreatedAt = DateTime.UtcNow,
                    IsOperation = false
                };

                _context.StyleCmPrice.Add(newVersion);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return new CmPriceCreateResponse
                {
                    StyleId = newVersion.StyleId,
                    CmPrice = newVersion.CmPrice,
                    Currency = newVersion.Currency,
                    EffectiveDate = newVersion.EffectiveDate
                };
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        public async Task<bool> StyleHasCmSplitsAsync(string styleId)
        {
            return await _context.StyleCmSplits
                .AnyAsync(s => s.StyleCmPrice!.StyleId == styleId
                            && s.StyleCmPrice.IsActive);
        }

        // API 1: PREVIEW — chỉ tính toán, không ghi DB
        // =====================================================================
        //public async Task<MergePreviewResponse> PreviewAsync(MergePreviewRequest request)
        //{
        //    if (request.PoIds is null || request.PoIds.Count < 2)
        //        throw new BusinessValidationException("Cần chọn ít nhất 2 PO để gom.");

        //    var source = await _context.MesFactoryPlans
        //        .Where(fp => request.PoIds.Contains(fp.PoId)
        //                  && fp.StyleId == request.StyleId && fp.ColorId == request.Color
        //                  && fp.FactoryId == request.FactoryId
        //                  && fp.IsActive
        //                  && fp.ParentPlanId == null)
        //        .ToListAsync();

        //    if (source.Count == 0)
        //        throw new BusinessValidationException("Không tìm thấy kế hoạch active nào ứng với các PO đã chọn.");

        //    if (source.Select(s => s.StyleId).Distinct().Count() > 1
        //        || source.Select(s => s.ColorId).Distinct().Count() > 1)
        //        throw new BusinessValidationException("Các PO được chọn không cùng Mã hàng/Màu, không thể gom.");

        //    var foundPoIds = source.Select(s => s.PoId).Distinct().ToList();
        //    var missingPoIds = request.PoIds.Except(foundPoIds).ToList();

        //    // Actual = SUM(Pass Output Quantity) từ QLCL, group theo plan_id
        //    var sourceIds = source.Select(s => s.Id).ToList();
        //    var actualQuery = _context.qualityManagementSessions
        //        .Where(q => q.PlanId.HasValue && sourceIds.Contains(q.PlanId.Value))
        //        .GroupBy(q => q.PlanId.Value)
        //        .Select(g => new
        //        {
        //            PlanId = g.Key,
        //            ActualQty = g.Sum(x => x.PassOutputQuantity)
        //        });

        //    var actualByPlan = await actualQuery.ToDictionaryAsync(x => x.PlanId, x => x.ActualQty);

        //    // ===== Xác định Style có phân công đoạn hay không (StyleCmPrice.IsOperation) =====
        //    var cmPriceEntity = await _context.StyleCmPrice
        //        .Where(e => e.IsActive == true && e.StyleId == request.StyleId)
        //        .FirstOrDefaultAsync();

        //    bool isOperation = cmPriceEntity?.IsOperation == true;

        //    decimal cmPrice = 0;
        //    var cmPriceDetails = new List<CmPriceDetailDto>();
        //    if (cmPriceEntity != null)
        //    {
        //        if (!isOperation)
        //        {
        //            cmPrice = cmPriceEntity.CmPrice;
        //        }
        //        else
        //        {
        //            cmPriceDetails = await _context.StyleCmSplits
        //                .Where(s => s.StyleCmPriceId == cmPriceEntity.Id)
        //                .Select(s => new CmPriceDetailDto
        //                {
        //                    Name = s.OperationName,
        //                    OperationCode = s.OperationCode,
        //                    SplitPrice = s.SplitPrice
        //                })
        //                .ToListAsync();
        //        }
        //    }
        //    // else: không có dữ liệu -> cmPrice giữ nguyên = 0

        //    // ===== Team breakdown (hiển thị) =====
        //    var assignedRows = source.Where(s => s.LineNumber != null).ToList();
        //    var unassignedRows = source.Where(s => s.LineNumber == null).ToList();

         
        //    int unassignedQty;
        //    if (isOperation)
        //    {
        //        var assignedMaxPerPo = assignedRows
        //            .GroupBy(s => s.PoId)
        //            .ToDictionary(g => g.Key, g => g.Max(x => x.TotalPlannedQty));

        //        unassignedQty = unassignedRows.Sum(u =>
        //        {
        //            var alreadyAssigned = assignedMaxPerPo.TryGetValue(u.PoId, out var a) ? a : 0;
        //            return Math.Max(0, u.TotalPlannedQty - alreadyAssigned);
        //        });
        //    }
        //    else
        //    {
        //        unassignedQty = unassignedRows.Sum(x => x.TotalPlannedQty);
        //    }
        //    var lineNumbers = assignedRows
        //        .Select(s => s.LineNumber!)
        //        .Distinct()
        //        .ToList();

        //    var lineNumberNameMap = await _context.workShift
        //        .Where(w => lineNumbers.Contains(w.Code))
        //        .ToDictionaryAsync(w => w.Code, w => w.Name);
        //    var teamBreakdown = assignedRows
        //        .GroupBy(s => new { s.LineNumber, OperationCode = isOperation ? s.OperationCode : null })
        //        .Select(g =>
        //        {
        //            var assigned = g.Sum(x => x.TotalPlannedQty);
        //            var actual = g.Sum(x => actualByPlan.TryGetValue(x.Id, out var a) ? a : 0);
        //            lineNumberNameMap.TryGetValue(g.Key.LineNumber!, out var lineNumberName);
        //            return new TeamBreakdownDto
        //            {
        //                LineNumber = g.Key.LineNumber,
        //                LineNumberName = lineNumberName ?? string.Empty,
        //                OperationCode = g.Key.OperationCode,
        //                AssignedQty = assigned,
        //                ActualQty = actual,
        //                RemainingQty = assigned - actual
        //            };
        //        })
        //        .ToList();

        //    if (unassignedRows.Count > 0)
        //    {
        //        teamBreakdown.Add(new TeamBreakdownDto
        //        {
        //            LineNumber = null,
        //            OperationCode = null,
        //            AssignedQty = unassignedQty,   // đã net-trừ đúng, không còn double-count
        //            ActualQty = 0,
        //            RemainingQty = unassignedQty
        //        });
        //    }

        //    teamBreakdown = teamBreakdown
        //        .OrderBy(t => t.LineNumber == null ? 1 : 0)
        //        .ThenBy(t => t.LineNumber)
        //        .ThenBy(t => t.OperationCode)
        //        .ToList();

        //    // ===== Tổng số lượng KH — tránh đếm trùng khi Style có phân công đoạn =====
        //    int totalAssigned;
        //    int? totalActual = null;
        //    int? totalRemaining = null;
        //    var operationBreakdown = new List<OperationActualDto>();

        //    if (!isOperation)
        //    {
        //        totalAssigned = source.Sum(x => x.TotalPlannedQty);
        //        totalActual = source.Sum(x => actualByPlan.TryGetValue(x.Id, out var a) ? a : 0);
        //        totalRemaining = totalAssigned - totalActual;
        //    }
        //    else
        //    {
        //        // assignedPerPo: mỗi po_id chỉ tính 1 lần cho phần đã phân tổ — lấy MAX giữa các công
        //        // đoạn của cùng po_id, vì các dòng operation khác nhau của cùng po_id đang theo dõi
        //        // cùng 1 lô sản phẩm. unassignedQty đã được tính net (trừ đúng phần đã phân tổ) ở trên.
        //        var assignedPerPo = assignedRows
        //            .GroupBy(s => s.PoId)
        //            .Sum(g => g.Max(x => x.TotalPlannedQty));

        //        totalAssigned = assignedPerPo + unassignedQty;
        //        // totalActual / totalRemaining CỐ Ý để null — xem operationBreakdown bên dưới.

        //        operationBreakdown = assignedRows
        //            .GroupBy(s => s.OperationCode)
        //            .Select(g =>
        //            {
        //                var opActual = g.Sum(x => actualByPlan.TryGetValue(x.Id, out var a) ? a : 0);
        //                var opName = cmPriceDetails.FirstOrDefault(c => c.OperationCode == g.Key)?.Name;
        //                return new OperationActualDto
        //                {
        //                    OperationCode = g.Key ?? string.Empty,
        //                    OperationName = opName,
        //                    ActualQty = opActual,
        //                    RemainingQty = totalAssigned - opActual
        //                };
        //            })
        //            .OrderBy(o => o.OperationCode)
        //            .ToList();
        //    }

        //    return new MergePreviewResponse
        //    {
        //        StyleId = source[0].StyleId,
        //        ColorId = source[0].ColorId,
        //        TotalNewPlanQty = totalAssigned,
        //        TotalActual = totalActual,
        //        TotalRemaining = totalRemaining,
        //        IsOperation = isOperation,
        //        OperationBreakdown = operationBreakdown,
        //        CmPrice = cmPrice,
        //        CmPriceDetails = cmPriceDetails,
        //        TeamBreakdown = teamBreakdown,
        //        //MissingPoIds = missingPoIds
        //    };
        //}

        // =====================================================================
        // API 2: EXECUTE — transactional, tạo PO Gom (1 dòng / Tổ), đánh dấu nguồn merged
        // =====================================================================
        private async Task<string> GenerateNewPoCodeAsync(string factoryId)
        {
            var period = DateTime.Now.ToString("yyyyMM");

            var seqRow = await _context.MesPoCodeSequences
                .FromSqlInterpolated($@"
            SELECT *
            FROM mes.po_code_sequence WITH (UPDLOCK, HOLDLOCK)
            WHERE factory_id = {factoryId}
              AND period = {period}")
                .FirstOrDefaultAsync();

            int nextSeq;

            if (seqRow == null)
            {
                nextSeq = 1;

                _context.MesPoCodeSequences.Add(new MesPoCodeSequences
                {
                    FactoryId = factoryId,
                    Period = period,
                    LastSeq = nextSeq,
                    UpdatedAt = DateTime.Now
                });
            }
            else
            {
                nextSeq = seqRow.LastSeq + 1;

                seqRow.LastSeq = nextSeq;
                seqRow.UpdatedAt = DateTime.Now;
            }

            return $"GOM-{factoryId}-{period}-{nextSeq:D4}";
        }

        //public async Task<MergeExecuteResponse> ExecuteAsync(MergeExecuteRequest request)
        //{
        //    if (request.PoIds is null || request.PoIds.Count < 2)
        //        throw new BusinessValidationException("Cần chọn ít nhất 2 PO để gom.");

        //    // Serializable để giảm rủi ro race condition (2 user cùng gom 1 lúc).
        //    using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        //    try
        //    {
        //        var source = await _context.MesFactoryPlans
        //            .Where(fp => request.PoIds.Contains(fp.PoId)
        //                      && fp.StyleId == request.StyleId && fp.ColorId == request.Color
        //                      && fp.FactoryId == request.FactoryId
        //                      && fp.IsActive
        //                      && fp.ParentPlanId == null)
        //            .ToListAsync();

        //        if (source.Count == 0)
        //            throw new BusinessValidationException("Không tìm thấy kế hoạch hợp lệ để gom (có thể vừa bị gom bởi thao tác khác).");

        //        if (source.Select(s => s.StyleId).Distinct().Count() > 1
        //            || source.Select(s => s.ColorId).Distinct().Count() > 1)
        //            throw new BusinessValidationException("Các PO được chọn không cùng Mã hàng/Màu.");

        //        // Cùng Style => phải cùng production_order_id (dữ liệu gốc từ ERP)
        //        if (source.Select(s => s.ProductionOrderId).Distinct().Count() > 1)
        //            throw new BusinessValidationException("Dữ liệu bất thường: các PO cùng Style nhưng khác production_order_id, không thể gom.");

        //        if (source.Select(s => s.VersionId).Distinct().Count() > 1)
        //            throw new BusinessValidationException("Các PO được chọn thuộc các phiên bản kế hoạch khác nhau, không thể gom.");

        //        var newPoCode = await GenerateNewPoCodeAsync(request.FactoryId);

        //        // Vẫn giữ check trùng để chắc chắn (phòng dữ liệu cũ/migrate lẫn vào)
        //        var newPoIdExists = await _context.MesFactoryPlans
        //            .AnyAsync(fp => fp.PoId == newPoCode && fp.FactoryId == request.FactoryId && fp.IsActive);
        //        if (newPoIdExists)
        //            throw new BusinessValidationException("Mã PO Gom vừa sinh bị trùng, vui lòng thử lại.");

        //        // ===== Style có phân công đoạn hay không -> nguồn giá CM =====
        //        var cmPriceEntity = await _context.StyleCmPrice
        //            .Where(e => e.IsActive == true && e.StyleId == request.StyleId)
        //            .FirstOrDefaultAsync();

        //        bool isOperation = cmPriceEntity?.IsOperation == true;

        //        decimal? flatCmPrice = null;
        //        var operationPriceMap = new Dictionary<string, decimal>();

        //        if (cmPriceEntity != null)
        //        {
        //            if (!isOperation)
        //                flatCmPrice = cmPriceEntity.CmPrice;
        //            else
        //                operationPriceMap = await _context.StyleCmSplits
        //                    .Where(s => s.StyleCmPriceId == cmPriceEntity.Id)
        //                    .ToDictionaryAsync(s => s.OperationCode, s => s.SplitPrice);
        //        }
        //        flatCmPrice ??= request.FallbackCmPrice; // style chưa cấu hình giá -> dùng giá fallback nếu có

        //        var styleId = source[0].StyleId;
        //        var colorId = source[0].ColorId;
        //        var productionOrderId = source[0].ProductionOrderId;
        //        var versionId = source[0].VersionId;
        //        var now = DateTime.UtcNow;

        //        var assignedRows = source.Where(s => s.LineNumber != null).ToList();
        //        var unassignedRows = source.Where(s => s.LineNumber == null).ToList();

        //        // Net-trừ phần đã phân tổ khỏi dòng "chưa phân tổ" (đồng bộ fix đã áp dụng ở PreviewAsync —
        //        // dòng line_number=NULL lưu NGUYÊN tổng gốc của po_id, không tự trừ phần đã phân tổ qua
        //        // các dòng công đoạn khác).
        //        int unassignedQty;
        //        if (isOperation)
        //        {
        //            var assignedMaxPerPo = assignedRows
        //                .GroupBy(s => s.PoId)
        //                .ToDictionary(g => g.Key, g => g.Max(x => x.TotalPlannedQty));

        //            unassignedQty = unassignedRows.Sum(u =>
        //            {
        //                var already = assignedMaxPerPo.TryGetValue(u.PoId, out var a) ? a : 0;
        //                return Math.Max(0, u.TotalPlannedQty - already);
        //            });
        //        }
        //        else
        //        {
        //            unassignedQty = unassignedRows.Sum(x => x.TotalPlannedQty);
        //        }

        //        // ===== Nhóm theo Tổ (+ Công đoạn nếu Style có phân công đoạn) =====
        //        // Mỗi nhóm -> 1 dòng factory_plans mới. Không phân công đoạn: nhóm theo Tổ như cũ.
        //        var groups = new List<MergeGroupPlan>();


        //        foreach (var g in assignedRows.GroupBy(s => new
        //        {
        //            s.LineNumber,
        //            OperationCode = isOperation ? s.OperationCode : null
        //        }))
        //        {
        //            groups.Add(new MergeGroupPlan
        //            {
        //                NewId = Guid.NewGuid(),
        //                LineNumber = g.Key.LineNumber,
        //                OperationCode = g.Key.OperationCode,
        //                TotalQty = g.Sum(x => x.TotalPlannedQty),
        //                SourceIds = g.Select(x => x.Id).ToList()
        //            });
        //        }

        //        if (unassignedRows.Count > 0)
        //        {
        //            groups.Add(new MergeGroupPlan
        //            {
        //                NewId = Guid.NewGuid(),
        //                LineNumber = null,
        //                OperationCode = null,
        //                TotalQty = unassignedQty,
        //                SourceIds = unassignedRows.Select(x => x.Id).ToList()
        //            });
        //        }

        //        // ===== Tạo các dòng factory_plans mới =====
        //        var newPlans = groups.Select(g => new MesFactoryPlan
        //        {
        //            Id = g.NewId,
        //            PlanCode = BuildPlanCode(request.NewPoName, g.LineNumber, g.OperationCode),
        //            ProductionOrderId = productionOrderId,   // kế thừa từ PO nguồn
        //            VersionId = versionId,                    // kế thừa từ PO nguồn
        //            FactoryId = request.FactoryId,
        //            StyleId = styleId,

        //            PoId = newPoCode,
        //            ColorId = colorId,
        //            LineNumber = g.LineNumber,
        //            OperationCode = g.OperationCode,
        //            //ProductionStartDate = request.ProductionStartDate,
        //            BufferDays = request.BufferDays,
        //            TotalPlannedQty = g.TotalQty,              // Rule 6: 100% KH
        //            Status = "merged",                           // kế hoạch mới, chờ duyệt lại
        //            CreatedBy = request.CreatedBy,
        //            CreatedAt = now,
        //            UpdatedAt = now,
        //            IsActive = g.TotalQty > 0,                  // phòng case "chưa phân tổ" net về 0
        //            IsGroup = true,
        //            MergedBy = request.UserId, MergedAt = now,
        //            //CmPrice = g.OperationCode != null
        //            //    ? (operationPriceMap.TryGetValue(g.OperationCode, out var p) ? p : (decimal?)null)
        //            //    : flatCmPrice
        //        }).ToList();

        //        _context.MesFactoryPlans.AddRange(newPlans);

        //        // ===== Đánh dấu các dòng nguồn: trỏ về đúng nhóm, chuyển merged =====
        //        var parentIdBySourceId = groups
        //            .SelectMany(g => g.SourceIds.Select(sid => (sid, g.NewId)))
        //            .ToDictionary(x => x.sid, x => x.NewId);

        //        foreach (var s in source)
        //        {
        //            s.ParentPlanId = parentIdBySourceId[s.Id];
        //            s.Status = "merged_source";
        //            s.IsActive = false;
        //            s.MergedAt = now;
        //            s.MergedBy = request.CreatedBy;
        //            s.UpdatedAt = now;
        //        }

        //        // ===== Gom factory_plan_lines(kế hoạch theo ngày) theo(WorkDate, SizeId) =====
        //        var sourcePlanIds = source.Select(s => s.Id).ToList();
        //        var sourceLines = await _context.MesFactoryPlanLines
        //            .Where(l => sourcePlanIds.Contains(l.FactoryPlanId))
        //            .ToListAsync();

        //        if (sourceLines.Count > 0)
        //        {
        //            // parentIdBySourceId: factory_plans.Id (nguồn) -> factory_plans.Id (nhóm mới) — đã build ở trên.
        //            // Nhiều dòng factory_plans nguồn (khác PO, cùng Tổ/Công đoạn) có thể trỏ về CÙNG 1 nhóm mới
        //            // => các dòng factory_plan_lines cùng (WorkDate, SizeId) của các plan nguồn đó phải CỘNG DỒN
        //            // lại thành 1 dòng duy nhất dưới nhóm mới, tránh tạo nhiều dòng trùng ngày/size.
        //            //
        //            // LƯU Ý: đang gộp TOÀN BỘ lịch cũ (kể cả các work_date đã qua), không chỉ phần tương lai.
        //            // Vì demand_created_qty/remaining_qty hiện chưa dùng (=0), việc gộp này chỉ mang tính chất
        //            // sao chép lại lịch phân bổ theo ngày, không có rủi ro double-count sản lượng thực tế (Actual
        //            // vẫn lấy riêng từ QLCL theo plan_id). Nếu muốn CHỈ giữ lịch tương lai (đúng tinh thần "rải lịch
        //            // tiếp nối" đã thống nhất trước đó), thêm điều kiện lọc: .Where(l => l.WorkDate.Date >= now.Date)
        //            // trước khi group.

        //            var newLines = sourceLines
        //                .GroupBy(l => new
        //                {
        //                    NewPlanId = parentIdBySourceId[l.FactoryPlanId],
        //                    WorkDate = l.WorkDate.Date, // chuẩn hoá bỏ phần time, tránh lệch nhóm do time-of-day khác nhau
        //                    l.SizeId
        //                })
        //                .Select(g => new MesFactoryPlanLine
        //                {
        //                    Id = Guid.NewGuid(),
        //                    FactoryPlanId = g.Key.NewPlanId,
        //                    SizeId = g.Key.SizeId,
        //                    WorkDate = g.Key.WorkDate,
        //                    PlannedQty = g.Sum(x => x.PlannedQty),
        //                    DemandCreatedQty = 0, // chưa dùng, giữ mặc định theo đúng hiện trạng
        //                    UpdatedAt = now
        //                })
        //                .ToList();

        //            _context.MesFactoryPlanLines.AddRange(newLines);

        //            // MesFactoryPlanLine không có cột Status/IsActive/ParentLineId (khác với MesFactoryPlan và
        //            // MesProductionOrderItem) -> không cần update gì thêm ở các dòng nguồn. Chúng vẫn gắn với
        //            // factory_plans CŨ (đã chuyển is_active=0, status=merged ở bước trên), nên tự động trở thành
        //            // dữ liệu lịch sử — miễn các view/API khác đang lọc theo factory_plans.is_active = 1 khi hiển
        //            // thị lịch hoạt động, sẽ không bị lẫn dữ liệu cũ vào.
        //        }

        //        // ===== Gom production_order_items theo size_id (+ shade_id nếu có) =====
        //        var sourceItems = await _context.MesProductionOrderItems
        //            .Where(i => request.PoIds.Contains(i.PoId)
        //                     && i.StyleId == request.StyleId && i.ColorId == request.Color
        //                     && i.FactoryId == request.FactoryId)
        //            .ToListAsync();

        //        if (sourceItems.Count > 0)
        //        {
        //            if (sourceItems.Select(s => s.VersionId).Distinct().Count() > 1)
        //                throw new BusinessValidationException("Các dòng chi tiết size (PO Items) thuộc các phiên bản kế hoạch khác nhau, không thể gom.");

        //            var itemGroups = sourceItems
        //                .GroupBy(s => new { s.SizeId })
        //                .Select(g => new
        //                {
        //                    g.Key.SizeId,
        //                    TotalQty = g.Sum(x => x.PlannedQty),
        //                    // Nhiều dòng nguồn có thể khác target_delivery_date -> lấy sớm nhất để an toàn giao hàng
        //                    TargetDeliveryDate = g.Min(x => x.TargetDeliveryDate),
        //                    SourceIds = g.Select(x => x.Id).ToList()
        //                })
        //                .ToList();

        //            var newItems = itemGroups.Select(g => new MesProductionOrderItem
        //            {
        //                Id = Guid.NewGuid(),
        //                VersionId = versionId,
        //                FactoryId = request.FactoryId,
        //                StyleId = styleId,
        //                PoId = newPoCode, //cần tạo,
        //                ColorId = colorId,
        //                //ShadeId = g.ShadeId,
        //                SizeId = g.SizeId,
        //                PlannedQty = g.TotalQty,
        //                TargetDeliveryDate = DateOnly.FromDateTime(request.ProductionStartDate),
        //                DisposalAction = null,   // reset, chờ xử lý lại theo PO gom mới
        //                DisposalNotes = null,
        //                CreatedAt = now,
        //                POName = request.NewPoName,
        //                Status = "merged"
        //            }).ToList();

        //            _context.MesProductionOrderItems.AddRange(newItems);

        //            var parentItemIdBySourceId = itemGroups
        //            .SelectMany(g => g.SourceIds.Select(sid => (
        //                sid,
        //                NewId: newItems.First(ni => ni.SizeId == g.SizeId ).Id
        //            )))
        //            .ToDictionary(x => x.sid, x => x.NewId);

        //            foreach (var s in sourceItems)
        //            {
        //                s.ParentItemId = parentItemIdBySourceId[s.Id];
        //                s.Status = "merged_source";
        //                s.IsActive = false;
        //                s.MergedAt = now;
        //                s.MergedBy = request.CreatedBy;
        //                s.UpdatedAt = now;
        //            }
        //        }

        //        await _context.SaveChangesAsync();
        //        await transaction.CommitAsync();



        //        return new MergeExecuteResponse
        //        {
        //            Success = true,
        //            NewPoId = newPoCode,
        //            CreatedPlans = newPlans
        //                .OrderBy(p => p.LineNumber == null ? 1 : 0)
        //                .ThenBy(p => p.LineNumber)
        //                .ThenBy(p => p.OperationCode)
        //                .Select(p => new CreatedPlanDto
        //                {
        //                    Id = p.Id,
        //                    PlanCode = p.PlanCode,
        //                    PoId = p.PoId,
        //                    LineNumber = p.LineNumber,
        //                    OperationCode = p.OperationCode,
        //                    TotalPlannedQty = p.TotalPlannedQty,
        //                    Status = p.Status,
        //                    CmPrice = flatCmPrice
        //                }).ToList()
        //        };
        //    }
        //    catch
        //    {
        //        await transaction.RollbackAsync();
        //        throw;
        //    }
        //}
        public async Task<MergeExecuteResponse> ExecuteAsync(MergeExecuteRequest request)
        {
            if (request.PoIds is null || request.PoIds.Count < 2)
                throw new BusinessValidationException("Cần chọn ít nhất 2 PO để gom.");

            using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            try
            {
                // Không filter ColorId nữa — 1 PO có thể có nhiều Màu, cần lấy TẤT CẢ dòng của các PO đã chọn.
                var source = await _context.MesFactoryPlans
                    .Where(fp => request.PoIds.Contains(fp.PoId)
                              && fp.StyleId == request.StyleId
                              && fp.FactoryId == request.FactoryId
                              && fp.IsActive
                              && fp.ParentPlanId == null)
                    .ToListAsync();

                if (source.Count == 0)
                    throw new BusinessValidationException("Không tìm thấy kế hoạch hợp lệ để gom (có thể vừa bị gom bởi thao tác khác).");

                if (source.Select(s => s.StyleId).Distinct().Count() > 1)
                    throw new BusinessValidationException("Các PO được chọn không cùng Mã hàng, không thể gom.");

                // KHÔNG còn validate cùng ColorId — 1 PO Gom có thể chứa nhiều Màu.

                if (source.Select(s => s.ProductionOrderId).Distinct().Count() > 1)
                    throw new BusinessValidationException("Dữ liệu bất thường: các PO cùng Style nhưng khác production_order_id, không thể gom.");

                if (source.Select(s => s.VersionId).Distinct().Count() > 1)
                    throw new BusinessValidationException("Các PO được chọn thuộc các phiên bản kế hoạch khác nhau, không thể gom.");

                var newPoCode = await GenerateNewPoCodeAsync(request.FactoryId);

                var newPoIdExists = await _context.MesFactoryPlans
                    .AnyAsync(fp => fp.PoId == newPoCode && fp.FactoryId == request.FactoryId && fp.IsActive);
                if (newPoIdExists)
                    throw new BusinessValidationException("Mã PO Gom vừa sinh bị trùng, vui lòng thử lại.");

                // ===== Style có phân công đoạn hay không -> nguồn giá CM (theo Style, không phụ thuộc Màu) =====
                var cmPriceEntity = await _context.StyleCmPrice
                    .Where(e => e.IsActive == true && e.StyleId == request.StyleId)
                    .FirstOrDefaultAsync();

                bool isOperation = cmPriceEntity?.IsOperation == true;

                decimal? flatCmPrice = null;
                var operationPriceMap = new Dictionary<string, decimal>();

                if (cmPriceEntity != null)
                {
                    if (!isOperation)
                        flatCmPrice = cmPriceEntity.CmPrice;
                    else
                        operationPriceMap = await _context.StyleCmSplits
                            .Where(s => s.StyleCmPriceId == cmPriceEntity.Id)
                            .ToDictionaryAsync(s => s.OperationCode, s => s.SplitPrice);
                }
                flatCmPrice ??= request.FallbackCmPrice;

                var styleId = source[0].StyleId;
                var productionOrderId = source[0].ProductionOrderId;
                var oldVersionId = source[0].VersionId;
                var now = DateTime.UtcNow;

                // ===== Tạo MesProductionOrderVersions MỚI cho PO gom =====
                var sourceVersion = await _context.MesProductionOrderVersions
                    .FirstOrDefaultAsync(v => v.Id == oldVersionId);

                if (sourceVersion == null)
                    throw new BusinessValidationException("Không tìm thấy phiên bản kế hoạch nguồn để tạo version mới cho PO gom.");

                var nextVersionNumber = await _context.MesProductionOrderVersions
                    .Where(v => v.ProductionOrderId == sourceVersion.ProductionOrderId)
                    .MaxAsync(v => (int?)v.VersionNumber) ?? 0;
                nextVersionNumber += 1;

                var newVersion = new MesProductionOrderVersion
                {
                    Id = Guid.NewGuid(),
                    ProductionOrderId = sourceVersion.ProductionOrderId,
                    VersionNumber = nextVersionNumber,
                    VersionStatus = sourceVersion.VersionStatus,
                    TriggerType = sourceVersion.TriggerType,
                    SourcePoType = sourceVersion.SourcePoType,
                    ErpPoId = sourceVersion.ErpPoId,
                    DeltaSummary = sourceVersion.DeltaSummary,
                    SupersededBy = sourceVersion.SupersededBy,
                    CreatedBy = request.CreatedBy,
                    CreatedAt = now
                };
                _context.MesProductionOrderVersions.Add(newVersion);
                await _context.SaveChangesAsync();
                var versionId = newVersion.Id;

                var assignedRows = source.Where(s => s.LineNumber != null).ToList();
                var unassignedRows = source.Where(s => s.LineNumber == null).ToList();

                // ===== Net-trừ phần đã phân tổ khỏi dòng "chưa phân tổ" — theo TỪNG (PoId, ColorId) =====
                // Vì 1 po_id có thể có NHIỀU dòng "chưa phân tổ" khác nhau (mỗi dòng ứng với 1 Màu),
                // không thể net theo PoId đơn thuần như trước — phải khoá thêm ColorId.
                Dictionary<(string PoId, string ColorId), int> assignedMaxPerPoColor = new();
                if (isOperation)
                {
                    assignedMaxPerPoColor = assignedRows
                        .GroupBy(s => (s.PoId, s.ColorId))
                        .ToDictionary(g => g.Key, g => g.Max(x => x.TotalPlannedQty));
                }

                int NetUnassignedQty(MesFactoryPlan u)
                {
                    if (!isOperation) return u.TotalPlannedQty;
                    var already = assignedMaxPerPoColor.TryGetValue((u.PoId, u.ColorId), out var a) ? a : 0;
                    return Math.Max(0, u.TotalPlannedQty - already);
                }

                // ===== Nhóm theo (Màu + Tổ + Công đoạn) — mỗi nhóm -> 1 dòng factory_plans mới =====
                var groups = new List<MergeGroupPlan>();

                foreach (var g in assignedRows.GroupBy(s => new
                {
                    s.ColorId,
                    s.LineNumber,
                    OperationCode = isOperation ? s.OperationCode : null
                }))
                {
                    groups.Add(new MergeGroupPlan
                    {
                        NewId = Guid.NewGuid(),
                        ColorId = g.Key.ColorId,
                        LineNumber = g.Key.LineNumber,
                        OperationCode = g.Key.OperationCode,
                        TotalQty = g.Sum(x => x.TotalPlannedQty),
                        SourceIds = g.Select(x => x.Id).ToList()
                    });
                }

                // "Chưa phân tổ" — tách riêng theo TỪNG Màu, không gộp chung 1 dòng cho mọi Màu.
                foreach (var colorGroup in unassignedRows.GroupBy(u => u.ColorId))
                {
                    var qty = colorGroup.Sum(NetUnassignedQty);
                    groups.Add(new MergeGroupPlan
                    {
                        NewId = Guid.NewGuid(),
                        ColorId = colorGroup.Key,
                        LineNumber = null,
                        OperationCode = null,
                        TotalQty = qty,
                        SourceIds = colorGroup.Select(x => x.Id).ToList()
                    });
                }

                // ===== Tạo các dòng factory_plans mới =====
                var newPlans = groups.Select(g => new MesFactoryPlan
                {
                    Id = g.NewId,
                    PlanCode = BuildPlanCode(request.NewPoName, g.ColorId, g.LineNumber, g.OperationCode),
                    ProductionOrderId = productionOrderId,
                    VersionId = versionId,
                    FactoryId = request.FactoryId,
                    StyleId = styleId,
                    PoId = newPoCode,
                    ColorId = g.ColorId,
                    LineNumber = g.LineNumber,
                    OperationCode = g.OperationCode,
                    BufferDays = request.BufferDays,
                    TotalPlannedQty = g.TotalQty,
                    Status = "merged",
                    CreatedBy = request.CreatedBy,
                    CreatedAt = now,
                    UpdatedAt = now,
                    IsActive = g.TotalQty > 0,
                    IsGroup = true,
                    MergedBy = request.UserId,
                    MergedAt = now,
                    //CmPrice = g.OperationCode != null
                    //    ? (operationPriceMap.TryGetValue(g.OperationCode, out var p) ? p : (decimal?)null)
                    //    : flatCmPrice
                }).ToList();

                _context.MesFactoryPlans.AddRange(newPlans);

                // ===== Đánh dấu các dòng nguồn: trỏ về đúng nhóm, chuyển merged_source =====
                var parentIdBySourceId = groups
                    .SelectMany(g => g.SourceIds.Select(sid => (sid, g.NewId)))
                    .ToDictionary(x => x.sid, x => x.NewId);

                foreach (var s in source)
                {
                    s.ParentPlanId = parentIdBySourceId[s.Id];
                    s.Status = "merged_source";
                    s.IsActive = false;
                    s.MergedAt = now;
                    s.MergedBy = request.CreatedBy;
                    s.UpdatedAt = now;
                }
                // ===== Gom MesFactoryPlanLine (kế hoạch theo ngày) theo (WorkDate, SizeId) =====
                var sourcePlanIds = source.Select(s => s.Id).ToList();
                var sourceLines = await _context.MesFactoryPlanLines
                    .Where(l => sourcePlanIds.Contains(l.FactoryPlanId))
                    .ToListAsync();

                if (sourceLines.Count > 0)
                {
                    // parentIdBySourceId đã được build ở khối trên (factory_plans.Id nguồn -> factory_plans.Id
                    // nhóm mới, đã tính đúng theo cả Màu + Tổ + Công đoạn). Nhiều dòng factory_plans nguồn (khác
                    // PO, cùng Màu + Tổ + Công đoạn) có thể trỏ về CÙNG 1 nhóm mới -> các dòng cùng (WorkDate,
                    // SizeId) của các plan nguồn đó phải CỘNG DỒN lại thành 1 dòng duy nhất dưới nhóm mới.
                    //
                    // LƯU Ý: đang gộp TOÀN BỘ lịch cũ (kể cả work_date đã qua). demand_created_qty/remaining_qty
                    // hiện chưa dùng (=0) nên việc gộp này không có rủi ro double-count Actual (Actual lấy riêng
                    // từ QLCL theo plan_id). Nếu chỉ muốn giữ lịch tương lai, thêm:
                    //   .Where(l => l.WorkDate.Date >= now.Date)
                    // trước GroupBy bên dưới.

                    var newLines = sourceLines
                        .GroupBy(l => new
                        {
                            NewPlanId = parentIdBySourceId[l.FactoryPlanId],
                            WorkDate = l.WorkDate.Date,
                            l.SizeId
                        })
                        .Select(g => new MesFactoryPlanLine
                        {
                            Id = Guid.NewGuid(),
                            FactoryPlanId = g.Key.NewPlanId,
                            SizeId = g.Key.SizeId,
                            WorkDate = g.Key.WorkDate,
                            PlannedQty = g.Sum(x => x.PlannedQty),
                            DemandCreatedQty = 0, // chưa dùng, giữ mặc định theo đúng hiện trạng
                            UpdatedAt = now
                        })
                        .ToList();

                    _context.MesFactoryPlanLines.AddRange(newLines);

                    // MesFactoryPlanLine không có cột Status/IsActive/ParentLineId -> không cần update gì thêm
                    // ở các dòng nguồn. Chúng vẫn gắn với factory_plans CŨ (đã chuyển is_active=0,
                    // status=merged_source ở bước trên) nên tự động là dữ liệu lịch sử.
                }
                // ===== Cập nhật dữ liệu THỰC HIỆN (QLCL) từ PO gốc sang PO mới =====
                var assignedSourceIds = assignedRows.Select(s => s.Id).ToList();

                if (assignedSourceIds.Count > 0)
                {
                    var qlclSessions = await _context.qualityManagementSessions
                        .Where(q => q.PlanId != null && assignedSourceIds.Contains(q.PlanId.Value))
                        .ToListAsync();

                    if (qlclSessions.Count > 0)
                    {
                        var sourcePoByPlanId = assignedRows.ToDictionary(s => s.Id, s => s.PoId);
                        var newSessions = new List<QualityManagementSession>();

                        foreach (var session in qlclSessions)
                        {
                            var sourcePlanId = session.PlanId!.Value;

                            // Chỉ xử lý session khớp đúng (PlanId, PoId) của PO gốc — tránh nhầm nếu có
                            // dữ liệu bất thường (plan_id trùng nhưng khác po_id).
                            if (!sourcePoByPlanId.TryGetValue(sourcePlanId, out var sourcePoId)
                                || session.SalesOrderNo != sourcePoId)
                                continue;

                            if (!parentIdBySourceId.TryGetValue(sourcePlanId, out var newPlanId))
                                continue; // an toàn: phòng trường hợp không xác định được nhóm mới tương ứng

                            // ===== Tạo bản ghi MỚI cho PO Gom — copy toàn bộ dữ liệu từ bản gốc =====
                            var newSession = new QualityManagementSession
                            {
                                No = Guid.NewGuid(),                    // khoá nghiệp vụ mới, không trùng bản gốc
                                TimeSlotId = session.TimeSlotId,
                                OperationType = session.OperationType,
                                WorkCenterCode = session.WorkCenterCode,
                                WorkShiftCode = session.WorkShiftCode,
                                PlanId = newPlanId,                  // cột PlanID (int) legacy — giữ nguyên như gốc
                                CustomerNo_ = session.CustomerNo_,
                                SalesOrderNo = newPoCode,                 // trỏ sang PO Gom mới
                                ItemNo = session.ItemNo,
                                ColorNo = session.ColorNo,
                                Status = 1,                                // bản ghi hiện hành
                                InspectionDate = session.InspectionDate,
                                PlanOutputQuantity = session.PlanOutputQuantity,
                                PassOutputQuantity = session.PassOutputQuantity,
                                CheckedQuantity = session.CheckedQuantity,
                                ItemName = session.ItemName,
                                Inspector = session.Inspector,
                                Size = session.Size,
                                PoName = session.PoName,
                                DefectiveProductCount = session.DefectiveProductCount,
                                TargetQtyPerDate = session.TargetQtyPerDate,
                            };

                            newSessions.Add(newSession);

                            // ===== Bản ghi GỐC: giữ nguyên toàn bộ dữ liệu, chỉ đánh dấu đã bị thay thế =====
                            session.Status = 0;
                        }

                        if (newSessions.Count > 0)
                            _context.qualityManagementSessions.AddRange(newSessions);
                    }
                }

                // ===== Gom production_order_items theo (ColorId, SizeId) =====
                var sourceItems = await _context.MesProductionOrderItems
                    .Where(i => request.PoIds.Contains(i.PoId)
                             && i.StyleId == request.StyleId
                             && i.FactoryId == request.FactoryId)
                    .ToListAsync();

                if (sourceItems.Count > 0)
                {
                    
                    var itemGroups = sourceItems
                        .GroupBy(s => new { s.ColorId, s.SizeId })
                        .Select(g => new
                        {
                            NewId = Guid.NewGuid(),
                            g.Key.ColorId,
                            g.Key.SizeId,
                            TotalQty = g.Sum(x => x.PlannedQty),
                            TargetDeliveryDate = g.Min(x => x.TargetDeliveryDate),
                            SourceIds = g.Select(x => x.Id).ToList()
                        })
                        .ToList();

                    var newItems = itemGroups.Select(g => new MesProductionOrderItem
                    {
                        Id = g.NewId,
                        VersionId = versionId,
                        FactoryId = request.FactoryId,
                        StyleId = styleId,
                        PoId = newPoCode,
                        ColorId = g.ColorId,
                        SizeId = g.SizeId,
                        PlannedQty = g.TotalQty,
                        TargetDeliveryDate = DateOnly.FromDateTime(request.ProductionStartDate),
                        DisposalAction = null,
                        DisposalNotes = null,
                        CreatedAt = now,
                        POName = request.NewPoName,
                        Status = "merged"
                    }).ToList();

                    _context.MesProductionOrderItems.AddRange(newItems);

                    var parentItemIdBySourceId = itemGroups
                        .SelectMany(g => g.SourceIds.Select(sid => (sid, g.NewId)))
                        .ToDictionary(x => x.sid, x => x.NewId);

                    foreach (var s in sourceItems)
                    {
                        s.ParentItemId = parentItemIdBySourceId[s.Id];
                        s.Status = "merged_source";
                        s.IsActive = false;
                        s.MergedAt = now;
                        s.MergedBy = request.CreatedBy;
                        s.UpdatedAt = now;
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return new MergeExecuteResponse
                {
                    Success = true,
                    NewPoId = newPoCode,
                    CreatedPlans = newPlans
                        .OrderBy(p => p.ColorId)
                        .ThenBy(p => p.LineNumber == null ? 1 : 0)
                        .ThenBy(p => p.LineNumber)
                        .ThenBy(p => p.OperationCode)
                        .Select(p => new CreatedPlanDto
                        {
                            Id = p.Id,
                            PlanCode = p.PlanCode,
                            PoId = p.PoId,
                            ColorId = p.ColorId,
                            LineNumber = p.LineNumber,
                            OperationCode = p.OperationCode,
                            TotalPlannedQty = p.TotalPlannedQty,
                            Status = p.Status,
                            CmPrice = flatCmPrice   // lấy từ chính dòng đã lưu, không dùng flatCmPrice cố định
                        }).ToList()
                };
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private static string BuildPlanCode(string newPoName, string colorId, string? lineNumber, string? operationCode)
        {
            var parts = new List<string> { newPoName, colorId, lineNumber ?? "UNASSIGNED" };
            if (!string.IsNullOrEmpty(operationCode))
                parts.Add(operationCode);
            return string.Join("-", parts);
        }

        // Cập nhật MergeGroupPlan — thêm ColorId
        private class MergeGroupPlan
        {
            public Guid NewId { get; set; }
            public string ColorId { get; set; } = default!;
            public string? LineNumber { get; set; }
            public string? OperationCode { get; set; }
            public int TotalQty { get; set; }
            public List<Guid> SourceIds { get; set; } = new();
        }
        //public async Task<MergeExecuteResponse> ExecuteAsync(MergeExecuteRequest request)
        //{
        //    if (request.PoIds is null || request.PoIds.Count < 2)
        //        throw new BusinessValidationException("Cần chọn ít nhất 2 PO để gom.");

        //    // Serializable để giảm rủi ro race condition (2 user cùng gom 1 lúc).
        //    using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        //    try
        //    {
        //        var source = await _context.MesFactoryPlans
        //            .Where(fp => request.PoIds.Contains(fp.PoId)
        //                      && fp.StyleId == request.StyleId && fp.ColorId == request.Color
        //                      && fp.FactoryId == request.FactoryId
        //                      && fp.IsActive
        //                      && fp.ParentPlanId == null)
        //            .ToListAsync();

        //        if (source.Count == 0)
        //            throw new BusinessValidationException("Không tìm thấy kế hoạch hợp lệ để gom (có thể vừa bị gom bởi thao tác khác).");

        //        if (source.Select(s => s.StyleId).Distinct().Count() > 1
        //            || source.Select(s => s.ColorId).Distinct().Count() > 1)
        //            throw new BusinessValidationException("Các PO được chọn không cùng Mã hàng/Màu.");

        //        // Cùng Style => phải cùng production_order_id (dữ liệu gốc từ ERP)
        //        if (source.Select(s => s.ProductionOrderId).Distinct().Count() > 1)
        //            throw new BusinessValidationException("Dữ liệu bất thường: các PO cùng Style nhưng khác production_order_id, không thể gom.");

        //        if (source.Select(s => s.VersionId).Distinct().Count() > 1)
        //            throw new BusinessValidationException("Các PO được chọn thuộc các phiên bản kế hoạch khác nhau, không thể gom.");

        //        var newPoCode = await GenerateNewPoCodeAsync(request.FactoryId);

        //        // Vẫn giữ check trùng để chắc chắn (phòng dữ liệu cũ/migrate lẫn vào)
        //        var newPoIdExists = await _context.MesFactoryPlans
        //            .AnyAsync(fp => fp.PoId == newPoCode && fp.FactoryId == request.FactoryId && fp.IsActive);
        //        if (newPoIdExists)
        //            throw new BusinessValidationException("Mã PO Gom vừa sinh bị trùng, vui lòng thử lại.");

        //        // ===== Style có phân công đoạn hay không -> nguồn giá CM =====
        //        var cmPriceEntity = await _context.StyleCmPrice
        //            .Where(e => e.IsActive == true && e.StyleId == request.StyleId)
        //            .FirstOrDefaultAsync();

        //        bool isOperation = cmPriceEntity?.IsOperation == true;

        //        decimal? flatCmPrice = null;
        //        var operationPriceMap = new Dictionary<string, decimal>();

        //        if (cmPriceEntity != null)
        //        {
        //            if (!isOperation)
        //                flatCmPrice = cmPriceEntity.CmPrice;
        //            else
        //                operationPriceMap = await _context.StyleCmSplits
        //                    .Where(s => s.StyleCmPriceId == cmPriceEntity.Id)
        //                    .ToDictionaryAsync(s => s.OperationCode, s => s.SplitPrice);
        //        }
        //        flatCmPrice ??= request.FallbackCmPrice; // style chưa cấu hình giá -> dùng giá fallback nếu có

        //        var styleId = source[0].StyleId;
        //        var colorId = source[0].ColorId;
        //        var productionOrderId = source[0].ProductionOrderId;
        //        var oldVersionId = source[0].VersionId; // version_id CŨ (của PO nguồn) — chỉ dùng để tra cứu version gốc
        //        var now = DateTime.UtcNow;

        //        // ===== Tạo MesProductionOrderVersions MỚI cho PO gom =====
        //        // version_id của MesProductionOrderItems/MesFactoryPlans (PO mới) phải trỏ về Id của version MỚI này,
        //        // KHÔNG dùng lại version_id cũ của các PO nguồn.
        //        var sourceVersion = await _context.MesProductionOrderVersions
        //            .FirstOrDefaultAsync(v => v.Id == oldVersionId);

        //        if (sourceVersion == null)
        //            throw new BusinessValidationException("Không tìm thấy phiên bản kế hoạch nguồn để tạo version mới cho PO gom.");
        //        var nextVersionNumber = await _context.MesProductionOrderVersions
        //            .Where(v => v.ProductionOrderId == sourceVersion.ProductionOrderId)
        //            .MaxAsync(v => (int?)v.VersionNumber) ?? 0;
        //        nextVersionNumber += 1;
        //        var newVersion = new MesProductionOrderVersion
        //        {
        //            Id = Guid.NewGuid(),
        //            ProductionOrderId = sourceVersion.ProductionOrderId,
        //            VersionNumber = nextVersionNumber,          // <-- số mới, không trùng
        //            VersionStatus = sourceVersion.VersionStatus,
        //            TriggerType = sourceVersion.TriggerType,
        //            SourcePoType = sourceVersion.SourcePoType,
        //            ErpPoId = sourceVersion.ErpPoId,
        //            DeltaSummary = sourceVersion.DeltaSummary,
        //            SupersededBy = sourceVersion.SupersededBy,
        //            CreatedBy = request.CreatedBy,
        //            CreatedAt = now
        //        };
        //        _context.MesProductionOrderVersions.Add(newVersion);
        //        await _context.SaveChangesAsync();
        //        var versionId = newVersion.Id; // dùng version MỚI này cho toàn bộ dữ liệu PO gom bên dưới

        //        var assignedRows = source.Where(s => s.LineNumber != null).ToList();
        //        var unassignedRows = source.Where(s => s.LineNumber == null).ToList();

        //        // Net-trừ phần đã phân tổ khỏi dòng "chưa phân tổ" (đồng bộ fix đã áp dụng ở PreviewAsync —
        //        // dòng line_number=NULL lưu NGUYÊN tổng gốc của po_id, không tự trừ phần đã phân tổ qua
        //        // các dòng công đoạn khác).
        //        int unassignedQty;
        //        if (isOperation)
        //        {
        //            var assignedMaxPerPo = assignedRows
        //                .GroupBy(s => s.PoId)
        //                .ToDictionary(g => g.Key, g => g.Max(x => x.TotalPlannedQty));

        //            unassignedQty = unassignedRows.Sum(u =>
        //            {
        //                var already = assignedMaxPerPo.TryGetValue(u.PoId, out var a) ? a : 0;
        //                return Math.Max(0, u.TotalPlannedQty - already);
        //            });
        //        }
        //        else
        //        {
        //            unassignedQty = unassignedRows.Sum(x => x.TotalPlannedQty);
        //        }

        //        // ===== Nhóm theo Tổ (+ Công đoạn nếu Style có phân công đoạn) =====
        //        // Mỗi nhóm -> 1 dòng factory_plans mới. Không phân công đoạn: nhóm theo Tổ như cũ.
        //        var groups = new List<MergeGroupPlan>();

        //        foreach (var g in assignedRows.GroupBy(s => new
        //        {
        //            s.LineNumber,
        //            OperationCode = isOperation ? s.OperationCode : null
        //        }))
        //        {
        //            groups.Add(new MergeGroupPlan
        //            {
        //                NewId = Guid.NewGuid(),
        //                LineNumber = g.Key.LineNumber,
        //                OperationCode = g.Key.OperationCode,
        //                TotalQty = g.Sum(x => x.TotalPlannedQty),
        //                SourceIds = g.Select(x => x.Id).ToList()
        //            });
        //        }

        //        if (unassignedRows.Count > 0)
        //        {
        //            groups.Add(new MergeGroupPlan
        //            {
        //                NewId = Guid.NewGuid(),
        //                LineNumber = null,
        //                OperationCode = null,
        //                TotalQty = unassignedQty,
        //                SourceIds = unassignedRows.Select(x => x.Id).ToList()
        //            });
        //        }

        //        // ===== Tạo các dòng factory_plans mới =====
        //        var newPlans = groups.Select(g => new MesFactoryPlan
        //        {
        //            Id = g.NewId,
        //            PlanCode = BuildPlanCode(request.NewPoName, g.LineNumber, g.OperationCode),
        //            ProductionOrderId = productionOrderId,   // kế thừa từ PO nguồn
        //            VersionId = versionId,                    // dùng VERSION MỚI vừa tạo
        //            FactoryId = request.FactoryId,
        //            StyleId = styleId,

        //            PoId = newPoCode,
        //            ColorId = colorId,
        //            LineNumber = g.LineNumber,
        //            OperationCode = g.OperationCode,
        //            //ProductionStartDate = request.ProductionStartDate,
        //            BufferDays = request.BufferDays,
        //            TotalPlannedQty = g.TotalQty,              // Rule 6: 100% KH
        //            Status = "merged",                           // kế hoạch mới, chờ duyệt lại
        //            CreatedBy = request.CreatedBy,
        //            CreatedAt = now,
        //            UpdatedAt = now,
        //            IsActive = g.TotalQty > 0,                  // phòng case "chưa phân tổ" net về 0
        //            IsGroup = true,
        //            MergedBy = request.UserId,
        //            MergedAt = now,
        //            //CmPrice = g.OperationCode != null
        //            //    ? (operationPriceMap.TryGetValue(g.OperationCode, out var p) ? p : (decimal?)null)
        //            //    : flatCmPrice
        //        }).ToList();

        //        _context.MesFactoryPlans.AddRange(newPlans);

        //        // ===== Đánh dấu các dòng nguồn: trỏ về đúng nhóm, chuyển merged =====
        //        var parentIdBySourceId = groups
        //            .SelectMany(g => g.SourceIds.Select(sid => (sid, g.NewId)))
        //            .ToDictionary(x => x.sid, x => x.NewId);

        //        foreach (var s in source)
        //        {
        //            s.ParentPlanId = parentIdBySourceId[s.Id];
        //            s.Status = "merged_source";
        //            s.IsActive = false;
        //            s.MergedAt = now;
        //            s.MergedBy = request.CreatedBy;
        //            s.UpdatedAt = now;
        //        }
        //        // ===== Cập nhật dữ liệu THỰC HIỆN (QLCL) từ PO gốc sang PO mới =====
        //        // CHỈ áp dụng cho các dòng ĐÃ CÓ TỔ (line_number != null), theo yêu cầu nghiệp vụ:
        //        // các dòng "chưa phân tổ" không gắn với Tổ nên không có phiên QLCL thực tế theo plan_id.
        //        var assignedSourceIds = assignedRows.Select(s => s.Id).ToList();

        //        if (assignedSourceIds.Count > 0)
        //        {
        //            var qlclSessions = await _context.qualityManagementSessions
        //                .Where(q => q.PlanId != null && assignedSourceIds.Contains(q.PlanId.Value))
        //                .ToListAsync();

        //            if (qlclSessions.Count > 0)
        //            {
        //                // Map plan_id (nguồn) -> po_id (nguồn) để đối chiếu đúng cặp (PlanId, PoId) của PO gốc
        //                var sourcePoByPlanId = assignedRows.ToDictionary(s => s.Id, s => s.PoId);

        //                foreach (var session in qlclSessions)
        //                {
        //                    var sourcePlanId = session.PlanId!.Value;

        //                    // Chỉ update session khớp đúng (PlanId, PoId) của PO gốc — tránh update nhầm
        //                    // nếu lỡ có plan_id trùng nhưng khác po_id (dữ liệu bất thường).
        //                    if (!sourcePoByPlanId.TryGetValue(sourcePlanId, out var sourcePoId)
        //                        || session.SalesOrderNo != sourcePoId)
        //                        continue;

        //                    if (!parentIdBySourceId.TryGetValue(sourcePlanId, out var newPlanId))
        //                        continue; // an toàn: phòng trường hợp không xác định được nhóm mới tương ứng

        //                    session.PlanId = newPlanId;
        //                    session.SalesOrderNo = newPoCode;
        //                }
        //            }
        //        }

        //        // (Đã bỏ bước gom MesFactoryPlanLine — sau khi gom, user tự cập nhật lại lịch/số lượng kế hoạch
        //        // theo ngày cho PO mới, không copy lịch cũ sang nữa.)

        //        // (Đã bỏ bước gom MesFactoryPlanLine — sau khi gom, user tự cập nhật lại lịch/số lượng kế hoạch
        //        // theo ngày cho PO mới, không copy lịch cũ sang nữa.)

        //        // ===== Gom production_order_items theo size_id =====
        //        var sourceItems = await _context.MesProductionOrderItems
        //            .Where(i => request.PoIds.Contains(i.PoId)
        //                     && i.StyleId == request.StyleId && i.ColorId == request.Color
        //                     && i.FactoryId == request.FactoryId)
        //            .ToListAsync();

        //        if (sourceItems.Count > 0)
        //        {
        //            if (sourceItems.Select(s => s.VersionId).Distinct().Count() > 1)
        //                throw new BusinessValidationException("Các dòng chi tiết size (PO Items) thuộc các phiên bản kế hoạch khác nhau, không thể gom.");

        //            var itemGroups = sourceItems
        //                .GroupBy(s => new { s.SizeId })
        //                .Select(g => new
        //                {
        //                    NewId = Guid.NewGuid(),
        //                    g.Key.SizeId,
        //                    TotalQty = g.Sum(x => x.PlannedQty),
        //                    // Nhiều dòng nguồn có thể khác target_delivery_date -> lấy sớm nhất để an toàn giao hàng
        //                    TargetDeliveryDate = g.Min(x => x.TargetDeliveryDate),
        //                    SourceIds = g.Select(x => x.Id).ToList()
        //                })
        //                .ToList();

        //            var newItems = itemGroups.Select(g => new MesProductionOrderItem
        //            {
        //                Id = g.NewId,
        //                VersionId = versionId,   // dùng VERSION MỚI vừa tạo — bằng Id của newVersion
        //                FactoryId = request.FactoryId,
        //                StyleId = styleId,
        //                PoId = newPoCode,
        //                ColorId = colorId,
        //                //ShadeId = g.ShadeId,
        //                SizeId = g.SizeId,
        //                PlannedQty = g.TotalQty,
        //                TargetDeliveryDate = DateOnly.FromDateTime(request.ProductionStartDate),
        //                DisposalAction = null,   // reset, chờ xử lý lại theo PO gom mới
        //                DisposalNotes = null,
        //                CreatedAt = now,
        //                POName = request.NewPoName,
        //                Status = "merged"
        //            }).ToList();

        //            _context.MesProductionOrderItems.AddRange(newItems);

        //            var parentItemIdBySourceId = itemGroups
        //                .SelectMany(g => g.SourceIds.Select(sid => (sid, g.NewId)))
        //                .ToDictionary(x => x.sid, x => x.NewId);

        //            foreach (var s in sourceItems)
        //            {
        //                s.ParentItemId = parentItemIdBySourceId[s.Id];
        //                s.Status = "merged_source";
        //                s.IsActive = false;
        //                s.MergedAt = now;
        //                s.MergedBy = request.CreatedBy;
        //                s.UpdatedAt = now;
        //            }
        //        }

        //        await _context.SaveChangesAsync();
        //        await transaction.CommitAsync();

        //        return new MergeExecuteResponse
        //        {
        //            Success = true,
        //            NewPoId = newPoCode,
        //            CreatedPlans = newPlans
        //                .OrderBy(p => p.LineNumber == null ? 1 : 0)
        //                .ThenBy(p => p.LineNumber)
        //                .ThenBy(p => p.OperationCode)
        //                .Select(p => new CreatedPlanDto
        //                {
        //                    Id = p.Id,
        //                    PlanCode = p.PlanCode,
        //                    PoId = p.PoId,
        //                    LineNumber = p.LineNumber,
        //                    OperationCode = p.OperationCode,
        //                    TotalPlannedQty = p.TotalPlannedQty,
        //                    Status = p.Status,
        //                    CmPrice = flatCmPrice
        //                }).ToList()
        //        };
        //    }
        //    catch
        //    {
        //        await transaction.RollbackAsync();
        //        throw;
        //    }
        //}
        private static string BuildPlanCode(string newPoId, string? lineNumber, string? operationCode)
        {
            var parts = new List<string> { newPoId, lineNumber ?? "UNASSIGNED" };
            if (!string.IsNullOrEmpty(operationCode))
                parts.Add(operationCode);
            return string.Join("-", parts);
        }

        

        public async Task<MergedPoDetailResponse> GetMergedPoDetailAsync(string poId)
        {
            // 1 PO Gom = nhiều dòng factory_plans (1 dòng / Tổ, hoặc / Tổ+Công đoạn) cùng chung po_id mới.
            var groupPlans = await _context.MesFactoryPlans
                .Where(fp => fp.PoId == poId && fp.IsGroup )
                .ToListAsync();

            if (groupPlans.Count == 0)
                throw new NotFoundException($"Không tìm thấy PO Gom với mã '{poId}'.");

            var header = groupPlans[0];
            var groupPlanIds = groupPlans.Select(g => g.Id).ToList();

            // Toàn bộ PO con (factory_plans nguồn) đã bị gom vào nhóm này
            var childPlans = await _context.MesFactoryPlans
                .Where(fp => fp.ParentPlanId != null && groupPlanIds.Contains(fp.ParentPlanId.Value) )
                .ToListAsync();

            if (childPlans.Count == 0)
                throw new NotFoundException($"PO '{poId}' không có dữ liệu PO con (có thể chưa từng được gom).");
            childPlans = childPlans.DistinctBy(c => c.PoId)   // giữ lại phần tử đầu tiên gặp cho mỗi PoId
    .ToList();

            var childPoIds = childPlans.Select(c => c.PoId).Distinct().ToList();

            var soHeaderNameByPoId = await _context.SalesOrderHeader.Where(s => childPoIds.Contains(s.No)).ToDictionaryAsync(s => s.No, s => s.POName);
            // Toàn bộ dòng production_order_items (chi tiết size) của các PO con — dùng để dựng SizeIds
            // và TargetDeliveryDate cho từng PO con. Đây là dữ liệu lịch sử vẫn còn nguyên (chỉ bị đánh
            // dấu status='merged' khi tách chức năng "Gom production_order_items" đã làm ở ExecuteAsync).
            // Lấy các item hiện tại (mới) thuộc PO Gom — đây là "item cha"
            var parentItems = await _context.MesProductionOrderItems
                .Where(i => i.PoId == poId)
                .ToListAsync();

            var parentItemIds = parentItems.Select(p => p.Id).ToList();

            // Dùng parent_item_id để lấy các item con thực sự (giữ đúng po_id gốc của từng PO con)
            var allItems = await _context.MesProductionOrderItems
                .Where(i => i.ParentItemId != null && parentItemIds.Contains(i.ParentItemId.Value))
                .ToListAsync();

            // TODO: xác nhận nguồn POName — hiện đang lấy từ production_order_items.POName (field này đã
            // thấy dùng ở ExecuteAsync khi gán cho item MỚI: "POName = request.NewPoName"). Giả định các
            // item CŨ (trước khi gom) cũng có sẵn POName tương ứng PO con của chúng.
            var poNameByPoId = allItems
                .Where(i => i.POName != null)
                .GroupBy(i => i.PoId)
                .ToDictionary(g => g.Key, g => g.First().POName!);

            // Tên công đoạn (nếu Style có phân công đoạn) — lấy từ StyleCmSplits, giống PreviewAsync/ExecuteAsync
            var operationNameMap = new Dictionary<string, string>();
            var cmPriceEntity = await _context.StyleCmPrice
                .Where(e => e.IsActive == true && e.StyleId == header.StyleId)
                .FirstOrDefaultAsync();
            if (cmPriceEntity?.IsOperation == true)
            {
                operationNameMap = await _context.StyleCmSplits
                    .Where(s => s.StyleCmPriceId == cmPriceEntity.Id)
                    .ToDictionaryAsync(s => s.OperationCode, s => s.OperationName);
            }

            // TODO: chưa có bảng/API tra tên Tổ theo line_number trong phạm vi đã trao đổi.
            // Nếu có (VD bảng work_center hoặc danh mục Tổ), thay hàm này bằng query thực tế.
            string? ResolveLineNumberName(string? lineNumber) => null;

            var children = childPlans.Select(c =>
            {
                var itemsOfThisPo = allItems.Where(i => i.PoId == c.PoId).ToList();

                return new MergedPoChildDetail
                {
                    PoId = c.PoId,
                    PoName = poNameByPoId.TryGetValue(c.PoId, out var n) ? n            // ưu tiên 1: POName từ production_order_items
                    : soHeaderNameByPoId.TryGetValue(c.PoId, out var soName) ? soName // ưu tiên 2: Name từ SaleOrderHeader
                    : c.PoId,
                    LineNumber = c.LineNumber,
                    LineNumberName = ResolveLineNumberName(c.LineNumber),
                    OperationCode = c.OperationCode,
                    OperationName = c.OperationCode != null && operationNameMap.TryGetValue(c.OperationCode, out var opName)
                        ? opName
                        : null,
                    TargetDeliveryDate = itemsOfThisPo
                        .Select(i => (DateTime?)i.TargetDeliveryDate.ToDateTime(TimeOnly.MinValue))
                        .DefaultIfEmpty()
                        .Min(),
                    TotalPlannedQty = c.TotalPlannedQty,
                    SizeIds = itemsOfThisPo
                        .GroupBy(i => i.SizeId)
                        .Select(g => new FactoryOrderSizeItem
                        {
                            SizeId = g.Key,
                            PlannedQty = g.Sum(x => x.PlannedQty),
                            TargetDeliveryDate = g
                                .Select(x => (DateTime?)x.TargetDeliveryDate.ToDateTime(TimeOnly.MinValue))
                                .Where(d => d.HasValue)
                                .DefaultIfEmpty()
                                .Min()
                        })
                        .OrderBy(s => s.SizeId)
                        .ToList()
                };
            })
            .OrderBy(c => c.LineNumber == null ? 1 : 0)
            .ThenBy(c => c.LineNumber)
            .ThenBy(c => c.OperationCode)
            .ToList();
            var styleItem = await _context.Item
            .Where(it => it.No == header.StyleId)
            .Select(it => new { it.Name })
            .FirstOrDefaultAsync();
            // Gộp 1 query cho SaleOrderHeader: dùng chung cho PO Gom (poId) và các PO con (childPoIds)
            var allPoIdsForNameLookup = childPoIds.Append(poId).Distinct().ToList();
           
            return new MergedPoDetailResponse
            {
                VersionId = header.VersionId.ToString(),
                StyleId = header.StyleId,
                StyleName = styleItem.Name,             // TODO: map sang tên Style thật nếu có bảng riêng lưu tên
                NewPoId = header.PoId,
                NewPoName = parentItems.FirstOrDefault()?.POName                    // ưu tiên 1: POName từ item cha
                ?? (soHeaderNameByPoId.TryGetValue(poId, out var soName)         // ưu tiên 2: Name từ SaleOrderHeader (No = poId)
                    ? soName : null)
                ?? header.PlanCode,
                ColorId = header.ColorId,
                ColorName = header.ColorId,             // TODO: map sang tên Màu thật nếu có bảng riêng
                FactoryId = header.FactoryId,
                MergedAt = childPlans.Select(c => c.MergedAt).Where(d => d.HasValue).Max(),
                Children = children
            };
        }
    }
}
