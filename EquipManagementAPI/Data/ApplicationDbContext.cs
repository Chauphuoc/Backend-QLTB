﻿using EquipManagementAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace EquipManagementAPI.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }
        public DbSet<Equipment> Equipment { get; set; }
        public DbSet<DocumentEntryHeader> DocumentEntryHeader { get; set; }
        public DbSet<Department> Department { get; set; }
        public DbSet<QRCodeEntryQLTB> QRCodeEntry { get; set; }
        public DbSet<DocumentEntry> DocumentEntry { get; set; }
        public DbSet<Vendor> Vendor { get; set; }
        public DbSet<MaintenanceTracking> maintenanceTrackings { get; set; }
        public DbSet<MaintenanceTrackingWeek> maintenanceTrackingsWeek { get; set; }
        public DbSet<EquipmentStatusLog> EquipmentStatusLog { get; set; }
        public DbSet<LocationXSD> locationXSDs { get; set; }
        public DbSet<MaintenanceType> maintenanceType { get; set;}
        public DbSet<MaintenanceContent> maintenanceContents {  get; set; }    
        public DbSet<EquipmentGroup> equipmentGroup { get; set; }
        public DbSet<MaintenanceHistory> maintenanceHistory { get; set;}
        public DbSet<MaintenanceTask> maintenanceTasks { get; set; }
        public DbSet<EquipmentLineNo> equipmentLineNo { get; set;}
        public DbSet<RepairRequestList> repairRequests { get; set; }
        public DbSet<NoSeriesLine> noSeriesLine { get; set; }
        public DbSet<RepairHistory> repairHistory { get; set; }
        public DbSet<RepairType> repairType { get; set; }
        public DbSet<YeucauBQLCXacNhan> yeucauBQLCXacNhan { get; set; }
        public DbSet<RepairContent> repairContent { get; set; }
    }
}
