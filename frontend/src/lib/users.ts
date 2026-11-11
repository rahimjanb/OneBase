import type { SalesScopeInfo } from "./sales/access";
/** Текущий пользователь (GET /api/auth/me). */
export type Me = {
  id: string;
  login: string;
  email: string | null;
  name: string;
  firstName: string;
  lastName: string;
  position: string | null;
  roles: string[];
  permissions: string[];
  /** Есть хоть одно право раздела «Настройки»; без него раздел скрыт. */
  canOpenSettings: boolean;
  /** Виден раздел HR (право «HR», интеграции или руководитель отдела с подразделениями Verifix). */
  hrAccess?: boolean;
  /** Заявки на сотрудника (HR → «Подбор»): руководитель отдела или тот, кто ведёт подбор. */
  hiringRequests?: boolean;
  /** «Настройки → ИИ-агенты»: отделы, агентами которых управляет пользователь. */
  agentDepartments?: string[];
  /** Отдел пользователя (по роли, у роли без отдела — выбранный в карточке). */
  departmentName?: string | null;
  /** Главная: my_day — «Мой день» (роль «Сотрудник»), overview — «Обзор» компании. */
  home?: "my_day" | "overview" | "work";
  /** Охват «Продаж» (с разделом «Продажи»): уровень и своя страница — вкладки раздела и вход в него. */
  salesScope?: SalesScopeInfo | null;
  /** Отделы, задачи которых открываются (свой, руководимые, у руководства — все); с разделами отделов решает, какие отделы видны. */
  departments?: string[];
};

export type UserRow = {
  id: string;
  login: string;
  firstName: string;
  lastName: string;
  fullName: string;
  email: string | null;
  phone: string | null;
  position: string | null;
  isActive: boolean;
  createdAt: string;
  /** Был в сети: последний запрос с действующим входом (отмечается не чаще раза в минуту); null — не заходил с начала отметок. */
  lastSeenAt?: string | null;
  /** В сети: OneBase открыт у пользователя (запрос за последние минуты). */
  online?: boolean;
  /** Последний вход (самый поздний сеанс на любом устройстве). */
  lastLoginAt?: string | null;
  roleId: string | null;
  roleName: string | null;
  /** Открытые разделы (коды каталога) — по роли или отмеченные вручную. */
  sections: string[];
  /** Разделы отмечены вручную (иначе — как у роли). */
  sectionsCustom: boolean;
  /** Права в разделах отделов и настройках (коды UserRights) — по роли или отмеченные вручную. */
  rights?: string[];
  /** Права отмечены вручную (иначе — как у роли). */
  rightsCustom?: boolean;
  isMe: boolean;
  /** false — у пользователя роль с правами, которых нет у вас: изменить может только администратор. */
  canEdit: boolean;
  /** Отдел (по роли, у роли без отдела — выбранный в карточке). */
  departmentId?: string | null;
  /** Сотрудник Verifix, к которому привязан пользователь («Мой день» — его рабочее время). */
  hrEmployee?: { id: string; name: string } | null;
};

export type RoleRow = {
  id: string;
  name: string;
  description: string | null;
  department: string | null;
  permissions: { code: string; label: string }[];
  users: number;
  /** Можно назначить: у вас есть все права роли. */
  assignable: boolean;
  opensSettings: boolean;
  /** Разделы по умолчанию для этой роли. */
  sections: string[];
  /** Права в разделах отделов и настройках по умолчанию для этой роли. */
  rights?: string[];
  departmentId?: string | null;
  /** Роль «Сотрудник»: отдел выбирается в карточке и обязателен, главная — «Мой день». */
  isEmployee?: boolean;
};

/** Раздел OneBase, который выбирается пользователю галочкой; grantable — у вас есть его права, выдать можно. */
export type UserSection = { key: string; title: string; description: string; grantable: boolean };

/**
 * Право в разделе отдела или в настройках (галочка): группа (supply, production, settings), что даёт; requires — включаются вместе с ним
 * (закупки — и просмотр заявок); needsSection — раздел, без которого право не действует (настройки продаж — «Продажи»); grantable — у вас
 * оно есть, выдать можно.
 */
export type UserRight = { key: string; group: string; title: string; description: string; requires: string[]; needsSection: string | null; grantable: boolean };

export type UserRightGroup = { key: string; title: string; description: string };

export type UsersView = {
  users: UserRow[];
  roles: RoleRow[];
  sections: UserSection[];
  rights?: UserRight[];
  rightGroups?: UserRightGroup[];
  departments?: { id: string; code: string; name: string }[];
  /** Verifix подключён: при создании пользователя выбирается его сотрудник или «Без привязки к Verifix». */
  verifixAvailable?: boolean;
};

/** Сотрудник Verifix для привязки (GET /api/users/hr-employees). LinkedTo — уже привязан к другому пользователю. */
export type HrCandidate = {
  id: string;
  name: string;
  firstName: string;
  lastName: string | null;
  phone: string | null;
  job: string | null;
  division: string | null;
  departmentId: string | null;
  linkedUserId: string | null;
  linkedTo: string | null;
};

/** «BEKBERGANOV» → «Bekberganov», «O'G'LI» → «O'g'li»: имена из Verifix приходят заглавными. */
export function titleCase(value: string | null | undefined): string {
  return (value ?? "")
    .toLowerCase()
    .split(/([\s-]+)/)
    .map((part) => (part.trim() ? part.charAt(0).toUpperCase() + part.slice(1) : part))
    .join("")
    .trim();
}

/** Права разделов — их показываем галочками, а не в списке «роль также даёт» (права отделов и настроек — тоже галочками, из каталога). */
export const sectionPermissions = new Set([
  "sales.read", "finance.read", "hr.read", "work.tasks", "files.read", "files.write", "files.read.all", "files.manage",
  "ai.agents.run", "field.plan", "field.use", "field.manage", "ai.data.manage", "telegram.campaigns",
]);

/**
 * Отметить или снять право: отмеченное включает то, без чего не работает (закупки — просмотр заявок); снятое снимает и те, кому оно нужно
 * (сняли просмотр — снимаются закупки). Порядок — как в каталоге.
 */
export function toggleRight(chosen: readonly string[], key: string, on: boolean, catalog: readonly Pick<UserRight, "key" | "requires">[]): string[] {
  const set = new Set(chosen);
  if (on) {
    const add = (k: string) => {
      if (set.has(k)) return;
      set.add(k);
      catalog.find((r) => r.key === k)?.requires.forEach(add);
    };
    add(key);
  } else {
    const drop = (k: string) => {
      set.delete(k);
      catalog.filter((r) => r.requires.includes(k) && set.has(r.key)).forEach((r) => drop(r.key));
    };
    drop(key);
  }
  return catalog.filter((r) => set.has(r.key)).map((r) => r.key);
}

/** Права, которые не действуют без снятого раздела (настройки продаж без «Продаж»), — убираются вместе с ним. */
export const rightsWithSections = (rights: readonly string[], sections: readonly string[], catalog: readonly Pick<UserRight, "key" | "needsSection">[]) =>
  rights.filter((k) => {
    const need = catalog.find((r) => r.key === k)?.needsSection;
    return !need || sections.includes(need);
  });

export const has = (me: Me | null, permission: string) => !!me?.permissions.includes(permission);

type T = (text: string, vars?: Record<string, string | number>) => string;

const tashkent = (d: Date, opts: Intl.DateTimeFormatOptions) => new Intl.DateTimeFormat("ru-RU", { timeZone: "Asia/Tashkent", ...opts }).format(d);
const dayOf = (d: Date) => tashkent(d, { year: "numeric", month: "2-digit", day: "2-digit" });

/** «03.11.2026 17:20» по Ташкенту. */
export const seenStamp = (iso: string) => tashkent(new Date(iso), { year: "numeric", month: "2-digit", day: "2-digit", hour: "2-digit", minute: "2-digit" }).replace(",", "");

/**
 * «Был в сети» для списка пользователей: «в сети» (OneBase открыт сейчас), «12 мин назад», «сегодня в 14:20», «вчера в 09:05», дата; без
 * отметок, но со входом — «входил …»; ни того ни другого — «не заходил». title — точное время и последний вход.
 */
export function seenOf(u: Pick<UserRow, "online" | "lastSeenAt" | "lastLoginAt">, t: T, now = new Date()): { text: string; tone: "online" | "recent" | "muted"; title: string } {
  const login = u.lastLoginAt ? t("Последний вход: {date}", { date: seenStamp(u.lastLoginAt) }) : t("Входов не было");
  if (u.online) return { text: t("в сети"), tone: "online", title: login };
  const iso = u.lastSeenAt ?? null;
  if (!iso) return u.lastLoginAt ? { text: t("входил {date}", { date: dayOf(new Date(u.lastLoginAt)) }), tone: "muted", title: login } : { text: t("не заходил"), tone: "muted", title: login };
  const at = new Date(iso);
  const title = `${t("Был в сети: {date}", { date: seenStamp(iso) })} · ${login}`;
  const minutes = Math.max(1, Math.round((now.getTime() - at.getTime()) / 60_000));
  if (minutes < 60) return { text: t("{n} мин назад", { n: minutes }), tone: "recent", title };
  const time = tashkent(at, { hour: "2-digit", minute: "2-digit" });
  if (dayOf(at) === dayOf(now)) return { text: t("сегодня в {time}", { time }), tone: "recent", title };
  if (dayOf(at) === dayOf(new Date(now.getTime() - 86_400_000))) return { text: t("вчера в {time}", { time }), tone: "muted", title };
  return { text: dayOf(at), tone: "muted", title };
}

export function initials(me: Pick<Me, "firstName" | "lastName" | "login">) {
  const letters = [me.firstName, me.lastName].map((x) => x.trim()[0] ?? "").join("");
  return (letters || me.login.slice(0, 2)).toUpperCase();
}
