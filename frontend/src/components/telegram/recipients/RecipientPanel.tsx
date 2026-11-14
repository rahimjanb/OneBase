"use client";

import { useEffect, useMemo, useRef, useState } from "react";
import Link from "next/link";
import { ArrowLeft, Check, ChevronDown, Eye, Globe, Loader2, Search, UserPlus, UserRound, Users, UsersRound, X } from "lucide-react";
import { useT } from "@/i18n/client";
import { plural } from "@/lib/format";
import { displayName, matrix, reportSummary, templateCodes, templateOf, type Template } from "@/lib/telegram-recipients";
import {
  isPdfJob,
  reportLanguages,
  reportsApi,
  type ReportJob,
  type ReportLanguage,
  type ReportOverview,
  type ReportRecipient,
  type ReportRecipientInput,
  type ReportRole,
} from "@/lib/telegramReports";
import { defaultScope } from "../TelegramReportsPreview";
import { Avatar, ROLE_TONE, Toggle, useDismiss } from "./bits";

export type PanelState = { id: string | null; input: ReportRecipientInput; focus?: "chats" | "test" };
export type PanelLayout = "inline" | "drawer" | "screen";

const ROLE_INFO: Record<ReportRole, { label: string; sub: string; hint: string }> = {
  rm: { label: "РМ", sub: "регион-менеджер", hint: "РМ получает отчёты по регионам, выбранным в его профиле" },
  sv: { label: "СВ", sub: "супервайзер", hint: "Супервайзер получает отчёты по ТП своего региона" },
  tp: { label: "ТП", sub: "торговый пред.", hint: "ТП получает только свои цифры" },
};

const TEMPLATES: { key: Template; label: string }[] = [
  { key: "role", label: "Как у {role}" },
  { key: "all", label: "Все" },
  { key: "evening", label: "Только итоги дня" },
  { key: "clear", label: "Очистить" },
];

type ChatFilter = "all" | "private" | "group" | "free";

/** Подпись запуска на чипе: «18:00», «18:00 пн–сб», «09:00 пн», «1-е». */
function slotLabel(job: ReportJob, t: (s: string) => string) {
  if (job.monthStart) return `${job.time} ${t("1-е")}`;
  if (job.monthEnd) return `${job.time} ${t("конец мес.")}`;
  const days = job.days.join(",");
  if (days === "6") return `${job.time} ${t("сб")}`;
  if (days === "1") return `${job.time} ${t("пн")}`;
  if (job.report === "weekly" && days === "1,2,3,4,5,6") return `${job.time} ${t("пн–сб")}`;
  return job.time;
}

const field =
  "h-10 w-full rounded-[10px] border border-line bg-page px-3 text-[13px] text-ink outline-none focus:border-accent disabled:opacity-50";

function Label({ children }: { children: React.ReactNode }) {
  return <span className="mb-1.5 block text-[11px] font-bold uppercase tracking-wide text-ink-4">{children}</span>;
}

function Step({ n, title, aside, children }: { n: number; title: string; aside?: React.ReactNode; children: React.ReactNode }) {
  return (
    <section className="border-b border-line px-4 py-4 last:border-b-0">
      <div className="mb-3 flex items-center gap-2.5">
        <span className="grid size-5 shrink-0 place-items-center rounded-full bg-accent text-[11px] font-bold text-white">{n}</span>
        <h3 className="min-w-0 flex-1 text-[14px] font-bold text-ink">{title}</h3>
        {aside}
      </div>
      {children}
    </section>
  );
}

/**
 * Панель «Новый получатель» / правка: 1) кто получает (роль, область, подпись), 2) куда (чаты), 3) какие отчёты и когда (шаблоны, запуски).
 * «Тест-отправка» — один выбранный отчёт в выбранные чаты с пометкой «ТЕСТ». Закрытие с несохранёнными изменениями — с вопросом.
 */
export function RecipientPanel({
  data,
  state,
  layout,
  onClose,
  onSaved,
  onMessage,
}: {
  data: ReportOverview;
  state: PanelState;
  layout: PanelLayout;
  onClose: () => void;
  onSaved: (saved: ReportRecipient, created: boolean) => void;
  onMessage: (text: string, tone?: "ok" | "bad") => void;
}) {
  const t = useT();
  const [input, setInput] = useState(state.input);
  const [initial] = useState(() => JSON.stringify(state.input));
  const [search, setSearch] = useState("");
  const [chatFilter, setChatFilter] = useState<ChatFilter>("all");
  const [allChats, setAllChats] = useState(false);
  const [openSlots, setOpenSlots] = useState<number[]>([]);
  const [busy, setBusy] = useState(false);
  const [testing, setTesting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const root = useRef<HTMLDivElement>(null);
  const chatsRef = useRef<HTMLDivElement>(null);
  const dirty = JSON.stringify(input) !== initial;
  const editing = data.recipients.find((r) => r.id === state.id) ?? null;
  const editingName = editing ? displayName(editing, data.regions) : "";

  const close = () => {
    if (dirty && !window.confirm(t("Закрыть без сохранения? Изменения пропадут."))) return;
    onClose();
  };

  // Esc закрывает (с вопросом при изменениях); фокус — в панель; «Привязать чат» — сразу к чатам.
  useEffect(() => {
    const key = (e: KeyboardEvent) => {
      if (e.key === "Escape" && !e.defaultPrevented) close();
    };
    document.addEventListener("keydown", key);
    return () => document.removeEventListener("keydown", key);
  });
  useEffect(() => {
    root.current?.querySelector<HTMLElement>("[data-autofocus]")?.focus({ preventScroll: true });
    if (state.focus === "chats") chatsRef.current?.scrollIntoView({ block: "start", behavior: "smooth" });
  }, [state.focus]);

  const region = data.regions.find((r) => r.id === input.regionId) ?? null;
  const tpRegion = input.role === "tp" ? (data.regions.find((r) => r.agents.some((a) => a.id === input.agentId)) ?? region) : null;
  const shownRegion = input.role === "tp" ? tpRegion : region;

  // РМ — пользователь с ролью «РМ»: регионы из его профиля (сервер берёт их при каждой отправке); без пользователя — направление или страна.
  const rmUsers = useMemo(() => data.rmUsers ?? [], [data.rmUsers]);
  const rmTaken = useMemo(
    () => new Set(data.recipients.filter((r) => r.role === "rm" && r.userId && r.id !== state.id).map((r) => r.userId as string)),
    [data.recipients, state.id],
  );
  const rmUser = input.role === "rm" && input.userId ? (rmUsers.find((u) => u.id === input.userId) ?? null) : null;
  const rmRegions =
    input.role !== "rm"
      ? []
      : input.userId
        ? data.regions.filter((r) => rmUser?.regionIds.includes(r.id))
        : data.regions.filter((r) => !input.directionId || r.directionId === input.directionId);
  const rmCountry = input.role === "rm" && !input.userId && !input.directionId;
  const pickRm = (userId: string | null, directionId: string | null = null) =>
    setInput((i) => {
      // Подтверждённый кодом личный чат этого пользователя — сразу в чаты, если чатов ещё не выбрали.
      const own = userId ? data.chats.find((c) => c.linkedUserId === userId && c.canSend) : undefined;
      return { ...i, userId, directionId: userId ? null : directionId, chatIds: own && i.chatIds.length === 0 ? [own.id] : i.chatIds };
    });

  const setRole = (role: ReportRole) => {
    if (role === input.role) return;
    const scope = defaultScope(data, role);
    const rm = role === "rm" ? (rmUsers.find((u) => !rmTaken.has(u.id)) ?? rmUsers[0] ?? null) : null;
    const own = rm ? data.chats.find((c) => c.linkedUserId === rm.id && c.canSend) : undefined;
    setInput((i) => ({
      ...i,
      ...scope,
      userId: rm?.id ?? null,
      chatIds: own && i.chatIds.length === 0 ? [own.id] : i.chatIds,
      reports: templateCodes("role", data.jobs, role),
    }));
  };

  // Чаты: кто ещё их получает (выбор разрешён), фильтр, выбранные и подтверждённые — сверху.
  const usedBy = useMemo(() => {
    const map = new Map<string, string[]>();
    for (const r of data.recipients) {
      if (r.id === state.id) continue;
      for (const c of r.chats) map.set(c.id, [...(map.get(c.id) ?? []), displayName(r, data.regions)]);
    }
    return map;
  }, [data.recipients, data.regions, state.id]);
  const chats = useMemo(() => {
    const q = search.trim().toLowerCase().replace(/^@/, "");
    return data.chats
      .filter((c) => !q || c.title.toLowerCase().includes(q) || (c.username ?? "").toLowerCase().includes(q) || (c.linkedUser ?? "").toLowerCase().includes(q))
      .filter((c) => (chatFilter === "private" ? c.type === "Private" : chatFilter === "group" ? c.type !== "Private" : chatFilter === "free" ? !usedBy.has(c.id) : true))
      .sort(
        (a, b) =>
          Number(input.chatIds.includes(b.id)) - Number(input.chatIds.includes(a.id)) ||
          Number(!!input.userId && b.linkedUserId === input.userId) - Number(!!input.userId && a.linkedUserId === input.userId) ||
          Number(!!b.linkedUser) - Number(!!a.linkedUser) ||
          Number(b.canSend) - Number(a.canSend),
      );
  }, [data.chats, search, chatFilter, usedBy, input.chatIds, input.userId]);
  const visibleChats = allChats || search ? chats : chats.slice(0, Math.max(4, chats.filter((c) => input.chatIds.includes(c.id)).length));
  const toggleChat = (id: string) => setInput((i) => ({ ...i, chatIds: i.chatIds.includes(id) ? i.chatIds.filter((x) => x !== id) : [...i.chatIds, id] }));

  // Отчёты: строки 1–9 роли, шаблон, сводка.
  const cells = matrix({ role: input.role, reports: input.reports }, data.jobs).filter((c) => c.slots.length > 0);
  const template = templateOf(input.reports, data.jobs, input.role);
  const summary = reportSummary(input.reports, input.role, data.jobs);
  const roleDefaults = templateCodes("role", data.jobs, input.role);
  const toggleReport = (codes: string[], on: boolean) =>
    setInput((i) => {
      const defaults = codes.filter((c) => roleDefaults.includes(c));
      return { ...i, reports: on ? [...new Set([...i.reports, ...(defaults.length ? defaults : codes)])] : i.reports.filter((c) => !codes.includes(c)) };
    });
  const toggleSlot = (code: string) => setInput((i) => ({ ...i, reports: i.reports.includes(code) ? i.reports.filter((c) => c !== code) : [...i.reports, code] }));
  const hasPdf = data.jobs.some((j) => isPdfJob(j) && input.reports.includes(j.code));
  const hasImages = data.jobs.some((j) => !isPdfJob(j) && input.reports.includes(j.code));

  const scopeReady = input.role === "rm" ? !input.userId || !!rmUser : input.role === "sv" ? !!input.regionId : !!input.agentId;
  const ready = scopeReady && input.chatIds.length > 0 && input.reports.length > 0;
  const missing = !scopeReady ? t("Выберите область получателя") : input.chatIds.length === 0 ? t("Выберите хотя бы один чат") : input.reports.length === 0 ? t("Включите хотя бы один отчёт") : null;

  const save = async () => {
    setBusy(true);
    setError(null);
    try {
      const saved = await reportsApi.saveRecipient(state.id, input);
      onSaved(saved, !state.id);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  };

  const test = async (job: ReportJob) => {
    const targets = data.chats.filter((c) => input.chatIds.includes(c.id) && c.canSend);
    if (targets.length === 0) return;
    setTesting(true);
    setError(null);
    try {
      const results: string[] = [];
      for (const chat of targets) {
        const result = await reportsApi.test(
          { job: job.code, role: input.role, userId: input.role === "rm" ? (input.userId ?? null) : null, directionId: input.directionId, regionId: input.regionId, supervisorId: input.supervisorId, agentId: input.agentId, recipientId: state.id, date: null, time: null, language: input.language ?? null },
          chat.id,
        );
        results.push("conflict" in result ? `${chat.title}: ${result.conflict}` : result.status === "sent" ? `${chat.title}: ${t("отправлен")}` : `${chat.title}: ${t(result.error ?? result.status)}`);
      }

      onMessage(`${t("Тест «{report}»", { report: `${t(job.reportTitle)} · ${job.time}` })} — ${results.join("; ")}`);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setTesting(false);
    }
  };

  const shell =
    layout === "inline"
      ? "sticky top-4 max-h-[calc(100dvh-6.5rem)] rounded-[16px] border border-accent shadow-[0_12px_40px_rgb(0_0_0/0.4)]"
      : layout === "drawer"
        ? "fixed inset-y-0 right-0 z-50 w-[min(440px,100vw)] border-l border-accent shadow-[0_12px_40px_rgb(0_0_0/0.5)]"
        : "fixed inset-0 z-50";

  return (
    <>
      {layout === "drawer" && <div aria-hidden className="fixed inset-0 z-40 bg-black/50" onClick={close} />}
      <div
        ref={root}
        role="dialog"
        aria-modal={layout !== "inline"}
        aria-label={state.id ? t("Получатель: {name}", { name: editingName }) : t("Новый получатель")}
        className={`flex flex-col overflow-hidden bg-surface-solid ${shell}`}
      >
        <header className={`flex shrink-0 items-center gap-3 border-b border-line px-4 py-3 ${layout === "screen" ? "pt-[calc(env(safe-area-inset-top)+12px)]" : ""}`}>
          {layout === "screen" && (
            <button type="button" onClick={close} aria-label={t("Назад")} className="grid size-9 shrink-0 place-items-center rounded-full bg-muted text-ink">
              <ArrowLeft className="size-[18px]" />
            </button>
          )}
          {editing ? (
            <Avatar name={editingName} role={editing.role} />
          ) : (
            <span aria-hidden className="grid size-[34px] shrink-0 place-items-center rounded-[10px] bg-accent-soft text-accent-strong">
              <UserPlus className="size-[18px]" />
            </span>
          )}
          <span className="min-w-0 flex-1">
            <span className="block truncate text-[15px] font-bold text-ink">{editing ? editingName : t("Новый получатель")}</span>
            <span className="block text-[11px] text-ink-4">{t("роль → чаты → отчёты")}</span>
          </span>
          {layout !== "screen" && (
            <button type="button" onClick={close} aria-label={t("Закрыть")} className="grid size-8 shrink-0 place-items-center rounded-lg text-ink-3 hover:bg-muted hover:text-ink">
              <X className="size-4" />
            </button>
          )}
        </header>

        <div className="min-h-0 flex-1 overflow-y-auto overscroll-contain">
          <Step n={1} title={t("Кто получает")}>
            <div role="radiogroup" aria-label={t("Роль")} className="grid grid-cols-3 gap-1 rounded-[12px] border border-line bg-page p-1">
              {(["rm", "sv", "tp"] as ReportRole[]).map((role) => {
                const on = input.role === role;
                return (
                  <button
                    key={role}
                    type="button"
                    role="radio"
                    aria-checked={on}
                    data-autofocus={on ? true : undefined}
                    onClick={() => setRole(role)}
                    className={`rounded-[9px] border px-1 py-2 text-center outline-none focus-visible:ring-2 focus-visible:ring-accent/60 ${on ? `bg-muted ${ROLE_TONE[role].ring}` : "border-transparent hover:bg-muted/60"}`}
                  >
                    <span className={`block text-[14px] font-bold ${on ? ROLE_TONE[role].pill.split(" ")[1] : "text-ink"}`}>{t(ROLE_INFO[role].label)}</span>
                    <span className="block truncate text-[10px] text-ink-4">{t(ROLE_INFO[role].sub)}</span>
                  </button>
                );
              })}
            </div>
            <p className="mt-2 text-[11px] text-ink-3">{t(ROLE_INFO[input.role].hint)}</p>

            <div className="mt-3 grid grid-cols-2 gap-3">
              {input.role === "rm" ? (
                <div className="col-span-2">
                  <Label>{t("Пользователь с ролью «РМ»")}</Label>
                  {input.userId && !rmUser && (
                    <p role="alert" className="mb-2 rounded-[10px] bg-bad-soft px-3 py-2 text-[12px] font-semibold text-bad">
                      {t("Этот пользователь больше не РМ или отключён — выберите РМ из списка")}
                    </p>
                  )}
                  <div role="radiogroup" aria-label={t("РМ")} className="max-h-[272px] space-y-0.5 overflow-y-auto overscroll-contain rounded-[12px] border border-line bg-page p-1">
                    {rmUsers.map((u) => {
                      const on = input.userId === u.id;
                      const names = data.regions.filter((r) => u.regionIds.includes(r.id)).map((r) => r.name);
                      return (
                        <button
                          key={u.id}
                          type="button"
                          role="radio"
                          aria-checked={on}
                          onClick={() => pickRm(u.id)}
                          className={`flex w-full items-center gap-2.5 rounded-[9px] border px-2.5 py-2 text-left outline-none focus-visible:ring-2 focus-visible:ring-accent/60 ${on ? "border-accent bg-accent/[0.07]" : "border-transparent hover:bg-muted"}`}
                        >
                          <Avatar name={u.name} role="rm" size={30} />
                          <span className="min-w-0 flex-1">
                            <span className="block truncate text-[13px] font-semibold text-ink">{u.name}</span>
                            <span className={`block truncate text-[11px] ${names.length ? "text-ink-4" : "text-warn"}`}>
                              {names.length ? `${names.join(", ")} · ${u.agents} ${t("ТП")}` : t("Нет регионов с ТП в профиле")}
                            </span>
                          </span>
                          {rmTaken.has(u.id) && <span className="shrink-0 rounded-md bg-muted px-1.5 py-0.5 text-[10px] font-semibold text-ink-3">{t("уже получает")}</span>}
                          {on && <Check aria-hidden className="size-4 shrink-0 text-accent-strong" />}
                        </button>
                      );
                    })}
                    <button
                      type="button"
                      role="radio"
                      aria-checked={rmCountry}
                      onClick={() => pickRm(null)}
                      className={`flex w-full items-center gap-2.5 rounded-[9px] border px-2.5 py-2 text-left outline-none focus-visible:ring-2 focus-visible:ring-accent/60 ${rmCountry ? "border-accent bg-accent/[0.07]" : "border-transparent hover:bg-muted"}`}
                    >
                      <span className="grid size-[30px] shrink-0 place-items-center rounded-full bg-muted text-ink-3">
                        <Globe aria-hidden className="size-4" />
                      </span>
                      <span className="min-w-0 flex-1">
                        <span className="block truncate text-[13px] font-semibold text-ink">{t("Вся страна")}</span>
                        <span className="block truncate text-[11px] text-ink-4">{t("все регионы · для РОП и руководства")}</span>
                      </span>
                      {rmCountry && <Check aria-hidden className="size-4 shrink-0 text-accent-strong" />}
                    </button>
                  </div>
                  {rmUsers.length === 0 && (
                    <p className="mt-2 text-[12px] text-ink-3">
                      {t("В системе нет пользователей с ролью «РМ». Роль и регионы назначаются в окне пользователя —")}{" "}
                      <Link href="/settings/users" className="font-semibold text-accent-strong hover:underline">
                        {t("Пользователи")}
                      </Link>
                    </p>
                  )}
                  {(data.directions.length > 0 || !!input.directionId) && (
                    <label className="mt-2 block">
                      <span className="mb-1 block text-[11px] text-ink-4">{t("Или по направлению оргструктуры")}</span>
                      <select className={field} value={input.directionId ?? ""} onChange={(e) => pickRm(null, e.target.value || null)}>
                        <option value="">{t("—")}</option>
                        {data.directions.map((d) => (
                          <option key={d.id} value={d.id}>
                            {d.name}
                            {d.manager ? ` · ${d.manager}` : ""}
                          </option>
                        ))}
                      </select>
                    </label>
                  )}
                </div>
              ) : (
                <>
                  <label className="block min-w-0">
                    <Label>{t("Регион")}</Label>
                    <select
                      className={field}
                      value={shownRegion?.id ?? ""}
                      onChange={(e) => {
                        const next = data.regions.find((r) => r.id === e.target.value);
                        setInput((i) => ({ ...i, regionId: next?.id ?? null, supervisorId: null, agentId: i.role === "tp" ? (next?.agents[0]?.id ?? null) : null }));
                      }}
                    >
                      <option value="" disabled>
                        {t("Выберите регион")}
                      </option>
                      {data.regions.map((r) => (
                        <option key={r.id} value={r.id}>
                          {r.name}
                        </option>
                      ))}
                    </select>
                  </label>
                  {input.role === "sv" ? (
                    <label className="block min-w-0">
                      <Label>{t("Супервайзер")}</Label>
                      <select
                        className={field}
                        value={input.supervisorId ?? ""}
                        disabled={!region}
                        onChange={(e) => setInput((i) => ({ ...i, supervisorId: e.target.value ? Number(e.target.value) : null }))}
                      >
                        <option value="" title={t("СВР региона")}>{region?.supervisorName ?? t("СВР региона")}</option>
                        {region?.supervisors.map((s) => (
                          <option key={s.id} value={s.id}>
                            {s.name}
                          </option>
                        ))}
                      </select>
                    </label>
                  ) : (
                    <label className="block min-w-0">
                      <Label>{t("Торговый пред.")}</Label>
                      <select
                        className={field}
                        value={input.agentId ?? ""}
                        disabled={!shownRegion}
                        onChange={(e) => setInput((i) => ({ ...i, agentId: e.target.value ? Number(e.target.value) : null }))}
                      >
                        <option value="" disabled>
                          {t("Выберите ТП")}
                        </option>
                        {shownRegion?.agents.map((a) => (
                          <option key={a.id} value={a.id}>
                            {a.name}
                          </option>
                        ))}
                      </select>
                    </label>
                  )}
                </>
              )}
            </div>

            {!(input.userId && !rmUser) && (
              <p className="mt-3 flex items-start gap-2 rounded-[10px] bg-muted px-3 py-2 text-[12px] text-ink-2">
                <Users aria-hidden className="mt-0.5 size-3.5 shrink-0 text-ink-3" />
                <span>
                  {input.role === "rm"
                    ? input.userId
                      ? `${t("В отчёте — регионы из профиля РМ")}: ${rmRegions.map((r) => r.name).join(", ") || "—"}. ${t("Поменяют регионы в профиле — отчёты пойдут по новым")}`
                      : rmCountry
                        ? t("В отчёте — все регионы страны: {n}", { n: rmRegions.length })
                        : `${t("В отчёте — регионы направления")}: ${rmRegions.map((r) => r.name).join(", ") || "—"}`
                    : input.role === "sv"
                      ? t("В отчёте — команда региона в «Продажах»: {n} ТП", { n: region?.agents.length ?? 0 })
                      : shownRegion?.supervisorName
                        ? t("Личные цифры ТП · СВ {name}", { name: shownRegion.supervisorName })
                        : t("У региона нет СВР в оргструктуре «Продаж» — ТП будет без супервайзера")}
                </span>
              </p>
            )}

            <label className="mt-3 block">
              <Label>{t("Подпись в отчёте (необязательно)")}</Label>
              <input className={field} value={input.title ?? ""} maxLength={200} onChange={(e) => setInput((i) => ({ ...i, title: e.target.value || null }))} placeholder={t("По умолчанию — область")} />
            </label>
          </Step>

          <div ref={chatsRef}>
            <Step
              n={2}
              title={t("Куда отправлять")}
              aside={<span className="rounded-md bg-accent-soft px-2 py-0.5 text-[11px] font-bold text-accent-strong">{t("выбрано {n}", { n: input.chatIds.length })}</span>}
            >
              <label className="relative block">
                <span className="sr-only">{t("Поиск чата")}</span>
                <Search aria-hidden className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-ink-4" />
                <input type="search" className={`${field} pl-9`} value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t("Название, @username или имя")} />
              </label>
              <div className="mt-2.5 flex flex-wrap gap-1.5">
                {(
                  [
                    ["all", "Все чаты"],
                    ["private", "Личные"],
                    ["group", "Группы"],
                    ["free", "Не привязаны"],
                  ] as const
                ).map(([key, label]) => (
                  <button
                    key={key}
                    type="button"
                    aria-pressed={chatFilter === key}
                    onClick={() => setChatFilter(key)}
                    className={`h-7 rounded-lg border px-2.5 text-[12px] font-semibold ${chatFilter === key ? "border-accent bg-accent-soft text-accent-strong" : "border-transparent bg-muted text-ink-2"}`}
                  >
                    {t(label)}
                  </button>
                ))}
              </div>
              <div className="mt-2.5 overflow-hidden rounded-[12px] border border-line">
                {visibleChats.length === 0 ? (
                  <p className="px-3 py-3 text-[12px] text-ink-3">{data.chats.length === 0 ? t("Чатов нет — обновите их на странице Telegram, вкладка «Чаты».") : t("Чатов не найдено.")}</p>
                ) : (
                  visibleChats.map((c) => {
                    const on = input.chatIds.includes(c.id);
                    const others = usedBy.get(c.id) ?? [];
                    const Icon = c.type === "Private" ? UserRound : UsersRound;
                    return (
                      <label
                        key={c.id}
                        className={`flex items-center gap-3 border-b border-line px-3 py-2.5 last:border-b-0 ${c.canSend ? "cursor-pointer" : "opacity-50"} ${on ? "bg-accent/[0.08]" : c.canSend ? "hover:bg-muted/60" : ""}`}
                      >
                        <input type="checkbox" checked={on} disabled={!c.canSend && !on} onChange={() => toggleChat(c.id)} className="size-[18px] shrink-0 accent-[var(--color-accent)]" />
                        <Icon aria-hidden className="size-4 shrink-0 text-ink-3" />
                        <span className="min-w-0 flex-1">
                          <span className="block truncate text-[12px] font-semibold text-ink">{c.title}</span>
                          <span className="block truncate text-[10px] text-ink-4">
                            {c.type === "Private" ? t("личный") : c.type === "Channel" ? t("канал") : t("группа")}
                            {c.username ? ` · @${c.username}` : ""}
                            {c.linkedUser ? ` · ${t("привязан: {name}", { name: c.linkedUser })}` : ""}
                            {!c.canSend && c.reason ? ` · ${t(c.reason)}` : ""}
                          </span>
                          {others.length > 0 && (
                            <span className="block truncate text-[10px] text-warn">
                              {t("уже получает: {names}", { names: others.slice(0, 2).join(", ") + (others.length > 2 ? ` +${others.length - 2}` : "") })}
                            </span>
                          )}
                        </span>
                      </label>
                    );
                  })
                )}
              </div>
              {!allChats && !search && chats.length > visibleChats.length && (
                <button type="button" onClick={() => setAllChats(true)} className="mt-2 flex items-center gap-1 text-[12px] font-semibold text-accent-strong hover:underline">
                  {t("Ещё {n} {word}", { n: chats.length - visibleChats.length, word: t(plural(chats.length - visibleChats.length, ["чат", "чата", "чатов"])) })}
                  <ChevronDown aria-hidden className="size-3.5" />
                </button>
              )}
            </Step>
          </div>

          <Step
            n={3}
            title={t("Какие отчёты и когда")}
            aside={
              <span className="text-[12px] font-bold text-accent-strong">
                {t("{on} из {of} · {n} в день", { on: summary.on, of: summary.of, n: summary.perDay })}
              </span>
            }
          >
            <div className="mb-3 flex flex-wrap items-center gap-1.5">
              <span className="text-[12px] text-ink-3">{t("Шаблон")}:</span>
              {TEMPLATES.map((x) => {
                const on = template === x.key;
                return (
                  <button
                    key={x.key}
                    type="button"
                    aria-pressed={on}
                    onClick={() => setInput((i) => ({ ...i, reports: templateCodes(x.key, data.jobs, i.role) }))}
                    className={`flex h-7 items-center gap-1 rounded-lg border px-2.5 text-[12px] font-semibold ${on ? "border-accent bg-accent-soft text-accent-strong" : "border-transparent bg-muted text-ink-2"}`}
                  >
                    {on && <Check aria-hidden className="size-3.5" />}
                    {t(x.label, { role: t(ROLE_INFO[input.role].label) })}
                  </button>
                );
              })}
              {!template && <span className="text-[12px] font-semibold text-ink-3">· {t("свой набор")}</span>}
            </div>
            <ul className="space-y-1.5">
              {cells.map((c) => {
                const codes = c.slots.map((s) => s.code);
                const limit = openSlots.includes(c.n) ? c.slots.length : 4;
                return (
                  <li key={c.n} className={`flex gap-3 rounded-[10px] px-2 py-[7px] ${c.on ? "bg-muted" : ""}`}>
                    <span className="pt-0.5">
                      <Toggle checked={c.on} onChange={(v) => toggleReport(codes, v)} label={`${c.n}. ${t(c.title)}`} tone="accent" size="sm" />
                    </span>
                    <span className="min-w-0 flex-1">
                      <span className={`block text-[12px] font-semibold ${c.on ? "text-ink" : "text-ink-3"}`}>
                        {c.n}. {t(c.title)}
                      </span>
                      <span className="mt-1 flex flex-wrap gap-1">
                        {c.slots.slice(0, limit).map((s) => {
                          const job = data.jobs.find((j) => j.code === s.code)!;
                          return (
                            <button
                              key={s.code}
                              type="button"
                              aria-pressed={s.on}
                              title={`${t(job.slotTitle)}${job.enabled ? "" : ` · ${t("запуск выключен в расписании")}`}`}
                              onClick={() => toggleSlot(s.code)}
                              className={`flex h-[22px] items-center gap-1 rounded-md border px-1.5 text-[10px] font-semibold tabular-nums ${
                                s.on ? "border-accent/40 bg-accent-soft text-accent-strong" : "border-line-strong text-ink-4"
                              } ${job.enabled ? "" : "border-dashed"}`}
                            >
                              {s.on && <Check aria-hidden className="size-3" />}
                              {slotLabel(job, t)}
                            </button>
                          );
                        })}
                        {c.slots.length > limit && (
                          <button type="button" onClick={() => setOpenSlots((l) => [...l, c.n])} className="h-[22px] rounded-md border border-line-strong px-1.5 text-[10px] font-semibold text-ink-3">
                            +{c.slots.length - limit}
                          </button>
                        )}
                      </span>
                    </span>
                  </li>
                );
              })}
            </ul>
            {(hasImages || hasPdf) && (
              <div className="mt-3 grid grid-cols-2 gap-3">
                {hasImages && (
                  <label className="block min-w-0">
                    <Label>{t("Язык картинок")}</Label>
                    <select className={field} value={input.language ?? ""} onChange={(e) => setInput((i) => ({ ...i, language: (e.target.value || null) as ReportLanguage | null }))}>
                      <option value="">{t("Как в настройках")}</option>
                      {reportLanguages.map((l) => (
                        <option key={l.code} value={l.code}>
                          {t(l.label)}
                        </option>
                      ))}
                    </select>
                  </label>
                )}
                {hasPdf && (
                  <label className="block min-w-0">
                    <Label>{t("Язык PDF")}</Label>
                    <select className={field} value={input.pdfLanguage ?? ""} onChange={(e) => setInput((i) => ({ ...i, pdfLanguage: (e.target.value || null) as ReportLanguage | null }))}>
                      <option value="">{t("Как в настройках")}</option>
                      {reportLanguages.map((l) => (
                        <option key={l.code} value={l.code}>
                          {t(l.label)}
                        </option>
                      ))}
                    </select>
                  </label>
                )}
              </div>
            )}
          </Step>
        </div>

        {error && <p className="mx-4 mb-0 mt-3 shrink-0 rounded-[10px] bg-bad-soft px-3 py-2 text-[12px] text-bad">{error}</p>}
        <footer className={`flex shrink-0 items-center gap-2 border-t border-line bg-muted px-4 py-3 ${layout === "screen" ? "pb-[calc(env(safe-area-inset-bottom)+12px)]" : ""}`}>
          <TestMenu
            jobs={data.jobs.filter((j) => input.reports.includes(j.code) && !isPdfJob(j))}
            disabled={!scopeReady || testing || !data.chats.some((c) => input.chatIds.includes(c.id) && c.canSend)}
            busy={testing}
            autoOpen={state.focus === "test"}
            onPick={test}
          />
          <span className="ml-auto" />
          <button type="button" onClick={close} className="h-10 rounded-[10px] border border-line bg-surface px-4 text-[13px] font-semibold text-ink">
            {t("Отмена")}
          </button>
          <button
            type="button"
            onClick={save}
            disabled={busy || !ready || (!!state.id && !dirty)}
            title={missing ?? undefined}
            className="flex h-10 items-center gap-1.5 rounded-[10px] bg-accent px-4 text-[13px] font-semibold text-white disabled:opacity-40"
          >
            {busy ? <Loader2 aria-hidden className="size-4 animate-spin" /> : <Check aria-hidden className="size-4" />}
            {state.id ? t("Сохранить") : t("Добавить")}
          </button>
        </footer>
      </div>
    </>
  );
}

/** «Тест-отправка»: один отчёт на выбор — в выбранные чаты, с пометкой «ТЕСТ». */
function TestMenu({ jobs, disabled, busy, autoOpen, onPick }: { jobs: ReportJob[]; disabled: boolean; busy: boolean; autoOpen: boolean; onPick: (job: ReportJob) => void }) {
  const t = useT();
  const [open, setOpen] = useState(autoOpen && !disabled);
  const ref = useDismiss(open, () => setOpen(false));
  return (
    <div ref={ref} className="relative">
      <button
        type="button"
        aria-expanded={open}
        disabled={disabled || jobs.length === 0}
        onClick={() => setOpen((v) => !v)}
        title={jobs.length === 0 ? t("Включите отчёт картинками — PDF проверяется на вкладке «Отчёты PDF»") : undefined}
        className="flex h-10 items-center gap-1.5 rounded-[10px] px-2 text-[13px] font-semibold text-accent-strong hover:bg-accent-soft disabled:opacity-40"
      >
        {busy ? <Loader2 aria-hidden className="size-4 animate-spin" /> : <Eye aria-hidden className="size-4" />}
        {t("Тест-отправка")}
      </button>
      {open && (
        <div className="absolute bottom-full left-0 z-50 mb-2 max-h-[320px] w-[300px] overflow-y-auto rounded-xl border border-line bg-surface-solid p-1.5 shadow-[0_12px_40px_rgb(0_0_0/0.4)]">
          <p className="px-2.5 pb-1.5 pt-1 text-[11px] text-ink-3">{t("Какой отчёт отправить в выбранные чаты с пометкой «ТЕСТ»")}</p>
          {jobs.map((j) => (
            <button
              key={j.code}
              type="button"
              onClick={() => {
                setOpen(false);
                onPick(j);
              }}
              className="flex w-full items-baseline gap-2 rounded-lg px-2.5 py-2 text-left text-[13px] text-ink hover:bg-muted"
            >
              <span className="w-11 shrink-0 font-semibold tabular-nums">{j.time}</span>
              <span className="min-w-0">
                <span className="block truncate">{t(j.reportTitle)}</span>
                <span className="block truncate text-[11px] text-ink-4">{t(j.slotTitle)}</span>
              </span>
            </button>
          ))}
        </div>
      )}
    </div>
  );
}

