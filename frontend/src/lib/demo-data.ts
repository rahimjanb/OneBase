// Демо-данные до подключения backend API. Структура повторяет будущие ответы API.

export type DepartmentStatus = "done" | "ok" | "attention";

export type Department = {
  code: string;
  name: string;
  /** Общий показатель выполнения плана, %. */
  score: number;
  status: DepartmentStatus;
  metric: { label: string; value: number };
  filesCount: number;
  foldersCount: number;
  updated: string;
};

export type Folder = {
  id: string;
  department: string;
  parentId: string | null;
  name: string;
  filesCount: number;
  updated: string;
  createdBy: string;
  createdAt: string;
};

export type FileKind = "xlsx" | "pdf" | "docx" | "other";

export type FileEntry = {
  id: string;
  folderId: string;
  name: string;
  kind: FileKind;
  sizeBytes: number;
  modified: string;
  uploadedBy: string;
  uploadedAt: string;
  version: number;
};

export const currentUser = {
  login: "rahimjanb",
  initials: "РБ",
  role: "Администратор",
};

export const kpis = [
  { label: "Общий KPI", value: "89,4%", trend: "+2,6% к прошлому периоду", tone: "up" },
  { label: "Выполнение планов", value: "92,1%", trend: "+4,2% к прошлому периоду", tone: "up" },
  { label: "Активные задачи", value: "128", trend: "24 на сегодня", tone: "today" },
  { label: "Просрочено", value: "07", trend: "−3 за неделю", tone: "warn" },
] as const;

export const departments: Department[] = [
  { code: "production", name: "Производство", score: 92, status: "done", metric: { label: "Выпуск продукции", value: 86 }, filesCount: 248, foldersCount: 18, updated: "Сегодня" },
  { code: "finance", name: "Финансы", score: 87, status: "attention", metric: { label: "Платёжная дисциплина", value: 72 }, filesCount: 126, foldersCount: 9, updated: "Сегодня" },
  { code: "sales", name: "Продажи", score: 94, status: "ok", metric: { label: "Выручка за период", value: 91 }, filesCount: 542, foldersCount: 27, updated: "10 мин назад" },
  { code: "marketing", name: "Маркетинг", score: 81, status: "attention", metric: { label: "Квалифицированные лиды", value: 64 }, filesCount: 183, foldersCount: 14, updated: "Сегодня" },
  { code: "supply", name: "Снабжение", score: 90, status: "ok", metric: { label: "Поставки в срок", value: 83 }, filesCount: 97, foldersCount: 8, updated: "Вчера" },
  { code: "hr", name: "HR", score: 88, status: "ok", metric: { label: "Удержание команды", value: 78 }, filesCount: 64, foldersCount: 6, updated: "Сегодня" },
];

export const attentionItems = [
  { department: "Финансы", text: "расходы выше плана на 8%" },
  { department: "Маркетинг", text: "лиды ниже целевого уровня" },
];


const folder = (
  id: string,
  department: string,
  name: string,
  filesCount: number,
  updated: string,
  createdAt: string,
  parentId: string | null = null,
  createdBy = "Иван Иванов",
): Folder => ({ id, department, parentId, name, filesCount, updated, createdBy, createdAt });

export const folders: Folder[] = [
  folder("reports", "sales", "Отчёты", 38, "28 сен", "28 сентября 2026"),
  folder("plans", "sales", "Планы", 24, "27 сен", "27 сентября 2026", null, "Анна Петрова"),
  folder("kpi", "sales", "KPI", 16, "26 сен", "26 сентября 2026"),
  folder("routes", "sales", "Маршруты", 42, "26 сен", "26 сентября 2026", null, "Олег Смирнов"),
  folder("sales-2026", "sales", "2026", 68, "25 сен", "12 января 2026"),
  folder("clients", "sales", "Клиенты", 94, "25 сен", "15 января 2026", null, "Анна Петрова"),
  folder("shared", "sales", "Общие документы", 18, "24 сен", "3 февраля 2026"),
  folder("mine", "sales", "Мои документы", 12, "Сегодня", "1 марта 2026", null, "rahimjanb"),
  folder("reports-2026", "sales", "2026", 24, "28 сен", "5 января 2026", "reports"),
  folder("reports-archive", "sales", "Архив", 14, "30 авг", "5 января 2026", "reports"),

  folder("prod-plans", "production", "Производственные планы", 64, "Сегодня", "10 января 2026", null, "Сергей Козлов"),
  folder("prod-quality", "production", "Контроль качества", 51, "Вчера", "10 января 2026", null, "Сергей Козлов"),
  folder("fin-budget", "finance", "Бюджет", 32, "Сегодня", "9 января 2026", null, "Мария Орлова"),
  folder("fin-invoices", "finance", "Счета", 58, "Сегодня", "9 января 2026", null, "Мария Орлова"),
  folder("mkt-campaigns", "marketing", "Кампании", 47, "Сегодня", "14 января 2026", null, "Дарья Волкова"),
  folder("sup-contracts", "supply", "Договоры с поставщиками", 29, "Вчера", "20 января 2026", null, "Павел Никитин"),
  folder("hr-staff", "hr", "Кадровые документы", 22, "Сегодня", "8 января 2026", null, "Елена Соколова"),
];

/** Файлы в каждой папке — от новых к старым. */
export const files: FileEntry[] = [
  { id: "f1", folderId: "reports", name: "Продажи_сентябрь.xlsx", kind: "xlsx", sizeBytes: 4.8 * 1024 * 1024, modified: "сегодня, 15:47", uploadedBy: "Иван Иванов", uploadedAt: "28.09.2026 15:42", version: 3 },
  { id: "f4", folderId: "reports", name: "План_продаж.pdf", kind: "pdf", sizeBytes: 1.2 * 1024 * 1024, modified: "сегодня, 12:18", uploadedBy: "Анна Петрова", uploadedAt: "28.09.2026 12:18", version: 1 },
  { id: "f3", folderId: "reports", name: "Отчёт_регион.pdf", kind: "pdf", sizeBytes: 2.1 * 1024 * 1024, modified: "27.09.2026, 16:05", uploadedBy: "Олег Смирнов", uploadedAt: "27.09.2026 16:05", version: 2 },
  { id: "f2", folderId: "reports", name: "Продажи_август.xlsx", kind: "xlsx", sizeBytes: 3.9 * 1024 * 1024, modified: "02.09.2026, 11:20", uploadedBy: "Иван Иванов", uploadedAt: "02.09.2026 11:20", version: 1 },
  { id: "f5", folderId: "kpi", name: "KPI_менеджеров.xlsx", kind: "xlsx", sizeBytes: 860 * 1024, modified: "вчера, 17:05", uploadedBy: "Иван Иванов", uploadedAt: "27.09.2026 17:05", version: 4 },
];

/** Недавние файлы отдела — по порядку загрузки. */
export const recentFileIds: Record<string, string[]> = {
  sales: ["f1", "f4", "f5"],
};

export const findDepartment = (code: string) => departments.find((d) => d.code === code);
export const findFolder = (id: string) => folders.find((f) => f.id === id);
export const childFolders = (department: string, parentId: string | null) =>
  folders.filter((f) => f.department === department && f.parentId === parentId);
export const folderFiles = (folderId: string) => files.filter((f) => f.folderId === folderId);
export const recentFiles = (department: string) =>
  (recentFileIds[department] ?? []).map((id) => files.find((f) => f.id === id)!).filter(Boolean);

/** Цепочка папок от корня отдела до указанной. */
export function folderPath(id: string): Folder[] {
  const path: Folder[] = [];
  for (let current = findFolder(id); current; current = current.parentId ? findFolder(current.parentId) : undefined) {
    path.unshift(current);
  }
  return path;
}
