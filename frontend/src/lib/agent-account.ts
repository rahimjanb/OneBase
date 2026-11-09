/** Номер Telegram у ИИ-агента («Настройки → ИИ-агенты» → агент → «Номер Telegram»): подпись и что изменится у номера при выборе. */

export type AgentAccountOption = {
  id: string;
  name: string;
  username: string | null;
  phoneHint: string | null;
  status: "New" | "CodeRequired" | "PasswordRequired" | "Connected" | "Disconnected" | "Error";
  /** Номер уже работает на отдел агента. */
  serves: boolean;
  /** Кому на номере новые диалоги (код и название отдела); номер без подразделений — продажам. */
  defaultDepartment: string | null;
  defaultDepartmentName: string | null;
  /** Номер выбран у агента другого отдела. */
  takenBy: string | null;
  selectable: boolean;
};

/** Подпись номера Telegram: название, @username, последние цифры. */
export function accountLabel(a: Pick<AgentAccountOption, "name" | "username" | "phoneHint">): string {
  return [a.name, a.username ? `@${a.username}` : null, a.phoneHint ? `…${a.phoneHint}` : null].filter(Boolean).join(" · ");
}

/**
 * Что изменится у номера, если выбрать его агенту отдела department: null — ничего (номер уже работает на отдел и отдаёт ему новые диалоги);
 * "configure" — номер начнёт работать на отдел и отдавать ему новые диалоги (может тот, кто настраивает номера); "foreign-default" — номер
 * работает на отдел, но новые диалоги идут другому отделу (а настраивать номера нельзя).
 */
export function accountChange(a: AgentAccountOption, department: string, canConfigure: boolean): null | "configure" | "foreign-default" {
  if (a.serves && a.defaultDepartment === department) return null;
  return canConfigure ? "configure" : a.serves ? "foreign-default" : null;
}
