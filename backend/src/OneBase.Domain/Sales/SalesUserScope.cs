namespace OneBase.Domain.Sales;

/// <summary>Уровень охвата пользователя в разделе «Продажи».</summary>
public enum SalesScopeLevel
{
    /// <summary>РМ: регионы своих направлений (регион, переведённый в другое направление, сразу уходит из охвата).</summary>
    Rm,

    /// <summary>Супервайзер (СВР): свои регионы и их ТП — в Linko ТП к супервайзеру не привязан, команда СВР — ТП региона.</summary>
    Supervisor,

    /// <summary>Торговый представитель: только он сам — свои продажи, клиенты, план и KPI.</summary>
    Agent,
}

/// <summary>
/// Охват пользователя в «Продажах» (назначает администратор пользователей). Нет записи — вся организация, как было до охватов (директор,
/// аналитики, финансы, производство…). Запись есть — пользователь видит только свою часть оргструктуры, и проверяет это сервер на каждом
/// запросе. Связь с оргструктурой — по её же данным: направления РМ (sales.Directions), регионы (sales.Regions = филиалы Linko), ТП Linko.
/// </summary>
public class SalesUserScope
{
    public Guid UserId { get; set; }

    public SalesScopeLevel Level { get; set; }

    /// <summary>РМ — направления (sales.Directions.Id).</summary>
    public List<Guid> DirectionIds { get; set; } = [];

    /// <summary>Супервайзер — регионы (sales.Regions.Id).</summary>
    public List<Guid> RegionIds { get; set; } = [];

    /// <summary>ТП — сотрудник Linko (linko.Users.Id).</summary>
    public long? AgentId { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? UpdatedById { get; set; }
}
