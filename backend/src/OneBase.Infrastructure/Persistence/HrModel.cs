using Microsoft.EntityFrameworkCore;
using OneBase.Domain.Hr;

namespace OneBase.Infrastructure.Persistence;

/// <summary>
/// Схема "hr": сотрудники, справочники, отметки и табель из Verifix, журнал и состояние синхронизации.
/// Сопоставление — только по ID Verifix (уникальные индексы): повторная загрузка обновляет строки, а не добавляет дубликаты.
/// </summary>
internal static class HrModel
{
    private const string Schema = "hr";

    public static void Configure(ModelBuilder b)
    {
        b.Entity<HrEmployee>(e =>
        {
            e.ToTable("Employees", Schema);
            e.HasKey(x => x.Id).HasName("PK_HrEmployees");
            e.HasIndex(x => x.VerifixId).IsUnique().HasDatabaseName("IX_HrEmployees_VerifixId");
            e.HasIndex(x => new { x.Status, x.DivisionId }).HasDatabaseName("IX_HrEmployees_Status_DivisionId");
            e.Property(x => x.FirstName).HasMaxLength(200);
            e.Property(x => x.LastName).HasMaxLength(200);
            e.Property(x => x.MiddleName).HasMaxLength(200);
            e.Property(x => x.FullName).HasMaxLength(600);
            e.Property(x => x.Phone).HasMaxLength(50);
            e.Property(x => x.Email).HasMaxLength(200);
            e.Property(x => x.Status).HasMaxLength(4);
            e.Property(x => x.State).HasMaxLength(4);
            e.Property(x => x.SourceHash).HasMaxLength(64);
            e.Property(x => x.Note).HasMaxLength(2000);
        });

        b.Entity<HrReference>(e =>
        {
            e.ToTable("References", Schema);
            e.HasKey(x => new { x.Kind, x.VerifixId }).HasName("PK_HrReferences");
            e.Property(x => x.Kind).HasMaxLength(16);
            e.Property(x => x.Name).HasMaxLength(300);
            e.Property(x => x.Code).HasMaxLength(50);
            e.Property(x => x.State).HasMaxLength(4);
            e.Property(x => x.Address).HasMaxLength(500);
            e.Property(x => x.DepartmentCode).HasMaxLength(64);
        });

        b.Entity<HrAttendanceEvent>(e =>
        {
            e.ToTable("AttendanceEvents", Schema);
            e.HasKey(x => x.Id).HasName("PK_HrAttendanceEvents");
            e.HasIndex(x => x.VerifixTrackId).IsUnique().HasDatabaseName("IX_HrAttendanceEvents_VerifixTrackId");
            e.HasIndex(x => new { x.LocalDate, x.VerifixEmployeeId }).HasDatabaseName("IX_HrAttendanceEvents_LocalDate_Employee");
            e.HasIndex(x => new { x.EmployeeId, x.LocalTime }).HasDatabaseName("IX_HrAttendanceEvents_EmployeeId_LocalTime");
            e.Property(x => x.TrackType).HasMaxLength(4);
            e.Property(x => x.MarkType).HasMaxLength(4);
            e.Property(x => x.RawTime).HasMaxLength(40);
            e.Property(x => x.LocalTime).HasColumnType("timestamp without time zone");
            e.HasOne<HrEmployee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_HrAttendanceEvents_HrEmployees_EmployeeId");
        });

        b.Entity<HrTimesheetDay>(e =>
        {
            e.ToTable("TimesheetDays", Schema);
            e.HasKey(x => x.Id).HasName("PK_HrTimesheetDays");
            e.HasIndex(x => new { x.StaffId, x.Date }).IsUnique().HasDatabaseName("IX_HrTimesheetDays_StaffId_Date");
            e.HasIndex(x => new { x.Date, x.EmployeeId }).HasDatabaseName("IX_HrTimesheetDays_Date_EmployeeId");
            e.Property(x => x.DayKind).HasMaxLength(4);
            e.Property(x => x.FactsJson).HasColumnType("jsonb");
            foreach (var time in new[] { nameof(HrTimesheetDay.PlanBegin), nameof(HrTimesheetDay.PlanEnd), nameof(HrTimesheetDay.BreakBegin),
                         nameof(HrTimesheetDay.BreakEnd), nameof(HrTimesheetDay.FirstIn), nameof(HrTimesheetDay.LastOut) })
            {
                e.Property(time).HasColumnType("timestamp without time zone");
            }

            e.HasOne<HrEmployee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_HrTimesheetDays_HrEmployees_EmployeeId");
        });

        b.Entity<HrSyncRun>(e =>
        {
            e.ToTable("SyncRuns", Schema);
            e.HasKey(x => x.Id).HasName("PK_HrSyncRuns");
            e.HasIndex(x => x.StartedAt).HasDatabaseName("IX_HrSyncRuns_StartedAt");
            // Не больше одного идущего запуска на всю базу — и на несколько экземпляров API.
            e.HasIndex(x => x.Status).IsUnique().HasFilter("\"Status\" = 'Running'").HasDatabaseName("IX_HrSyncRuns_SingleRunning");
            e.Property(x => x.Trigger).HasMaxLength(16);
            e.Property(x => x.Status).HasMaxLength(16);
            e.Property(x => x.Message).HasMaxLength(1000);
            e.Property(x => x.StepsJson).HasColumnType("jsonb");
        });

        b.Entity<HrSyncState>(e =>
        {
            e.ToTable("SyncStates", Schema);
            e.HasKey(x => x.Entity).HasName("PK_HrSyncStates");
            e.Property(x => x.Entity).HasMaxLength(32);
            e.Property(x => x.LastError).HasMaxLength(1000);
        });
    }
}
