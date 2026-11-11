"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { CalendarPlus, Loader2, Search, Send } from "lucide-react";
import { button, field, primary } from "@/components/ai/form";
import { ErrorText, Label, useAction } from "@/components/recruitment/ui";
import { useT } from "@/i18n/client";
import { stageLabel, type Stage } from "@/lib/recruitment";
import { flowApi, formatLabel, interviewStatusLabel, slotText, tashkentIso, tashkentTime, type BookableRow, type Slot } from "@/lib/recruitment-flow";

/** Сегодня по Ташкенту — «2026-11-12». */
const todayTashkent = () => new Date().toLocaleDateString("sv-SE", { timeZone: "Asia/Tashkent" });

/**
 * «Новое собеседование» — из «Собеседований» и из календаря. Кандидат — поиск по открытым откликам (имя, телефон, вакансия). Дальше HR либо
 * назначает время сам: свободное время интервьюера и участников вакансии на выбранный день (или своё время — сервер проверит занятость),
 * формат и место, «кандидат уже подтвердил», — либо отправляет кандидату в Telegram три свободных варианта, он выберет номером.
 * Собеседование и событие календаря — одна запись: событие появляется, когда время подтверждено.
 */
export function NewInterviewForm({ date, onDone, onCancel }: { date?: string; onDone: (message: string) => void; onCancel: () => void }) {
  const t = useT();
  const [query, setQuery] = useState("");
  const [rows, setRows] = useState<BookableRow[] | null>(null);
  const [picked, setPicked] = useState<BookableRow | null>(null);
  const [mode, setMode] = useState<"book" | "propose">("book");
  const [day, setDay] = useState(date ?? todayTashkent());
  const [slots, setSlots] = useState<Slot[] | null>(null);
  const [time, setTime] = useState("");
  const [format, setFormat] = useState("");
  const [place, setPlace] = useState("");
  const [confirmed, setConfirmed] = useState(false);
  const { busy, error, setError, run } = useAction();

  useEffect(() => {
    if (picked) return;
    const timer = window.setTimeout(() => {
      flowApi.bookable(query.trim()).then(setRows, () => setRows([]));
    }, query ? 250 : 0);
    return () => window.clearTimeout(timer);
  }, [query, picked]);

  // Новое время, день или кандидат — прежняя ошибка («занято») больше не про то, что выбрано.
  useEffect(() => setError(null), [time, day, picked, mode, setError]);

  useEffect(() => {
    if (!picked || mode !== "book" || !day) return;
    setSlots(null);
    flowApi.daySlots(picked.vacancyId, day).then(setSlots, () => setSlots([]));
  }, [picked, mode, day]);

  const planned = picked?.interviewId && picked.interviewStatus !== "Proposed";

  const book = () =>
    run("book", async () => {
      if (!picked || !time) return;
      const online = format === "online";
      await flowApi.book(picked.applicationId, tashkentIso(day, time), format || null, online ? null : place || null, confirmed, online ? place || null : null);
      onDone(
        confirmed
          ? t("Собеседование назначено — оно уже в календаре.")
          : picked.hasTelegram
            ? t("Кандидату ушёл вопрос «вам удобно?». Время держится 24 часа; в календаре собеседование появится, когда кандидат подтвердит.")
            : t("Время держится 24 часа. Подтвердите его с кандидатом и отметьте «Кандидат подтвердил» в «Собеседованиях» — тогда оно появится в календаре."),
      );
    });

  const propose = () =>
    run("propose", async () => {
      if (!picked) return;
      const list = await flowApi.propose(picked.applicationId);
      const offer = list.find((i) => i.status === "Proposed");
      onDone(
        !offer || offer.proposedSlots.length === 0
          ? t("Свободного времени у интервьюеров вакансии нет — HR получит задачу «расписание».")
          : picked.hasTelegram
            ? t("Кандидату в Telegram ушли варианты: {list}. Он выберет номером — собеседование появится в календаре.", { list: offer.proposedSlots.map((s) => slotText(s.startsAt)).join("; ") })
            : t("Варианты: {list}. Диалога в Telegram с кандидатом нет — сообщите их сами; они видны в карточке кандидата.", { list: offer.proposedSlots.map((s) => slotText(s.startsAt)).join("; ") }),
      );
    });

  if (!picked) {
    return (
      <div className="space-y-3">
        <label className="relative block">
          <Search aria-hidden className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-ink-3" />
          <input
            autoFocus
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder={t("Кандидат, телефон или вакансия (V12)")}
            aria-label={t("Найти кандидата")}
            className={`${field} pl-9`}
          />
        </label>
        {rows === null ? (
          <Loader2 aria-label={t("Загрузка")} className="size-5 animate-spin text-ink-3" />
        ) : rows.length === 0 ? (
          <p className="text-sm text-ink-3">{query ? t("Никого не нашли среди открытых откликов.") : t("Открытых откликов нет — сначала добавьте кандидата на вакансию.")}</p>
        ) : (
          <ul className="max-h-80 divide-y divide-line overflow-y-auto rounded-lg border border-line">
            {rows.map((r) => (
              <li key={r.applicationId}>
                <button type="button" onClick={() => setPicked(r)} className="flex min-h-[48px] w-full flex-col items-start gap-0.5 px-3 py-2 text-left hover:bg-surface-2">
                  <span className="text-sm font-medium text-ink">
                    {r.candidateName}
                    {r.phone && <span className="font-normal text-ink-3"> · {r.phone}</span>}
                  </span>
                  <span className="text-xs text-ink-2">
                    {r.vacancyCode} {r.vacancyTitle} · {t(stageLabel[r.stage as Stage] ?? r.stage)}
                    {r.interviewStatus && (
                      <span className="text-warn">
                        {" "}
                        · {t(interviewStatusLabel[r.interviewStatus] ?? r.interviewStatus)}
                        {r.interviewStartsAt && ` ${slotText(r.interviewStartsAt)}`}
                      </span>
                    )}
                  </span>
                </button>
              </li>
            ))}
          </ul>
        )}
        <div className="flex justify-end">
          <button type="button" className={button} onClick={onCancel}>
            {t("Отмена")}
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-3 text-sm">
      <div className="flex flex-wrap items-start justify-between gap-2 rounded-lg border border-line bg-muted/40 px-3 py-2">
        <div className="min-w-0">
          <p className="font-medium text-ink">{picked.candidateName}</p>
          <p className="text-xs text-ink-2">
            {picked.vacancyCode} {picked.vacancyTitle}
            {!picked.hasTelegram && ` · ${t("нет диалога в Telegram")}`}
          </p>
        </div>
        <button type="button" className="text-xs font-semibold text-link" onClick={() => setPicked(null)}>
          {t("Другой кандидат")}
        </button>
      </div>

      {planned ? (
        <p className="rounded-lg bg-warn-soft px-3 py-2 text-warn">
          {t("У кандидата уже есть собеседование по этой вакансии ({status}{at}). Перенести или отменить его можно в «Собеседованиях».", {
            status: t(interviewStatusLabel[picked.interviewStatus ?? ""] ?? picked.interviewStatus ?? ""),
            at: picked.interviewStartsAt ? `, ${slotText(picked.interviewStartsAt)}` : "",
          })}{" "}
          <Link href="/hr/recruitment/interviews" className="font-semibold underline">
            {t("Открыть")}
          </Link>
        </p>
      ) : (
        <>
          <div className="flex flex-wrap gap-1.5" role="radiogroup" aria-label={t("Как назначить")}>
            <button type="button" role="radio" aria-checked={mode === "book"} className={mode === "book" ? primary : button} onClick={() => setMode("book")}>
              <CalendarPlus className="size-4" /> {t("Назначить время")}
            </button>
            <button type="button" role="radio" aria-checked={mode === "propose"} className={mode === "propose" ? primary : button} onClick={() => setMode("propose")}>
              <Send className="size-4" /> {t("Пусть выберет кандидат")}
            </button>
          </div>

          {mode === "propose" ? (
            <p className="text-xs text-ink-2">
              {t("Кандидату в Telegram уйдут три ближайших свободных времени интервьюеров вакансии (в разные дни) — он выберет номером, AI-рекрутер запишет и поставит собеседование в календарь. Попросит другое время — рекрутер подберёт по календарю.")}
            </p>
          ) : (
            <>
              <Label label="День">
                <input type="date" className={field} value={day} min={todayTashkent()} onChange={(e) => { setDay(e.target.value); setTime(""); }} />
              </Label>
              <div>
                <span className="mb-1 block text-xs font-medium text-ink-2">{t("Свободное время интервьюеров (Ташкент)")}</span>
                {slots === null ? (
                  <Loader2 aria-label={t("Загрузка")} className="size-4 animate-spin text-ink-3" />
                ) : slots.length === 0 ? (
                  <p className="text-xs text-ink-3">{t("В этот день свободного времени по настройкам вакансии нет — выберите другой день или укажите своё время.")}</p>
                ) : (
                  <div className="flex flex-wrap gap-1.5">
                    {slots.map((s) => {
                      const hhmm = tashkentTime(s.startsAt);
                      return (
                        <button key={s.startsAt} type="button" aria-pressed={time === hhmm} className={time === hhmm ? primary : button} onClick={() => setTime(hhmm)}>
                          {hhmm}
                        </button>
                      );
                    })}
                  </div>
                )}
              </div>
              <Label label="Или своё время" hint="Сервер проверит, свободны ли интервьюер и участники.">
                <input type="time" className={field} value={time} onChange={(e) => setTime(e.target.value)} />
              </Label>
              <div className="grid gap-3 sm:grid-cols-2">
                <Label label="Формат">
                  <select className={field} value={format} onChange={(e) => setFormat(e.target.value)}>
                    <option value="">{t("Как в настройках вакансии")}</option>
                    {Object.entries(formatLabel).map(([k, v]) => (
                      <option key={k} value={k}>
                        {t(v)}
                      </option>
                    ))}
                  </select>
                </Label>
                {format !== "phone" && (
                  <Label label={format === "online" ? "Ссылка на встречу" : "Адрес"} hint="Пусто — из настроек вакансии.">
                    <input className={field} value={place} maxLength={format === "online" ? 500 : 300} onChange={(e) => setPlace(e.target.value)} />
                  </Label>
                )}
              </div>
              <label className="flex min-h-[44px] items-center gap-2 text-ink-2">
                <input type="checkbox" checked={confirmed} onChange={(e) => setConfirmed(e.target.checked)} className="size-4" />
                {t("Кандидат уже согласился на это время (по телефону или лично) — сразу в календарь")}
              </label>
              {!confirmed && (
                <p className="text-xs text-ink-3">
                  {picked.hasTelegram
                    ? t("Кандидату в Telegram придёт вопрос «вам удобно?»; время держится 24 часа, в календаре появится после его «да».")
                    : t("Диалога в Telegram нет: время держится 24 часа — подтвердите его с кандидатом сами.")}
                </p>
              )}
            </>
          )}
        </>
      )}

      <ErrorText error={error} />
      <div className="flex justify-end gap-2 border-t border-line pt-3">
        <button type="button" className={button} onClick={onCancel}>
          {t("Отмена")}
        </button>
        {!planned &&
          (mode === "book" ? (
            <button type="button" className={primary} disabled={!!busy || !time || !day} onClick={book}>
              {busy === "book" ? <Loader2 className="size-4 animate-spin" /> : <CalendarPlus className="size-4" />}
              {t("Назначить")}
            </button>
          ) : (
            <button type="button" className={primary} disabled={!!busy} onClick={propose}>
              {busy === "propose" ? <Loader2 className="size-4 animate-spin" /> : <Send className="size-4" />}
              {t("Отправить варианты")}
            </button>
          ))}
      </div>
    </div>
  );
}
