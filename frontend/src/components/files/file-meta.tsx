import { File, FileSpreadsheet, FileText } from "lucide-react";
import type { FileKind } from "@/lib/demo-data";

const kinds: Record<FileKind, { short: string; long: string; icon: typeof File; color: string }> = {
  xlsx: { short: "Excel", long: "Лист Microsoft Excel", icon: FileSpreadsheet, color: "text-emerald-600" },
  pdf: { short: "PDF", long: "PDF документ", icon: FileText, color: "text-red-500" },
  docx: { short: "Word", long: "Документ Microsoft Word", icon: FileText, color: "text-blue-600" },
  other: { short: "Файл", long: "Файл", icon: File, color: "text-ink-3" },
};

export const kindShort = (kind: FileKind) => kinds[kind].short;
export const kindLong = (kind: FileKind) => kinds[kind].long;

export function kindFromName(name: string): FileKind {
  const ext = name.split(".").pop()?.toLowerCase();
  if (ext === "xlsx" || ext === "xls" || ext === "csv") return "xlsx";
  if (ext === "pdf") return "pdf";
  if (ext === "docx" || ext === "doc") return "docx";
  return "other";
}

export function FileIcon({ kind }: { kind: FileKind }) {
  const { icon: Icon, color } = kinds[kind];
  return <Icon className={`size-4 shrink-0 ${color}`} strokeWidth={1.75} />;
}
