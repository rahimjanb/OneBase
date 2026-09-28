using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Domain.AI;
using OneBase.Domain.Audit;
using OneBase.Domain.Files;
using OneBase.Domain.Identity;
using OneBase.Domain.Sales;

namespace OneBase.Infrastructure.Persistence;

public sealed class OneBaseDbContext(DbContextOptions<OneBaseDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Department> Departments => Set<Department>();

    public DbSet<Folder> Folders => Set<Folder>();
    public DbSet<FileItem> Files => Set<FileItem>();
    public DbSet<FileVersion> FileVersions => Set<FileVersion>();
    public DbSet<ResourcePermission> ResourcePermissions => Set<ResourcePermission>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<AgentToolGrant> AgentToolGrants => Set<AgentToolGrant>();
    public DbSet<ApprovalRequest> ApprovalRequests => Set<ApprovalRequest>();

    // Linko (зеркало SFA)
    public DbSet<LinkoUser> LinkoUsers => Set<LinkoUser>();
    public DbSet<LinkoMarket> LinkoMarkets => Set<LinkoMarket>();
    public DbSet<LinkoProduct> LinkoProducts => Set<LinkoProduct>();
    public DbSet<LinkoProductType> LinkoProductTypes => Set<LinkoProductType>();
    public DbSet<LinkoBorder> LinkoBorders => Set<LinkoBorder>();
    public DbSet<LinkoMarketUser> LinkoMarketUsers => Set<LinkoMarketUser>();
    public DbSet<LinkoOrder> LinkoOrders => Set<LinkoOrder>();
    public DbSet<LinkoOrderLine> LinkoOrderLines => Set<LinkoOrderLine>();
    public DbSet<LinkoOrderReturn> LinkoOrderReturns => Set<LinkoOrderReturn>();
    public DbSet<LinkoOrderReturnLine> LinkoOrderReturnLines => Set<LinkoOrderReturnLine>();
    public DbSet<LinkoVisit> LinkoVisits => Set<LinkoVisit>();
    public DbSet<LinkoKpiPlan> LinkoKpiPlans => Set<LinkoKpiPlan>();
    public DbSet<LinkoSyncState> LinkoSyncStates => Set<LinkoSyncState>();

    // Продажи: оргструктура, планы, цели
    public DbSet<SalesDirection> SalesDirections => Set<SalesDirection>();
    public DbSet<SalesRegion> SalesRegions => Set<SalesRegion>();
    public DbSet<SalesAgentProfile> SalesAgentProfiles => Set<SalesAgentProfile>();
    public DbSet<SalesRegionPlan> SalesRegionPlans => Set<SalesRegionPlan>();
    public DbSet<SalesAgentPlan> SalesAgentPlans => Set<SalesAgentPlan>();
    public DbSet<SalesTarget> SalesTargets => Set<SalesTarget>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        SalesModel.ConfigureLinko(b);
        SalesModel.ConfigureSales(b);

        // Identity
        b.Entity<Department>(e =>
        {
            e.Property(x => x.Code).HasMaxLength(64);
            e.Property(x => x.Name).HasMaxLength(200);
            e.HasIndex(x => x.Code).IsUnique();
        });

        b.Entity<User>(e =>
        {
            e.Property(x => x.Email).HasMaxLength(256);
            e.Property(x => x.FullName).HasMaxLength(200);
            e.HasIndex(x => x.Email).IsUnique();
            e.HasOne(x => x.Department).WithMany(x => x.Users).HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<Role>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100);
            e.HasIndex(x => x.Name).IsUnique();
        });

        b.Entity<UserRole>(e =>
        {
            e.HasKey(x => new { x.UserId, x.RoleId });
            e.HasOne(x => x.User).WithMany(x => x.Roles).HasForeignKey(x => x.UserId);
            e.HasOne(x => x.Role).WithMany(x => x.Users).HasForeignKey(x => x.RoleId);
        });

        b.Entity<RolePermission>(e =>
        {
            e.HasKey(x => new { x.RoleId, x.Code });
            e.Property(x => x.Code).HasMaxLength(100);
            e.HasOne(x => x.Role).WithMany(x => x.Permissions).HasForeignKey(x => x.RoleId);
        });

        // Files
        b.Entity<Folder>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(255);
            e.HasOne(x => x.Parent).WithMany(x => x.Children).HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => new { x.ParentId, x.Name });
        });

        b.Entity<FileItem>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(255);
            e.Property(x => x.ContentType).HasMaxLength(255);
            e.HasOne(x => x.Folder).WithMany(x => x.Files).HasForeignKey(x => x.FolderId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.FolderId, x.Name });
        });

        b.Entity<FileVersion>(e =>
        {
            e.Property(x => x.ObjectKey).HasMaxLength(512);
            e.Property(x => x.Sha256).HasMaxLength(64);
            e.HasOne(x => x.FileItem).WithMany(x => x.Versions).HasForeignKey(x => x.FileItemId);
            e.HasIndex(x => new { x.FileItemId, x.Number }).IsUnique();
        });

        b.Entity<ResourcePermission>(e =>
        {
            e.Property(x => x.PrincipalType).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.PrincipalId).HasMaxLength(100);
            e.HasOne<Folder>().WithMany(x => x.Permissions).HasForeignKey(x => x.FolderId);
            e.HasOne<FileItem>().WithMany(x => x.Permissions).HasForeignKey(x => x.FileItemId);
            e.HasIndex(x => new { x.PrincipalType, x.PrincipalId });
            e.ToTable(t => t.HasCheckConstraint(
                "CK_ResourcePermissions_SingleTarget",
                "(\"FolderId\" IS NULL) <> (\"FileItemId\" IS NULL)"));
        });

        // Audit
        b.Entity<AuditLog>(e =>
        {
            e.Property(x => x.ActorType).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.ActorId).HasMaxLength(100);
            e.Property(x => x.Action).HasMaxLength(100);
            e.Property(x => x.EntityType).HasMaxLength(100);
            e.Property(x => x.EntityId).HasMaxLength(100);
            e.Property(x => x.Data).HasColumnType("jsonb");
            e.HasIndex(x => x.Timestamp);
            e.HasIndex(x => new { x.EntityType, x.EntityId });
        });

        // AI
        b.Entity<AgentToolGrant>(e =>
        {
            e.Property(x => x.AgentCode).HasMaxLength(64);
            e.Property(x => x.ToolName).HasMaxLength(100);
            e.HasIndex(x => new { x.AgentCode, x.ToolName }).IsUnique();
        });

        b.Entity<ApprovalRequest>(e =>
        {
            e.Property(x => x.AgentCode).HasMaxLength(64);
            e.Property(x => x.ToolName).HasMaxLength(100);
            e.Property(x => x.Arguments).HasColumnType("jsonb");
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.Version).IsRowVersion();
            e.HasIndex(x => x.Status);
        });
    }
}
