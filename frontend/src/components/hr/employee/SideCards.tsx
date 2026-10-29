"use client";

import { useRouter } from "next/navigation";
import { useEffect, useLayoutEffect, useRef, useState } from "react";
import { CircleCheck, Loader2, Lock } from "lucide-react";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import { dayLabel, type HrEmployeeCard } from "@/lib/hr";
import { tenure } from "@/lib/hr-employee";
import { card, focus } from "../overview/bits";

/** «30.10 в 03:55» по Ташкенту. */
const at = (iso: string) => {
  const p = Object.fromEntries(
    new Intl.DateTimeFormat("ru-RU", { timeZone: "Asia/Tashkent", day: "2-digit", month: "2-digit", hour: "2-digit", minute: "2-digit" })
      .formatToParts(new Date(iso))
      .map((x) => [x.type, x.value]),
  );
  return { date: `${p.day}.${p.month}`, time: `${p.hour}:${p.minute}` };
};

/**
 * «Данные Verifix» — только чтение: поля меняются в Verifix. Пустое — «—»; телефон и почта — ссылки. Стаж — до сегодня или до увольнения.
 */
export function VerifixCard({ data, today }: { data: HrEmployeeCard; today: string }) {
  const t = useT();
  const e = data.employee;
  const empty = <span className="text-(--ts-subtle)">—</span>;
  const hired = e.hiringDate ? (
    <>
      {dayLabel(e.hiringDate, true)} <span className="text-(--ts-muted)">· {tenure(e.hiringDate, e.dismissalDate ?? today, t)}</span>
    </>
  ) : (
    empty
  );
  const rows: [string, React.ReactNode][] = [
    ["ID Verifix", <span key="id" className="tabular-nums">{e.verifixId}</span>],
    ["Подразделение", e.division ?? empty],
    ["Должность", e.job ?? empty],
    ["График", e.schedule ?? empty],
    ["Принят", hired],
    ["Уволен", e.dismissalDate ? dayLabel(e.dismissalDate, true) : empty],
    [
      "Телефон",
      e.phone ? (
        <a href={`tel:+${e.phone.replace(/\D/g, "")}`} className={`rounded text-(--ts-link) hover:underline ${focus}`}>
          +{e.phone.replace(/\D/g, "")}
        </a>
      ) : (
        empty
      ),
    ],
    [
      "Эл. почта",
      data.email ? (
        <a href={`mailto:${data.email}`} className={`break-all rounded text-(--ts-link) hover:underline ${focus}`}>
          {data.email}
        </a>
      ) : (
        empty
      ),
    ],
  ];
  const updated = at(data.lastSyncedAt);

  return (
    <section aria-labelledby="hr-verifix" className={`${card} px-5 py-[18px]`}>
      <div className="flex items-center justify-between gap-3">
        <h2 id="hr-verifix" className="text-[15px] font-semibold text-(--ts-text)">
          {t("Данные Verifix")}
        </h2>
        <Lock aria-label={t("Только чтение")} className="size-4 text-(--ts-subtle)" />
      </div>
      <dl className="mt-2.5">
        {rows.map(([label, value]) => (
          <div key={label} className="grid grid-cols-[112px_minmax(0,1fr)] gap-3 border-b border-(--ts-border) py-[9px] text-[13px] md:grid-cols-[110px_minmax(0,1fr)]">
            <dt className="text-(--ts-muted)">{t(label)}</dt>
            <dd className="min-w-0 break-words font-medium text-(--ts-text)">{value}</dd>
          </div>
        ))}
      </dl>
      <p className="mt-3 text-xs leading-relaxed text-(--ts-subtle)">
        {t("Поля приходят из Verifix и меняются только там. Обновлено {date} в {time}.", updated)}
        {e.missingSince && <> {t("В последней выгрузке Verifix сотрудника не было ({date}) — запись сохранена.", { date: at(e.missingSince).date })}</>}
      </p>
    </section>
  );
}

/**
 * «Заметка HR» — поле OneBase, синхронизация с Verifix его не меняет. «Сохранить» активна, только когда текст изменён; Ctrl/Cmd+Enter — сохранить.
 * После сохранения — уведомление и «Изменено: кто, когда». Ошибка — текст остаётся в поле, под ним причина и «Повторить».
 */
export function NoteCard({ data, demo }: { data: HrEmployeeCard; demo: boolean }) {
  const t = useT();
  const router = useRouter();
  const [saved, setSaved] = useState(data.note ?? "");
  const [text, setText] = useState(data.note ?? "");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [toast, setToast] = useState(false);
  const area = useRef<HTMLTextAreaElement>(null);

  // Высота поля — по тексту (не меньше 96).
  useLayoutEffect(() => {
    const el = area.current;
    if (!el) return;
    el.style.height = "auto";
    el.style.height = `${Math.max(96, el.scrollHeight + 2)}px`;
  }, [text]);

  useEffect(() => {
    if (!toast) return;
    const timer = setTimeout(() => setToast(false), 3000);
    return () => clearTimeout(timer);
  }, [toast]);

  const changed = text !== saved;
  const save = async () => {
    if (!changed || busy) return;
    setBusy(true);
    setError(null);
    try {
      if (!demo) await bff(`hr/employees/${data.employee.id}/note`, { method: "PUT", body: JSON.stringify({ note: text }) });
      setSaved(text);
      setToast(true);
      router.refresh();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  };
  const author = data.noteUpdatedAt ? at(data.noteUpdatedAt) : null;

  return (
    <section aria-labelledby="hr-note" className={`${card} px-5 py-[18px]`}>
      <div className="flex items-baseline justify-between gap-3">
        <h2 id="hr-note" className="text-[15px] font-semibold text-(--ts-text)">
          {t("Заметка HR")}
        </h2>
        <span className="text-[11px] text-(--ts-subtle)">
          <span className="md:hidden">{t("только в OneBase")}</span>
          <span className="max-md:hidden">{t("видна только в OneBase")}</span>
        </span>
      </div>

      {!data.canEditNote ? (
        <p className="mt-3 whitespace-pre-wrap text-sm text-(--ts-text-2)">{data.note || t("Заметок нет.")}</p>
      ) : (
        <>
          <textarea
            ref={area}
            value={text}
            onChange={(e) => setText(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === "Enter" && (e.metaKey || e.ctrlKey)) {
                e.preventDefault();
                void save();
              }
            }}
            maxLength={2000}
            aria-label={t("Заметка HR")}
            aria-invalid={!!error}
            aria-describedby={error ? "hr-note-error" : undefined}
            placeholder={t("Например: выдан пропуск, работает по индивидуальному графику")}
            className="mt-3 block min-h-24 w-full resize-none rounded-[10px] border border-(--ts-border-strong) bg-(--ts-bg) px-3.5 py-3 text-sm leading-relaxed text-(--ts-text) placeholder:text-(--ts-subtle) focus:border-(--ts-accent) focus:outline-none focus:ring-2 focus:ring-(--ts-accent)/25"
          />
          {error && (
            <p id="hr-note-error" role="alert" className="mt-2 text-xs text-(--ts-danger)">
              {t("Не удалось сохранить: {message}", { message: error })}
            </p>
          )}
          <div className="mt-3 flex flex-wrap items-center gap-x-3 gap-y-2">
            <button
              type="button"
              onClick={save}
              disabled={!changed || busy}
              className={`inline-flex h-10 cursor-pointer items-center justify-center gap-2 rounded-[10px] border border-(--ts-border-strong) bg-(--ts-surface) px-4 text-sm font-medium text-(--ts-text) transition-colors duration-150 hover:bg-(--ts-surface-2) disabled:cursor-default disabled:opacity-50 max-md:w-full ${focus}`}
            >
              {busy && <Loader2 aria-hidden className="size-4 animate-spin" />}
              {error ? t("Повторить") : <><span className="md:hidden">{t("Сохранить заметку")}</span><span className="max-md:hidden">{t("Сохранить")}</span></>}
            </button>
            {author && data.noteUpdatedBy && !changed && (
              <span className="text-xs text-(--ts-subtle)">{t("Изменено: {who}, {date} {time}", { who: data.noteUpdatedBy, ...author })}</span>
            )}
            {author && !data.noteUpdatedBy && !changed && <span className="text-xs text-(--ts-subtle)">{t("Изменено {date} {time}", author)}</span>}
            {changed && !busy && <span className="text-xs text-(--ts-subtle) max-md:hidden">{t("Ctrl+Enter — сохранить")}</span>}
          </div>
        </>
      )}

      {toast && (
        <div role="status" className="fixed bottom-24 left-1/2 z-50 flex -translate-x-1/2 items-center gap-2 rounded-[12px] bg-(--ts-pop-bg) px-4 py-2.5 text-sm font-medium text-(--ts-pop-text) shadow-(--ts-pop-shadow) md:bottom-8">
          <CircleCheck aria-hidden className="size-4 text-(--ts-pop-success)" />
          {t("Заметка сохранена")}
        </div>
      )}
    </section>
  );
}
