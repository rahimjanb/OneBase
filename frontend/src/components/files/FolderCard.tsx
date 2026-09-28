import Link from "next/link";
import { Folder as FolderIcon, Info } from "lucide-react";
import type { Folder } from "@/lib/demo-data";
import { filesLabel } from "@/lib/format";

export function FolderCard({
  folder,
  href,
  departmentName,
  compact = false,
}: {
  folder: Folder;
  href: string;
  departmentName: string;
  compact?: boolean;
}) {
  return (
    <div className="relative">
      <Link
        href={href}
        className={`block rounded-xl border border-line bg-surface p-4 pr-10 transition-colors hover:border-accent/40 ${compact ? "" : "min-h-[88px]"}`}
      >
        <div className="flex items-start gap-3">
          <FolderIcon className="mt-px size-5 shrink-0 text-warn" strokeWidth={1.75} />
          <div className="min-w-0">
            <div className="truncate text-sm font-semibold text-ink">{folder.name}</div>
            <div className="mt-1.5 text-xs text-ink-3">
              {filesLabel(folder.filesCount)}
              {!compact && <> · {folder.updated}</>}
            </div>
          </div>
        </div>
      </Link>

      {/* Подсказка вынесена из ссылки, чтобы не вкладывать интерактивные элементы */}
      <span tabIndex={0} aria-label={`Сведения о папке ${folder.name}`} className="group/info absolute right-3 top-4 outline-none">
        <Info className="size-4 text-ink-3 group-hover/info:text-ink-2 group-focus/info:text-ink-2" strokeWidth={1.75} />
        <span
          role="tooltip"
          className="pointer-events-none invisible absolute right-0 top-full z-20 mt-2 w-60 rounded-lg bg-sidebar p-3.5 text-xs leading-relaxed text-slate-200 opacity-0 shadow-xl transition-opacity group-hover/info:visible group-hover/info:opacity-100 group-focus/info:visible group-focus/info:opacity-100"
        >
          <span className="block font-semibold text-white">Папка: {folder.name}</span>
          <span className="mt-1 block">
            Создал: {folder.createdBy} · {departmentName}
          </span>
          <span className="block">Создана: {folder.createdAt}</span>
          <span className="block">{filesLabel(folder.filesCount)}</span>
        </span>
      </span>
    </div>
  );
}
