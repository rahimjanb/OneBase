namespace OneBase.Application.Security;

/// <summary>Коды разрешений RBAC. Выдаются ролям и попадают в JWT как claim "perm".</summary>
public static class Permissions
{
    public const string UsersManage = "users.manage";
    public const string FilesRead = "files.read";
    public const string FilesWrite = "files.write";
    public const string AgentsRun = "ai.agents.run";
    public const string ApprovalsDecide = "ai.approvals.decide";
    public const string AuditRead = "audit.read";

    public static readonly IReadOnlyList<string> All =
    [
        UsersManage,
        FilesRead,
        FilesWrite,
        AgentsRun,
        ApprovalsDecide,
        AuditRead,
    ];
}
