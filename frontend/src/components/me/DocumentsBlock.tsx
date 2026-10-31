"use client";

import { useCallback, useEffect, useState } from "react";
import { Download, File, FileImage, FileSpreadsheet, FileText, FolderArchive, FolderOpen, Presentation, type LucideIcon } from "lucide-react";
import { PreviewDialog } from "@/components/files/FileDialogs";
import { useLocale, useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import { downloadUrl, type FileRow } from "@/lib/files";
import { documentNote, fileKind, type DocumentsTab, type FileKind, type MyDocument } from "@/lib/my-day";
import { BlockHeader, Empty, Sk, card, focusRing } from "./parts";

const TABS: { key: DocumentsTab; label: string }[] = [
  { key: "recent", label: "Последние" },
  { key: "reports", label: "Отчёты" },
  { key: "mine", label: "Мои" },
  { key: "assigned", label: "Назначенные" },
];

const kinds: Record<FileKind, { icon: LucideIcon; tile: string }> = {
  sheet: { icon: FileSpreadsheet, tile: "bg-ok-soft text-ok" },
  pdf: { icon: FileText, tile: "bg-bad-soft text-bad" },
  doc: { icon: FileText, tile: "bg-accent-soft text-link" },
  slides: { icon: Presentation, tile: "bg-warn-soft text-warn" },
  image: { icon: FileImage, tile: "bg-violet-soft text-violet" },
  archive: { icon: FolderArchive, tile: "bg-surface-2 text-ink-2" },
  other: { icon: File, tile: "bg-surface-2 text-ink-2" },
};

type Loaded = { available: boolean; items: MyDocument[] };

/** Файл «Файлов» для окна просмотра (то же окно, что в разделе «Файлы»). */
const asFileRow = (doc: MyDocument): FileRow => ({
  id: doc.id,
  folderId: "",
  name: doc.name,
  extension: doc.name.includes(".") ? doc.name.slice(doc.name.lastIndexOf(".") + 1).toLowerCase() : "",
  contentType: doc.contentType,
  sizeBytes: doc.sizeBytes,
  version: 0,
  updatedAt: doc.updatedAt,
  updatedBy: doc.by ? { name: doc.by, viaWindows: false } : null,
  hidden: false,
});

const urlOf = (doc: MyDocument, inline = false) =>
  doc.source === "attachment" ? `/bff/api/work/attachments/${doc.id}` : downloadUrl({ id: doc.id, name: doc.name }, inline);

/**
 * «Мои документы»: вкладки «Последние · Отчёты · Мои · Назначенные», до 5 файлов (на телефоне — 3) с учётом прав «Файлов». Назначенные —
 * вложения к моим задачам от других. Нажатие — просмотр файла (окно «Файлов»; вложение задачи — в новой вкладке), справа — скачать.
 */
export function DocumentsBlock() {
  const t = useT();
  const locale = useLocale();
  const [tab, setTab] = useState<DocumentsTab>("recent");
  const [cache, setCache] = useState<Partial<Record<DocumentsTab, Loaded>>>({});
  const [error, setError] = useState<DocumentsTab | null>(null);
  const [preview, setPreview] = useState<FileRow | null>(null);

  const load = useCallback(async (key: DocumentsTab) => {
    try {
      const data = await bff<Loaded>(`me/documents?tab=${key}`);
      setCache((c) => ({ ...c, [key]: data }));
      setError((e) => (e === key ? null : e));
    } catch {
      setError(key);
    }
  }, []);

  useEffect(() => {
    if (!cache[tab]) void load(tab);
  }, [tab, cache, load]);

  const current = cache[tab];

  return (
    <section aria-label={t("Мои документы")} className={card}>
      <BlockHeader icon={FolderOpen} title={t("Мои документы")} href="/base" action={t("Все файлы")} />
      <div role="tablist" aria-label={t("Мои документы")} className="mt-3 grid grid-cols-4 gap-1 rounded-xl border border-line bg-surface-2 p-1">
        {TABS.map((x) => (
          <button
            key={x.key}
            type="button"
            role="tab"
            aria-selected={tab === x.key}
            onClick={() => setTab(x.key)}
            className={`min-h-[36px] truncate rounded-lg px-1.5 text-[12.5px] font-semibold transition-colors lg:text-[13px] ${
              tab === x.key ? "bg-surface text-ink shadow-sm" : "text-ink-3 hover:text-ink"
            } ${focusRing}`}
          >
            {t(x.label)}
          </button>
        ))}
      </div>
      <div role="tabpanel" className="mt-3">
        {error === tab ? (
          <p className="text-[13.5px] text-ink-3">
            {t("Не удалось загрузить данные.")}{" "}
            <button type="button" onClick={() => void load(tab)} className={`font-semibold text-link ${focusRing}`}>
              {t("Повторить")}
            </button>
          </p>
        ) : !current ? (
          <div className="space-y-3" aria-busy="true">
            {[0, 1, 2].map((i) => (
              <div key={i} className="flex gap-3">
                <Sk className="size-[38px] shrink-0 rounded-xl" />
                <Sk className="h-10 flex-1" />
              </div>
            ))}
          </div>
        ) : current.items.length === 0 ? (
          <Empty>{t(current.available ? "Документов пока нет." : "Нет доступа к файлам.")}</Empty>
        ) : (
          <ul className="space-y-1">
            {current.items.map((doc, i) => {
              const kind = kinds[fileKind(doc.name, doc.contentType)];
              return (
                <li key={doc.id} className={`flex items-center gap-2 ${i >= 3 ? "max-lg:hidden" : ""}`}>
                  {doc.source === "file" ? (
                    <button
                      type="button"
                      onClick={() => setPreview(asFileRow(doc))}
                      className={`-ml-2 flex min-h-[52px] min-w-0 flex-1 items-center gap-3 rounded-lg px-2 py-1.5 text-left transition-colors hover:bg-surface-2 ${focusRing}`}
                    >
                      <DocBody doc={doc} kind={kind} note={documentNote(doc, locale, t)} />
                    </button>
                  ) : (
                    <a
                      href={urlOf(doc, true)}
                      target="_blank"
                      rel="noreferrer"
                      className={`-ml-2 flex min-h-[52px] min-w-0 flex-1 items-center gap-3 rounded-lg px-2 py-1.5 transition-colors hover:bg-surface-2 ${focusRing}`}
                    >
                      <DocBody doc={doc} kind={kind} note={documentNote(doc, locale, t)} />
                    </a>
                  )}
                  <a
                    href={urlOf(doc)}
                    download={doc.name}
                    aria-label={t("Скачать {name}", { name: doc.name })}
                    className={`grid size-11 shrink-0 place-items-center rounded-lg text-ink-3 transition-colors hover:bg-surface-2 hover:text-ink lg:size-9 ${focusRing}`}
                  >
                    <Download aria-hidden className="size-[18px]" />
                  </a>
                </li>
              );
            })}
          </ul>
        )}
      </div>
      {preview && <PreviewDialog file={preview} onClose={() => setPreview(null)} />}
    </section>
  );
}

function DocBody({ doc, kind, note }: { doc: MyDocument; kind: { icon: LucideIcon; tile: string }; note: string }) {
  return (
    <>
      <span aria-hidden className={`grid size-[38px] shrink-0 place-items-center rounded-xl ${kind.tile}`}>
        <kind.icon className="size-[18px]" />
      </span>
      <span className="min-w-0">
        <span className="block truncate text-[14px] font-medium text-ink">{doc.name}</span>
        <span className="block truncate text-[12px] text-ink-3">{note}</span>
      </span>
    </>
  );
}
