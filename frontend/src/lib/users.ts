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
};

/** Раздел OneBase, который выбирается пользователю галочкой; grantable — у вас есть его права, выдать можно. */
export type UserSection = { key: string; title: string; description: string; grantable: boolean };

export type UsersView = { users: UserRow[]; roles: RoleRow[]; sections: UserSection[] };

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
