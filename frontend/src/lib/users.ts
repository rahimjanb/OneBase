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
  /** Отдел пользователя (по роли, у роли без отдела — выбранный в карточке). */
  departmentName?: string | null;
  /** Главная: my_day — «Мой день» (роль «Сотрудник»), overview — «Обзор» компании. */
  home?: "my_day" | "overview";
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
  roleId: string | null;
  roleName: string | null;
  /** Открытые разделы (коды каталога) — по роли или отмеченные вручную. */
  sections: string[];
  /** Разделы отмечены вручную (иначе — как у роли). */
  sectionsCustom: boolean;
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
  departmentId?: string | null;
  /** Роль «Сотрудник»: отдел выбирается в карточке и обязателен, главная — «Мой день». */
  isEmployee?: boolean;
};

/** Раздел OneBase, который выбирается пользователю галочкой; grantable — у вас есть его права, выдать можно. */
export type UserSection = { key: string; title: string; description: string; grantable: boolean };

export type UsersView = {
  users: UserRow[];
  roles: RoleRow[];
  sections: UserSection[];
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

/** Права разделов — их показываем галочками, а не в списке «роль также даёт». */
export const sectionPermissions = new Set([
  "sales.read", "sales.manage", "finance.read", "hr.read", "work.tasks", "files.read", "files.write", "files.read.all", "files.manage",
  "ai.agents.run", "field.plan", "field.use", "field.manage", "ai.data.manage", "telegram.campaigns",
]);

export const has = (me: Me | null, permission: string) => !!me?.permissions.includes(permission);

export function initials(me: Pick<Me, "firstName" | "lastName" | "login">) {
  const letters = [me.firstName, me.lastName].map((x) => x.trim()[0] ?? "").join("");
  return (letters || me.login.slice(0, 2)).toUpperCase();
}
