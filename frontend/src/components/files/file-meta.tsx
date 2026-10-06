import { File, FileArchive, FileImage, FileSpreadsheet, FileText, Folder } from "lucide-react";
import { typeGroup } from "@/lib/files";

const groups = {
  docs: { short: "Документ", icon: FileText, color: "text-blue-600" },
  sheets: { short: "Таблица", icon: FileSpreadsheet, color: "text-emerald-600" },
  pdf: { short: "PDF", icon: FileText, color: "text-red-500" },
  images: { short: "Картинка", icon: FileImage, color: "text-violet-500" },
  other: { short: "Файл", icon: File, color: "text-ink-3" },
} as const;

/** «XLSX», «PDF», «Файл» — тип для колонки «Тип». */
export const typeShort = (extension: string) => (extension ? extension.toUpperCase() : groups[typeGroup(extension)].short);

export function FileIcon({ extension }: { extension: string }) {
  const { icon: Icon, color } = ["zip", "rar", "7z"].includes(extension)
    ? { icon: FileArchive, color: "text-amber-600" }
    : groups[typeGroup(extension)];
  return <Icon className={`size-4 shrink-0 ${color}`} strokeWidth={1.75} />;
}

export function FolderIcon() {
  return <Folder className="size-4 shrink-0 text-amber-500" strokeWidth={1.75} fill="currentColor" fillOpacity={0.15} />;
}
