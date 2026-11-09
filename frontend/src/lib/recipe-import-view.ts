/** Импорт техкарт из Excel: подписи статусов, тон, отбор рецептур, разбор чисел нормы — чистые функции (проверяются node --test). */

export type ImportRecipeStatus = "Ready" | "NeedsReview" | "Duplicate" | "Empty" | "Unchanged" | "Applied";
export type ImportAction = "None" | "Create" | "NewVersion";
export type ImportFilter = "all" | "ready" | "review" | "unchanged" | "skipped" | "applied" | "excluded";

export const importStatusLabel: Record<ImportRecipeStatus, string> = {
  Ready: "Готова",
  NeedsReview: "Нужна проверка",
  Duplicate: "Дубль столбца",
  Empty: "Пустая",
  Unchanged: "Без изменений",
  Applied: "Черновик создан",
};

export const importStatusTone: Record<ImportRecipeStatus, string> = {
  Ready: "bg-ok-soft text-ok",
  NeedsReview: "bg-warn-soft text-warn",
  Duplicate: "bg-muted text-ink-3",
  Empty: "bg-muted text-ink-3",
  Unchanged: "bg-muted text-ink-2",
  Applied: "bg-accent-soft text-accent-strong",
};

export const importActionLabel: Record<ImportAction, string> = {
  None: "",
  Create: "новая техкарта",
  NewVersion: "новая версия",
};

type Row = { status: ImportRecipeStatus; include: boolean };

/** Попадает ли рецептура в отбор: исключённые технологом — только в «Исключены»; дубли и пустые — в «Дубли и пустые». */
export function matchesFilter(row: Row, filter: ImportFilter): boolean {
  if (filter === "excluded") return !row.include;
  if (filter === "all") return true;
  if (!row.include) return false;
  switch (filter) {
    case "ready":
      return row.status === "Ready";
    case "review":
      return row.status === "NeedsReview";
    case "unchanged":
      return row.status === "Unchanged";
    case "skipped":
      return row.status === "Duplicate" || row.status === "Empty";
    case "applied":
      return row.status === "Applied";
  }
}

/** Сколько рецептур в каждом отборе — для вкладок. */
export function filterCounts(rows: Row[]): Record<ImportFilter, number> {
  const filters: ImportFilter[] = ["all", "ready", "review", "unchanged", "skipped", "applied", "excluded"];
  return Object.fromEntries(filters.map((f) => [f, rows.filter((r) => matchesFilter(r, f)).length])) as Record<ImportFilter, number>;
}

/**
 * Норма, введённая технологом: «25,542», «25.542», «1 050» → число; не больше 6 знаков после запятой, больше нуля. Иначе — null (поле
 * подсвечивается, ничего не округляется).
 */
export function parseManualNorm(text: string): number | null {
  const t = text.replace(/[\s ]/g, "").replace(",", ".");
  if (!/^\d+(\.\d+)?$/.test(t)) return null;
  const decimals = t.includes(".") ? t.split(".")[1].length : 0;
  const value = Number(t);
  return decimals <= 6 && value > 0 ? value : null;
}

/** Размер файла для людей: 2,5 МБ, 840 КБ. */
export function fileSize(bytes: number): string {
  if (bytes >= 1024 * 1024) return `${(bytes / 1024 / 1024).toFixed(1).replace(".", ",")} МБ`;
  return `${Math.max(1, Math.round(bytes / 1024))} КБ`;
}
