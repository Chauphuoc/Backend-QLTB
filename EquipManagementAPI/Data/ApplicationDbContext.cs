using EquipManagementAPI.Models;
using EquipManagementAPI.Models.DTOs;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace EquipManagementAPI.Data;

public class ApplicationDbContext : DbContext
{
    public DbSet<Equipment> Equipment { get; set; }
    public DbSet<Item> Item { get; set; }

    public DbSet<DocumentEntryHeader> DocumentEntryHeader { get; set; }

    public DbSet<Department> Department { get; set; }

    public DbSet<QRCodeEntryQLTB> QRCodeEntry { get; set; }

    public DbSet<DocumentEntry> DocumentEntry { get; set; }

    public DbSet<Vendor> Vendor { get; set; }

    public DbSet<MaintenanceTracking> maintenanceTrackings { get; set; }

    public DbSet<MaintenanceTrackingWeek> maintenanceTrackingsWeek { get; set; }

    public DbSet<EquipmentStatusLog> EquipmentStatusLog { get; set; }

    public DbSet<LocationXSD> locationXSDs { get; set; }

    public DbSet<MaintenanceType> maintenanceType { get; set; }

    public DbSet<MaintenanceContent> maintenanceContents { get; set; }

    public DbSet<EquipmentGroup> equipmentGroup { get; set; }

    public DbSet<MaintenanceHistory> maintenanceHistory { get; set; }
    public DbSet<MaintenanceHistoryWeek> maintenanceHistoryWeek { get; set; }

    public DbSet<MaintenanceTask> maintenanceTasks { get; set; }

    public DbSet<EquipmentLineNo> equipmentLineNo { get; set; }

    public DbSet<RepairRequestList> repairRequests { get; set; }

    public DbSet<NoSeriesLine> noSeriesLine { get; set; }
    public DbSet<NoSeriesLineQLCL> noSeriesLineQLCL { get; set; }
    public DbSet<RepairHistory> repairHistory { get; set; }

    public DbSet<RepairType> repairType { get; set; }

    public DbSet<YeucauBQLCXacNhan> yeucauBQLCXacNhan { get; set; }

    public DbSet<RepairContent> repairContent { get; set; }

    public DbSet<Employee> employee { get; set; }

    public DbSet<MaintenanceCheckList> maintenanceCheckList { get; set; }

    public DbSet<WorkShift> workShift { get; set; }

    public DbSet<UserRole> userRoles { get; set; }

    public DbSet<Roles> roles { get; set; }
    public DbSet<InventoryScan> inventoryScans { get; set; }
    public DbSet<Repair_Description> repair_Descriptions { get; set; }
    public DbSet<WorkCenter> workCenters { get; set; }
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }
    public DbSet<DefectDetail> defectDetail { get; set; }
    public DbSet<QualityManagementSession> qualityManagementSessions { get; set; }
    public DbSet<SalesOrderLineFactoryPlanQueue> SalesOrderLineFactoryPlanQueues { get; set; }
    public DbSet<Defects> Defects { get; set; }
    public DbSet<SalesOrderLineFactoryPlanQueueSchedule> SalesOrderLineFactoryPlanQueueSchedules { get; set; }
    public DbSet<UnitOfMeasure> Unit { get; set; }
    public DbSet<ProductionPlan> ProductionPlans { get; set; }
    public DbSet<TimeSlot> TimeSlots { get; set; }
    public DbSet<TeamPlanSummaryDTO> TeamPlanSummaryDTOs { get; set; }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TeamPlanSummaryDTO>().HasNoKey();
        modelBuilder.Entity<HourlyOutputDTO>().HasNoKey();
        modelBuilder.Entity<DefectDetail>()
    .HasOne(d => d.Session)
    .WithMany(s => s.Defects)
    .HasForeignKey(d => d.QualityManagementCode)   // 👈 FK thật
    .HasPrincipalKey(s => s.No);                 // 👈 PK bên session (QUAN TRỌNG)
        modelBuilder.Entity<MesFactoryPlan>()
    .HasOne(p => p.Parent)
    .WithMany(p => p.Children)
    .HasForeignKey(p => p.ParentPlanId)
    .HasPrincipalKey(p => p.Id)
    .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<MesFactoryPlanLine>()
    .HasOne(l => l.Plan)
    .WithMany(p => p.Lines)
    .HasForeignKey(l => l.FactoryPlanId)
    .OnDelete(DeleteBehavior.Cascade); // hoặc Restrict tùy nghiệp vụ
    }
    public DbSet<GarmentParts> GarmentParts { get; set; }
    public DbSet<CuttingSupplyRequest> CuttingSupplyRequests { get; set; }
    public DbSet<CuttingSupplyRequestItems> CuttingSupplyRequestItems { get; set; }
    public DbSet<CuttingSupplyRequestDefects> CuttingSupplyRequestDefects { get; set; }
    public DbSet<UserDeviceToken> userDeviceTokens { get; set; }
    public DbSet<MesProductionOrder> MesProductionOrders { get; set; }
    public DbSet<MesProductionOrderVersion> MesProductionOrderVersions { get; set; }
    public DbSet<MesProductionOrderItem> MesProductionOrderItems { get; set; }
    public DbSet<MesFactoryPlan> MesFactoryPlans { get; set; }
    public DbSet<MesFactoryPlanLine> MesFactoryPlanLines { get; set; }
    public DbSet<MesFactoryPlanDaily> MesFactoryPlanDailies { get; set; }
    // NAV source tables (read-only sync)
    public DbSet<SalesOrderLineFactoryPlan> SalesOrderLineFactoryPlans { get; set; }
    public DbSet<SalesOrderLineFactory> SalesOrderLineFactories { get; set; }
    public DbSet<SalesOrderLineDetailFactory> SalesOrderLineDetailFactories { get; set; }
    public DbSet<QualityInspectorDelegation>  QualityInspectorDelegations { get; set; }
    public DbSet<EmployeeAssignmentHistory> EmployeeAssignmentHistory { get; set; }
    public DbSet<QLCL_Reinspection> Reinspections { get; set; }
    public DbSet<QLCL_ReinspectionItem> ReinspectionItems { get; set; }
    public DbSet<QLCL_ReinspectionSessionLink> ReinspectionSessionLinks { get; set; }
    public DbSet<QLCL_ReinspectionAudit> ReinspectionAudits { get; set; }
    public DbSet<CurrencyExchangeRates> CurrencyExchangeRates { get; set; }
    public DbSet<SalesOrderHeader> SalesOrderHeader { get; set;}
    public DbSet<StyleCmPrice> StyleCmPrice { get; set; }
    public DbSet<StyleCmSplit> StyleCmSplits {  get; set; } 
    public DbSet<ItemGroup2> itemGroup2 { get; set; }
    public DbSet<FactoryPlanStatusHistory> FactoryPlanStatusHistory { get; set;}
    public DbSet<MesPoCodeSequences> MesPoCodeSequences { get; set; }
    public DbSet<MesPoRenameHistory> MesPoRenameHistory { get; set; }
    public DbSet<ItemSize> ItemSizes { get; set; }
    public DbSet<HRDailyWorkingHour> HRDailyWorkingHours { get; set; }
    public DbSet<RegisterOverTime> RegisterOverTimes { get; set; }
    public DbSet<UserSetupMobile> UserSetupMobiles { get; set; }
    public DbSet<SalesOrderLine> SalesOrderLine { get; set; }
    public DbSet<SalesOrderLineDetail> SalesOrderLineDetails { get; set; }
}
