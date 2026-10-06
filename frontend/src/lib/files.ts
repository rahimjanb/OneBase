import { bff } from "@/lib/bff";

/** «Файлы отделов»: типы ответов /api/files и запросы из браузера (через /bff, токен в httpOnly-cookie). */

export type Access = "none" | "read" | "write" | "manage";

export type FileUserRef = { name: string; viaWindows: boolean };

export type FileRow = {
  id: string;
  folderId: string;
  name: string;
  extension: string;
  contentType: string;
  sizeBytes: number;
  version: number;
  updatedAt: string;
  updatedBy: FileUserRef | null;
  hidden: boolean;
};

export type FolderRow = { id: string; name: string; folders: number; files: number; updatedAt: string };

export type DepartmentRef = { id: string; code: string; name: string; access: Access };

export type FolderView = {
  department: DepartmentRef;
  id: string;
  name: string;
  parentId: string | null;
  path: { id: string; name: string }[];
  folders: FolderRow[];
  files: FileRow[];
  canWrite: boolean;
};

export type DepartmentSummary = {
  id: string;
  code: string;
  name: string;
  access: Access;
  rootFolderId: string;
  files: number;
  folders: number;
  sizeBytes: number;
  updatedAt: string | null;
  connection: { status: "active" | "revoked" | "none"; canView: boolean; canManage: boolean };
};

export type SearchRow = { file: FileRow; department: DepartmentRef; folderPath: string };

export type TrashRow = { id: string; isFolder: boolean; name: string; sizeBytes: number; folderPath: string; deletedAt: string };

export type VersionRow = { number: number; sizeBytes: number; createdAt: string; by: FileUserRef | null; current: boolean };

export type PreviewSheet = { name: string; rows: string[][]; truncated: boolean };

export type FilePreview = { kind: "table" | "text" | "media" | "none"; sheets: PreviewSheet[]; paragraphs: string[]; truncated: boolean; note: string | null };

export type ConnectionView = {
  status: "active" | "revoked" | "none";
  connectionType: string;
  address: string | null;
  uncPath: string | null;
  username: string | null;
  access: "read" | "write";
  createdAt: string | null;
  lastUsedAt: string | null;
  passwordChangedAt: string | null;
  revokedAt: string | null;
  domainConfigured: boolean;
  canView: boolean;
  canManage: boolean;
  guide: string[];
  notes: string[];
};

export type ConnectionTest = { ok: boolean; steps: { name: string; ok: boolean; detail: string }[] };

export type DepartmentAccessRow = { id: string; code: string; name: string; access: Access };

const json = (body: unknown): RequestInit => ({ body: JSON.stringify(body) });

export const filesApi = {
  folder: (id: string) => bff<FolderView>(`files/folders/${id}`),
  createFolder: (parentId: string, name: string) => bff<{ id: string; name: string }>("files/folders", { method: "POST", ...json({ parentId, name }) }),
  renameFolder: (id: string, name: string) => bff<void>(`files/folders/${id}`, { method: "PUT", ...json({ name }) }),
  moveFolder: (id: string, targetFolderId: string) => bff<void>(`files/folders/${id}/move`, { method: "POST", ...json({ targetFolderId, replace: false }) }),
  deleteFolder: (id: string) => bff<void>(`files/folders/${id}`, { method: "DELETE" }),
  restoreFolder: (id: string) => bff<void>(`files/folders/${id}/restore`, { method: "POST" }),
  renameFile: (id: string, name: string) => bff<void>(`files/${id}`, { method: "PUT", ...json({ name }) }),
  moveFile: (id: string, targetFolderId: string) => bff<void>(`files/${id}/move`, { method: "POST", ...json({ targetFolderId, replace: false }) }),
  deleteFile: (id: string) => bff<void>(`files/${id}`, { method: "DELETE" }),
  restoreFile: (id: string) => bff<void>(`files/${id}/restore`, { method: "POST" }),
  versions: (id: string) => bff<VersionRow[]>(`files/${id}/versions`),
  preview: (id: string) => bff<FilePreview>(`files/${id}/preview`),
  trash: (code: string) => bff<TrashRow[]>(`files/departments/${code}/trash`),
  search: (q: string, department?: string) =>
    bff<SearchRow[]>(`files/search?q=${encodeURIComponent(q)}${department ? `&department=${encodeURIComponent(department)}` : ""}`),
  departments: () => bff<DepartmentSummary[]>("files/departments"),
  connection: (code: string) => bff<ConnectionView>(`files/departments/${code}/connection`),
  createConnection: (code: string) => bff<ConnectionView>(`files/departments/${code}/connection`, { method: "POST" }),
  regenerate: (code: string) => bff<ConnectionView>(`files/departments/${code}/connection/regenerate`, { method: "POST" }),
  revoke: (code: string) => bff<ConnectionView>(`files/departments/${code}/connection/revoke`, { method: "POST" }),
  password: (code: string) => bff<{ password: string }>(`files/departments/${code}/connection/password`, { method: "POST" }),
  test: (code: string) => bff<ConnectionTest>(`files/departments/${code}/connection/test`, { method: "POST" }),
  access: (code: string) => bff<DepartmentAccessRow[]>(`files/departments/${code}/access`),
  setAccess: (code: string, departmentId: string, access: "none" | "read" | "write") =>
    bff<void>(`files/departments/${code}/access`, { method: "PUT", ...json({ departmentId, access }) }),
};

/**
 * Ссылка на скачивание (inline — открыть картинку или PDF в браузере). Имя файла — в конце адреса: браузеры и менеджеры загрузок,
 * которые не читают Content-Disposition, берут имя и расширение из адреса (иначе сохраняют «download.html»).
 */
export const downloadUrl = (file: { id: string; name: string }, inline = false, version?: number) =>
  `/bff/api/files/${file.id}/download/${encodeURIComponent(file.name)}?inline=${inline}${version ? `&version=${version}` : ""}`;

/**
 * Загрузка файлов в папку одним запросом (multipart). XMLHttpRequest — ради прогресса: fetch его не сообщает.
 * Ошибка — Error с текстом сервера.
 */
export function uploadFiles(folderId: string, files: File[], onProgress: (loaded: number, total: number) => void): Promise<void> {
  return new Promise((resolve, reject) => {
    const form = new FormData();
    for (const file of files) form.append("files", file, file.name);
    const xhr = new XMLHttpRequest();
    xhr.open("POST", `/bff/api/files/folders/${folderId}/upload`);
    xhr.upload.onprogress = (e) => e.lengthComputable && onProgress(e.loaded, e.total);
    xhr.onerror = () => reject(new Error("Нет связи с сервером OneBase — файл не загружен."));
    xhr.onload = () => {
      if (xhr.status >= 200 && xhr.status < 300) return resolve();
      let message = `Ошибка ${xhr.status}`;
      try {
        message = JSON.parse(xhr.responseText).error ?? message;
      } catch {
        // не JSON
      }
      reject(new Error(xhr.status === 413 ? "Файл слишком большой для загрузки через сайт." : message));
    };
    xhr.send(form);
  });
}

export const accessLabel: Record<Access, string> = {
  none: "нет доступа",
  read: "только чтение",
  write: "чтение и запись",
  manage: "администратор",
};

const dateTime = new Intl.DateTimeFormat("ru-RU", { timeZone: "Asia/Tashkent", day: "2-digit", month: "2-digit", year: "numeric", hour: "2-digit", minute: "2-digit" });
const timeOnly = new Intl.DateTimeFormat("ru-RU", { timeZone: "Asia/Tashkent", hour: "2-digit", minute: "2-digit" });
const dayKey = new Intl.DateTimeFormat("en-CA", { timeZone: "Asia/Tashkent" });

/** «Сегодня, 14:05», «Вчера, 09:30» или «03.10.2026, 18:00». */
export function modifiedLabel(iso: string | null | undefined): string {
  if (!iso) return "—";
  const date = new Date(iso);
  const today = dayKey.format(new Date());
  const yesterday = dayKey.format(new Date(Date.now() - 86_400_000));
  const day = dayKey.format(date);
  if (day === today) return `Сегодня, ${timeOnly.format(date)}`;
  if (day === yesterday) return `Вчера, ${timeOnly.format(date)}`;
  return dateTime.format(date);
}

export type TypeFilter = "all" | "docs" | "sheets" | "pdf" | "images" | "other";

export const typeFilters: { key: TypeFilter; label: string }[] = [
  { key: "all", label: "Все" },
  { key: "docs", label: "Документы" },
  { key: "sheets", label: "Таблицы" },
  { key: "pdf", label: "PDF" },
  { key: "images", label: "Картинки" },
  { key: "other", label: "Другое" },
];

/** Группа типа по расширению — для фильтра и значка (представление, не бизнес-правило). */
export function typeGroup(extension: string): Exclude<TypeFilter, "all"> {
  if (["doc", "docx", "txt", "rtf", "odt"].includes(extension)) return "docs";
  if (["xls", "xlsx", "csv", "ods"].includes(extension)) return "sheets";
  if (extension === "pdf") return "pdf";
  if (["png", "jpg", "jpeg", "webp", "gif"].includes(extension)) return "images";
  return "other";
}
