using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Domain.Audit;
using OneBase.Domain.Field;

namespace OneBase.Application.Field;

public sealed record FieldVisitRow(
    Guid Id,
    long MarketId,
    string MarketName,
    string? Address,
    Guid AgentId,
    string AgentName,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    int? Minutes,
    FieldVisitStatus Status,
    FieldVisitResult? Result,
    decimal? Amount,
    string? Comment,
    FieldGeoStatus GeoStatus,
    double? DistanceM,
    double? AccuracyM,
    bool HasPhoto,
    Guid? RouteId,
    Guid? JointVisitId);

public sealed record FieldVisitQuery(Guid? AgentId, long? MarketId, DateOnly? From, DateOnly? To, FieldVisitResult? Result, FieldGeoStatus? GeoStatus, int Page = 1, int PageSize = 50);

public sealed record FieldVisitStartInput(long MarketId, Guid? RoutePointId, double? Latitude, double? Longitude, double? AccuracyM, Guid? JointVisitId);

public sealed record FieldVisitFinishInput(FieldVisitResult Result, decimal? Amount, string? Comment, DateOnly? RevisitDate);

public sealed record FieldJointVisitRow(Guid Id, DateOnly Date, TimeOnly? Time, long? MarketId, string? MarketName, string Objective, string? Result, string? Comment,
    FieldJointVisitStatus Status, IReadOnlyList<FieldParticipant> Participants, bool CanEdit);

public sealed record FieldParticipant(Guid MemberId, string Name, FieldRole Role);

public sealed record FieldJointVisitInput(DateOnly Date, TimeOnly? Time, long? MarketId, string? Objective, List<Guid>? ParticipantIds);

public sealed record FieldJointVisitUpdate(FieldJointVisitStatus Status, string? Result, string? Comment);

/// <summary>
/// Визиты: прибыл → начать визит (геопозиция и расстояние до точки) → результат → завершить. Плохой GPS визит не блокирует —
/// отметка «далеко»/«нет GPS» видна супервайзеру. Один незавершённый визит на участника.
/// </summary>
public sealed class FieldVisitService(
    IAppDbContext db,
    FieldDirectory directory,
    FieldAssignments assignments,
    FieldSettingsStore settingsStore,
    FieldRouteService routes,
    FieldTaskService tasks,
    ILinkoSalesProvider linko,
    IFileStorage storage,
    FieldNotifier notifier,
    IAuditLogger audit)
{
    public const long MaxPhotoBytes = 10 * 1024 * 1024;

    private IQueryable<FieldVisit> Visible(FieldScope scope)
    {
        var members = scope.MemberIds.ToArray();
        return db.FieldVisits.AsNoTracking().Where(v => members.Contains(v.AgentId));
    }

    public async Task<PageResult<FieldVisitRow>> ListAsync(FieldScope scope, FieldVisitQuery query, CancellationToken ct)
    {
        var q = Visible(scope);
        if (query.AgentId is { } agent)
        {
            q = q.Where(v => v.AgentId == agent);
        }

        if (query.MarketId is { } market)
        {
            q = q.Where(v => v.MarketId == market);
        }

        if (query.From is { } from)
        {
            var start = FieldClock.DayStartUtc(from);
            q = q.Where(v => v.StartedAt >= start);
        }

        if (query.To is { } to)
        {
            var end = FieldClock.DayStartUtc(to.AddDays(1));
            q = q.Where(v => v.StartedAt < end);
        }

        if (query.Result is { } result)
        {
            q = q.Where(v => v.Result == result);
        }

        if (query.GeoStatus is { } geo)
        {
            q = q.Where(v => v.GeoStatus == geo);
        }

        var total = await q.CountAsync(ct);
        var pageSize = Math.Clamp(query.PageSize, 10, 200);
        var page = Math.Max(query.Page, 1);
        var rows = await q.OrderByDescending(v => v.StartedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PageResult<FieldVisitRow>(await RowsAsync(rows, ct), total, page, pageSize);
    }

    public async Task<FieldVisitRow> GetAsync(FieldScope scope, Guid id, CancellationToken ct)
    {
        var visit = await Visible(scope).FirstOrDefaultAsync(v => v.Id == id, ct) ?? throw new FieldNotFoundException("Визит не найден.");
        return (await RowsAsync([visit], ct))[0];
    }

    /// <summary>Незавершённый визит текущего участника (для экрана «идёт визит»).</summary>
    public async Task<FieldVisitRow?> ActiveAsync(FieldScope scope, CancellationToken ct)
    {
        if (scope.MemberId is not { } me)
        {
            return null;
        }

        var visit = await db.FieldVisits.AsNoTracking().FirstOrDefaultAsync(v => v.AgentId == me && v.Status == FieldVisitStatus.InProgress, ct);
        return visit is null ? null : (await RowsAsync([visit], ct))[0];
    }

    private async Task<List<FieldVisitRow>> RowsAsync(IReadOnlyList<FieldVisit> visits, CancellationToken ct)
    {
        var snapshot = await directory.GetAsync(ct);
        var markets = await linko.MarketsAsync(visits.Select(v => v.MarketId).ToList(), ct);
        return visits.Select(v =>
        {
            var m = markets.GetValueOrDefault(v.MarketId);
            return new FieldVisitRow(v.Id, v.MarketId, m?.Name ?? $"Точка {v.MarketId}", m?.Address, v.AgentId, snapshot.NameOf(v.AgentId), v.StartedAt, v.EndedAt,
                v.EndedAt is { } e ? (int)Math.Round((e - v.StartedAt).TotalMinutes) : null, v.Status, v.Result, v.Amount, v.Comment, v.GeoStatus, v.DistanceM, v.AccuracyM,
                v.PhotoKey is not null, v.RouteId, v.JointVisitId);
        }).ToList();
    }

    public async Task<FieldVisitRow> StartAsync(FieldScope scope, FieldVisitStartInput input, CancellationToken ct)
    {
        var me = scope.MemberId ?? throw new FieldForbiddenException("Визит начинает участник Sales Base.");
        var market = await assignments.RequireMarketAsync(scope, input.MarketId, ct, allowRoutePoints: true);
        if (await db.FieldVisits.AsNoTracking().FirstOrDefaultAsync(v => v.AgentId == me && v.Status == FieldVisitStatus.InProgress, ct) is { } active)
        {
            throw new FieldConflictException("Сначала завершите начатый визит.", new { visitId = active.Id, active.MarketId });
        }

        if (input.Latitude is { } la && Math.Abs(la) > 90 || input.Longitude is { } lo && Math.Abs(lo) > 180)
        {
            throw new FieldValidationException("Неверные координаты.");
        }

        var settings = await settingsStore.GetAsync(ct);
        var (geo, distance) = FieldGeo.Check(market.Lat, market.Lon, input.Latitude, input.Longitude, input.AccuracyM, settings.GeoRadiusM, settings.GpsToleranceM);
        var snapshot = await directory.GetAsync(ct);

        FieldRoutePoint? point = null;
        if (input.RoutePointId is { } pid)
        {
            point = await db.FieldRoutePoints.Include(p => p.Route).FirstOrDefaultAsync(p => p.Id == pid, ct);
            if (point?.Route is null || point.MarketId != input.MarketId || point.Route.AgentId != me)
            {
                throw new FieldValidationException("Точка маршрута не совпадает с визитом.");
            }

            // Уже посещённую точку или маршрут другого дня не трогаем: повторный визит идёт без привязки, прежний результат точки сохраняется.
            if (point.Status is not (FieldPointStatus.Planned or FieldPointStatus.Skipped) || point.Route.Date != FieldClock.Today)
            {
                point = null;
            }
        }
        else
        {
            // Визит без явной точки маршрута — привязываем к сегодняшнему маршруту, если точка в нём есть.
            var today = FieldClock.Today;
            point = await db.FieldRoutePoints.Include(p => p.Route)
                .FirstOrDefaultAsync(p => p.MarketId == input.MarketId && p.Route!.AgentId == me && p.Route.Date == today && p.Status == FieldPointStatus.Planned, ct);
        }

        if (input.JointVisitId is { } jid && !await db.FieldJointVisitParticipants.AnyAsync(p => p.JointVisitId == jid && p.MemberId == me, ct))
        {
            throw new FieldValidationException("Вы не участник этого совместного выезда.");
        }

        var visit = new FieldVisit
        {
            MarketId = input.MarketId,
            AgentId = me,
            SupervisorId = snapshot.SupervisorOf(me),
            RouteId = point?.RouteId,
            RoutePointId = point?.Id,
            Latitude = input.Latitude,
            Longitude = input.Longitude,
            AccuracyM = input.AccuracyM is { } acc ? Math.Round(acc) : null,
            DistanceM = distance,
            GeoStatus = geo,
            JointVisitId = input.JointVisitId,
        };
        db.FieldVisits.Add(visit);
        if (point?.Route is not null)
        {
            point.Status = FieldPointStatus.InProgress;
            point.ActualArrival = visit.StartedAt;
            point.VisitId = visit.Id;
            point.Route.Status = FieldRouteStatus.InProgress;
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Двойное нажатие: второй визит отсекает уникальный индекс «один идущий визит на участника».
            if (await db.FieldVisits.AsNoTracking().FirstOrDefaultAsync(v => v.AgentId == me && v.Status == FieldVisitStatus.InProgress && v.Id != visit.Id, ct) is { } running)
            {
                throw new FieldConflictException("Сначала завершите начатый визит.", new { visitId = running.Id, running.MarketId });
            }

            throw;
        }

        await audit.LogAsync(ActorType.User, scope.UserId.ToString(), "field.visit.started", "field_visit", visit.Id.ToString(),
            new { visit.MarketId, geo = geo.ToString(), distance }, ct);
        return (await RowsAsync([visit], ct))[0];
    }

    public async Task<FieldVisitRow> FinishAsync(FieldScope scope, Guid id, FieldVisitFinishInput input, CancellationToken ct)
    {
        var visit = await db.FieldVisits.FirstOrDefaultAsync(v => v.Id == id, ct);
        if (visit is null || visit.AgentId != scope.MemberId)
        {
            throw new FieldNotFoundException("Визит не найден.");
        }

        if (visit.Status != FieldVisitStatus.InProgress)
        {
            throw new FieldValidationException("Визит уже завершён.");
        }

        if (input.Amount is < 0 or > 10_000_000_000)
        {
            throw new FieldValidationException("Сумма — от 0 до 10 млрд сум.");
        }

        visit.Status = FieldVisitStatus.Completed;
        visit.Result = input.Result;
        visit.Amount = input.Amount;
        visit.Comment = string.IsNullOrWhiteSpace(input.Comment) ? null : input.Comment.Trim()[..Math.Min(input.Comment.Trim().Length, 2000)];
        visit.EndedAt = DateTimeOffset.UtcNow;
        visit.UpdatedAt = visit.EndedAt;

        if (visit.RoutePointId is { } pid && await db.FieldRoutePoints.FirstOrDefaultAsync(p => p.Id == pid, ct) is { } point)
        {
            point.Status = FieldPointStatus.Visited;
            point.ActualDeparture = visit.EndedAt;
        }

        // Повторный визит — задача себе на выбранную дату (по умолчанию через 2 дня).
        if (input.Result == FieldVisitResult.Revisit)
        {
            var market = (await linko.MarketsAsync([visit.MarketId], ct)).GetValueOrDefault(visit.MarketId);
            var due = input.RevisitDate is { } d && d >= FieldClock.Today ? d : FieldClock.Today.AddDays(2);
            tasks.CreateSystem($"Повторный визит: {market?.Name ?? $"точка {visit.MarketId}"}", visit.Comment, visit.AgentId, visit.SupervisorId, visit.MarketId,
                FieldPriority.Medium, due, FieldActorType.System, null, visit.AgentId);
        }

        if (input.Result is FieldVisitResult.Closed)
        {
            notifier.Notify(visit.SupervisorId, FieldNotificationKind.Problem, "Агент отметил точку закрытой", visit.Comment, $"/field/customers/{visit.MarketId}");
        }

        await db.SaveChangesAsync(ct);
        if (visit.RouteId is { } routeId)
        {
            await routes.CloseIfDoneAsync(routeId, ct);
            await db.SaveChangesAsync(ct);
        }

        await audit.LogAsync(ActorType.User, scope.UserId.ToString(), "field.visit.finished", "field_visit", visit.Id.ToString(),
            new { result = input.Result.ToString(), input.Amount }, ct);
        return (await RowsAsync([visit], ct))[0];
    }

    public async Task CancelAsync(FieldScope scope, Guid id, CancellationToken ct)
    {
        var visit = await db.FieldVisits.FirstOrDefaultAsync(v => v.Id == id, ct);
        if (visit is null || visit.AgentId != scope.MemberId || visit.Status != FieldVisitStatus.InProgress)
        {
            throw new FieldNotFoundException("Визит не найден.");
        }

        visit.Status = FieldVisitStatus.Cancelled;
        visit.EndedAt = DateTimeOffset.UtcNow;
        if (visit.RoutePointId is { } pid && await db.FieldRoutePoints.FirstOrDefaultAsync(p => p.Id == pid, ct) is { } point && point.VisitId == visit.Id)
        {
            point.Status = FieldPointStatus.Planned;
            point.ActualArrival = null;
            point.VisitId = null;
        }

        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, scope.UserId.ToString(), "field.visit.cancelled", "field_visit", visit.Id.ToString(), null, ct);
    }

    private static readonly TimeSpan PhotoWindow = TimeSpan.FromMinutes(30);

    public async Task SavePhotoAsync(FieldScope scope, Guid id, Stream content, long size, string contentType, CancellationToken ct)
    {
        var visit = await db.FieldVisits.FirstOrDefaultAsync(v => v.Id == id, ct);
        if (visit is null || visit.AgentId != scope.MemberId)
        {
            throw new FieldNotFoundException("Визит не найден.");
        }

        // Фото — подтверждение визита: во время визита или в течение получаса после завершения, не задним числом.
        if (visit.Status == FieldVisitStatus.Cancelled
            || (visit.Status != FieldVisitStatus.InProgress && (visit.EndedAt is not { } ended || DateTimeOffset.UtcNow - ended > PhotoWindow)))
        {
            throw new FieldValidationException("Фото добавляется во время визита или в течение 30 минут после него.");
        }

        if (size is <= 0 or > MaxPhotoBytes)
        {
            throw new FieldValidationException("Фото — до 10 МБ.");
        }

        var ext = contentType switch
        {
            "image/jpeg" => "jpg",
            "image/png" => "png",
            "image/webp" => "webp",
            "image/heic" or "image/heif" => "heic",
            _ => throw new FieldValidationException("Фото — JPEG, PNG, WebP или HEIC."),
        };

        var key = $"field/visits/{visit.Id:N}/{Guid.CreateVersion7():N}.{ext}";
        await storage.PutAsync(key, content, size, contentType, ct);
        if (visit.PhotoKey is { } old)
        {
            try
            {
                await storage.DeleteAsync(old, ct);
            }
            catch (Exception)
            {
                // Старое фото не удалилось — не страшно, ссылка на него уже заменена.
            }
        }

        visit.PhotoKey = key;
        visit.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task<(string Key, string ContentType)> PhotoAsync(FieldScope scope, Guid id, CancellationToken ct)
    {
        var visit = await Visible(scope).FirstOrDefaultAsync(v => v.Id == id, ct);
        if (visit?.PhotoKey is not { } key)
        {
            throw new FieldNotFoundException("Фото нет.");
        }

        var contentType = Path.GetExtension(key) switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".heic" => "image/heic",
            _ => "image/jpeg",
        };
        return (key, contentType);
    }

    // ── Совместные выезды ──

    public async Task<List<FieldJointVisitRow>> JointListAsync(FieldScope scope, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var members = scope.MemberIds.ToArray();
        var rows = await db.FieldJointVisits.AsNoTracking().Include(j => j.Participants)
            .Where(j => j.Date >= from && j.Date <= to && j.Participants.Any(p => members.Contains(p.MemberId)))
            .OrderBy(j => j.Date).ThenBy(j => j.Time)
            .ToListAsync(ct);
        return await JointRowsAsync(scope, rows, ct);
    }

    private async Task<List<FieldJointVisitRow>> JointRowsAsync(FieldScope scope, IReadOnlyList<FieldJointVisit> rows, CancellationToken ct)
    {
        var snapshot = await directory.GetAsync(ct);
        var markets = await linko.MarketsAsync(rows.Where(r => r.MarketId is not null).Select(r => r.MarketId!.Value).ToList(), ct);
        return rows.Select(j => new FieldJointVisitRow(j.Id, j.Date, j.Time, j.MarketId, j.MarketId is { } m ? markets.GetValueOrDefault(m)?.Name : null, j.Objective, j.Result, j.Comment, j.Status,
                j.Participants.Select(p => new FieldParticipant(p.MemberId, snapshot.NameOf(p.MemberId), snapshot.Members.GetValueOrDefault(p.MemberId)?.Role ?? FieldRole.Agent)).ToList(),
                scope.CanPlan || j.CreatedById == scope.MemberId))
            .ToList();
    }

    /// <summary>Совместный выезд: супервайзер + агент или два агента. Ставит РМ или супервайзер; участники — из его зоны.</summary>
    public async Task<Guid> JointCreateAsync(FieldScope scope, FieldJointVisitInput input, CancellationToken ct)
    {
        if (!scope.CanPlan)
        {
            throw new FieldForbiddenException("Совместный выезд назначает супервайзер или РМ.");
        }

        var objective = (input.Objective ?? string.Empty).Trim();
        if (objective.Length is 0 or > 500)
        {
            throw new FieldValidationException("Укажите цель выезда (до 500 символов).");
        }

        var participants = (input.ParticipantIds ?? []).Distinct().ToList();
        if (participants.Count < 2 || participants.Count > 6)
        {
            throw new FieldValidationException("В совместном выезде — от 2 до 6 участников.");
        }

        if (participants.Any(p => !scope.CanSeeMember(p)))
        {
            throw new FieldForbiddenException("Участники — только из вашей зоны.");
        }

        if (input.Date < FieldClock.Today)
        {
            throw new FieldValidationException("Дата выезда — сегодня или позже.");
        }

        if (input.MarketId is { } marketId)
        {
            await assignments.RequireMarketAsync(scope, marketId, ct);
        }

        var joint = new FieldJointVisit { Date = input.Date, Time = input.Time, MarketId = input.MarketId, Objective = objective, CreatedById = scope.MemberId };
        foreach (var p in participants)
        {
            joint.Participants.Add(new FieldJointVisitParticipant { JointVisitId = joint.Id, MemberId = p });
        }

        db.FieldJointVisits.Add(joint);
        notifier.Notify(participants.Where(p => p != scope.MemberId).Select(p => (Guid?)p), FieldNotificationKind.JointVisit,
            $"Совместный выезд {input.Date:dd.MM}{(input.Time is { } t ? $" в {t:HH\\:mm}" : string.Empty)}", objective, "/field/visits?tab=joint");
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, scope.UserId.ToString(), "field.joint_visit.created", "field_joint_visit", joint.Id.ToString(),
            new { joint.Date, joint.MarketId, participants }, ct);
        return joint.Id;
    }

    public async Task JointUpdateAsync(FieldScope scope, Guid id, FieldJointVisitUpdate input, CancellationToken ct)
    {
        var joint = await db.FieldJointVisits.Include(j => j.Participants).FirstOrDefaultAsync(j => j.Id == id, ct);
        if (joint is null || !joint.Participants.Any(p => scope.CanSeeMember(p.MemberId)))
        {
            throw new FieldNotFoundException("Совместный выезд не найден.");
        }

        if (!scope.CanPlan && joint.Participants.All(p => p.MemberId != scope.MemberId))
        {
            throw new FieldForbiddenException("Итог выезда записывает участник, супервайзер или РМ.");
        }

        joint.Status = input.Status;
        joint.Result = string.IsNullOrWhiteSpace(input.Result) ? joint.Result : input.Result.Trim()[..Math.Min(input.Result.Trim().Length, 2000)];
        joint.Comment = string.IsNullOrWhiteSpace(input.Comment) ? joint.Comment : input.Comment.Trim()[..Math.Min(input.Comment.Trim().Length, 2000)];
        joint.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, scope.UserId.ToString(), "field.joint_visit.updated", "field_joint_visit", joint.Id.ToString(),
            new { status = joint.Status.ToString() }, ct);
    }
}
