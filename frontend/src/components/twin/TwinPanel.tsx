"use client";

import Link from "next/link";
import { useEffect, useMemo, useState } from "react";
import { AlertTriangle, ArrowLeft, ArrowUpRight, Building2, Check, ChevronRight, CircleAlert, Link2, Loader2, Package, Pencil, Warehouse, X } from "lucide-react";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import {
  cardOf,
  contentsOf,
  LAYERS,
  signalOf,
  type LayerKey,
  type Metric,
  type Section,
  type Tone,
  type TwinObject,
  type TwinOptions,
  type TwinSnapshot,
} from "@/lib/twin";

export const toneDot: Record<Tone, string> = {
  ok: "bg-ok",
  warn: "bg-warn",
  bad: "bg-bad",
  info: "bg-accent-bright",
  nodata: "bg-ink-4",
  off: "bg-ink-4/40",
};

export const toneText: Record<Tone, string> = {
  ok: "text-ok",
  warn: "text-warn",
  bad: "text-bad",
  info: "text-accent-bright",
  nodata: "text-ink-4",
  off: "text-ink-4",
};

const toneSoft: Record<Tone, string> = {
  ok: "bg-ok/12 text-ok",
  warn: "bg-warn/14 text-warn",
  bad: "bg-bad/12 text-bad",
  info: "bg-accent-bright/14 text-accent-bright",
  nodata: "bg-muted text-ink-3",
  off: "bg-muted text-ink-4",
};

/**
 * Контекстная панель двойника: без выбора — объекты территории со статусом слоя; выбрано здание — его карточка (проблемы и предупреждения,
 * разделы отделов и складов с переходом в модуль); администратор пользователей привязывает здание к отделам и складам.
 */
export function TwinPanel({
  snapshot,
  loading,
  layer,
  selected,
  onSelect,
  departmentHref,
  onSaved,
}: {
  snapshot: TwinSnapshot | null;
  /** Первый снимок ещё идёт (расчёт разделов без кэша — секунды). */
  loading: boolean;
  layer: LayerKey;
  selected: string | null;
  onSelect: (key: string | null) => void;
  departmentHref: (code: string) => string;
  onSaved: () => Promise<void>;
}) {
  const t = useT();
  const [editing, setEditing] = useState(false);
  const object = snapshot?.objects.find((o) => o.key === selected) ?? null;

  useEffect(() => setEditing(false), [selected]);

  if (!snapshot && loading) {
    return (
      <div className="flex flex-col gap-2 px-5 py-5" aria-busy>
        <p className="flex items-center gap-2 pb-2 text-[13px] text-ink-3">
          <Loader2 className="size-4 animate-spin" aria-hidden />
          {t("Собираем показатели из модулей OneBase…")}
        </p>
        {Array.from({ length: 6 }, (_, i) => (
          <div key={i} className="h-12 animate-pulse rounded-[12px] bg-muted" />
        ))}
      </div>
    );
  }

  if (!snapshot) {
    return (
      <div className="flex flex-col items-center gap-2 px-6 py-10 text-center text-[13px] text-ink-3">
        <CircleAlert className="size-5 text-ink-4" aria-hidden />
        {t("Показатели не загрузились. Модель территории работает, данные появятся после обновления.")}
      </div>
    );
  }

  if (object && editing) {
    return <LinkEditor object={object} onClose={() => setEditing(false)} onSaved={async () => (await onSaved(), setEditing(false))} />;
  }

  if (object) {
    return <BuildingCard object={object} snapshot={snapshot} layer={layer} onBack={() => onSelect(null)} onEdit={snapshot.canEdit ? () => setEditing(true) : null} departmentHref={departmentHref} />;
  }

  return <SiteList snapshot={snapshot} layer={layer} onSelect={onSelect} />;
}

// ---------- Список объектов ----------

function SiteList({ snapshot, layer, onSelect }: { snapshot: TwinSnapshot; layer: LayerKey; onSelect: (key: string) => void }) {
  const t = useT();
  const rows = snapshot.objects.map((o) => ({ o, s: signalOf(o, snapshot, layer, t) }));
  const linked = snapshot.objects.some((o) => o.links.length > 0);
  const counts = (["bad", "warn", "ok"] as Tone[]).map((tone) => ({ tone, n: rows.filter((r) => r.s.tone === tone).length })).filter((c) => c.n > 0);
  const layerLabel = LAYERS.find((l) => l.key === layer)?.label ?? "";

  return (
    <div className="flex min-h-0 flex-1 flex-col">
      <div className="flex items-end justify-between gap-3 px-5 pb-3 pt-4">
        <div className="min-w-0">
          <h2 className="text-[15px] font-semibold tracking-[-0.01em] text-ink">{t("Объекты территории")}</h2>
          <p className="mt-0.5 text-[12px] text-ink-3">{t("Слой «{layer}» · {n} зданий", { layer: t(layerLabel), n: rows.length })}</p>
        </div>
        {counts.length > 0 && (
          <div className="flex shrink-0 items-center gap-1.5">
            {counts.map((c) => (
              <span key={c.tone} className={`inline-flex h-6 min-w-6 items-center justify-center gap-1 rounded-full px-2 text-[11.5px] font-semibold tabular-nums ${toneSoft[c.tone]}`}>
                <span className={`size-1.5 rounded-full ${toneDot[c.tone]}`} aria-hidden />
                {c.n}
              </span>
            ))}
          </div>
        )}
      </div>

      {!linked && (
        <div className="mx-4 mb-3 flex gap-2.5 rounded-[12px] border border-accent-bright/25 bg-accent-bright/8 px-3.5 py-3 text-[12.5px] leading-snug text-ink-2">
          <Link2 className="mt-0.5 size-4 shrink-0 text-accent-bright" aria-hidden />
          <span>
            {snapshot.canEdit
              ? t("Здания ещё не привязаны к отделам и складам. Выберите здание и нажмите «Привязать» — карточка покажет показатели из модулей OneBase.")
              : t("Здания ещё не привязаны к отделам и складам. Привязки задаёт администратор пользователей.")}
          </span>
        </div>
      )}

      <ul className="min-h-0 flex-1 overflow-y-auto px-2 pb-3">
        {rows.map(({ o, s }) => {
          const c = contentsOf(o, snapshot);
          const what = [...c.departments.map((d) => d.name), ...c.warehouses.map((w) => w.name), ...c.plants.map((p) => p.name)];
          return (
            <li key={o.key}>
              <button
                type="button"
                onClick={() => onSelect(o.key)}
                className="group flex w-full cursor-pointer items-center gap-3 rounded-[12px] px-3 py-2.5 text-left transition-colors hover:bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent"
              >
                <span className={`size-2.5 shrink-0 rounded-full ${toneDot[s.tone]}`} aria-hidden />
                <span className="min-w-0 flex-1">
                  <span className="block truncate text-[13.5px] font-medium text-ink">{o.name}</span>
                  <span className="block truncate text-[11.5px] text-ink-3">{what.length > 0 ? what.join(" · ") : t("Не привязано")}</span>
                </span>
                {s.value && s.tone !== "off" && <span className={`max-w-[42%] shrink-0 truncate text-right text-[12px] font-semibold tabular-nums ${toneText[s.tone]}`}>{s.value}</span>}
                <ChevronRight className="size-4 shrink-0 text-ink-4 transition-transform group-hover:translate-x-0.5" aria-hidden />
              </button>
            </li>
          );
        })}
      </ul>
    </div>
  );
}

// ---------- Карточка здания ----------

const sectionIcon = { department: Building2, warehouse: Warehouse, plant: Package, domain: Building2 } as const;

function BuildingCard({
  object,
  snapshot,
  layer,
  onBack,
  onEdit,
  departmentHref,
}: {
  object: TwinObject;
  snapshot: TwinSnapshot;
  layer: LayerKey;
  onBack: () => void;
  onEdit: (() => void) | null;
  departmentHref: (code: string) => string;
}) {
  const t = useT();
  const card = useMemo(() => cardOf(object, snapshot, t, { departmentHref }), [object, snapshot, t, departmentHref]);
  const signal = signalOf(object, snapshot, layer, t);
  const layerLabel = LAYERS.find((l) => l.key === layer)?.label ?? "";

  return (
    <div className="flex min-h-0 flex-1 flex-col">
      <div className="border-b border-line px-5 pb-4 pt-3.5">
        <div className="flex items-center justify-between gap-2">
          <button
            type="button"
            onClick={onBack}
            className="-ml-1.5 inline-flex h-8 cursor-pointer items-center gap-1 rounded-lg px-1.5 text-[12.5px] font-medium text-ink-3 transition-colors hover:bg-muted hover:text-ink focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent"
          >
            <ArrowLeft className="size-4" aria-hidden />
            {t("Все объекты")}
          </button>
          {onEdit && (
            <button
              type="button"
              onClick={onEdit}
              className="inline-flex h-8 cursor-pointer items-center gap-1.5 rounded-lg border border-line px-2.5 text-[12.5px] font-medium text-ink transition-colors hover:bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent"
            >
              {object.links.length > 0 ? <Pencil className="size-3.5" aria-hidden /> : <Link2 className="size-3.5" aria-hidden />}
              {object.links.length > 0 ? t("Изменить") : t("Привязать")}
            </button>
          )}
        </div>
        <h2 className="mt-2 text-[19px] font-semibold leading-tight tracking-[-0.02em] text-ink">{object.name}</h2>
        {object.name !== object.defaultName && <p className="mt-0.5 text-[11.5px] text-ink-4">{object.defaultName}</p>}
        <div className="mt-3 flex flex-wrap items-center gap-1.5">
          {signal.tone !== "off" && (
            <span className={`inline-flex h-6 items-center gap-1.5 rounded-full px-2.5 text-[11.5px] font-semibold ${toneSoft[signal.tone]}`}>
              <span className={`size-1.5 rounded-full ${toneDot[signal.tone]}`} aria-hidden />
              {t(layerLabel)}
              {signal.value ? ` · ${signal.value}` : ""}
            </span>
          )}
          {object.links.map((l) => (
            <span key={`${l.kind}-${l.id}`} className="inline-flex h-6 items-center rounded-full border border-line px-2.5 text-[11.5px] font-medium text-ink-2">
              {l.name}
            </span>
          ))}
        </div>
      </div>

      <div className="min-h-0 flex-1 overflow-y-auto px-4 pb-5 pt-4">
        {object.links.length === 0 ? (
          <div className="flex flex-col items-center gap-2 rounded-[14px] border border-dashed border-line-strong px-5 py-8 text-center">
            <Link2 className="size-5 text-ink-4" aria-hidden />
            <p className="text-[13px] font-medium text-ink">{t("Здание не привязано")}</p>
            <p className="text-[12px] leading-snug text-ink-3">
              {onEdit
                ? t("Укажите, какие отделы и склады находятся в здании, — здесь появятся их показатели.")
                : t("Какие отделы и склады находятся в здании, задаёт администратор пользователей.")}
            </p>
          </div>
        ) : (
          <div className="flex flex-col gap-4">
            {card.problems.length > 0 && (
              <section aria-label={t("Проблемы и предупреждения")} className="rounded-[14px] border border-line bg-surface p-3">
                <h3 className="flex items-center gap-1.5 px-1 pb-2 text-[12.5px] font-semibold text-ink">
                  <AlertTriangle className="size-3.5 text-warn" aria-hidden />
                  {t("Проблемы и предупреждения")}
                  <span className="ml-auto text-[11.5px] font-medium tabular-nums text-ink-3">{card.problems.length}</span>
                </h3>
                <ul className="flex flex-col">
                  {card.problems.slice(0, 12).map((p, i) => {
                    const body = (
                      <>
                        <span className={`mt-[5px] size-2 shrink-0 rounded-full ${toneDot[p.tone]}`} aria-hidden />
                        <span className="min-w-0 flex-1">
                          <span className="block text-[12.5px] leading-snug text-ink">{p.text}</span>
                          <span className="block text-[11px] text-ink-4">{p.source}</span>
                        </span>
                        {p.href && <ChevronRight className="mt-0.5 size-3.5 shrink-0 text-ink-4" aria-hidden />}
                      </>
                    );
                    return (
                      <li key={i}>
                        {p.href ? (
                          <Link href={p.href} className="flex gap-2.5 rounded-[10px] px-1.5 py-1.5 transition-colors hover:bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent">
                            {body}
                          </Link>
                        ) : (
                          <div className="flex gap-2.5 px-1.5 py-1.5">{body}</div>
                        )}
                      </li>
                    );
                  })}
                </ul>
              </section>
            )}
            {card.sections.map((s) => (
              <CardSection key={s.key} section={s} />
            ))}
          </div>
        )}
      </div>
    </div>
  );
}

function CardSection({ section }: { section: Section }) {
  const t = useT();
  const Icon = sectionIcon[section.kind];
  return (
    <section className="rounded-[14px] border border-line bg-surface">
      <div className="flex items-center gap-2 px-3.5 pb-1 pt-3">
        <Icon className="size-3.5 shrink-0 text-ink-3" aria-hidden />
        <h3 className="min-w-0 flex-1 truncate text-[13px] font-semibold text-ink">{section.title}</h3>
        {section.href && (
          <Link
            href={section.href}
            className="inline-flex h-7 shrink-0 items-center gap-1 rounded-md px-1.5 text-[12px] font-medium text-accent-bright transition-colors hover:bg-accent-bright/10 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent"
          >
            {t("Открыть")}
            <ArrowUpRight className="size-3.5" aria-hidden />
          </Link>
        )}
      </div>
      <dl className="grid grid-cols-2 gap-px p-1.5">
        {section.metrics.map((m) => (
          <MetricCell key={m.label} metric={m} />
        ))}
      </dl>
    </section>
  );
}

function MetricCell({ metric: m }: { metric: Metric }) {
  const missing = m.state !== "ok";
  const tone = m.tone ?? (missing ? "nodata" : undefined);
  const content = (
    <>
      <dt className="text-[11.5px] leading-tight text-ink-3">{m.label}</dt>
      <dd className={`mt-1 tabular-nums ${missing ? "text-[13px] font-medium text-ink-4" : `text-[17px] font-semibold tracking-[-0.01em] ${tone && tone !== "info" ? toneText[tone] : "text-ink"}`}`}>
        {m.value}
      </dd>
      {m.hint && <dd className="mt-0.5 text-[11px] leading-snug text-ink-4">{m.hint}</dd>}
    </>
  );
  const base = "flex min-w-0 flex-col rounded-[10px] px-2 py-2";
  return m.href && !missing ? (
    <Link href={m.href} className={`${base} transition-colors hover:bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent`}>
      {content}
    </Link>
  ) : (
    <div className={base}>{content}</div>
  );
}

// ---------- Привязки ----------

function LinkEditor({ object, onClose, onSaved }: { object: TwinObject; onClose: () => void; onSaved: () => Promise<void> }) {
  const t = useT();
  const [options, setOptions] = useState<TwinOptions | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [name, setName] = useState(object.name === object.defaultName ? "" : object.name);
  const initial = (kind: string) => new Set(object.links.filter((l) => l.kind === kind).map((l) => l.id));
  const [departments, setDepartments] = useState(() => initial("department"));
  const [warehouses, setWarehouses] = useState(() => initial("warehouse"));
  const [plants, setPlants] = useState(() => initial("plant"));
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    bff<TwinOptions>("twin/options").then(setOptions, (e: Error) => setLoadError(e.message));
  }, []);

  const toggle = (set: Set<string>, update: (s: Set<string>) => void, id: string) => {
    const next = new Set(set);
    if (next.has(id)) next.delete(id);
    else next.add(id);
    update(next);
  };

  const save = async () => {
    setSaving(true);
    setError(null);
    try {
      await bff(`twin/objects/${encodeURIComponent(object.key)}`, {
        method: "PUT",
        body: JSON.stringify({ name, departmentIds: [...departments], warehouseIds: [...warehouses], plantIds: [...plants].map(Number) }),
      });
      await onSaved();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setSaving(false);
    }
  };

  const group = (title: string, hint: string | null, list: { id: string; name: string }[], set: Set<string>, update: (s: Set<string>) => void) => (
    <fieldset className="rounded-[14px] border border-line bg-surface p-1.5">
      <legend className="sr-only">{title}</legend>
      <p className="px-2 pb-1 pt-1.5 text-[12px] font-semibold text-ink">{title}</p>
      {hint && <p className="px-2 pb-1 text-[11px] leading-snug text-ink-4">{hint}</p>}
      {list.length === 0 ? (
        <p className="px-2 pb-2 text-[12px] text-ink-4">{t("Нет вариантов")}</p>
      ) : (
        list.map((o) => {
          const on = set.has(o.id);
          return (
            <label key={o.id} className="flex cursor-pointer items-center gap-2.5 rounded-[10px] px-2 py-2 transition-colors hover:bg-muted">
              <input type="checkbox" checked={on} onChange={() => toggle(set, update, o.id)} className="peer sr-only" />
              <span
                aria-hidden
                className={`grid size-[18px] shrink-0 place-items-center rounded-[6px] border transition-colors peer-focus-visible:ring-2 peer-focus-visible:ring-accent ${on ? "border-accent bg-accent text-white" : "border-line-strong"}`}
              >
                {on && <Check className="size-3" strokeWidth={3} />}
              </span>
              <span className="text-[13px] text-ink">{o.name}</span>
            </label>
          );
        })
      )}
    </fieldset>
  );

  return (
    <div className="flex min-h-0 flex-1 flex-col">
      <div className="flex items-center justify-between gap-2 border-b border-line px-5 pb-3 pt-3.5">
        <div className="min-w-0">
          <h2 className="truncate text-[15px] font-semibold text-ink">{t("Привязки здания")}</h2>
          <p className="truncate text-[12px] text-ink-3">{object.defaultName}</p>
        </div>
        <button
          type="button"
          onClick={onClose}
          aria-label={t("Закрыть")}
          className="grid size-8 shrink-0 cursor-pointer place-items-center rounded-lg text-ink-3 transition-colors hover:bg-muted hover:text-ink focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent"
        >
          <X className="size-4" />
        </button>
      </div>
      <div className="min-h-0 flex-1 overflow-y-auto px-4 pb-4 pt-4">
        <label className="block px-1">
          <span className="text-[12px] font-semibold text-ink">{t("Название в компании")}</span>
          <input
            value={name}
            onChange={(e) => setName(e.target.value)}
            maxLength={120}
            placeholder={object.defaultName}
            className="mt-1.5 h-10 w-full rounded-[10px] border border-line bg-surface-solid px-3 text-[13.5px] text-ink outline-none placeholder:text-ink-4 focus:border-accent focus:ring-2 focus:ring-accent/25"
          />
          <span className="mt-1 block text-[11px] text-ink-4">{t("Пусто — название из модели территории.")}</span>
        </label>
        {loadError ? (
          <p role="alert" className="mt-4 text-[12.5px] text-bad">{loadError}</p>
        ) : !options ? (
          <div className="mt-6 flex justify-center text-ink-3">
            <Loader2 className="size-5 animate-spin" aria-label={t("Загрузка…")} />
          </div>
        ) : (
          <div className="mt-4 flex flex-col gap-3">
            {group(t("Отделы"), t("Показатели отделов: задачи, люди, заявки, расходы и данные их направления."), options.departments, departments, setDepartments)}
            {group(t("Склады снабжения"), t("Остатки, позиции ниже минимума, выдача."), options.warehouses, warehouses, setWarehouses)}
            {group(t("Склад готовой продукции (Linko)"), t("Остаток, дни покрытия и дефицит по складу завода."), options.plants, plants, setPlants)}
          </div>
        )}
      </div>
      <div className="flex items-center gap-2 border-t border-line px-4 py-3">
        {error && (
          <p role="alert" className="min-w-0 flex-1 text-[12px] leading-snug text-bad">
            {error}
          </p>
        )}
        <div className="ml-auto flex shrink-0 gap-2">
          <button
            type="button"
            onClick={onClose}
            className="h-9 cursor-pointer rounded-[10px] border border-line px-3.5 text-[13px] font-medium text-ink transition-colors hover:bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent"
          >
            {t("Отмена")}
          </button>
          <button
            type="button"
            onClick={save}
            disabled={saving || !options}
            className="inline-flex h-9 cursor-pointer items-center gap-1.5 rounded-[10px] bg-accent px-4 text-[13px] font-semibold text-white transition-opacity hover:opacity-90 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent focus-visible:ring-offset-2 disabled:cursor-default disabled:opacity-50"
          >
            {saving && <Loader2 className="size-3.5 animate-spin" aria-hidden />}
            {t("Сохранить")}
          </button>
        </div>
      </div>
    </div>
  );
}
