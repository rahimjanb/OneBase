"use client";

import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Loader2, Send, UserPlus, Users, X } from "lucide-react";
import { Sheet } from "@/components/field/Sheet";
import { useT } from "@/i18n/client";
import { plural } from "@/lib/format";
import {
  NO_FILTERS,
  addChats,
  addReports,
  attention,
  disable,
  displayName,
  enable,
  facets as countFacets,
  filtersQuery,
  groupRows,
  inputOf,
  matches,
  nameOf,
  nearSends,
  regionIdsOf,
  setTimes,
  templateCodes,
  type Issue,
  type RecipientFilters,
} from "@/lib/telegram-recipients";
import { deliveryStatus, reportsApi, type ReportOverview, type ReportRecipient, type ReportRecipientInput } from "@/lib/telegramReports";
import { defaultScope } from "../TelegramReportsPreview";
import { Toast, type ToastData } from "./bits";
import { BulkBar, BulkMenus, DisableDialog, type BulkHandlers } from "./BulkActions";
import { FilterBar, MobileFilters } from "./RecipientFilters";
import { RecipientCards } from "./RecipientCards";
import { RecipientPanel, type PanelLayout, type PanelState } from "./RecipientPanel";
import { RecipientsTable, type RowAction } from "./RecipientsTable";
import { AttentionCard, NearSendsCard } from "./TodayCards";

/** Панель справа помещается рядом с таблицей, когда вкладка не уже этого (иначе — выезжает поверх). */
const INLINE_PANEL = 1100;

function useWidth<T extends HTMLElement>() {
  const ref = useRef<T>(null);
  const [width, setWidth] = useState(0);
  useEffect(() => {
    const el = ref.current;
    if (!el) return;
    const observer = new ResizeObserver(([entry]) => setWidth(entry.contentRect.width));
    observer.observe(el);
    return () => observer.disconnect();
  }, []);
  return [ref, width] as const;
}

function useDesktop() {
  const [desktop, setDesktop] = useState(true);
  useEffect(() => {
    const query = window.matchMedia("(min-width: 1024px)");
    const update = () => setDesktop(query.matches);
    update();
    query.addEventListener("change", update);
    return () => query.removeEventListener("change", update);
  }, []);
  return desktop;
}

/**
 * Вкладка «Получатели»: фильтры (в адресе) со счётчиками, таблица по регионам (телефон и планшет — карточки), массовые действия одним
 * сохранением с «Отменить», переключатель с причиной и сроком отключения, панель «Новый получатель / правка», ближайшие отправки и
 * «Требуют внимания». Логика рассылки не меняется.
 */
export function RecipientsTab({
  data,
  setData,
  reload,
  initialFilters,
  onSchedule,
  addSignal,
}: {
  data: ReportOverview;
  setData: (update: (d: ReportOverview) => ReportOverview) => void;
  reload: () => Promise<void>;
  initialFilters: RecipientFilters;
  onSchedule: () => void;
  /** Растёт — открыть «Новый получатель» (кнопка в шапке телефона). */
  addSignal: number;
}) {
  const t = useT();
  const [f, setF] = useState(initialFilters);
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [panel, setPanel] = useState<PanelState | null>(null);
  const [busy, setBusy] = useState<Set<string>>(new Set());
  const [bulkBusy, setBulkBusy] = useState(false);
  const [toast, setToast] = useState<ToastData | null>(null);
  const [disabling, setDisabling] = useState<ReportRecipient[] | null>(null);
  const [sending, setSending] = useState<ReportRecipient | null>(null);
  const [now, setNow] = useState(() => new Date());
  const [wrap, width] = useWidth<HTMLDivElement>();
  const desktop = useDesktop();
  const canAdd = data.canUseSales && !!data.settings.accountId;

  useEffect(() => {
    const timer = setInterval(() => setNow(new Date()), 60_000);
    return () => clearInterval(timer);
  }, []);

  // Фильтры — в адресе (без перехода): вкладка и всё, что отличается от умолчания.
  useEffect(() => {
    const query = filtersQuery(f);
    const url = `${window.location.pathname}?tab=recipients${query ? `&${query}` : ""}`;
    if (`${window.location.pathname}${window.location.search}` !== url) window.history.replaceState(window.history.state, "", url);
  }, [f]);

  const set = useCallback((patch: Partial<RecipientFilters>) => setF((x) => ({ ...x, ...patch })), []);
  const ctx = useMemo(
    () => ({ regions: data.regions, jobs: data.jobs, chats: data.chats, rmUsers: data.rmUsers, now }),
    [data.regions, data.jobs, data.chats, data.rmUsers, now],
  );
  // Имя для списка (ТП — по «Продажам», СВ — по супервайзеру); на сервер уходит прежнее — inputOf его не берёт.
  const recipients = useMemo(() => data.recipients.map((r) => ({ ...r, display: displayName(r, data.regions) })), [data.recipients, data.regions]);
  const rows = useMemo(() => recipients.filter((r) => matches(r, f, ctx)), [recipients, f, ctx]);
  const facets = useMemo(() => countFacets(recipients, f, ctx), [recipients, f, ctx]);
  const groups = useMemo(() => groupRows(rows, f, ctx), [rows, f, ctx]);
  const near = useMemo(() => nearSends(data.jobs, recipients, now), [data.jobs, recipients, now]);
  const watch = useMemo(() => attention(recipients, data.metrics, now), [recipients, data.metrics, now]);
  const regionsShown = useMemo(() => new Set(recipients.flatMap((r) => regionIdsOf(r, data.regions, data.rmUsers))).size, [recipients, data.regions, data.rmUsers]);
  const regionNamesOf = useCallback(
    (r: ReportRecipient) => regionIdsOf(r, data.regions, data.rmUsers).map((id) => data.regions.find((x) => x.id === id)?.name ?? "").filter(Boolean),
    [data.regions, data.rmUsers],
  );

  // Выбор: только существующие и видимые строки.
  useEffect(() => {
    const visible = new Set(rows.map((r) => r.id));
    setSelected((s) => (([...s].every((id) => visible.has(id)) ? s : new Set([...s].filter((id) => visible.has(id))))));
  }, [rows]);
  const select = (ids: string[], on: boolean) =>
    setSelected((s) => {
      const next = new Set(s);
      for (const id of ids) {
        if (on) next.add(id);
        else next.delete(id);
      }
      return next;
    });
  const chosen = recipients.filter((r) => selected.has(r.id));

  useEffect(() => {
    if (addSignal > 0 && canAdd) openNew();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [addSignal]);

  const say = (text: string, tone: "ok" | "bad" = "ok", undo?: () => void) => setToast({ id: Date.now(), text, tone, undo });
  const replace = (saved: ReportRecipient[]) => setData((d) => ({ ...d, recipients: d.recipients.map((r) => saved.find((s) => s.id === r.id) ?? r) }));

  /** Массовое сохранение: одно на всех; «Отменить» возвращает прежние настройки так же одним сохранением. */
  const bulk = async (items: { id: string; input: ReportRecipientInput }[], message: string) => {
    if (items.length === 0) return;
    const before = items.map((x) => ({ id: x.id, input: inputOf(data.recipients.find((r) => r.id === x.id)!) }));
    setBulkBusy(true);
    try {
      replace(await reportsApi.saveRecipients(items));
      say(message, "ok", async () => {
        try {
          replace(await reportsApi.saveRecipients(before));
          say(t("Изменения отменены."));
        } catch (e) {
          say((e as Error).message, "bad");
        }
      });
    } catch (e) {
      say((e as Error).message, "bad");
    } finally {
      setBulkBusy(false);
    }
  };

  const forChosen = (change: (input: ReportRecipientInput) => ReportRecipientInput) =>
    chosen.map((r) => ({ id: r.id, input: change(inputOf(r)) })).filter((x) => JSON.stringify(x.input) !== JSON.stringify(inputOf(data.recipients.find((r) => r.id === x.id)!)));
  const word = (n: number) => t(plural(n, ["получателя", "получателей", "получателей"]));
  const handlers: BulkHandlers = {
    report: (codes, title) => {
      const items = forChosen((i) => addReports(i, codes, data.jobs));
      if (items.length === 0) return say(t("У выбранных этот отчёт уже включён."));
      void bulk(items, t("Включён отчёт «{report}» для {n} {word}", { report: title, n: items.length, word: word(items.length) }));
    },
    times: (times) => {
      const items = forChosen((i) => setTimes(i, times, data.jobs));
      if (items.length === 0) return say(t("Ничего не изменилось."));
      void bulk(items, t("Время отправки изменено у {n} {word}", { n: items.length, word: word(items.length) }));
    },
    chats: (ids) => {
      const items = forChosen((i) => addChats(i, ids));
      if (items.length === 0) return say(t("Эти чаты у выбранных уже есть."));
      void bulk(items, t("Чат привязан к {n} {word}", { n: items.length, word: plural(items.length, [t("получателю"), t("получателям"), t("получателям")]) }));
    },
    disable: () => setDisabling(chosen.filter((r) => r.enabled)),
  };

  /** Переключатель строки: включение — сразу (с откатом при ошибке), выключение — с вопросом о причине и сроке. */
  const toggle = async (r: ReportRecipient, on: boolean) => {
    if (!on) return setDisabling([r]);
    await saveOne(r, enable(inputOf(r)), t("«{name}» включён", { name: nameOf(r) }));
  };
  const saveOne = async (r: ReportRecipient, input: ReportRecipientInput, message: string) => {
    setBusy((s) => new Set(s).add(r.id));
    const before = r;
    setData((d) => ({
      ...d,
      recipients: d.recipients.map((x) => (x.id === r.id ? { ...x, enabled: input.enabled, disabledReason: input.disabledReason ?? null, disabledUntil: input.disabledUntil ?? null } : x)),
    }));
    try {
      const saved = await reportsApi.saveRecipient(r.id, input);
      replace([saved]);
      say(message, "ok", async () => {
        try {
          replace([await reportsApi.saveRecipient(r.id, inputOf(before))]);
        } catch (e) {
          say((e as Error).message, "bad");
        }
      });
    } catch (e) {
      replace([before]);
      say((e as Error).message, "bad");
    } finally {
      setBusy((s) => {
        const next = new Set(s);
        next.delete(r.id);
        return next;
      });
    }
  };
  const confirmDisable = (reason: string | null, until: string | null) => {
    const list = disabling ?? [];
    setDisabling(null);
    if (list.length === 1) {
      void saveOne(list[0], disable(inputOf(list[0]), reason, until), t("«{name}» отключён", { name: nameOf(list[0]) }));
    } else if (list.length > 1) {
      void bulk(
        list.map((r) => ({ id: r.id, input: disable(inputOf(r), reason, until) })),
        t("Отключено {n} {word}", { n: list.length, word: word(list.length) }),
      );
    }
  };

  const openNew = () => {
    const scope = defaultScope(data, "sv");
    setPanel({ id: null, input: { ...scope, title: null, chatIds: [], reports: templateCodes("role", data.jobs, "sv"), enabled: true } });
  };
  const action = async (r: ReportRecipient, kind: RowAction) => {
    if (kind === "edit" || kind === "test" || kind === "chats") return setPanel({ id: r.id, input: inputOf(r), focus: kind === "edit" ? undefined : kind });
    if (kind === "duplicate") return setPanel({ id: null, input: { ...inputOf(r), title: null, chatIds: [], agentId: r.role === "tp" ? null : r.agentId, enabled: true, disabledReason: null, disabledUntil: null } });
    if (kind === "send") return setSending(r);
    if (!window.confirm(t("Удалить получателя «{name}»? История отправок сохранится.", { name: nameOf(r) }))) return;
    try {
      await reportsApi.deleteRecipient(r.id);
      setData((d) => ({ ...d, recipients: d.recipients.filter((x) => x.id !== r.id) }));
      if (panel?.id === r.id) setPanel(null);
      say(t("«{name}» удалён", { name: nameOf(r) }));
    } catch (e) {
      say((e as Error).message, "bad");
    }
  };

  const layout: PanelLayout = !desktop ? "screen" : width >= INLINE_PANEL ? "inline" : "drawer";
  const inline = !!panel && layout === "inline";
  const filtered = rows.length === 0 && data.recipients.length > 0;
  const empty = data.recipients.length === 0;

  const emptyView = empty ? (
    <div className="rounded-[16px] border border-dashed border-line-strong bg-surface px-6 py-10 text-center">
      <Users aria-hidden className="mx-auto size-8 text-ink-4" />
      <p className="mx-auto mt-3 max-w-[420px] text-[14px] text-ink-2">{t("Добавьте первого получателя — отчёты начнут уходить по расписанию")}</p>
      <button type="button" disabled={!canAdd} onClick={openNew} className="mx-auto mt-4 flex h-10 items-center gap-2 rounded-[10px] bg-accent px-4 text-[13px] font-semibold text-white disabled:opacity-40">
        <UserPlus aria-hidden className="size-4" />
        {t("Добавить получателя")}
      </button>
    </div>
  ) : filtered ? (
    <div className="rounded-[16px] border border-line bg-surface px-6 py-10 text-center">
      <Users aria-hidden className="mx-auto size-8 text-ink-4" />
      <p className="mt-3 text-[14px] font-semibold text-ink">{t("Никого не нашли по фильтрам")}</p>
      <button type="button" onClick={() => setF({ ...NO_FILTERS, sort: f.sort, flat: f.flat })} className="mt-3 text-[13px] font-semibold text-accent-strong hover:underline">
        {t("Сбросить фильтры")}
      </button>
    </div>
  ) : null;

  const panelView = panel ? (
    <RecipientPanel
      key={`${panel.id ?? "new"}:${panel.focus ?? ""}`}
      data={data}
      state={panel}
      layout={layout}
      onClose={() => setPanel(null)}
      onMessage={(text, tone) => say(text, tone)}
      onSaved={(saved, created) => {
        setPanel(null);
        setData((d) => ({ ...d, recipients: created ? [...d.recipients, saved] : d.recipients.map((r) => (r.id === saved.id ? saved : r)) }));
        say(created ? t("Получатель «{name}» добавлен", { name: displayName(saved, data.regions) }) : t("«{name}» сохранён", { name: displayName(saved, data.regions) }));
        void reload();
      }}
    />
  ) : null;

  const issue = (i: Issue) => set({ ...NO_FILTERS, sort: f.sort, flat: f.flat, issues: [i] });
  const bottom = (
    <div className={`grid grid-cols-1 gap-4 ${inline ? "" : "xl:grid-cols-2"}`}>
      <NearSendsCard items={near} onSchedule={onSchedule} />
      <AttentionCard
        a={watch}
        onIssue={issue}
        onEnable={(ids) =>
          void bulk(
            recipients.filter((r) => ids.includes(r.id)).map((r) => ({ id: r.id, input: enable(inputOf(r)) })),
            t("Включено {n} {word}", { n: ids.length, word: word(ids.length) }),
          )
        }
      />
    </div>
  );

  return (
    <div ref={wrap}>
      {!data.settings.accountId && (
        <p className="mb-3 rounded-[12px] bg-warn-soft px-3 py-2 text-[13px] text-warn">{t("Сначала выберите аккаунт Telegram — «Изменить» в строке «Отправка».")}</p>
      )}
      {desktop ? (
        <>
          <FilterBar f={f} set={set} facets={facets} regions={data.regions} jobs={data.jobs} total={data.recipients.length} regionsShown={regionsShown} onAdd={openNew} canAdd={canAdd} />
          <div className={`mt-4 grid items-start gap-4 ${inline ? (width >= 1240 ? "grid-cols-[minmax(0,1fr)_440px]" : "grid-cols-[minmax(0,1fr)_400px]") : ""}`}>
            <div className="min-w-0 space-y-4">
              {emptyView ?? (
                <RecipientsTable
                  groups={groups}
                  jobs={data.jobs}
                  regionNamesOf={regionNamesOf}
                  selected={selected}
                  onSelect={select}
                  onOpen={(r) => setPanel({ id: r.id, input: inputOf(r) })}
                  onToggle={toggle}
                  onAction={action}
                  busy={busy}
                  editingId={panel?.id ?? null}
                  flat={f.flat}
                  onFlat={(flat) => set({ flat })}
                  bulkBar={
                    selected.size > 0 ? (
                      <BulkBar
                        count={selected.size}
                        total={rows.length}
                        inputs={chosen.map(inputOf)}
                        jobs={data.jobs}
                        chats={data.chats}
                        busy={bulkBusy}
                        onAll={(on) => select(rows.map((r) => r.id), on)}
                        onClear={() => setSelected(new Set())}
                        handlers={handlers}
                      />
                    ) : null
                  }
                />
              )}
              {bottom}
            </div>
            {layout === "inline" && panelView}
          </div>
          {layout === "drawer" && panelView}
        </>
      ) : (
        <>
          <MobileFilters f={f} set={set} facets={facets} regions={data.regions} jobs={data.jobs} recipients={recipients} ctx={ctx} />
          <div className="mt-4 space-y-4">
            {emptyView ?? (
              <RecipientCards
                groups={groups}
                jobs={data.jobs}
                regionNamesOf={regionNamesOf}
                selected={selected}
                onSelect={select}
                onOpen={(r) => setPanel({ id: r.id, input: inputOf(r) })}
                onToggle={toggle}
                onAction={action}
                busy={busy}
              />
            )}
            {bottom}
          </div>
          {selected.size > 0 && (
            <div className="fixed inset-x-3 bottom-[calc(76px+env(safe-area-inset-bottom))] z-40 rounded-[16px] border border-line bg-surface-solid p-2 shadow-[0_12px_40px_rgb(0_0_0/0.45)]">
              <div className="mb-1 flex items-center gap-2 px-2">
                <span className="flex-1 text-[13px] font-bold text-accent-strong">{t("Выбрано {n}", { n: selected.size })}</span>
                <button type="button" onClick={() => select(rows.map((r) => r.id), true)} className="text-[12px] font-semibold text-ink-3">
                  {t("Все {n}", { n: rows.length })}
                </button>
                <button type="button" onClick={() => setSelected(new Set())} aria-label={t("Снять выбор")} className="grid size-7 place-items-center rounded-md text-ink-3">
                  <X className="size-4" />
                </button>
              </div>
              <div className="flex">
                <BulkMenus inputs={chosen.map(inputOf)} jobs={data.jobs} chats={data.chats} busy={bulkBusy} handlers={handlers} compact />
              </div>
            </div>
          )}
          {panelView}
        </>
      )}
      <DisableDialog open={!!disabling && disabling.length > 0} names={(disabling ?? []).map(nameOf)} onClose={() => setDisabling(null)} onConfirm={confirmDisable} />
      <SendNowSheet recipient={sending} data={data} onClose={() => setSending(null)} onDone={(text, tone) => (say(text, tone), void reload())} />
      <Toast toast={toast} onClose={() => setToast(null)} />
    </div>
  );
}

/** «Отправить сейчас»: какой запуск — и отправка во все чаты получателя (реальные данные); уже отправлен сегодня — с подтверждением. */
function SendNowSheet({
  recipient,
  data,
  onClose,
  onDone,
}: {
  recipient: ReportRecipient | null;
  data: ReportOverview;
  onClose: () => void;
  onDone: (text: string, tone: "ok" | "bad") => void;
}) {
  const t = useT();
  const [busy, setBusy] = useState<string | null>(null);
  const jobs = recipient ? data.jobs.filter((j) => j.roles.includes(recipient.role) && j.report !== "weekly-pdf") : [];
  const send = async (code: string) => {
    if (!recipient) return;
    setBusy(code);
    try {
      let result = await reportsApi.send(recipient.id, code, false);
      if ("conflict" in result) {
        if (!window.confirm(result.conflict)) return;
        result = await reportsApi.send(recipient.id, code, true);
        if ("conflict" in result) throw new Error(result.conflict);
      }

      const job = data.jobs.find((j) => j.code === code)!;
      const sent = result.filter((x) => x.status === "sent").length;
      const lines = result.filter((x) => x.status !== "sent").map((x) => `${x.recipientTitle}: ${t(deliveryStatus[x.status].label)}${x.error ? ` — ${t(x.error)}` : ""}`);
      onDone([t("«{report}»: отправлено в {sent} из {total} чатов.", { report: `${t(job.reportTitle)} · ${job.time}`, sent, total: result.length }), ...lines].join(" "), sent > 0 ? "ok" : "bad");
      onClose();
    } catch (e) {
      onDone((e as Error).message, "bad");
    } finally {
      setBusy(null);
    }
  };
  return (
    <Sheet open={!!recipient} onClose={onClose} title={t("Отправить сейчас")} subtitle={recipient ? `${nameOf(recipient)} · ${t("реальные данные, во все чаты")}` : undefined}>
      <ul className="space-y-1">
        {jobs.map((j) => (
          <li key={j.code}>
            <button
              type="button"
              disabled={!!busy}
              onClick={() => send(j.code)}
              className={`flex w-full items-center gap-3 rounded-[10px] px-3 py-2.5 text-left text-[13px] hover:bg-muted disabled:opacity-50 ${recipient?.reports.includes(j.code) ? "text-ink" : "text-ink-3"}`}
            >
              <span className="w-11 shrink-0 font-semibold tabular-nums">{j.time}</span>
              <span className="min-w-0 flex-1">
                <span className="block truncate">{t(j.reportTitle)}</span>
                <span className="block truncate text-[11px] text-ink-4">
                  {t(j.slotTitle)}
                  {recipient?.reports.includes(j.code) ? "" : ` · ${t("не в его расписании")}`}
                </span>
              </span>
              {busy === j.code ? <Loader2 aria-hidden className="size-4 animate-spin" /> : <Send aria-hidden className="size-4 text-ink-4" />}
            </button>
          </li>
        ))}
      </ul>
    </Sheet>
  );
}
