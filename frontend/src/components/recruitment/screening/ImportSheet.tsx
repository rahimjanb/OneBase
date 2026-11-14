"use client";

import { useEffect, useRef, useState } from "react";
import { FileUp, Link2, Loader2, Type } from "lucide-react";
import { button, field, primary } from "@/components/ai/form";
import { Sheet } from "@/components/field/Sheet";
import { ErrorText, Label, textarea, useAction } from "@/components/recruitment/ui";
import { useT } from "@/i18n/client";
import { consentLabel } from "@/lib/recruitment";
import { screeningApi, type ImportResult, type ScreeningVacancy } from "@/lib/screening";

type Mode = "file" | "text" | "link";

const MODES: { key: Mode; label: string; icon: typeof FileUp }[] = [
  { key: "file", label: "Файл", icon: FileUp },
  { key: "text", label: "Текст", icon: Type },
  { key: "link", label: "Ссылка на профиль", icon: Link2 },
];

/**
 * Ручной импорт резюме в AI-скрининг: файл (PDF, DOCX…; можно несколько), вставленный текст или ссылка на профиль (резюме по ссылке не
 * скачивается — HR загружает файл). Тот же файл или тот же профиль HH — без второй карточки. Согласие на обработку данных — по желанию.
 */
export function ImportSheet({ open, onClose, vacancies, vacancyId, onDone }: {
  open: boolean;
  onClose: () => void;
  vacancies: ScreeningVacancy[];
  vacancyId: string;
  onDone: (results: ImportResult[]) => void;
}) {
  const t = useT();
  const [mode, setMode] = useState<Mode>("file");
  const [vacancy, setVacancy] = useState(vacancyId);
  const [files, setFiles] = useState<File[]>([]);
  const [text, setText] = useState("");
  const [url, setUrl] = useState("");
  const [name, setName] = useState("");
  const [consent, setConsent] = useState("");
  const [results, setResults] = useState<{ name: string; message: string; ok: boolean }[]>([]);
  const input = useRef<HTMLInputElement>(null);
  // Окно открыли — вакансия та, что выбрана на экране (а не при первом показе страницы); прошлые итоги — убрать.
  useEffect(() => {
    if (!open) return;
    setVacancy(vacancyId);
    setResults([]);
  }, [open, vacancyId]);
  const { busy, error, run } = useAction();
  const open_ = vacancies.filter((v) => v.status === "Open" || v.status === "Paused");
  const target = vacancy || open_[0]?.id || "";
  const ready = !!target && (mode === "file" ? files.length > 0 : mode === "text" ? text.trim().length >= 50 : url.trim().length > 0);

  const submit = () =>
    run("import", async () => {
      const done: ImportResult[] = [];
      const lines: { name: string; message: string; ok: boolean }[] = [];
      if (mode === "file") {
        for (const f of files) {
          try {
            const r = await screeningApi.importFile(target, f, consent || null);
            done.push(r);
            lines.push({ name: f.name, message: r.message, ok: true });
          } catch (e) {
            lines.push({ name: f.name, message: (e as Error).message, ok: false });
          }
        }
        setFiles([]);
      } else if (mode === "text") {
        const r = await screeningApi.importText({ vacancyId: target, text, fullName: name.trim() || null, consentChannel: consent || null });
        done.push(r);
        lines.push({ name: name.trim() || t("Текст резюме"), message: r.message, ok: true });
        setText("");
      } else {
        const r = await screeningApi.importLink({ vacancyId: target, url: url.trim(), fullName: name.trim() || null, consentChannel: consent || null });
        done.push(r);
        lines.push({ name: url.trim(), message: r.message, ok: true });
        setUrl("");
      }
      setResults(lines);
      if (done.length > 0) onDone(done);
    });

  return (
    <Sheet open={open} onClose={onClose} title={t("Загрузить резюме")} subtitle={t("Кандидат появится в списке; резюме разберёт и проанализирует AI")}>
      <div className="space-y-4 pb-2">
        <div role="tablist" className="flex gap-1 rounded-xl border border-line bg-surface p-1">
          {MODES.map((m) => {
            const Icon = m.icon;
            const on = m.key === mode;
            return (
              <button
                key={m.key}
                type="button"
                role="tab"
                aria-selected={on}
                onClick={() => setMode(m.key)}
                className={`flex flex-1 items-center justify-center gap-1.5 rounded-[9px] px-2 py-2 text-[13px] font-medium outline-none focus-visible:ring-2 focus-visible:ring-accent ${on ? "bg-accent text-white" : "text-ink-2 hover:bg-muted"}`}
              >
                <Icon aria-hidden className="size-4" />
                {t(m.label)}
              </button>
            );
          })}
        </div>

        <Label label="Вакансия">
          <select className={field} value={target} onChange={(e) => setVacancy(e.target.value)}>
            {open_.length === 0 && <option value="">{t("Нет открытых вакансий")}</option>}
            {open_.map((v) => (
              <option key={v.id} value={v.id}>
                {v.code} · {v.title}
              </option>
            ))}
          </select>
        </Label>

        {mode === "file" && (
          <div>
            <input
              ref={input}
              type="file"
              multiple
              className="sr-only"
              accept=".pdf,.docx,.doc,.rtf,.odt,.txt,.jpg,.jpeg,.png,.webp"
              onChange={(e) => setFiles(Array.from(e.target.files ?? []).slice(0, 20))}
            />
            <button
              type="button"
              onClick={() => input.current?.click()}
              className="flex w-full flex-col items-center gap-1.5 rounded-xl border-2 border-dashed border-line bg-surface px-4 py-6 text-center outline-none hover:border-accent/60 focus-visible:ring-2 focus-visible:ring-accent"
            >
              <FileUp aria-hidden className="size-6 text-accent-strong" />
              <span className="text-[13px] font-semibold text-ink">{files.length > 0 ? t("Выбрано файлов: {n}", { n: files.length }) : t("Выбрать файлы")}</span>
              <span className="text-[11px] text-ink-3">{t("PDF, DOCX, DOC, RTF, ODT, TXT или фото — до 20 МБ; можно несколько")}</span>
            </button>
            {files.length > 0 && <p className="mt-1.5 truncate text-[12px] text-ink-3">{files.map((f) => f.name).join(", ")}</p>}
          </div>
        )}
        {mode === "text" && (
          <>
            <Label label="Имя кандидата" hint="Можно не указывать — возьмём из резюме.">
              <input className={field} value={name} onChange={(e) => setName(e.target.value)} />
            </Label>
            <Label label="Текст резюме">
              <textarea className={`${textarea} min-h-48`} value={text} onChange={(e) => setText(e.target.value)} />
            </Label>
          </>
        )}
        {mode === "link" && (
          <>
            <Label label="Ссылка на профиль" hint="Например, https://hh.uz/resume/… Резюме по ссылке не загружается автоматически: просмотр через API HH расходует лимит — откройте профиль, скачайте резюме и загрузите файлом.">
              <input className={field} inputMode="url" placeholder="https://hh.uz/resume/…" value={url} onChange={(e) => setUrl(e.target.value)} />
            </Label>
            <Label label="Имя кандидата" hint="Для ссылок не с HH — обязательно.">
              <input className={field} value={name} onChange={(e) => setName(e.target.value)} />
            </Label>
          </>
        )}

        <Label label="Согласие на обработку данных" hint="Нужно, чтобы держать кандидата в резерве.">
          <select className={field} value={consent} onChange={(e) => setConsent(e.target.value)}>
            <option value="">{t("Не отмечать")}</option>
            {Object.entries(consentLabel).map(([k, v]) => (
              <option key={k} value={k}>
                {t(v)}
              </option>
            ))}
          </select>
        </Label>

        <p className="rounded-lg bg-muted/60 px-3 py-2 text-[11px] leading-relaxed text-ink-3">
          {t("Загружайте только резюме, полученные законно (кандидат сам прислал или откликнулся). Импорт не для обхода ограничений HH. Разбор и анализ — платные вызовы AI.")}
        </p>

        {results.length > 0 && (
          <ul className="space-y-1">
            {results.map((r, i) => (
              <li key={i} className={`rounded-lg px-3 py-2 text-[12px] ${r.ok ? "bg-ok-soft text-ink" : "bg-bad-soft text-bad"}`}>
                <b className="font-semibold">{r.name}</b>: {t(r.message)}
              </li>
            ))}
          </ul>
        )}
        <ErrorText error={error} />

        <div className="flex gap-2">
          <button type="button" className={primary} disabled={!ready || !!busy} onClick={submit}>
            {busy ? <Loader2 className="size-4 animate-spin" /> : <FileUp className="size-4" />}
            {t("Загрузить")}
          </button>
          <button type="button" className={button} onClick={onClose}>
            {t("Закрыть")}
          </button>
        </div>
      </div>
    </Sheet>
  );
}
