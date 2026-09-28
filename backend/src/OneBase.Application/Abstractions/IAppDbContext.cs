using Microsoft.EntityFrameworkCore;
using OneBase.Domain.AI;
using OneBase.Domain.Audit;
using OneBase.Domain.Files;
using OneBase.Domain.Identity;

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

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
