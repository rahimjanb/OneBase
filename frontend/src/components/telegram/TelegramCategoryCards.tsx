"use client";

import { useEffect, useState } from "react";
import { CalendarClock, Eye, Loader2, Pencil, Search, Send, Shapes, Users } from "lucide-react";
import { button, field, primary } from "@/components/ai/form";
import { Note } from "@/components/sales/bits";
import { useT } from "@/i18n/client";
import { chatTypeLabel } from "@/lib/telegram";
import {
  allProducts,
  cardJobs,
  cardRecipients,
  categoryLabel,
  choiceText,
  effectiveCategories,
  effectiveProducts,
  keepProducts,
  pickedProducts,
  sameChoice,
  sameProducts,
  toggleCategory,
  toggleProduct,
  type CategoryCardOption,
  type CategoryCardsView,
  type ProductChoice,
} from "@/lib/telegram-category-cards";
import {
  categoryCardsApi,
  deliveryStatus,
  reportLanguages,
  reportsApi,
  roleOf,
  weekDays,
  type ReportJob,
  type ReportLanguage,
  type ReportOverview,
  type ReportPreview,
  type ReportPreviewInput,
  type ReportSendResult,
} from "@/lib/telegramReports";
import type { ReportsTab } from "./TelegramReportsOverview";
import { RoleChip, ScopePicker, defaultScope, when, type Scope } from "./TelegramReportsPreview";
import { Toggle } from "./recipients/bits";

function Card({ title, icon: Icon, action, children }: { title: string; icon: typeof Shapes; action?: React.ReactNode; children: React.ReactNode }) {
  const t = useT();
  return (
    <section className="rounded-xl border border-line bg-surface">
      <div className="flex items-center justify-between gap-3 border-b border-line px-4 py-3">
        <h3 className="flex items-center gap-2 font-semibold text-ink">
          <Icon aria-hidden className="size-4 text-ink-3" />
          {t(title)}
        </h3>
        {action}
      </div>
      <div className="p-4">{children}</div>
    </section>
  );
}

/**
 * Чипы категорий: выбранные — акцентом, порядок — как в «Настройках продаж». onToggle — какую нажали: родитель пересчитывает выбор от
 * предыдущего значения (toggleCategory в функции обновления), чтобы быстрые клики подряд не теряли друг друга.
 */
function CategoryChips({ options, selected, max, onToggle, disabled }: { options: CategoryCardOption[]; selected: string[]; max: number; onToggle: (name: string) => void; disabled?: boolean }) {
  const t = useT();
  return (
    <div role="group" aria-label={t("Категории")} className="flex flex-wrap gap-2">
      {options.map((c) => {
        const on = selected.includes(c.name);
        const full = !on && selected.length >= max;
        return (
          <button
            key={c.name}
            type="button"
            aria-pressed={on}
            disabled={disabled || full}
            onClick={() => onToggle(c.name)}
            className={`inline-flex h-8 items-center gap-1.5 rounded-full border px-3 text-[13px] font-medium transition-colors disabled:opacity-50 ${
              on ? "border-accent/40 bg-accent-soft text-accent-strong" : "border-line bg-surface text-ink-2 hover:border-accent/30"
            }`}
          >
            {c.name}
            {c.alias && <span className={on ? "text-accent-strong/70" : "text-ink-4"}>· {c.alias}</span>}
          </button>
        );
      })}
    </div>
  );
}

/**
 * Товары выбранных категорий: у каждой — «все товары (N)» или «выбрано k из N», раскрытая — флажки товаров (код и название) с поиском.
 * Отмечены все — снова вся категория (и новые товары в ней тоже попадут); последний отмеченный товар снять нельзя. onChange — функция
 * обновления от предыдущего выбора, как у категорий.
 */
function ProductChoices({
  options,
  names,
  products,
  onChange,
  disabled,
}: {
  options: CategoryCardOption[];
  names: string[];
  products: ProductChoice;
  onChange: (update: (prev: ProductChoice) => ProductChoice) => void;
  disabled?: boolean;
}) {
  const t = useT();
  const [open, setOpen] = useState<string | null>(null);
  const [search, setSearch] = useState("");
  const chosen = names.map((n) => options.find((o) => o.name === n)).filter((o): o is CategoryCardOption => !!o && (o.products?.length ?? 0) > 0);
  if (chosen.length === 0) return null;

  return (
    <div className="mt-3">
      <span className="mb-1.5 block text-xs font-medium text-ink-2">{t("Товары в карточках")}</span>
      <ul className="flex flex-col divide-y divide-line rounded-lg border border-line">
        {chosen.map((o) => {
          const list = o.products ?? [];
          const own = products[o.name];
          const expanded = open === o.name;
          const picked = pickedProducts(products, o);
          const q = search.trim().toLowerCase();
          const visible = expanded ? list.filter((p) => !q || p.name.toLowerCase().includes(q) || (p.code ?? "").toLowerCase().includes(q)) : [];
          return (
            <li key={o.name} className="px-3 py-2">
              <div className="flex flex-wrap items-center gap-x-3 gap-y-1">
                <span className="min-w-0 flex-1 text-sm">
                  <span className="font-medium text-ink">{o.name}</span>
                  <span className={`ml-2 ${own ? "font-medium text-accent-strong" : "text-ink-3"}`}>
                    {own ? t("выбрано {n} из {total}", { n: own.length, total: list.length }) : t("все товары ({total})", { total: list.length })}
                  </span>
                </span>
                {own && (
                  <button type="button" className="text-[13px] font-semibold text-ink-2 hover:underline" onClick={() => onChange((prev) => allProducts(prev, o.name))} disabled={disabled}>
                    {t("Все товары")}
                  </button>
                )}
                <button
                  type="button"
                  aria-expanded={expanded}
                  className="text-[13px] font-semibold text-accent-strong hover:underline"
                  onClick={() => {
                    setOpen(expanded ? null : o.name);
                    setSearch("");
                  }}
                  disabled={disabled}
                >
                  {expanded ? t("Скрыть") : t("Выбрать товары")}
                </button>
              </div>
              {expanded && (
                <div className="mt-2">
                  {list.length > 8 && (
                    <div className="relative mb-2">
                      <Search aria-hidden className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-ink-3" />
                      <input className={`${field} pl-9`} value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t("Название или код товара")} />
                    </div>
                  )}
                  <ul className="max-h-64 overflow-y-auto" aria-label={t("Товары: {name}", { name: o.name })}>
                    {visible.map((p) => {
                      const on = picked.includes(p.id);
                      return (
                        <li key={p.id}>
                          <label className="flex cursor-pointer items-start gap-2.5 rounded-md px-2 py-1.5 hover:bg-muted">
                            <input
                              type="checkbox"
                              checked={on}
                              disabled={disabled || (on && picked.length === 1)}
                              onChange={() => onChange((prev) => toggleProduct(prev, o, p.id))}
                              className="mt-0.5 size-4 shrink-0 accent-[var(--color-accent)]"
                            />
                            <span className="min-w-0 flex-1 text-sm text-ink">
                              {/* Код — если его нет в начале названия (в Linko обычно «603 «Twizos» …»). */}
                              {p.code && !p.name.startsWith(p.code) && <span className="mr-1.5 tabular-nums text-ink-3">{p.code}</span>}
                              {p.name}
                            </span>
                          </label>
                        </li>
                      );
                    })}
                    {visible.length === 0 && <li className="px-2 py-1.5 text-sm text-ink-3">{t("Ничего не найдено")}</li>}
                  </ul>
                  <p className="mt-1.5 text-xs text-ink-3">{t("Цифры карточки — только по отмеченным товарам; план дня — только у всей категории.")}</p>
                </div>
              )}
            </li>
          );
        })}
      </ul>
    </div>
  );
}

const daysText = (days: number[], t: (s: string) => string) =>
  days.length === 7 ? t("каждый день") : days.length === 6 && !days.includes(7) ? t("пн–сб") : days.map((d) => t(weekDays.find((w) => w.day === d)?.label ?? "")).join(", ");

/**
 * «Карточки категорий» (отчёт 11 «Итоги дня по категориям», только день итога): какие категории уходят по умолчанию, расписание запусков
 * (9:00 — итог прошлого рабочего дня, 19:00 — итог дня), кто подписан и его категории, предпросмотр картинок и тест в выбранный чат.
 * Подписка получателя — на вкладке «Получатели» (отчёт 11); данные и права проверяет сервер. Прежний отчёт 10 с месяцем категории убран.
 */
export function CategoryCardsPanel({ data, onTab, onJob }: { data: ReportOverview; onTab: (tab: ReportsTab) => void; onJob: (job: ReportJob) => void }) {
  const t = useT();
  const [view, setView] = useState<CategoryCardsView | null>(null);
  const [draft, setDraft] = useState<string[]>([]);
  const [draftProducts, setDraftProducts] = useState<ProductChoice>({});
  const [editing, setEditing] = useState<{ id: string; own: boolean; names: string[]; products: ProductChoice } | null>(null);
  const [busy, setBusy] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const jobs = cardJobs(data.jobs);
  const subscribed = cardRecipients(data.recipients);

  useEffect(() => {
    let alive = true;
    categoryCardsApi
      .get()
      .then((v) => {
        if (!alive) return;
        setView(v);
        setDraft(v.defaults);
        setDraftProducts(v.defaultProducts ?? {});
      })
      .catch((e: Error) => alive && setError(e.message));
    return () => {
      alive = false;
    };
  }, []);

  const run = async (key: string, action: () => Promise<void>) => {
    setBusy(key);
    setError(null);
    try {
      await action();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(null);
    }
  };

  const saveDefaults = () =>
    run("defaults", async () => {
      const next = await categoryCardsApi.saveDefaults(draft, keepProducts(draftProducts, draft));
      setView(next);
      setDraft(next.defaults);
      setDraftProducts(next.defaultProducts ?? {});
    });

  const saveRecipient = () =>
    editing &&
    run(`r:${editing.id}`, async () => {
      setView(await categoryCardsApi.saveRecipient(editing.id, editing.own ? editing.names : null, editing.own ? keepProducts(editing.products, editing.names) : null));
      setEditing(null);
    });

  const toggleJob = (job: ReportJob, enabled: boolean) =>
    run(`j:${job.code}`, async () =>
      onJob(await reportsApi.saveJob(job.code, { enabled, time: job.time, timeZone: job.timeZone, days: job.days, catchUpMinutes: job.catchUpMinutes })),
    );

  if (!view) {
    return error ? (
      <p className="rounded-lg bg-bad-soft px-3 py-2 text-sm text-bad">{error}</p>
    ) : (
      <div className="grid min-h-[240px] place-items-center text-sm text-ink-3" aria-busy>
        <Loader2 aria-hidden className="size-5 animate-spin" />
      </div>
    );
  }

  const total = view.categories.length;
  const ownOf = (id: string) => view.recipients.find((r) => r.id === id) ?? null;
  const defaultProducts = view.defaultProducts ?? {};
  const changed = !sameChoice(draft, view.defaults) || !sameProducts(keepProducts(draftProducts, draft), defaultProducts);
  const label = (n: number) => t("товаров: {n}", { n });

  return (
    <div className="grid gap-6 xl:grid-cols-[minmax(0,460px)_minmax(0,1fr)]">
      <div className="flex min-w-0 flex-col gap-4">
        <p className="text-sm text-ink-2">
          {t(
            "Отдельная картинка на каждую выбранную категорию — только за один день, без месяца, в области получателя: кг, сумма, точки и заказы, артикулы, цена за кг, план дня и выполнение, регионы (РМ) или ТП (СВ) за день, артикулы дня с фото. Все картинки — одним альбомом.",
          )}
        </p>
        <p className="text-sm text-ink-2">
          {t("В выбранной категории можно оставить все товары или отметить только нужные (кнопка «Выбрать товары») — тогда все цифры карточки будут только по ним.")}
        </p>
        {error && <p className="rounded-lg bg-bad-soft px-3 py-2 text-sm text-bad">{error}</p>}

        <Card title="Категории по умолчанию" icon={Shapes}>
          <CategoryChips
            options={view.categories}
            selected={draft}
            max={view.max}
            onToggle={(name) => setDraft((d) => toggleCategory(d, name, view.categories, view.max))}
            disabled={busy !== null}
          />
          <ProductChoices options={view.categories} names={draft} products={draftProducts} onChange={setDraftProducts} disabled={busy !== null} />
          <p className="mt-3 text-xs text-ink-3">
            {draft.length === 0
              ? t("Ничего не выбрано — уйдут все {n} категорий.", { n: Math.min(total, view.max) })
              : t("Уйдут: {list}. Получателям без своего выбора.", { list: choiceText(draft, -1, keepProducts(draftProducts, draft), label) })}
          </p>
          <div className="mt-3 flex flex-wrap gap-2">
            <button className={primary} onClick={saveDefaults} disabled={busy !== null || !changed}>
              {busy === "defaults" && <Loader2 aria-hidden className="size-4 animate-spin" />}
              {t("Сохранить")}
            </button>
            {changed && (
              <button
                className={button}
                onClick={() => {
                  setDraft(view.defaults);
                  setDraftProducts(defaultProducts);
                }}
                disabled={busy !== null}
              >
                {t("Отменить")}
              </button>
            )}
          </div>
        </Card>

        <Card
          title="Когда уходит"
          icon={CalendarClock}
          action={
            <button className="text-[13px] font-semibold text-accent-strong hover:underline" onClick={() => onTab("schedule")}>
              {t("Изменить время")}
            </button>
          }
        >
          <ul className="flex flex-col gap-3">
            {jobs.map((job, i) => (
              <li key={job.code} className="flex flex-col gap-2">
                {jobs[i - 1]?.report !== job.report && (
                  <span className={`text-xs font-semibold uppercase tracking-wide text-ink-3 ${i > 0 ? "mt-2" : ""}`}>{t(job.reportTitle)}</span>
                )}
                <span className="flex items-center gap-3">
                  <Toggle
                    checked={job.enabled}
                    disabled={busy !== null}
                    onChange={(v) => toggleJob(job, v)}
                    label={t(job.enabled ? "Выключить «{name}»" : "Включить «{name}»", { name: `${t(job.reportTitle)} — ${t(job.slotTitle)}` })}
                  />
                  <span className="min-w-0 flex-1">
                    <span className="block text-sm font-medium text-ink">
                      {job.time} — {t(job.slotTitle)}
                    </span>
                    <span className="block text-xs text-ink-3">
                      {daysText(job.days, t)} · {t("получателей: {n}", { n: job.recipients })}
                      {job.enabled && job.nextRunAt ? ` · ${t("следующая {at}", { at: when(job.nextRunAt) })}` : ""}
                    </span>
                  </span>
                </span>
              </li>
            ))}
          </ul>
          {!data.settings.accountId && <p className="mt-3 rounded-lg bg-warn-soft px-3 py-2 text-xs text-warn">{t("Аккаунт Telegram для автоотчётов не выбран — отправки не уйдут.")}</p>}
        </Card>

        <Card
          title="Кому уходит"
          icon={Users}
          action={
            <button className="text-[13px] font-semibold text-accent-strong hover:underline" onClick={() => onTab("recipients")}>
              {t("Получатели")}
            </button>
          }
        >
          {subscribed.length === 0 ? (
            <p className="text-sm text-ink-3">
              {t("Пока никто не подписан. На вкладке «Получатели» откройте получателя (или добавьте нового) и включите отчёт 11 «Итоги дня по категориям».")}
            </p>
          ) : (
            <ul className="flex flex-col divide-y divide-line">
              {subscribed.map((r) => {
                const mine = ownOf(r.id);
                const own = mine?.categories ?? null;
                const open = editing?.id === r.id;
                return (
                  <li key={r.id} className="py-3 first:pt-0 last:pb-0">
                    <div className="flex items-start gap-2">
                      <RoleChip role={r.role} />
                      <span className="min-w-0 flex-1">
                        <span className="block truncate text-sm font-medium text-ink">{r.name}</span>
                        <span className="block text-xs text-ink-3">
                          {own
                            ? t(choiceText(own, total, mine?.products ?? {}, label))
                            : `${t("по умолчанию")}: ${t(choiceText(effectiveCategories(null, view), total, effectiveProducts(null, null, view), label))}`}
                          {!r.enabled && ` · ${t("выключен")}`}
                        </span>
                      </span>
                      {!open && (
                        <button
                          className="inline-flex items-center gap-1 text-[13px] font-semibold text-accent-strong hover:underline"
                          onClick={() =>
                            setEditing({
                              id: r.id,
                              own: own !== null,
                              names: own ?? effectiveCategories(null, view),
                              products: effectiveProducts(own, mine?.products, view),
                            })
                          }
                          aria-label={t("Категории: {name}", { name: r.name })}
                        >
                          <Pencil aria-hidden className="size-3.5" />
                          {t("Категории")}
                        </button>
                      )}
                    </div>
                    {open && editing && (
                      <div className="mt-3 rounded-lg bg-muted p-3">
                        <label className="flex items-center gap-2 text-sm text-ink">
                          <Toggle checked={editing.own} onChange={(v) => setEditing((e) => (e ? { ...e, own: v } : e))} label={t("Свой выбор")} tone="accent" size="sm" />
                          {t("Свой выбор категорий")}
                        </label>
                        {editing.own && (
                          <div className="mt-3">
                            <CategoryChips
                              options={view.categories}
                              selected={editing.names}
                              max={view.max}
                              onToggle={(name) => setEditing((e) => (e ? { ...e, names: toggleCategory(e.names, name, view.categories, view.max) } : e))}
                            />
                            <ProductChoices
                              options={view.categories}
                              names={editing.names}
                              products={editing.products}
                              onChange={(update) => setEditing((e) => (e ? { ...e, products: update(e.products) } : e))}
                            />
                          </div>
                        )}
                        <div className="mt-3 flex flex-wrap gap-2">
                          <button className={primary} onClick={saveRecipient} disabled={busy !== null || (editing.own && editing.names.length === 0)}>
                            {busy === `r:${r.id}` && <Loader2 aria-hidden className="size-4 animate-spin" />}
                            {t("Сохранить")}
                          </button>
                          <button className={button} onClick={() => setEditing(null)} disabled={busy !== null}>
                            {t("Отменить")}
                          </button>
                        </div>
                      </div>
                    )}
                  </li>
                );
              })}
            </ul>
          )}
        </Card>
      </div>

      <PreviewCard data={data} view={view} jobs={jobs} />
    </div>
  );
}

/** Предпросмотр и тест: категории, получатель или роль и область, запуск, язык; картинки — по одной на категорию. */
function PreviewCard({ data, view, jobs }: { data: ReportOverview; view: CategoryCardsView; jobs: ReportJob[] }) {
  const t = useT();
  const [categories, setCategories] = useState<string[]>(() => effectiveCategories(null, view).slice(0, 3));
  const [products, setProducts] = useState<ProductChoice>(() => effectiveProducts(null, null, view));
  const [job, setJob] = useState(jobs.at(-1)?.code ?? "category-day.evening");
  const [recipientId, setRecipientId] = useState("");
  const [scope, setScope] = useState<Scope>(() => defaultScope(data, "rm"));
  const [language, setLanguage] = useState<ReportLanguage>(data.settings.language);
  const [chatId, setChatId] = useState("");
  const [preview, setPreview] = useState<ReportPreview | null>(null);
  const [result, setResult] = useState<ReportSendResult | null>(null);
  const [busy, setBusy] = useState<"preview" | "test" | null>(null);
  const [error, setError] = useState<string | null>(null);
  const recipient = data.recipients.find((r) => r.id === recipientId);

  const input = (): ReportPreviewInput => ({
    job,
    role: recipient ? null : scope.role,
    userId: recipient || scope.role !== "rm" ? null : scope.userId,
    directionId: recipient ? null : scope.directionId,
    regionId: recipient ? null : scope.regionId,
    supervisorId: recipient ? null : scope.supervisorId,
    agentId: recipient ? null : scope.agentId,
    recipientId: recipient?.id ?? null,
    date: null,
    time: null,
    language,
    categories,
    products: keepProducts(products, categories),
  });

  const act = async (kind: "preview" | "test") => {
    if (kind === "test") {
      const chat = data.chats.find((c) => c.id === chatId);
      if (!window.confirm(t("Отправить тестовые карточки на реальных данных в «{chat}»? Получателям они не уйдут.", { chat: chat?.title ?? "" }))) return;
    }

    setBusy(kind);
    setError(null);
    setResult(null);
    try {
      if (kind === "preview") {
        setPreview(await reportsApi.preview(input()));
      } else {
        const sent = await reportsApi.test(input(), chatId);
        if ("conflict" in sent) throw new Error(sent.conflict);
        setResult(sent);
      }
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(null);
    }
  };

  return (
    <Card title="Предпросмотр и тест" icon={Eye}>
      <div className="grid gap-4 2xl:grid-cols-[minmax(0,380px)_minmax(0,1fr)]">
        <div className="min-w-0">
          <span className="mb-1.5 block text-sm font-medium text-ink">{t("Категории")}</span>
          <CategoryChips
            options={view.categories}
            selected={categories}
            max={view.max}
            onToggle={(name) => setCategories((d) => toggleCategory(d, name, view.categories, view.max))}
            disabled={busy !== null}
          />
          <ProductChoices options={view.categories} names={categories} products={products} onChange={setProducts} disabled={busy !== null} />

          <label className="mt-4 block">
            <span className="mb-1.5 block text-sm font-medium text-ink">{t("Запуск")}</span>
            <select className={field} value={job} onChange={(e) => setJob(e.target.value)}>
              {jobs.map((j) => (
                <option key={j.code} value={j.code}>
                  {t(j.reportTitle)} · {j.time} — {t(j.slotTitle)}
                </option>
              ))}
            </select>
          </label>

          <label className="mt-4 block">
            <span className="mb-1.5 block text-sm font-medium text-ink">{t("Для кого")}</span>
            <select className={field} value={recipientId} onChange={(e) => setRecipientId(e.target.value)}>
              <option value="">{t("Выбрать роль и область")}</option>
              {data.recipients.map((r) => (
                <option key={r.id} value={r.id}>
                  {t(roleOf(r.role).label)} · {r.name}
                </option>
              ))}
            </select>
          </label>
          {!recipient && (
            <div className="mt-3">
              <ScopePicker data={data} scope={scope} onChange={setScope} />
            </div>
          )}

          <label className="mt-3 block">
            <span className="mb-1 block text-xs font-medium text-ink-2">{t("Язык")}</span>
            <select className={field} value={language} onChange={(e) => setLanguage(e.target.value as ReportLanguage)}>
              {reportLanguages.map((l) => (
                <option key={l.code} value={l.code}>
                  {t(l.label)}
                </option>
              ))}
            </select>
          </label>

          <button className={`${primary} mt-4`} onClick={() => act("preview")} disabled={busy !== null || !data.canUseSales || categories.length === 0}>
            {busy === "preview" ? <Loader2 aria-hidden className="size-4 animate-spin" /> : <Eye aria-hidden className="size-4" />}
            {t("Предпросмотр")}
          </button>

          <div className="mt-6 border-t border-line pt-4">
            <span className="mb-1.5 block text-sm font-medium text-ink">{t("Тестовая отправка")}</span>
            <select className={field} value={chatId} onChange={(e) => setChatId(e.target.value)} disabled={!data.settings.accountId}>
              <option value="">{data.settings.accountId ? t("Выберите чат или группу") : t("Сначала выберите аккаунт Telegram")}</option>
              {data.chats.map((c) => (
                <option key={c.id} value={c.id} disabled={!c.canSend}>
                  {c.title} · {t(chatTypeLabel[c.type])}
                  {c.linkedUser ? ` · ${c.linkedUser}` : ""}
                </option>
              ))}
            </select>
            <button className={`${button} mt-3`} onClick={() => act("test")} disabled={busy !== null || !chatId || !data.canUseSales || categories.length === 0}>
              {busy === "test" ? <Loader2 aria-hidden className="size-4 animate-spin" /> : <Send aria-hidden className="size-4" />}
              {t("Отправить тест в этот чат")}
            </button>
            {!data.telegramEnabled && <p className="mt-2 text-xs text-ink-3">{t("В этой среде Telegram выключен — тестовая отправка вернёт ошибку.")}</p>}
          </div>

          {result && (
            <p className={`mt-4 rounded-lg px-3 py-2 text-sm ${result.status === "sent" ? "bg-ok-soft text-ok" : result.status === "skipped" ? "bg-muted text-ink-2" : "bg-bad-soft text-bad"}`}>
              {result.status === "sent"
                ? t("Отправлено в «{chat}»: {pages} стр.", { chat: result.recipientTitle, pages: result.pages })
                : `${t(deliveryStatus[result.status].label)}: ${t(result.error ?? "")}`}
            </p>
          )}
          {error && <p className="mt-4 rounded-lg bg-bad-soft px-3 py-2 text-sm text-bad">{error}</p>}
          <Note>{t("Данные — как на странице категории «Продаж», в области получателя: ТП видит только свои цифры, СВ — свой регион, РМ — свои регионы. Тест записывается в историю.")}</Note>
        </div>

        <div className="min-w-0">
          {preview ? (
            <div>
              {!preview.applies && (
                <p className="mb-2 rounded-lg bg-warn-soft px-3 py-2 text-sm text-warn">{t("По расписанию не уйдёт: {reason}", { reason: t(preview.skipReason ?? "") })}</p>
              )}
              <p className="whitespace-pre-line rounded-lg bg-muted px-3 py-2 text-sm text-ink">{preview.caption}</p>
              <p className="mt-2 text-xs text-ink-3">{t("Картинок: {n} · {kb} КБ", { n: preview.pages.length, kb: Math.round(preview.bytes / 1024) })}</p>
              {preview.notes.length > 0 && <p className="mt-2 text-xs text-warn">{preview.notes.join(" · ")}</p>}
              <div className="mt-3 grid gap-4 sm:grid-cols-2">
                {preview.pages.map((src, i) => (
                  // eslint-disable-next-line @next/next/no-img-element -- картинка из ответа сервера (data:), оптимизатор Next не нужен
                  <img key={i} src={src} alt={t("Карточка {n}", { n: i + 1 })} className="w-full rounded-xl border border-line shadow-sm" />
                ))}
              </div>
            </div>
          ) : (
            <div className="grid h-full min-h-[240px] place-items-center rounded-xl border border-dashed border-line px-4 text-center text-sm text-ink-3">
              {t("Выберите категории и нажмите «Предпросмотр» — карточки появятся здесь.")}
            </div>
          )}
        </div>
      </div>
    </Card>
  );
}
