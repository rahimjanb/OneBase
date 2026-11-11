"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import { CalendarClock, CalendarPlus, CheckCircle2, ClipboardCheck, FileText, Loader2, XCircle } from "lucide-react";
import { button, field, primary } from "@/components/ai/form";
import { Modal } from "@/components/files/Modal";
import { NewInterviewForm } from "@/components/recruitment/NewInterview";
import { Badge, ErrorText, Label, textarea, useAction } from "@/components/recruitment/ui";
import { useT } from "@/i18n/client";
import { recruitmentApi, stageLabel, STAGES, when, type RecruitmentContext, type Stage } from "@/lib/recruitment";
import {
  cancelledByLabel,
  confirmationLabel,
  flowApi,
  formatLabel,
  interviewStatusLabel,
  interviewViews,
  resultLabel,
  slotText,
  type FeedbackInput,
  type InterviewResult,
  type InterviewRow,
  type Slot,
} from "@/lib/recruitment-flow";

const statusTone: Record<string, string> = {
  Proposed: "bg-muted text-ink-2",
  Awaiting: "bg-warn-soft text-warn",
  Confirmed: "bg-ok-soft text-ok",
  Rescheduled: "bg-accent-soft text-accent-strong",
  Completed: "bg-muted text-ink-2",
  NoShow: "bg-bad-soft text-bad",
  Cancelled: "bg-muted text-ink-3",
};

const active = (i: InterviewRow) => ["Awaiting", "Confirmed", "Rescheduled"].includes(i.status);

/**
 * HR → «Подбор» → «Собеседования»: сегодня, завтра, неделя, предстоящие, прошедшие, отменённые, перенесённые. У каждого — кандидат и резюме,
 * вакансия и отдел, интервьюер, время, формат, статус, подтверждение кандидатом, оценка AI; действия HR — перенести, отменить, отметить
 * «кандидат подтвердил», «прошло» / «не пришёл», итоги. Собеседование и событие календаря меняются вместе. «Новое собеседование» — HR
 * назначает время сам или отправляет кандидату варианты.
 */
export function InterviewsBoard({ ctx }: { ctx: RecruitmentContext }) {
  const t = useT();
  const [view, setView] = useState("upcoming");
  const [rows, setRows] = useState<InterviewRow[] | null>(null);
  const [creating, setCreating] = useState(false);
  const [notice, setNotice] = useState<string | null>(null);
  const close = useCallback(() => setCreating(false), []);
  const { busy, error, run } = useAction();
  const load = () => run("load", async () => setRows(await flowApi.interviewsView(view)));
  useEffect(() => {
    setRows(null);
    load();
  }, [view]); // eslint-disable-line react-hooks/exhaustive-deps

  const replace = (row: InterviewRow) => setRows((list) => list?.map((r) => (r.id === row.id ? row : r)) ?? null);

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div className="flex flex-wrap gap-1.5" role="tablist">
          {interviewViews.map(([key, label]) => (
            <button key={key} role="tab" aria-selected={view === key} className={view === key ? primary : button} onClick={() => setView(key)}>
              {t(label)}
            </button>
          ))}
        </div>
        {ctx.canManage && (
          <button className={primary} onClick={() => { setNotice(null); setCreating(true); }}>
            <CalendarPlus className="size-4" /> {t("Новое собеседование")}
          </button>
        )}
      </div>
      {notice && (
        <p role="status" className="rounded-lg bg-ok-soft px-3 py-2 text-sm text-ok">
          {notice}
        </p>
      )}
      {creating && (
        <Modal title={t("Новое собеседование")} onClose={close}>
          <div className="px-5 py-4">
            <NewInterviewForm
              onCancel={close}
              onDone={(message) => {
                setCreating(false);
                setNotice(message);
                if (view === "upcoming") load();
                else setView("upcoming");
              }}
            />
          </div>
        </Modal>
      )}
      <ErrorText error={error} />
      {!rows ? (
        <p className="text-sm text-ink-3">{busy === "load" ? <Loader2 className="inline size-4 animate-spin" /> : null} {t("Загрузка…")}</p>
      ) : rows.length === 0 ? (
        <p className="rounded-xl border border-dashed border-line p-6 text-center text-sm text-ink-3">{t("Собеседований нет.")}</p>
      ) : (
        <ul className="space-y-2">
          {rows.map((i) => (
            <InterviewCard key={i.id} i={i} ctx={ctx} onRow={replace} onReload={load} />
          ))}
        </ul>
      )}
    </div>
  );
}

function InterviewCard({ i, ctx, onRow, onReload }: { i: InterviewRow; ctx: RecruitmentContext; onRow: (r: InterviewRow) => void; onReload: () => void }) {
  const t = useT();
  const { busy, error, run } = useAction();
  const [panel, setPanel] = useState<"move" | "feedback" | null>(null);
  const manage = ctx.canManage;
  const act = (what: string, action: () => Promise<InterviewRow>) => run(what, async () => onRow(await action()));
  const where = [t(formatLabel[i.format] ?? i.format), i.format === "online" ? i.meetingUrl : i.location].filter(Boolean).join(", ");

  return (
    <li className="rounded-xl border border-line bg-surface p-3 text-sm">
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div className="min-w-0">
          <div className="flex flex-wrap items-center gap-2">
            <Link href={`/hr/recruitment/candidates/${i.candidateId}`} className="font-semibold text-ink hover:text-accent-strong">
              {i.candidateName}
            </Link>
            <Badge className={statusTone[i.status] ?? "bg-muted text-ink-2"}>{t(interviewStatusLabel[i.status] ?? i.status)}</Badge>
            {active(i) && <span className="text-xs text-ink-3">{t(confirmationLabel[i.candidateConfirmation] ?? i.candidateConfirmation)}</span>}
          </div>
          <p className="mt-0.5 text-xs text-ink-2">
            {i.vacancyCode} {i.vacancyTitle}
            {i.department && ` · ${i.department}`}
          </p>
        </div>
        <div className="text-right text-sm tabular-nums">
          {i.startsAt ? (
            <>
              <span className="font-medium">{slotText(i.startsAt)}</span>
              {i.previousStartsAt && <span className="block text-xs text-ink-3">{t("было: {at}", { at: slotText(i.previousStartsAt) })}</span>}
            </>
          ) : (
            <span className="text-xs text-ink-3">{t("варианты: {list}", { list: i.proposedSlots.map((s) => slotText(s.startsAt)).join("; ") || "—" })}</span>
          )}
        </div>
      </div>

      <dl className="mt-2 grid gap-x-6 gap-y-0.5 text-xs sm:grid-cols-[auto_1fr]">
        <dt className="text-ink-3">{t("Интервьюер")}</dt>
        <dd className="text-ink-2">
          {i.interviewer ?? "—"}
          {i.interviewers.filter((n) => n !== i.interviewer).length > 0 && ` · ${t("участники")}: ${i.interviewers.filter((n) => n !== i.interviewer).join(", ")}`}
        </dd>
        <dt className="text-ink-3">{t("Формат")}</dt>
        <dd className="text-ink-2">{where || "—"}</dd>
        {i.status === "Awaiting" && i.holdUntil && (
          <>
            <dt className="text-ink-3">{t("Время держится")}</dt>
            <dd className="text-ink-2">{t("до {at} — дальше освободится", { at: slotText(i.holdUntil) })}</dd>
          </>
        )}
        {(i.aiScore != null || i.aiSummary) && (
          <>
            <dt className="text-ink-3">{t("Оценка AI")}</dt>
            <dd className="text-ink-2">
              {i.aiScore != null && <b className="tabular-nums">{t("{n} из 100", { n: i.aiScore })}</b>}
              {i.aiScore != null && i.aiSummary && " · "}
              {i.aiSummary}
            </dd>
          </>
        )}
        {i.status === "Cancelled" && (
          <>
            <dt className="text-ink-3">{t("Отменил")}</dt>
            <dd className="text-ink-2">
              {t(cancelledByLabel[i.cancelledBy ?? ""] ?? i.cancelledBy ?? "—")}
              {i.cancelReason && ` · ${i.cancelReason}`}
            </dd>
          </>
        )}
      </dl>

      <div className="mt-2 flex flex-wrap items-center gap-3 text-xs">
        {i.resumeId && (
          <a className="inline-flex items-center gap-1 text-accent-strong hover:underline" href={recruitmentApi.resumeUrl(i.resumeId)}>
            <FileText className="size-4" /> {t("Резюме")}
          </a>
        )}
        {i.calendarEventId && (
          <Link className="inline-flex items-center gap-1 text-accent-strong hover:underline" href="/calendar">
            <CalendarClock className="size-4" /> {t("В календаре")}
          </Link>
        )}
      </div>

      {(i.feedback ?? []).map((f) => (
        <div key={f.id} className="mt-2 rounded-lg bg-muted/50 p-2 text-xs">
          <b>{t(resultLabel[f.result])}</b> · {f.author} · {when(f.at)}
          {(f.technical || f.communication || f.professional) && (
            <span className="block text-ink-2 tabular-nums">
              {t("техника {a}, общение {b}, профессиональные качества {c} (из 5)", { a: f.technical ?? "—", b: f.communication ?? "—", c: f.professional ?? "—" })}
            </span>
          )}
          {f.strengths && <span className="block">{t("Сильные стороны")}: {f.strengths}</span>}
          {f.weaknesses && <span className="block">{t("Слабые стороны")}: {f.weaknesses}</span>}
          {f.comments && <span className="block">{f.comments}</span>}
          {f.recommendation && <span className="block">{t("Рекомендация")}: {f.recommendation}</span>}
          {f.nextStage && <span className="block">{t("Следующий этап")}: {t(stageLabel[f.nextStage])}</span>}
        </div>
      ))}

      {manage && (
        <div className="mt-2 flex flex-wrap gap-2">
          {i.status === "Awaiting" && (
            <button className={button} disabled={!!busy} onClick={() => act("confirm", () => flowApi.confirmInterview(i.id))} title={t("Кандидат подтвердил по телефону или лично")}>
              <CheckCircle2 className="size-4" /> {t("Кандидат подтвердил")}
            </button>
          )}
          {active(i) && (
            <button className={button} onClick={() => setPanel(panel === "move" ? null : "move")}>
              <CalendarClock className="size-4" /> {t("Перенести")}
            </button>
          )}
          {(active(i) || i.status === "Completed" || i.status === "NoShow") && i.status !== "Awaiting" && (
            <button className={button} onClick={() => setPanel(panel === "feedback" ? null : "feedback")}>
              <ClipboardCheck className="size-4" /> {t("Итоги")}
            </button>
          )}
          {(i.status === "Confirmed" || i.status === "Rescheduled") && (
            <button className={button} disabled={!!busy} onClick={() => run("noshow", async () => {
              await flowApi.interviewStatus(i.id, "NoShow");
              onReload();
            })}>
              {t("Не пришёл")}
            </button>
          )}
          {(active(i) || i.status === "Proposed") && (
            <button
              className={button}
              disabled={!!busy}
              onClick={() => {
                const reason = prompt(t("Причина отмены — для HR и участников; кандидату придёт только сообщение об отмене"), "");
                if (reason !== null) act("cancel", () => flowApi.cancelInterview(i.id, reason || null));
              }}
            >
              <XCircle className="size-4" /> {t("Отменить")}
            </button>
          )}
        </div>
      )}
      {panel === "move" && <MovePanel i={i} onDone={(row) => { onRow(row); setPanel(null); }} />}
      {panel === "feedback" && <FeedbackPanel i={i} onDone={(row) => { onRow(row); setPanel(null); }} />}
      <ErrorText error={error} />
    </li>
  );
}

/** Перенос: свободное время интервьюеров или своё время (проверяется на занятость). Кандидату придёт сообщение, он подтвердит новое время. */
function MovePanel({ i, onDone }: { i: InterviewRow; onDone: (row: InterviewRow) => void }) {
  const t = useT();
  const [slots, setSlots] = useState<Slot[] | null>(null);
  const [custom, setCustom] = useState("");
  const { busy, error, run } = useAction();
  useEffect(() => {
    flowApi.slots(i.vacancyId).then(setSlots, () => setSlots([]));
  }, [i.vacancyId]);
  const move = (startsAt: string) => run("move", async () => onDone(await flowApi.reschedule(i.id, startsAt)));
  return (
    <div className="mt-2 rounded-lg border border-line p-3">
      <p className="mb-2 text-xs text-ink-3">{t("Событие календаря перенесётся вместе с собеседованием; кандидату придёт сообщение с просьбой подтвердить.")}</p>
      <div className="flex flex-wrap gap-2">
        {slots === null ? <Loader2 className="size-4 animate-spin" /> : slots.length === 0 ? <span className="text-xs text-ink-3">{t("Свободного времени на 2 недели нет.")}</span> : null}
        {slots?.map((s) => (
          <button key={s.startsAt} className={button} disabled={!!busy} onClick={() => move(s.startsAt)}>
            {slotText(s.startsAt)}
          </button>
        ))}
      </div>
      <div className="mt-2 flex flex-wrap items-end gap-2">
        <Label label="Другое время (Ташкент)">
          <input type="datetime-local" className={field} value={custom} onChange={(e) => setCustom(e.target.value)} />
        </Label>
        <button className={primary} disabled={!!busy || !custom} onClick={() => move(`${custom}:00+05:00`)}>
          {t("Перенести")}
        </button>
      </div>
      <ErrorText error={error} />
    </div>
  );
}

/** Итоги собеседования: результат, оценки 1–5, сильные и слабые стороны, комментарий, рекомендация, следующий этап (решение HR). */
function FeedbackPanel({ i, onDone }: { i: InterviewRow; onDone: (row: InterviewRow) => void }) {
  const t = useT();
  const mine = i.feedback?.[0];
  const [f, setF] = useState<FeedbackInput>({
    result: mine?.result ?? "WaitingDecision",
    technical: mine?.technical ?? null,
    communication: mine?.communication ?? null,
    professional: mine?.professional ?? null,
    strengths: mine?.strengths ?? null,
    weaknesses: mine?.weaknesses ?? null,
    comments: mine?.comments ?? null,
    recommendation: mine?.recommendation ?? null,
    nextStage: null,
  });
  const { busy, error, run } = useAction();
  const score = (key: "technical" | "communication" | "professional", label: string) => (
    <Label label={label}>
      <select className={field} value={f[key] ?? ""} onChange={(e) => setF({ ...f, [key]: e.target.value ? Number(e.target.value) : null })}>
        <option value="">—</option>
        {[1, 2, 3, 4, 5].map((n) => (
          <option key={n} value={n}>
            {n}
          </option>
        ))}
      </select>
    </Label>
  );
  const text = (key: "strengths" | "weaknesses" | "comments" | "recommendation", label: string) => (
    <Label label={label} className="sm:col-span-3">
      <textarea className={textarea} value={f[key] ?? ""} onChange={(e) => setF({ ...f, [key]: e.target.value || null })} />
    </Label>
  );
  return (
    <div className="mt-2 grid gap-3 rounded-lg border border-line p-3 sm:grid-cols-3">
      <Label label="Результат" className="sm:col-span-3">
        <select className={field} value={f.result} onChange={(e) => setF({ ...f, result: e.target.value as InterviewResult })}>
          {(Object.keys(resultLabel) as InterviewResult[]).map((r) => (
            <option key={r} value={r}>
              {t(resultLabel[r])}
            </option>
          ))}
        </select>
      </Label>
      {score("technical", "Техническая оценка")}
      {score("communication", "Общение")}
      {score("professional", "Профессиональные качества")}
      {text("strengths", "Сильные стороны")}
      {text("weaknesses", "Слабые стороны")}
      {text("comments", "Комментарий")}
      {text("recommendation", "Рекомендация")}
      <Label label="Следующий этап отклика" hint="Решение HR: пусто — этап не меняется." className="sm:col-span-3">
        <select className={field} value={f.nextStage ?? ""} onChange={(e) => setF({ ...f, nextStage: (e.target.value || null) as Stage | null })}>
          <option value="">{t("Не менять")}</option>
          {STAGES.filter((s) => s !== "New").map((s) => (
            <option key={s} value={s}>
              {t(stageLabel[s])}
            </option>
          ))}
        </select>
      </Label>
      <div className="sm:col-span-3">
        <button className={primary} disabled={!!busy} onClick={() => run("save", async () => onDone(await flowApi.feedback(i.id, f)))}>
          {t("Сохранить итоги")}
        </button>
      </div>
      <ErrorText error={error} />
    </div>
  );
}

const dayNames: [string, string][] = [
  ["1", "Пн"],
  ["2", "Вт"],
  ["3", "Ср"],
  ["4", "Чт"],
  ["5", "Пт"],
  ["6", "Сб"],
  ["7", "Вс"],
];

/**
 * Вакансия → «Собеседования»: ответственный интервьюер и участники (их занятость — по календарю OneBase), длительность, дни, часы, перерывы,
 * минимальный срок и горизонт записи, формат, адрес или ссылка, часовой пояс, порог оценки AI для приглашения, что сказать кандидату.
 * Пустое поле — общие настройки подбора (подсказка показывает, что действует).
 */
export function InterviewSettingsCard({ ctx, vacancyId, people }: { ctx: RecruitmentContext; vacancyId: string; people: { id: string; name: string; position: string | null }[] }) {
  const t = useT();
  const [s, setS] = useState<import("@/lib/recruitment-flow").InterviewSettings | null>(null);
  const [slots, setSlots] = useState<Slot[] | null>(null);
  const [saved, setSaved] = useState(false);
  const { busy, error, run } = useAction();
  useEffect(() => {
    flowApi.interviewSettings(vacancyId).then(setS, () => undefined);
  }, [vacancyId]);
  if (!s) return <p className="text-sm text-ink-3">{t("Загрузка…")}</p>;
  const set = (patch: Partial<typeof s>) => {
    setSaved(false);
    setS({ ...s, ...patch });
  };
  const days = (s.days ?? s.effectiveDays).split(",").filter(Boolean);
  const num = (v: string) => (v === "" ? null : Number(v));
  const off = !ctx.canManage;
  return (
    <div className="space-y-3">
      <div className="grid gap-3 sm:grid-cols-2">
        <Label label="Ответственный интервьюер">
          <select className={field} disabled={off} value={s.interviewerId ?? ""} onChange={(e) => set({ interviewerId: e.target.value || null })}>
            <option value="">{t("Первый из участников или рекрутер вакансии")}</option>
            {people.map((p) => (
              <option key={p.id} value={p.id}>
                {p.name}
              </option>
            ))}
          </select>
        </Label>
        <Label label="Участники" hint="Их занятость тоже проверяется; они — участники события в календаре.">
          <select
            multiple
            className={`${field} h-24`}
            disabled={off}
            value={s.participants}
            onChange={(e) => set({ participants: Array.from(e.target.selectedOptions).map((o) => o.value) })}
          >
            {people.map((p) => (
              <option key={p.id} value={p.id}>
                {p.name}
                {p.position && ` — ${p.position}`}
              </option>
            ))}
          </select>
        </Label>
        <Label label="Длительность, минут" hint={t("Сейчас: {n}", { n: s.effectiveDuration })}>
          <input type="number" min={15} max={240} className={field} disabled={off} value={s.durationMinutes ?? ""} onChange={(e) => set({ durationMinutes: num(e.target.value) })} />
        </Label>
        <div>
          <span className="mb-1 block text-xs font-medium text-ink-2">{t("Дни недели")}</span>
          <div className="flex flex-wrap gap-1">
            {dayNames.map(([d, label]) => (
              <label key={d} className="flex items-center gap-1 rounded border border-line px-2 py-1 text-xs">
                <input
                  type="checkbox"
                  disabled={off}
                  checked={days.includes(d)}
                  onChange={(e) => set({ days: (e.target.checked ? [...days, d] : days.filter((x) => x !== d)).sort().join(",") })}
                />
                {t(label)}
              </label>
            ))}
          </div>
        </div>
        <div className="grid grid-cols-2 gap-2">
          <Label label="С">
            <input type="time" className={field} disabled={off} value={s.dayStart ?? s.effectiveDayStart} onChange={(e) => set({ dayStart: e.target.value })} />
          </Label>
          <Label label="До">
            <input type="time" className={field} disabled={off} value={s.dayEnd ?? s.effectiveDayEnd} onChange={(e) => set({ dayEnd: e.target.value })} />
          </Label>
        </div>
        <Label label="Перерывы" hint="Например «13:00-14:00», несколько — через запятую.">
          <input className={field} disabled={off} value={s.breaks ?? ""} onChange={(e) => set({ breaks: e.target.value || null })} placeholder="13:00-14:00" />
        </Label>
        <Label label="Записывать не раньше чем через, часов" hint={t("Сейчас: {n}", { n: s.effectiveNotice })}>
          <input type="number" min={0} max={168} className={field} disabled={off} value={s.minNoticeHours ?? ""} onChange={(e) => set({ minNoticeHours: num(e.target.value) })} />
        </Label>
        <Label label="Записывать не дальше чем на, дней" hint={t("Сейчас: {n}", { n: s.effectiveHorizon })}>
          <input type="number" min={1} max={90} className={field} disabled={off} value={s.maxAdvanceDays ?? ""} onChange={(e) => set({ maxAdvanceDays: num(e.target.value) })} />
        </Label>
        <Label label="Формат">
          <select className={field} disabled={off} value={s.format ?? ""} onChange={(e) => set({ format: e.target.value || null })}>
            <option value="">{t(formatLabel[s.effectiveFormat] ?? s.effectiveFormat)}</option>
            {Object.entries(formatLabel).map(([k, v]) => (
              <option key={k} value={k}>
                {t(v)}
              </option>
            ))}
          </select>
        </Label>
        <Label label="Адрес офиса">
          <input className={field} disabled={off} value={s.address ?? ""} onChange={(e) => set({ address: e.target.value || null })} maxLength={300} />
        </Label>
        <Label label="Ссылка на онлайн-встречу" hint="https://… — для формата «онлайн».">
          <input className={field} disabled={off} value={s.meetingUrl ?? ""} onChange={(e) => set({ meetingUrl: e.target.value || null })} maxLength={500} />
        </Label>
        <Label label="Часовой пояс" hint={t("Сейчас: {tz}", { tz: s.effectiveTimeZone })}>
          <input className={field} disabled={off} value={s.timeZone ?? ""} onChange={(e) => set({ timeZone: e.target.value || null })} placeholder="Asia/Tashkent" />
        </Label>
        <Label label="Приглашать при оценке AI не ниже" hint="0–100. Пусто — когда пригласить, решает разговор (вопросы отбора); с порогом — AI приглашает сам, только если оценка не ниже.">
          <input type="number" min={0} max={100} className={field} disabled={off} value={s.inviteMinScore ?? ""} onChange={(e) => set({ inviteMinScore: num(e.target.value) })} />
        </Label>
        <Label label="Что сказать кандидату" hint="Что взять, как пройти. Попадёт в событие календаря." className="sm:col-span-2">
          <textarea className={textarea} disabled={off} value={s.instructions ?? ""} onChange={(e) => set({ instructions: e.target.value || null })} maxLength={1000} />
        </Label>
      </div>
      <div className="flex flex-wrap items-center gap-2">
        {ctx.canManage && (
          <button
            className={primary}
            disabled={!!busy}
            onClick={() => run("save", async () => {
              const { vacancyId: _v, effectiveInterviewerId: _a, effectiveDuration: _b, effectiveDays: _c, effectiveDayStart: _d, effectiveDayEnd: _e, effectiveNotice: _f,
                effectiveHorizon: _g, effectiveFormat: _h, effectiveTimeZone: _i, ...input } = s;
              setS(await flowApi.saveInterviewSettings(vacancyId, input));
              setSaved(true);
            })}
          >
            {t("Сохранить")}
          </button>
        )}
        <button className={button} onClick={() => run("slots", async () => setSlots(await flowApi.slots(vacancyId)))}>
          {t("Свободное время")}
        </button>
        {saved && <span className="text-sm text-ok">{t("Сохранено.")}</span>}
      </div>
      {slots && <p className="text-xs text-ink-2">{slots.length === 0 ? t("Свободного времени на 2 недели нет.") : slots.map((x) => slotText(x.startsAt)).join(" · ")}</p>}
      <ErrorText error={error} />
    </div>
  );
}
