"use client";

import { useEffect, useState } from "react";
import { ChevronUp, ClipboardList, Copy, FileText, Loader2, Minus, PackagePlus, Plus, RotateCcw } from "lucide-react";
import { Button, SearchInput } from "@/components/ui";
import { TopChip } from "@/components/sales/outstock";
import { tNow, useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import { plural } from "@/lib/format";
import { kg, money, num } from "@/lib/sales/format";
import type { AutoOrderItem, AutoOrderView } from "@/lib/sales/types";

// Автозаказ на «Рек. остатке»: регион → рекомендуемый заказ в коробках (сервер, StockService.AutoOrderAsync) → правка коробок → акт
// приёма-передачи в Word (сервер собирает его по коробкам, названия, единицы и цены — свои). Правки держатся в браузере по региону.

const storageKey = "onebase.autoorder";
type Edits = Record<string, Record<string, number>>;
type Show = "rec" | "all" | "edited";

function loadEdits(): Edits {
  try {
    const raw = window.localStorage.getItem(storageKey);
    return raw ? (JSON.parse(raw) as Edits) : {};
  } catch {
    return {};
  }
}

function saveEdits(edits: Edits) {
  try {
    window.localStorage.setItem(storageKey, JSON.stringify(edits));
  } catch {
    // приватный режим — правки живут до перезагрузки страницы
  }
}

const today = () => {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
};

const qtyOf = (it: AutoOrderItem, boxes: number) => Math.round(boxes * it.unitsPerBox * 1000) / 1000;
const kgOf = (it: AutoOrderItem, boxes: number) => boxes * (it.boxKg ?? 0);
const sumOf = (it: AutoOrderItem, boxes: number) => (it.price ? Math.round(qtyOf(it, boxes) * it.price) : 0);
// Для вставки в Excel: дробь с запятой, без разделителя тысяч — как ждёт русская и узбекская локаль.
const cellNum = (n: number) => n.toLocaleString("ru-RU", { useGrouping: false, maximumFractionDigits: 3 });
const qtyText = (it: AutoOrderItem, q: number) => num(q, it.unit === "кг" && q % 1 ? 1 : 0);

/** Карточка «Автозаказ» над таблицей «Рек. остатка»: свёрнута до «Собрать заказ». */
export function AutoOrder({ initialRegion }: { initialRegion: string | null }) {
  const t = useT();
  const [open, setOpen] = useState(false);
  const [region, setRegion] = useState<string | null>(initialRegion);
  const [view, setView] = useState<AutoOrderView | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!open) return;
    let alive = true;
    setLoading(true);
    setError(null);
    bff<AutoOrderView>(`sales/stock/autoorder${region ? `?region=${encodeURIComponent(region)}` : ""}`)
      .then((v) => alive && setView(v))
      .catch((e: Error) => alive && setError(e.message))
      .finally(() => alive && setLoading(false));
    return () => {
      alive = false;
    };
  }, [open, region]);

  return (
    <section className="mt-6 rounded-xl border border-line bg-surface">
      <div className="flex flex-wrap items-center gap-3 px-4 py-3.5 sm:px-5">
        <span className="flex size-10 shrink-0 items-center justify-center rounded-lg bg-accent-soft text-accent">
          <ClipboardList className="size-5" aria-hidden />
        </span>
        <div className="min-w-0 flex-1">
          <h2 className="text-base font-semibold text-ink">{t("Автозаказ")}</h2>
          <p className="text-xs text-ink-3">{t("Заказ на склад региона в коробках: рекомендация, ваша правка и акт приёма-передачи в Word")}</p>
        </div>
        {open ? (
          <Button size="sm" onClick={() => setOpen(false)}>
            <ChevronUp className="size-3.5" />
            {t("Свернуть")}
          </Button>
        ) : (
          <Button size="sm" variant="primary" onClick={() => setOpen(true)}>
            <PackagePlus className="size-3.5" />
            {t("Собрать заказ")}
          </Button>
        )}
      </div>
      {open && (
        <div className="border-t border-line">
          {error && <p className="mx-4 mt-4 rounded-lg bg-bad-soft px-3 py-2 text-sm text-bad sm:mx-5">{error}</p>}
          {!view && loading && (
            <div className="flex items-center gap-2 px-4 py-6 text-sm text-ink-3 sm:px-5">
              <Loader2 className="size-4 animate-spin" />
              {t("Загрузка…")}
            </div>
          )}
          {view && <AutoOrderPanel key={view.regionId} view={view} loading={loading} onRegion={setRegion} />}
        </div>
      )}
    </section>
  );
}

function AutoOrderPanel({ view, loading, onRegion }: { view: AutoOrderView; loading: boolean; onRegion: (id: string) => void }) {
  const t = useT();
  const [edits, setEdits] = useState<Edits>(loadEdits);
  const [date, setDate] = useState(today);
  const [show, setShow] = useState<Show>("rec");
  const [query, setQuery] = useState("");
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);
  const mine = edits[view.regionId] ?? {};
  const boxesOf = (it: AutoOrderItem) => mine[String(it.productId)] ?? it.recBoxes;
  const edited = (it: AutoOrderItem) => mine[String(it.productId)] !== undefined && mine[String(it.productId)] !== it.recBoxes;

  const update = (change: (region: Record<string, number>) => Record<string, number> | null) =>
    setEdits((e) => {
      const region = change({ ...(e[view.regionId] ?? {}) });
      const next = { ...e };
      if (region && Object.keys(region).length > 0) next[view.regionId] = region;
      else delete next[view.regionId];
      saveEdits(next);
      return next;
    });

  const setBoxes = (it: AutoOrderItem, boxes: number) =>
    update((r) => ({ ...r, [String(it.productId)]: Math.min(1_000_000, Math.max(0, Math.round(boxes) || 0)) }));
  const resetRow = (it: AutoOrderItem) =>
    update((r) => {
      delete r[String(it.productId)];
      return r;
    });
  const resetAll = () => update(() => null);

  const editedCount = view.items.filter(edited).length;
  const recCount = view.items.filter((it) => it.recBoxes > 0).length;
  const rows = view.items.filter((it) => (show === "all" ? true : show === "edited" ? edited(it) : it.recBoxes > 0 || boxesOf(it) > 0));
  const needle = query.trim().toLowerCase();
  const visible = needle ? rows.filter((it) => `${it.name} ${it.code ?? ""} ${it.category}`.toLowerCase().includes(needle)) : rows;
  // Итоги — по всем позициям региона с коробками, а не только по видимым после поиска.
  const picked = view.items.map((it) => ({ it, boxes: boxesOf(it) })).filter((x) => x.boxes > 0);
  const totals = picked.reduce((a, x) => ({ boxes: a.boxes + x.boxes, kg: a.kg + kgOf(x.it, x.boxes), sum: a.sum + sumOf(x.it, x.boxes) }), { boxes: 0, kg: 0, sum: 0 });

  const flash = (text: string, ok = true) => {
    setMessage({ text, ok });
    setTimeout(() => setMessage(null), 3000);
  };

  const copy = async () => {
    const lines = [
      [t("Наименование"), t("Коробки"), t("Ед. изм."), t("Количество"), t("Вес, кг"), t("Цена, сум"), t("Сумма, сум")].join("\t"),
      ...picked.map(({ it, boxes }) => [it.name, boxes, it.unit, cellNum(qtyOf(it, boxes)), cellNum(kgOf(it, boxes)), it.price ?? "", sumOf(it, boxes) || ""].join("\t")),
    ];
    try {
      await navigator.clipboard.writeText(lines.join("\n"));
      flash(t("Таблица скопирована — вставьте в Excel"));
    } catch {
      flash(t("Не удалось скопировать"), false);
    }
  };

  const download = async () => {
    if (picked.length === 0) {
      flash(t("В заказе нет ни одной коробки"), false);
      return;
    }

    setBusy(true);
    const [y, m, d] = date.split("-");
    const name = `${t("Акт приёма-передачи")} ${view.regionName} ${d}.${m}.${y}`.replace(/[\\/:*?"<>|«»]/g, " ").replace(/\s+/g, " ").trim();
    try {
      const response = await fetch(`/bff/api/sales/stock/autoorder/act/${encodeURIComponent(name)}.docx`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ regionId: view.regionId, date, lines: picked.map(({ it, boxes }) => ({ productId: it.productId, boxes })) }),
      });
      if (!response.ok) {
        const text = await response.text();
        let error = text;
        try {
          const json = JSON.parse(text);
          error = json.error ?? json.detail ?? json.title ?? text;
        } catch {
          // не JSON
        }
        throw new Error(response.status === 403 ? tNow("Недостаточно прав.") : error ? tNow(error) : tNow("Ошибка {status}", { status: response.status }));
      }

      const url = URL.createObjectURL(await response.blob());
      const a = Object.assign(document.createElement("a"), { href: url, download: `${name}.docx` });
      document.body.append(a);
      a.click();
      a.remove();
      setTimeout(() => URL.revokeObjectURL(url), 10_000);
    } catch (e) {
      flash(
        e instanceof TypeError
          ? t("Нет связи с сервером OneBase — ответ не пришёл. Обновите страницу и проверьте, не выполнилось ли действие, затем повторите.")
          : (e as Error).message,
        false,
      );
    } finally {
      setBusy(false);
    }
  };

  const shows: { key: Show; label: string; count: number }[] = [
    { key: "rec", label: t("Рекомендованные"), count: recCount },
    { key: "all", label: t("Все товары"), count: view.items.length },
    { key: "edited", label: t("Изменённые"), count: editedCount },
  ];
  const caption = "mb-1.5 block text-xs font-medium text-ink-2";
  const control = "h-10 w-full rounded-lg border border-line bg-surface px-3 text-sm text-ink focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/20 max-lg:h-11";
  const th = "whitespace-nowrap px-3 py-2 text-[11px] font-semibold uppercase tracking-wide text-ink-3";
  const td = "px-3 py-2.5 align-middle";
  const step = "flex size-8 items-center justify-center rounded-md border border-line text-ink-2 hover:bg-muted disabled:opacity-40 max-lg:size-10";
  return (
    <>
      <div className="grid grid-cols-1 gap-3 px-4 pt-4 sm:grid-cols-2 sm:px-5 lg:grid-cols-[minmax(180px,220px)_170px_auto_minmax(200px,1fr)] lg:items-end">
        <label className="block">
          <span className={caption}>{t("Склад региона")}</span>
          <select value={view.regionId} onChange={(e) => onRegion(e.target.value)} className={`${control} font-medium`}>
            {view.regions.map((r) => (
              <option key={r.id} value={r.id}>
                {r.name}
              </option>
            ))}
          </select>
        </label>
        <label className="block">
          <span className={caption}>{t("Дата акта")}</span>
          <input type="date" value={date} onChange={(e) => setDate(e.target.value || date)} className={control} />
        </label>
        <div className="min-w-0">
          <span className={caption}>{t("Показать")}</span>
          <div className="no-scrollbar flex max-w-full overflow-x-auto rounded-lg border border-line text-sm" role="group">
            {shows.map((s) => (
              <button
                key={s.key}
                type="button"
                onClick={() => setShow(s.key)}
                aria-pressed={show === s.key}
                disabled={s.key === "edited" && s.count === 0 && show !== "edited"}
                className={`flex h-10 shrink-0 items-center gap-1.5 whitespace-nowrap px-3 disabled:opacity-40 max-lg:h-11 ${show === s.key ? "bg-accent text-white" : "bg-surface text-ink hover:bg-muted"}`}
              >
                {s.label}
                <span className={`rounded-full px-1.5 text-[11px] tabular-nums ${show === s.key ? "bg-white/20" : "bg-muted text-ink-3"}`}>{num(s.count)}</span>
              </button>
            ))}
          </div>
        </div>
        <div>
          <span className={caption}>{t("Поиск")}</span>
          <SearchInput value={query} onChange={(e) => setQuery(e.target.value)} placeholder={t("Название, код или категория")} />
        </div>
      </div>

      <div className="mx-4 mt-4 grid grid-cols-2 gap-3 rounded-lg bg-muted px-4 py-3 sm:mx-5 sm:flex sm:flex-wrap sm:items-center sm:gap-x-8">
        <Fact label={t("Позиций")} value={num(picked.length)} />
        <Fact label={t("Коробок")} value={num(totals.boxes)} />
        <Fact label={t("Вес")} value={`${kg(totals.kg)} ${t("кг")}`} />
        <Fact label={t("Сумма")} value={`${money(totals.sum, t)} ${t("сум")}`} />
        {loading && <Loader2 className="size-4 animate-spin text-ink-3" />}
        {editedCount > 0 && (
          <div className="col-span-2 flex flex-wrap items-center gap-2 text-xs sm:ml-auto">
            <span className="rounded-full bg-accent-soft px-2.5 py-1 font-medium text-accent-strong">
              {t("Изменено вручную: {n}", { n: num(editedCount) })}
            </span>
            <button type="button" onClick={resetAll} className="inline-flex items-center gap-1 font-medium text-accent hover:underline max-lg:min-h-11">
              <RotateCcw className="size-3.5" />
              {t("Вернуть рекомендацию")}
            </button>
          </div>
        )}
      </div>

      <div className="mt-3 max-h-[600px] overflow-auto">
        <table className="w-full min-w-max border-collapse text-sm">
          <thead className="sticky top-0 z-[2] bg-surface-solid shadow-[inset_0_-1px_0_var(--color-line)]">
            <tr>
              <th className={`${th} sticky left-0 z-[1] bg-surface-solid pl-4 text-left sm:pl-5`}>{t("Товар")}</th>
              <th className={`${th} text-right max-lg:hidden`} title={t("Остаток склада региона; бронь — заказы магазинов, которые ещё не отгружены; рекомендация считается от доступного")}>
                {t("На складе, кг")}
              </th>
              <th className={`${th} text-right max-lg:hidden`}>{t("Рекомендация")}</th>
              <th className={`${th} text-center`}>{t("Коробки")}</th>
              <th className={`${th} text-right`}>{t("Количество")}</th>
              <th className={`${th} text-right`}>{t("Вес, кг")}</th>
              <th className={`${th} text-right`}>{t("Цена, сум")}</th>
              <th className={`${th} pr-4 text-right sm:pr-5`}>{t("Сумма, сум")}</th>
            </tr>
          </thead>
          <tbody>
            {visible.length === 0 && (
              <tr>
                <td colSpan={8} className="px-4 py-8 text-center text-sm text-ink-3">
                  {needle
                    ? t("Ничего не найдено")
                    : show === "edited"
                      ? t("Правок пока нет")
                      : t("Рекомендованного заказа нет — откройте «Все товары», чтобы добавить вручную")}
                </td>
              </tr>
            )}
            {visible.map((it) => {
              const boxes = boxesOf(it);
              const changed = edited(it);
              const bg = changed ? "bg-accent-soft" : "bg-surface-solid";
              return (
                <tr key={it.productId} className={`group/row border-b border-line ${changed ? "bg-accent-soft" : "hover:bg-muted"}`}>
                  <td className={`${td} sticky left-0 z-[1] pl-4 sm:pl-5 ${bg} ${changed ? "shadow-[inset_3px_0_0_var(--color-accent)]" : "group-hover/row:bg-muted-solid"}`}>
                    <span className="block min-w-[220px] max-w-[360px] max-lg:min-w-0 max-lg:max-w-[44vw]">
                      <span className={`block whitespace-normal font-medium ${boxes ? "text-ink" : "text-ink-2"}`}>
                        {it.name} {it.top && <TopChip />}
                      </span>
                      <span className="block text-xs text-ink-3">
                        {[it.code ? t("код {code}", { code: it.code }) : null, t(it.category), t("{n} {unit} в коробке", { n: num(it.unitsPerBox, 3), unit: t(it.unit) })]
                          .filter(Boolean)
                          .join(" · ")}
                      </span>
                      <span className="block text-xs text-ink-3 lg:hidden">
                        <Stock it={it} inline />
                      </span>
                    </span>
                  </td>
                  <td className={`${td} text-right tabular-nums max-lg:hidden`}>
                    <Stock it={it} />
                  </td>
                  <td className={`${td} text-right tabular-nums max-lg:hidden`}>
                    <Recommendation it={it} changed={changed} onReset={() => resetRow(it)} />
                  </td>
                  <td className={td}>
                    {/* На телефоне колонки «Рекомендация» нет — она под полем, чтобы коробки были видны без прокрутки вбок. */}
                    <div className="flex items-center justify-center gap-1">
                      <button type="button" className={step} onClick={() => setBoxes(it, boxes - 1)} disabled={boxes <= 0} aria-label={t("Меньше на коробку")}>
                        <Minus className="size-3.5" />
                      </button>
                      <input
                        type="number"
                        min={0}
                        step={1}
                        inputMode="numeric"
                        value={boxes || ""}
                        placeholder="0"
                        onChange={(e) => setBoxes(it, Number(e.target.value))}
                        aria-label={t("Коробок: {name}", { name: it.name })}
                        className="h-8 w-16 rounded-md border border-line bg-surface px-1 text-center font-semibold tabular-nums text-ink [appearance:textfield] focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/20 max-lg:h-10 [&::-webkit-inner-spin-button]:appearance-none [&::-webkit-outer-spin-button]:appearance-none"
                      />
                      <button type="button" className={step} onClick={() => setBoxes(it, boxes + 1)} aria-label={t("Больше на коробку")}>
                        <Plus className="size-3.5" />
                      </button>
                    </div>
                    <div className="mt-1 text-center text-xs lg:hidden">
                      <Recommendation it={it} changed={changed} onReset={() => resetRow(it)} short />
                    </div>
                  </td>
                  <td className={`${td} whitespace-nowrap text-right tabular-nums`}>
                    {boxes ? (
                      <>
                        {qtyText(it, qtyOf(it, boxes))} <span className="text-ink-3">{t(it.unit)}</span>
                      </>
                    ) : (
                      <span className="text-ink-3">—</span>
                    )}
                  </td>
                  <td className={`${td} text-right tabular-nums`}>
                    {boxes ? kg(kgOf(it, boxes)) : <span className="text-ink-3">—</span>}
                    {boxes > 0 && it.factoryFreeKg != null && kgOf(it, boxes) > it.factoryFreeKg + 0.001 && (
                      <span
                        className="block whitespace-nowrap text-[11px] font-medium text-warn"
                        title={t("Остаток завода {stock} кг, из них в брони {reserved} кг", { stock: kg(it.factoryKg), reserved: kg((it.factoryKg ?? 0) - it.factoryFreeKg) })}
                      >
                        {t("на заводе доступно {n} кг", { n: kg(it.factoryFreeKg) })}
                      </span>
                    )}
                  </td>
                  <td className={`${td} text-right tabular-nums text-ink-2`}>
                    {it.price ? num(it.price) : <span title={t("Товара нет в прайсе «{priceList}»", { priceList: view.priceList ?? "" })}>—</span>}
                  </td>
                  <td className={`${td} pr-4 text-right font-semibold tabular-nums sm:pr-5`}>{boxes && it.price ? num(sumOf(it, boxes)) : <span className="font-normal text-ink-3">—</span>}</td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>

      <div className="flex flex-wrap items-center gap-3 border-t border-line px-4 py-3.5 sm:px-5">
        <p className="min-w-0 flex-1 text-xs leading-relaxed text-ink-3 max-lg:basis-full">
          {t("Рекомендация — заказ, который доводит доступный остаток склада (остаток − бронь: заказы магазинов, которые ещё не отгружены) до запаса на {days} дн. продаж, в целых коробках. Цена — прайс «{priceList}».", {
            days: view.coverDays,
            priceList: view.priceList ?? "Дилерга кириш нарх",
          })}
          {view.withoutBox > 0 && ` ${t("Без веса коробки и потому без автозаказа: {n} {products}.", { n: num(view.withoutBox), products: t(plural(view.withoutBox, ["товар", "товара", "товаров"])) })}`}
        </p>
        {message && <span className={`text-xs font-medium ${message.ok ? "text-ok" : "text-bad"}`}>{message.text}</span>}
        <div className="flex gap-2 max-sm:w-full max-sm:flex-col-reverse">
          <Button onClick={copy} className="max-sm:w-full">
            <Copy className="size-4" />
            {t("Копировать для Excel")}
          </Button>
          <Button variant="primary" onClick={download} disabled={busy} className="max-sm:w-full">
            {busy ? <Loader2 className="size-4 animate-spin" /> : <FileText className="size-4" />}
            {t("Акт приёма-передачи")}
            <span className="rounded bg-white/20 px-1.5 py-px text-[10px] font-semibold uppercase">Word</span>
          </Button>
        </div>
      </div>
    </>
  );
}

/** Остаток склада региона и бронь: доступно = остаток − бронь (от него — рекомендация). inline — строкой под названием на телефоне. */
function Stock({ it, inline = false }: { it: AutoOrderItem; inline?: boolean }) {
  const t = useT();
  const reserved = it.reservedKg > 0;
  if (inline) {
    return (
      <>
        {t("на складе {n} кг", { n: kg(it.stockKg) })}
        {reserved && ` · ${t("бронь {r} кг · доступно {f} кг", { r: kg(it.reservedKg), f: kg(it.freeKg) })}`}
      </>
    );
  }

  return (
    <span className="inline-flex flex-col items-end leading-tight">
      <span className={it.stockKg > 0 ? "text-ink" : "text-ink-3"}>{it.stockKg > 0 ? kg(it.stockKg) : "0"}</span>
      {reserved && (
        <span className="whitespace-nowrap text-[11px] text-ink-3" title={t("Заказы магазинов, которые ещё не отгружены: рекомендация считается от доступного")}>
          {t("бронь {r} · доступно {f}", { r: kg(it.reservedKg), f: kg(it.freeKg) })}
        </span>
      )}
    </span>
  );
}

/** Рекомендация строки; после правки — ссылка, возвращающая её. short — подпись под полем на телефоне. */
function Recommendation({ it, changed, onReset, short = false }: { it: AutoOrderItem; changed: boolean; onReset: () => void; short?: boolean }) {
  const t = useT();
  const text = it.recBoxes ? t("{n} кор.", { n: num(it.recBoxes) }) : short ? "0" : "—";
  if (changed) {
    return (
      <button type="button" onClick={onReset} title={t("Вернуть рекомендацию")} className="inline-flex items-center gap-1 text-xs font-medium text-accent hover:underline max-lg:min-h-8">
        <RotateCcw className="size-3" />
        {short ? t("рек. {n}", { n: text }) : it.recBoxes ? text : "0"}
      </button>
    );
  }

  return <span className="text-ink-3">{short ? (it.recBoxes ? t("рек. {n}", { n: text }) : null) : text}</span>;
}

function Fact({ label, value }: { label: string; value: string }) {
  return (
    <div className="min-w-0">
      <div className="text-[10px] font-medium uppercase tracking-[0.08em] text-ink-3">{label}</div>
      <div className="text-base font-semibold tabular-nums text-ink">{value}</div>
    </div>
  );
}
