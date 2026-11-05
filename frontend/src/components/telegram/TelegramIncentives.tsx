"use client";

import { useEffect, useMemo, useState } from "react";
import { CopyPlus, Loader2, Plus, RefreshCw, Trash2, X } from "lucide-react";
import { button, field, primary } from "@/components/ai/form";
import { Alert, Note } from "@/components/sales/bits";
import { useT } from "@/i18n/client";
import {
  incentiveImages,
  incentiveStates,
  incentivesApi,
  type IncentiveMonthShare,
  type IncentiveOverview,
  type IncentiveProgram,
  type IncentiveProgramInput,
  type IncentiveStandings,
  type IncentiveStoreSet,
  type IncentiveTier,
  type ReportOverview,
} from "@/lib/telegramReports";
import { Switch } from "./TelegramReports";
import type { ReportsTab } from "./TelegramReportsOverview";
import { when } from "./TelegramReportsPreview";

const day = (iso: string) => new Date(`${iso}T00:00:00`).toLocaleDateString("ru-RU");
const kg = (v: number) => v.toLocaleString("ru-RU", { maximumFractionDigits: Math.abs(v) < 100 ? 1 : 0 });
const months = ["январь", "февраль", "март", "апрель", "май", "июнь", "июль", "август", "сентябрь", "октябрь", "ноябрь", "декабрь"];

/** Месяцы периода акции: [год, месяц]. */
function periodMonths(start: string, end: string): [number, number][] {
  const result: [number, number][] = [];
  if (!start || !end || end < start) return result;
  let [y, m] = start.split("-").map(Number);
  const [ey, em] = end.split("-").map(Number);
  while (y < ey || (y === ey && m <= em)) {
    result.push([y, m]);
    m += 1;
    if (m > 12) {
      m = 1;
      y += 1;
    }
    if (result.length > 14) break;
  }
  return result;
}

const inputOf = (p: IncentiveProgram): IncentiveProgramInput => ({
  name: p.name,
  product: p.product,
  productTypeIds: p.productTypeIds,
  start: p.start,
  end: p.end,
  tiers: p.tiers,
  months: p.months,
  storeSets: [...p.storeSets].sort((a, b) => a.minAvg - b.minAvg),
  wholesaleAgentIds: p.wholesaleAgentIds,
  separateWholesale: p.separateWholesale,
  svCopies: p.svCopies,
  svSummary: p.svSummary,
  rmMode: p.rmMode,
  staleData: p.staleData,
  enabled: p.enabled,
  maxStores: p.maxStores,
});

/**
 * «Акции»: накопительные акции для ТП (Twizos и будущие) — период, товары, призы, ориентиры месяцев, цели магазинов маршрута, кто что получает,
 * поведение при несвежих данных Linko, включение; новая акция — по образцу; «накоплено сейчас». Карточки уходят запусками «9. Накопительная
 * акция» (вкладки «Расписание», «Получатели», «Предпросмотр и тест», «История»). Проверки — на сервере.
 */
export function IncentivesPanel({ data, onTab }: { data: ReportOverview; onTab: (tab: ReportsTab) => void }) {
  const t = useT();
  const [overview, setOverview] = useState<IncentiveOverview | null>(null);
  const [code, setCode] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const load = async (select?: string) => {
    try {
      const o = await incentivesApi.overview();
      setOverview(o);
      setCode((current) => select ?? current ?? (o.programs.find((p) => p.state === "running") ?? o.programs[0])?.code ?? null);
      setError(null);
    } catch (e) {
      setError((e as Error).message);
    }
  };

  useEffect(() => {
    void load();
  }, []);

  if (error) return <Alert tone="warn">{error}</Alert>;
  if (!overview) {
    return (
      <p className="flex items-center gap-2 text-sm text-ink-3">
        <Loader2 className="size-4 animate-spin" />
        {t("Загрузка…")}
      </p>
    );
  }

  const program = overview.programs.find((p) => p.code === code) ?? null;
  const tabs: { tab: ReportsTab; label: string }[] = [
    { tab: "schedule", label: "Расписание" },
    { tab: "recipients", label: "Получатели" },
    { tab: "preview", label: "Предпросмотр и тест" },
    { tab: "history", label: "История" },
  ];

  return (
    <div className="space-y-6">
      <div className="text-sm text-ink-2">
        <p>
          {t(
            "Накопительная акция для ТП: ТП копит кг товаров акции за период, порог — приз. Карточки «9. Накопительная акция» уходят по расписанию автоотчётов: 08:30, 13:00, 18:00 (после «Поля боя»), суббота — неделя и рейтинг, последний рабочий день месяца — итог месяца.",
          )}
        </p>
        <div className="mt-2 flex flex-wrap gap-2">
          {tabs.map((x) => (
            <button key={x.tab} className={button} onClick={() => onTab(x.tab)}>
              {t(x.label)}
            </button>
          ))}
        </div>
      </div>

      <div className="flex flex-wrap gap-2">
        {overview.programs.map((p) => (
          <button
            key={p.code}
            onClick={() => setCode(p.code)}
            className={`rounded-xl border px-3 py-2 text-left text-sm ${p.code === code ? "border-accent bg-accent/5" : "border-line hover:bg-muted"}`}
          >
            <span className="block font-medium text-ink">{p.name}</span>
            <span className="block text-xs text-ink-3">
              {day(p.start)} – {day(p.end)} · <span className={incentiveStates[p.state].className}>{t(incentiveStates[p.state].label)}</span>
            </span>
          </button>
        ))}
        {overview.programs.length === 0 && <p className="text-sm text-ink-3">{t("Акций пока нет.")}</p>}
      </div>

      {program && (
        <ProgramForm
          key={`${program.code}-${program.updatedAt}`}
          program={program}
          overview={overview}
          data={data}
          onSaved={(saved) => setOverview((o) => o && { ...o, programs: o.programs.map((p) => (p.code === saved.code ? saved : p)) })}
          onCreated={(created) => void load(created.code)}
        />
      )}
      {program && <StandingsPanel program={program} canUseSales={data.canUseSales} />}
    </div>
  );
}

function Block({ title, hint, children }: { title: string; hint?: string; children: React.ReactNode }) {
  const t = useT();
  return (
    <div>
      <div className="text-sm font-medium text-ink">{t(title)}</div>
      {hint && <p className="mt-0.5 text-xs text-ink-3">{t(hint)}</p>}
      <div className="mt-2">{children}</div>
    </div>
  );
}

function Labeled({ label, className = "", children }: { label: string; className?: string; children: React.ReactNode }) {
  const t = useT();
  return (
    <label className={`block ${className}`}>
      <span className="mb-1 block text-xs font-medium text-ink-2">{t(label)}</span>
      {children}
    </label>
  );
}

const cell = `${field} min-w-0`;

function ProgramForm({
  program,
  overview,
  data,
  onSaved,
  onCreated,
}: {
  program: IncentiveProgram;
  overview: IncentiveOverview;
  data: ReportOverview;
  onSaved: (p: IncentiveProgram) => void;
  onCreated: (p: IncentiveProgram) => void;
}) {
  const t = useT();
  const [form, setForm] = useState(() => inputOf(program));
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);
  const dirty = JSON.stringify(form) !== JSON.stringify(inputOf(program));
  const edit = (patch: Partial<IncentiveProgramInput>) => (setForm((f) => ({ ...f, ...patch })), setSaved(false));

  const types = useMemo(() => new Map(overview.productTypes.map((x) => [x.id, x])), [overview.productTypes]);
  const typeOptions = useMemo(() => {
    const roots = overview.productTypes.filter((x) => x.parentId === null);
    return roots.flatMap((r) => [r, ...overview.productTypes.filter((x) => x.parentId === r.id)]);
  }, [overview.productTypes]);
  const agentNames = useMemo(() => {
    const names = new Map<number, string>(overview.agents.map((a) => [a.id, a.name]));
    for (const region of data.regions) for (const a of region.agents) names.set(a.id, `${a.name} · ${region.name}`);
    return names;
  }, [overview.agents, data.regions]);
  const periodList = periodMonths(form.start, form.end);

  const save = async () => {
    setBusy(true);
    setError(null);
    try {
      const inPeriod = new Set(periodList.map(([y, m]) => `${y}-${m}`));
      const result = await incentivesApi.save(program.code, { ...form, months: form.months.filter((m) => inPeriod.has(`${m.year}-${m.month}`) && m.share > 0) });
      setForm(inputOf(result));
      onSaved(result);
      setSaved(true);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  };

  const setTier = (i: number, patch: Partial<IncentiveTier>) => edit({ tiers: form.tiers.map((x, j) => (j === i ? { ...x, ...patch } : x)) });
  const setSet = (i: number, patch: Partial<IncentiveStoreSet>) => edit({ storeSets: form.storeSets.map((x, j) => (j === i ? { ...x, ...patch } : x)) });
  const share = (y: number, m: number) => form.months.find((x) => x.year === y && x.month === m)?.share ?? 0;
  const setShare = (y: number, m: number, value: number) => {
    const rest = form.months.filter((x) => !(x.year === y && x.month === m));
    const next: IncentiveMonthShare[] = value > 0 ? [...rest, { year: y, month: m, share: value }] : rest;
    edit({ months: next.sort((a, b) => a.year - b.year || a.month - b.month) });
  };
  const shareSum = Math.round(periodList.reduce((s, [y, m]) => s + share(y, m), 0) * 100);
  const text = (v: string | null) => v ?? "";
  const orNull = (v: string) => (v.trim() ? v : null);

  return (
    <div className="rounded-xl border border-line">
      <div className="flex flex-wrap items-center gap-3 border-b border-line px-4 py-3">
        <div className="min-w-0 flex-1">
          <h3 className="font-semibold text-ink">{program.name}</h3>
          <p className="text-xs text-ink-3">
            <span className={incentiveStates[program.state].className}>{t(incentiveStates[program.state].label)}</span>
            {(program.state === "running" || program.state === "upcoming") && ` · ${t("рабочих дней до конца: {n}", { n: program.workDaysLeft })}`}
            {program.updatedAt && ` · ${t("изменена {time}", { time: when(program.updatedAt) })}`}
          </p>
        </div>
        <label className="flex items-center gap-2 text-sm font-medium text-ink">
          <Switch checked={form.enabled} onChange={(v) => edit({ enabled: v })} label={t("Акция включена")} />
          {t("Акция включена")}
        </label>
      </div>

      <div className="space-y-6 p-4">
        {form.enabled && !program.enabled && (
          <Alert tone="info" className="mt-0">
            {t("После сохранения карточки уйдут по включённым запускам «9. Накопительная акция» во вкладке «Расписание» — получателям, подписанным на них.")}
          </Alert>
        )}

        <Block title="Акция">
          <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
            <Labeled label="Название">
              <input className={field} value={form.name} onChange={(e) => edit({ name: e.target.value })} />
            </Labeled>
            <Labeled label="Товар на карточках">
              <input className={field} value={form.product} onChange={(e) => edit({ product: e.target.value })} />
            </Labeled>
            <Labeled label="Начало">
              <input type="date" className={field} value={form.start} onChange={(e) => edit({ start: e.target.value })} />
            </Labeled>
            <Labeled label="Конец">
              <input type="date" className={field} value={form.end} onChange={(e) => edit({ end: e.target.value })} />
            </Labeled>
          </div>
        </Block>

        <Block title="Товары акции" hint="Типы товаров Linko, которые копятся: доставленные кг минус возвраты; подтипы (140 г, 200 г, 35 г) входят сами.">
          <div className="flex flex-wrap items-center gap-2">
            {form.productTypeIds.map((id) => (
              <span key={id} className="inline-flex items-center gap-1 rounded-lg bg-muted px-2 py-1 text-sm text-ink">
                {types.get(id)?.name ?? `#${id}`}
                <button aria-label={t("Убрать")} onClick={() => edit({ productTypeIds: form.productTypeIds.filter((x) => x !== id) })} className="text-ink-3 hover:text-ink">
                  <X className="size-3.5" />
                </button>
              </span>
            ))}
            <select
              className={`${field} max-w-64`}
              value=""
              onChange={(e) => e.target.value && edit({ productTypeIds: [...form.productTypeIds, Number(e.target.value)] })}
            >
              <option value="">{t("Добавить тип…")}</option>
              {typeOptions
                .filter((x) => !form.productTypeIds.includes(x.id))
                .map((x) => (
                  <option key={x.id} value={x.id}>
                    {x.parentId === null ? x.name : `— ${x.name}`}
                  </option>
                ))}
            </select>
          </div>
        </Block>

        <Block title="Призы" hint="Один ТП — один приз: по самой высокой взятой ступени. «До кого» — как в подписи: «до планшета».">
          <div className="space-y-2">
            {form.tiers.map((tier, i) => (
              <div key={i} className="grid items-end gap-2 sm:grid-cols-[100px_1fr_1fr_1fr_150px_1fr_1fr_auto]">
                <Labeled label="Порог, кг">
                  <input type="number" min={1} className={cell} value={tier.kg} onChange={(e) => setTier(i, { kg: Number(e.target.value) || 0 })} />
                </Labeled>
                <Labeled label="Приз">
                  <input className={cell} value={tier.prize} onChange={(e) => setTier(i, { prize: e.target.value })} />
                </Labeled>
                <Labeled label="Коротко">
                  <input className={cell} value={tier.short} onChange={(e) => setTier(i, { short: e.target.value })} />
                </Labeled>
                <Labeled label="До кого">
                  <input className={cell} value={text(tier.of)} onChange={(e) => setTier(i, { of: orNull(e.target.value) })} placeholder={tier.short} />
                </Labeled>
                <Labeled label="Картинка">
                  <select className={cell} value={tier.image} onChange={(e) => setTier(i, { image: e.target.value })}>
                    {overview.images.map((img) => (
                      <option key={img} value={img}>
                        {t(incentiveImages[img] ?? img)}
                      </option>
                    ))}
                  </select>
                </Labeled>
                <Labeled label="Приз (узб.)">
                  <input className={cell} value={text(tier.prizeUz)} onChange={(e) => setTier(i, { prizeUz: orNull(e.target.value) })} />
                </Labeled>
                <Labeled label="Коротко (узб.)">
                  <input className={cell} value={text(tier.shortUz)} onChange={(e) => setTier(i, { shortUz: orNull(e.target.value) })} />
                </Labeled>
                <button className={button} aria-label={t("Убрать")} onClick={() => edit({ tiers: form.tiers.filter((_, j) => j !== i) })}>
                  <Trash2 className="size-4" />
                </button>
              </div>
            ))}
            {form.tiers.length < 5 && (
              <button
                className={button}
                onClick={() => edit({ tiers: [...form.tiers, { kg: 0, prize: "", short: "", image: "gift", prizeUz: null, shortUz: null, of: null }] })}
              >
                <Plus className="size-4" />
                {t("Ступень")}
              </button>
            )}
          </div>
        </Block>

        <Block title="Ориентиры месяцев" hint="Доля порога на месяц — для карточки «Итог месяца» (лист «Модель»: 20 / 35 / 45%).">
          <div className="flex flex-wrap gap-3">
            {periodList.map(([y, m]) => (
              <Labeled key={`${y}-${m}`} label={`${t(months[m - 1])} ${y}, %`} className="w-[150px]">
                <input
                  type="number"
                  min={0}
                  max={100}
                  className={field}
                  value={Math.round(share(y, m) * 100)}
                  onChange={(e) => setShare(y, m, Math.min(100, Math.max(0, Number(e.target.value) || 0)) / 100)}
                />
              </Labeled>
            ))}
          </div>
          <p className={`mt-1 text-xs ${shareSum > 100 ? "text-bad" : "text-ink-3"}`}>{t("Вместе: {n}%", { n: shareSum })}</p>
        </Block>

        <Block
          title="Цели магазинов маршрута"
          hint="Набор по среднему закупу ТТ за 4 месяца (как в «Поле боя»). Где «Поле боя» уже поставило Твизос-цель — набор того же уровня. Нужен набор «от 0 сум» — для мелких ТТ."
        >
          <div className="mb-3 flex flex-wrap items-end gap-3">
            <Labeled label="Магазинов с целью в день, не больше" className="w-[260px]">
              <input
                type="number"
                min={1}
                max={30}
                className={field}
                value={form.maxStores}
                onChange={(e) => edit({ maxStores: Math.min(30, Math.max(1, Number(e.target.value) || 1)) })}
              />
            </Labeled>
            <p className="pb-2 text-xs text-ink-3">{t("Сначала магазины с целью «Поля боя», потом дозаказ и подключение по закупу; на карточке — в порядке маршрута.")}</p>
          </div>
          <div className="space-y-2">
            {form.storeSets.map((set, i) => (
              <div key={i} className="grid items-end gap-2 sm:grid-cols-[140px_1fr_90px_1fr_136px_1fr_1fr_auto]">
                <Labeled label="Закуп от, сум/мес">
                  <input type="number" min={0} step={100000} className={cell} value={set.minAvg} onChange={(e) => setSet(i, { minAvg: Number(e.target.value) || 0 })} />
                </Labeled>
                <Labeled label="Набор">
                  <input className={cell} value={set.contents} onChange={(e) => setSet(i, { contents: e.target.value })} />
                </Labeled>
                <Labeled label="Вес, кг">
                  <input type="number" min={0} step={0.01} className={cell} value={set.kg} onChange={(e) => setSet(i, { kg: Number(e.target.value) || 0 })} />
                </Labeled>
                <Labeled label="Приз клиенту">
                  <input className={cell} value={text(set.prize)} onChange={(e) => setSet(i, { prize: orNull(e.target.value) })} />
                </Labeled>
                <Labeled label="Уровень «Поля боя»">
                  <input className={cell} value={text(set.level)} onChange={(e) => setSet(i, { level: orNull(e.target.value) })} placeholder="300" />
                </Labeled>
                <Labeled label="Набор (узб.)">
                  <input className={cell} value={text(set.contentsUz)} onChange={(e) => setSet(i, { contentsUz: orNull(e.target.value) })} />
                </Labeled>
                <Labeled label="Приз (узб.)">
                  <input className={cell} value={text(set.prizeUz)} onChange={(e) => setSet(i, { prizeUz: orNull(e.target.value) })} />
                </Labeled>
                <button className={button} aria-label={t("Убрать")} onClick={() => edit({ storeSets: form.storeSets.filter((_, j) => j !== i) })}>
                  <Trash2 className="size-4" />
                </button>
              </div>
            ))}
            {form.storeSets.length < 8 && (
              <button
                className={button}
                onClick={() => edit({ storeSets: [...form.storeSets, { minAvg: 0, contents: "", kg: 1, prize: null, level: null, contentsUz: null, prizeUz: null }] })}
              >
                <Plus className="size-4" />
                {t("Набор")}
              </button>
            )}
          </div>
        </Block>

        <Block title="Кто что получает" hint="ТП — только свою карточку. Рейтинг для канала — получатель «РМ · вся страна» с групповым чатом и запуском «рейтинг акции».">
          <div className="grid gap-4 lg:grid-cols-3">
            <label className="flex items-start gap-3 text-sm text-ink">
              <Switch checked={form.svCopies} onChange={(v) => edit({ svCopies: v })} label={t("СВ — копии карточек ТП")} />
              <span>
                <span className="font-medium">{t("СВ — копии карточек ТП")}</span>
                <span className="block text-xs text-ink-3">{t("В 08:30 и 18:00 — карточки ТП региона, подписанных на акцию (никого — все ТП региона, до 10).")}</span>
              </span>
            </label>
            <label className="flex items-start gap-3 text-sm text-ink">
              <Switch checked={form.svSummary} onChange={(v) => edit({ svSummary: v })} label={t("СВ — сводка команды")} />
              <span>
                <span className="font-medium">{t("СВ — сводка команды")}</span>
                <span className="block text-xs text-ink-3">{t("Во всех запусках: накоплено, сегодня, кто отстаёт от нормы, кто у порога.")}</span>
              </span>
            </label>
            <Labeled label="РМ получает">
              <select className={field} value={form.rmMode} onChange={(e) => edit({ rmMode: e.target.value as IncentiveProgramInput["rmMode"] })}>
                <option value="summary">{t("Сводку регионов")}</option>
                <option value="cards">{t("Сводку и карточки ТП")}</option>
              </select>
            </Labeled>
          </div>
        </Block>

        <Block title="Оптовые ТП" hint="У оптовых ТП объёмы выше — отдельный рейтинг; в рейтинге розницы их нет.">
          <label className="flex items-center gap-3 text-sm text-ink">
            <Switch checked={form.separateWholesale} onChange={(v) => edit({ separateWholesale: v })} label={t("Отдельный рейтинг оптовых")} />
            {t("Отдельный рейтинг оптовых")}
          </label>
          <div className="mt-2 flex flex-wrap items-center gap-2">
            {form.wholesaleAgentIds.map((id) => (
              <span key={id} className="inline-flex items-center gap-1 rounded-lg bg-muted px-2 py-1 text-sm text-ink">
                {agentNames.get(id) ?? t("ТП {id}", { id })}
                <button aria-label={t("Убрать")} onClick={() => edit({ wholesaleAgentIds: form.wholesaleAgentIds.filter((x) => x !== id) })} className="text-ink-3 hover:text-ink">
                  <X className="size-3.5" />
                </button>
              </span>
            ))}
            <select
              className={`${field} max-w-72`}
              value=""
              onChange={(e) => e.target.value && edit({ wholesaleAgentIds: [...form.wholesaleAgentIds, Number(e.target.value)] })}
            >
              <option value="">{t("Добавить ТП…")}</option>
              {data.regions.map((region) => (
                <optgroup key={region.id} label={region.name}>
                  {region.agents
                    .filter((a) => !form.wholesaleAgentIds.includes(a.id))
                    .map((a) => (
                      <option key={a.id} value={a.id}>
                        {a.name}
                      </option>
                    ))}
                </optgroup>
              ))}
            </select>
          </div>
        </Block>

        <Block title="Если данные Linko не обновились к отправке">
          <div className="flex flex-wrap gap-4 text-sm text-ink">
            {(
              [
                ["stamp", "Отправить последний подтверждённый результат с отметкой «данные на …»"],
                ["skip", "Не отправлять — в истории «пропущено» с причиной"],
              ] as const
            ).map(([value, label]) => (
              <label key={value} className="flex items-center gap-2">
                <input type="radio" name={`stale-${program.code}`} checked={form.staleData === value} onChange={() => edit({ staleData: value })} />
                {t(label)}
              </label>
            ))}
          </div>
        </Block>

        {saved && !dirty && <p className="text-sm text-ok">{t("Сохранено.")}</p>}
        {error && <p className="rounded-lg bg-bad-soft px-3 py-2 text-sm text-bad">{error}</p>}
        <div className="flex flex-wrap gap-2">
          <button className={primary} onClick={save} disabled={busy || !dirty}>
            {busy && <Loader2 className="size-4 animate-spin" />}
            {t("Сохранить")}
          </button>
          {dirty && (
            <button className={button} onClick={() => setForm(inputOf(program))} disabled={busy}>
              {t("Отмена")}
            </button>
          )}
        </div>

        <CreateByTemplate form={form} onCreated={onCreated} />
      </div>
    </div>
  );
}

/** Новая акция по образцу текущей: свой код, название и период; создаётся выключенной. */
function CreateByTemplate({ form, onCreated }: { form: IncentiveProgramInput; onCreated: (p: IncentiveProgram) => void }) {
  const t = useT();
  const [open, setOpen] = useState(false);
  const [draft, setDraft] = useState({ code: "", name: "", start: "", end: "" });
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (!open) {
    return (
      <button className={button} onClick={() => setOpen(true)}>
        <CopyPlus className="size-4" />
        {t("Новая акция по образцу")}
      </button>
    );
  }

  const create = async () => {
    setBusy(true);
    setError(null);
    try {
      const created = await incentivesApi.create({ ...form, ...draft, months: [], enabled: false });
      setOpen(false);
      onCreated(created);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="rounded-xl border border-dashed border-line p-3">
      <div className="text-sm font-medium text-ink">{t("Новая акция по образцу")}</div>
      <p className="text-xs text-ink-3">{t("Призы, товары, цели магазинов и получатели — как у этой акции; ориентиры месяцев — заново. Создаётся выключенной.")}</p>
      <div className="mt-2 grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <Labeled label="Код (латиницей)">
          <input className={field} value={draft.code} placeholder="twizos-2027q1" onChange={(e) => setDraft((d) => ({ ...d, code: e.target.value }))} />
        </Labeled>
        <Labeled label="Название">
          <input className={field} value={draft.name} onChange={(e) => setDraft((d) => ({ ...d, name: e.target.value }))} />
        </Labeled>
        <Labeled label="Начало">
          <input type="date" className={field} value={draft.start} onChange={(e) => setDraft((d) => ({ ...d, start: e.target.value }))} />
        </Labeled>
        <Labeled label="Конец">
          <input type="date" className={field} value={draft.end} onChange={(e) => setDraft((d) => ({ ...d, end: e.target.value }))} />
        </Labeled>
      </div>
      {error && <p className="mt-2 rounded-lg bg-bad-soft px-3 py-2 text-sm text-bad">{error}</p>}
      <div className="mt-3 flex gap-2">
        <button className={primary} onClick={create} disabled={busy || !draft.code || !draft.name || !draft.start || !draft.end}>
          {busy && <Loader2 className="size-4 animate-spin" />}
          {t("Создать")}
        </button>
        <button className={button} onClick={() => setOpen(false)} disabled={busy}>
          {t("Отмена")}
        </button>
      </div>
    </div>
  );
}

/** Накоплено сейчас — как в карточке 18:00: розница и опт своими местами, приз и сколько до следующего. */
function StandingsPanel({ program, canUseSales }: { program: IncentiveProgram; canUseSales: boolean }) {
  const t = useT();
  const [data, setData] = useState<IncentiveStandings | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [wholesale, setWholesale] = useState(false);

  const load = async () => {
    setBusy(true);
    setError(null);
    try {
      setData(await incentivesApi.standings(program.code));
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  };

  const rows = (data?.rows ?? []).filter((r) => r.wholesale === wholesale);
  return (
    <div className="rounded-xl border border-line">
      <div className="flex flex-wrap items-center gap-3 border-b border-line px-4 py-3">
        <div className="min-w-0 flex-1">
          <h3 className="font-semibold text-ink">{t("Накоплено сейчас")}</h3>
          <p className="text-xs text-ink-3">
            {t("Как на карточке 18:00: доставлено с начала акции по вчера минус возвраты плюс заказы сегодня. Итог акции — по доставленным накладным после её конца.")}
          </p>
        </div>
        <button className={button} onClick={load} disabled={busy || !canUseSales}>
          {busy ? <Loader2 className="size-4 animate-spin" /> : <RefreshCw className="size-4" />}
          {data ? t("Обновить") : t("Показать")}
        </button>
      </div>
      <div className="p-4">
        {!canUseSales && <Note>{t("Нужно право на данные продаж (sales.read).")}</Note>}
        {error && <p className="rounded-lg bg-bad-soft px-3 py-2 text-sm text-bad">{error}</p>}
        {data && (
          <>
            <div className="mb-3 flex flex-wrap items-center gap-2 text-xs text-ink-3">
              {(
                [
                  [false, "Розница"],
                  [true, "Оптовые"],
                ] as const
              ).map(([value, label]) => (
                <button
                  key={label}
                  onClick={() => setWholesale(value)}
                  className={`rounded-lg px-2.5 py-1 text-sm ${wholesale === value ? "bg-accent text-white" : "border border-line text-ink-2 hover:bg-muted"}`}
                >
                  {t(label)} · {data.rows.filter((r) => r.wholesale === value && r.place !== null).length}
                </button>
              ))}
              <span>
                {t("на {date}", { date: day(data.date) })} · {t("данные Linko на {time}", { time: when(data.dataAsOf) })} ·{" "}
                {t("рабочих дней до конца: {n}", { n: data.workDaysLeft })}
              </span>
            </div>
            <div className="overflow-x-auto">
              <table className="w-full min-w-[720px] text-sm">
                <thead>
                  <tr className="border-b border-line text-left text-xs text-ink-3">
                    <th className="py-2 pr-2 font-medium">{t("Место")}</th>
                    <th className="py-2 pr-2 font-medium">{t("ТП")}</th>
                    <th className="py-2 pr-2 font-medium">{t("Регион")}</th>
                    <th className="py-2 pr-2 text-right font-medium">{t("Накоплено, кг")}</th>
                    <th className="py-2 pr-2 text-right font-medium">{t("Сегодня")}</th>
                    <th className="py-2 pr-2 text-right font-medium">{t("Возвраты")}</th>
                    <th className="py-2 font-medium">{t("Приз")}</th>
                  </tr>
                </thead>
                <tbody>
                  {rows.map((r) => (
                    <tr key={r.agentId} className="border-b border-line/60">
                      <td className="py-1.5 pr-2 tabular-nums text-ink-2">{r.place ?? "—"}</td>
                      <td className="py-1.5 pr-2 text-ink">{r.name}</td>
                      <td className="py-1.5 pr-2 text-ink-2">{r.region ?? "—"}</td>
                      <td className="py-1.5 pr-2 text-right font-medium tabular-nums text-ink">{kg(r.total)}</td>
                      <td className="py-1.5 pr-2 text-right tabular-nums text-ink-2">{r.today > 0 ? kg(r.today) : "—"}</td>
                      <td className="py-1.5 pr-2 text-right tabular-nums text-ink-2">{r.returned > 0 ? kg(r.returned) : "—"}</td>
                      <td className="py-1.5 text-ink-2">
                        {r.reached && <span className="text-ok">{r.reached} ✓ </span>}
                        {r.next && r.toNext !== null && t("до «{prize}» — {kg} кг", { prize: r.next, kg: kg(r.toNext) })}
                      </td>
                    </tr>
                  ))}
                  {rows.length === 0 && (
                    <tr>
                      <td colSpan={7} className="py-3 text-ink-3">
                        {t("ТП нет.")}
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
          </>
        )}
      </div>
    </div>
  );
}
