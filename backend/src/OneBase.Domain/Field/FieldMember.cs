using OneBase.Domain.Common;

namespace OneBase.Domain.Field;

/// <summary>Роль участника Sales Base. Определяет область видимости: РМ — организация, супервайзер — команда, агент — свои данные.</summary>
public enum FieldRole
{
    Rm,
    Supervisor,
    Agent,
}

/// <summary>
/// Участник Sales Base: пользователь OneBase (для входа) и сотрудник Linko (для продаж, визитов, планов).
/// Связи с Linko — по его идентификатору, без внешнего ключа: зеркало Linko очищается при перезагрузке.
/// </summary>
public class FieldMember : Entity
{
    /// <summary>Пользователь OneBase; null — доступ ещё не выдан (участник виден РМ и супервайзеру, но войти не может).</summary>
    public Guid? UserId { get; set; }

    /// <summary>Сотрудник Linko (linko.Users.Id): заказы, визиты, планы и точки берутся по нему.</summary>
    public long? LinkoUserId { get; set; }

    public FieldRole Role { get; set; }
    public required string FullName { get; set; }
    public string? Phone { get; set; }

    /// <summary>Команда агента. У супервайзера и РМ — null (супервайзер ведёт команды через FieldTeam.SupervisorId).</summary>
    public Guid? TeamId { get; set; }
    public FieldTeam? Team { get; set; }

    /// <summary>Филиалы Linko, которые видит РМ; пусто — вся организация.</summary>
    public List<long> BranchIds { get; set; } = [];

    public bool IsActive { get; set; } = true;
}

/// <summary>Команда: супервайзер и его агенты; филиал Linko — для карты, фильтров и подсказок.</summary>
public class FieldTeam : Entity
{
    public required string Name { get; set; }
    public Guid? SupervisorId { get; set; }
    public FieldMember? Supervisor { get; set; }

    /// <summary>Филиал Linko (linko.Markets.BranchId / Orders.BranchId).</summary>
    public long? BranchId { get; set; }

    public bool IsActive { get; set; } = true;
    public List<FieldMember> Agents { get; set; } = [];
}
