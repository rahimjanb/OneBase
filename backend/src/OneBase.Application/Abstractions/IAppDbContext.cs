using Microsoft.EntityFrameworkCore;
using OneBase.Domain.AI;
using OneBase.Domain.Audit;
using OneBase.Domain.Files;
using OneBase.Domain.Identity;
using OneBase.Domain.Sales;

namespace OneBase.Application.Abstractions;

public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<Role> Roles { get; }
    DbSet<Department> Departments { get; }

    DbSet<Folder> Folders { get; }
    DbSet<FileItem> Files { get; }
    DbSet<FileVersion> FileVersions { get; }
    DbSet<ResourcePermission> ResourcePermissions { get; }

    DbSet<AuditLog> AuditLogs { get; }

    DbSet<AgentToolGrant> AgentToolGrants { get; }
    DbSet<ApprovalRequest> ApprovalRequests { get; }

    DbSet<LinkoUser> LinkoUsers { get; }
    DbSet<LinkoMarket> LinkoMarkets { get; }
    DbSet<LinkoProduct> LinkoProducts { get; }
    DbSet<LinkoProductType> LinkoProductTypes { get; }
    DbSet<LinkoMarketUser> LinkoMarketUsers { get; }
    DbSet<LinkoOrder> LinkoOrders { get; }
    DbSet<LinkoOrderLine> LinkoOrderLines { get; }
    DbSet<LinkoOrderReturn> LinkoOrderReturns { get; }
    DbSet<LinkoOrderReturnLine> LinkoOrderReturnLines { get; }
    DbSet<LinkoVisit> LinkoVisits { get; }
    DbSet<LinkoKpiPlan> LinkoKpiPlans { get; }
    DbSet<LinkoSyncState> LinkoSyncStates { get; }

    DbSet<SalesDirection> SalesDirections { get; }
    DbSet<SalesRegion> SalesRegions { get; }
    DbSet<SalesAgentProfile> SalesAgentProfiles { get; }
    DbSet<SalesRegionPlan> SalesRegionPlans { get; }
    DbSet<SalesAgentPlan> SalesAgentPlans { get; }
    DbSet<SalesTarget> SalesTargets { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
