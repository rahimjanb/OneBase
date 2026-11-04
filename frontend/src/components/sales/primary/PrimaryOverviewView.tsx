import Link from "next/link";
import { ArrowRight, ArrowUpRight, ChevronDown, ChevronRight, Factory, Flag, Globe, Info, MapPin, Package, Store, Tag, Users, type LucideIcon } from "lucide-react";
import { getT } from "@/i18n/server";
import type { T } from "@/i18n/core";
import { choiceQuery, type PrimaryChoice } from "@/lib/primary-period";
import { bigSum, bigSumParts, money, num, pct, plural } from "@/lib/sales/format";
import type { PrimaryOverview, PrimaryOverviewDirection } from "@/lib/sales/types";

type Look = { id: "republic" | "export"; name: string; route: string; icon: LucideIcon; cta: string; bar: string; tile: string; soft: string; text: string; ring: string };

/** Цвета направлений — везде одинаковые: республика — акцент, экспорт — фиолетовый. */
const looks: Record<PrimaryOverviewDirection["id"], Look> = {
  republic: {
    id: "republic",
    name: "Республика",
    route: "завод → дилеры и прямые клиенты",
    icon: MapPin,
    cta: "Открыть: Дилеры и клиенты",
    bar: "bg-accent",
    tile: "bg-accent-soft text-accent-strong",
    soft: "bg-accent-soft text-accent-strong",
    text: "text-accent-strong",
    ring: "hover:border-accent/40 focus-visible:border-accent/60",
  },
  export: {
    id: "export",
    name: "Экспорт",
    route: "завод → экспорт",
    icon: Globe,
    cta: "Открыть: Страны и заказы",
    bar: "bg-violet",
    tile: "bg-violet-soft text-violet-strong",
    soft: "bg-violet-soft text-violet-strong",
    text: "text-violet-strong",
    ring: "hover:border-violet/40 focus-visible:border-violet/60",
  },
};

/** Тонны — всегда с одним знаком: «4 441,0 т», «774,7 т». */
const tonsFormat = new Intl.NumberFormat("ru-RU", { minimumFractionDigits: 1, maximumFractionDigits: 1 });
const tons = (value: number) => tonsFormat.format(value);
/** Главная и вторая величины по выбранным единицам: «4 441,0» + «т» или «143,7» + «млрд сум». */
function values(unit: PrimaryChoice["unit"], t: number, amount: number, tr: T) {
  const [n, scale] = bigSumParts(amount, tr);
  const ton = { value: tons(t), unit: tr("т"), full: `${tons(t)} ${tr("т")}` };
  const sum = { value: n, unit: `${scale} ${tr("сум")}`.trim(), full: `${bigSum(amount, tr)} ${tr("сум")}` };
  return unit === "sum" ? { main: sum, second: ton } : { main: ton, second: sum };
}

/** Полоса долей: сегменты цветом направлений, у каждого — подсказка (тонны, сумма, доля); пустой период — пустая дорожка. */
function ShareBar({ data, unit, height, t }: { data: PrimaryOverview; unit: PrimaryChoice["unit"]; height: string; t: T }) {
  const parts = data.directions.map((d) => ({ d, share: (unit === "sum" ? d.shareAmount : d.shareTons) ?? 0 })).filter((x) => x.share > 0);
  const label = data.directions.map((d) => `${t(looks[d.id].name)} ${pct(unit === "sum" ? d.shareAmount : d.shareTons, 1)}`).join(", ");
  return (
    <div role="img" aria-label={label} className={`flex w-full gap-[3px] overflow-hidden rounded-full bg-muted ${height}`}>
      {parts.map(({ d, share }) => (
        <div
          key={d.id}
          title={`${t(looks[d.id].name)}: ${tons(d.tons)} ${t("т")} · ${bigSum(d.amount, t)} ${t("сум")} · ${pct(share, 1)}`}
          className={`${looks[d.id].bar} h-full first:rounded-l-full last:rounded-r-full`}
          style={{ width: `${share * 100}%` }}
        />
      ))}
    </div>
  );
}

/**
 * Итог «Отгрузка завода всего»: главная величина по единицам, вторая и период мелко; справа (на телефоне — под цифрами) — полоса долей
 * и легенда.
 */
export async function PrimaryTotal({ data, unit, words }: { data: PrimaryOverview; unit: PrimaryChoice["unit"]; words: string }) {
  const t = await getT();
  const v = values(unit, data.totalTons, data.totalAmount, t);
  return (
    <section className="rounded-[20px] border border-accent/20 bg-surface bg-[linear-gradient(110deg,rgb(43_85_214/0.18),transparent_60%)] p-5 lg:flex lg:items-center lg:gap-8 lg:rounded-[18px] lg:px-6 lg:py-[22px]">
      <div className="lg:w-[340px] lg:shrink-0">
        <div className="flex items-center gap-2 text-[13px] font-semibold text-ink">
          <Factory aria-hidden className="size-4 text-accent-strong" strokeWidth={1.9} />
          {t("Отгрузка завода всего")}
        </div>
        <div className="mt-2 flex flex-wrap items-baseline gap-x-2">
          <span className="text-[34px] font-bold leading-none tracking-tight tabular-nums text-ink lg:text-[40px]" title={v.main.full}>
            {v.main.value}
          </span>
          <span className="text-[18px] text-ink-3">{v.main.unit}</span>
          <span className="text-[13px] tabular-nums text-ink-3 lg:hidden">· {v.second.full}</span>
        </div>
        <p className="mt-2 text-[13px] tabular-nums text-ink-3 max-lg:hidden">
          {v.second.full} · {words}
        </p>
      </div>
      <div className="mt-4 min-w-0 flex-1 lg:mt-0">
        <ShareBar data={data} unit={unit} height="h-2.5 lg:h-3.5" t={t} />
        <div className="mt-3 flex flex-wrap items-center justify-between gap-x-4 gap-y-1 text-[13px]">
          {data.directions.map((d) => (
            <span key={d.id} className="inline-flex items-baseline gap-1.5">
              <span aria-hidden className={`size-2.5 self-center rounded-[3px] ${looks[d.id].bar}`} />
              <span className="text-ink">{t(looks[d.id].name)}</span>
              <span className="font-bold tabular-nums text-ink">{pct(unit === "sum" ? d.shareAmount : d.shareTons, 1)}</span>
              <span className="tabular-nums text-ink-3 max-lg:hidden">· {unit === "sum" ? `${bigSum(d.amount, t)} ${t("сум")}` : `${tons(d.tons)} ${t("т")}`}</span>
            </span>
          ))}
        </div>
      </div>
    </section>
  );
}

/** Метрика карточки: значение и подпись (на телефоне — короткая, если есть); переносятся, а не обрезаются. */
function Metric({ icon: Icon, value, label, short, text, className = "" }: { icon: LucideIcon; value: string; label: string; short?: string; text?: boolean; className?: string }) {
  return (
    <div className={`min-w-0 px-3 first:pl-0 last:pr-0 ${className}`}>
      <div className={`flex items-start gap-1.5 leading-tight tabular-nums text-ink ${text ? "text-[14px] font-semibold lg:pt-[3px]" : "text-[16px] font-bold lg:text-[17px]"}`}>
        <Icon aria-hidden className="mt-1 size-[13px] shrink-0 text-ink-3 max-lg:hidden" strokeWidth={1.9} />
        <span className="min-w-0 break-words">{value}</span>
      </div>
      <div className="mt-0.5 text-[11px] leading-snug text-ink-3">
        {short ? (
          <>
            <span className="lg:hidden">{short}</span>
            <span className="max-lg:hidden">{label}</span>
          </>
        ) : (
          label
        )}
      </div>
    </div>
  );
}

/**
 * Карточка направления — целиком ссылка на детальную страницу: шапка (плитка цветом направления, название, маршрут, стрелка), главная и
 * вторая величины, полоса доли, метрики, «Открыть: …». Пустой период — «0 т» и подпись.
 */
export async function DirectionCard({ d, unit, href }: { d: PrimaryOverviewDirection; unit: PrimaryChoice["unit"]; href: string }) {
  const t = await getT();
  const look = looks[d.id];
  const v = values(unit, d.tons, d.amount, t);
  const share = unit === "sum" ? d.shareAmount : d.shareTons;
  const price = d.avgPricePerTon == null ? "—" : money(d.avgPricePerTon, t);
  const empty = d.tons === 0 && d.amount === 0;
  const metrics =
    d.id === "republic"
      ? [
          { icon: Users, value: num(d.counterparties ?? 0), label: t(plural(d.counterparties ?? 0, ["контрагент", "контрагента", "контрагентов"])) },
          { icon: Store, value: t("Дилеры и прямые"), label: t("базары, сети, фирм. магазин"), text: true, className: "max-lg:hidden" },
          { icon: Tag, value: price, label: t("сум за тонну, в среднем"), short: t("сум/т в среднем") },
        ]
      : [
          { icon: Flag, value: num(d.countries ?? 0), label: t(plural(d.countries ?? 0, ["страна", "страны", "стран"])) },
          { icon: Package, value: num(d.orders), label: t(plural(d.orders, ["заказ", "заказа", "заказов"])) },
          { icon: Tag, value: price, label: t("сум за тонну, в среднем"), short: t("сум/т") },
        ];
  return (
    <Link
      href={href}
      aria-label={t("{name}: {value}, {share} отгрузки завода — открыть", { name: t(look.name), value: v.main.full, share: pct(share, 1) })}
      className={`group flex flex-col gap-[18px] rounded-[20px] border border-line bg-surface p-5 transition-[transform,border-color] hover:-translate-y-px focus-visible:-translate-y-px focus-visible:outline-none lg:rounded-[18px] lg:p-[22px] ${look.ring}`}
    >
      <div className="flex items-start gap-3">
        <span className={`grid size-10 shrink-0 place-items-center rounded-xl lg:size-[42px] ${look.tile}`}>
          <look.icon aria-hidden className="size-5" strokeWidth={1.9} />
        </span>
        <span className="min-w-0 flex-1">
          <span className="block text-[17px] font-bold leading-tight text-ink lg:text-[18px]">{t(look.name)}</span>
          <span className="mt-0.5 block text-[12px] text-ink-3">{t(look.route)}</span>
        </span>
        <span aria-hidden className="grid size-[34px] shrink-0 place-items-center rounded-full border border-line text-ink-2 transition-colors group-hover:text-ink max-lg:hidden">
          <ArrowUpRight className="size-4" />
        </span>
        <ChevronRight aria-hidden className="size-5 shrink-0 self-center text-ink-3 lg:hidden" />
      </div>

      <div>
        <div className="flex flex-wrap items-baseline justify-between gap-x-3 gap-y-1">
          <span className="flex items-baseline gap-1.5">
            <span className="text-[28px] font-bold leading-none tracking-tight tabular-nums text-ink lg:text-[34px]" title={v.main.full}>
              {v.main.value}
            </span>
            <span className="text-[15px] text-ink-3">{v.main.unit}</span>
          </span>
          <span className="text-[15px] font-semibold tabular-nums text-ink-2" title={v.second.full}>
            {v.second.full}
          </span>
        </div>
        <div className="mt-3 h-1.5 overflow-hidden rounded-full bg-muted lg:h-2">
          <div className={`h-full rounded-full ${look.bar}`} style={{ width: `${(share ?? 0) * 100}%` }} />
        </div>
        <p className="mt-2 text-[12px] text-ink-3">{empty ? t("Отгрузок за период нет") : t("{pct} отгрузки завода", { pct: pct(share, 1) })}</p>
      </div>

      <div className="grid grid-cols-[repeat(auto-fit,minmax(0,1fr))] divide-x divide-line border-t border-line pt-4">
        {metrics.map((m) => (
          <Metric key={m.label + m.value} {...m} />
        ))}
      </div>

      <span className={`flex h-11 items-center justify-center gap-1.5 rounded-[11px] text-[13px] font-semibold ${look.soft}`}>
        {t(look.cta)}
        <ArrowRight aria-hidden className="size-4" />
      </span>
    </Link>
  );
}

const methodology = [
  {
    id: "republic" as const,
    text: "перемещения Linko со склада завода на склады дилеров и заказы прямых клиентов завода (базары, сети, фирменный магазин), нетто возвратов.",
  },
  {
    id: "export" as const,
    text: "заказы филиала «Завод» торговым точкам с типом EXPORT, нетто возвратов; страна — по названию или адресу точки в Linko.",
  },
];

/** «Как считаются направления»: на компьютере — карточка, на телефоне — свёрнутый блок. Текст переносится, не обрезается. */
export async function PrimaryMethodology() {
  const t = await getT();
  const rows = (
    <div className="space-y-3">
      {methodology.map((m) => (
        <div key={m.id} className="flex gap-3">
          <span aria-hidden className={`mt-0.5 h-[18px] w-[3px] shrink-0 rounded-full ${looks[m.id].bar}`} />
          <p className="text-[13px] leading-[1.5] text-ink-3">
            <span className="font-medium text-ink">{t(looks[m.id].name)}</span> — {t(m.text)}
          </p>
        </div>
      ))}
    </div>
  );
  return (
    <>
      <section className="rounded-[18px] border border-line bg-surface px-[18px] py-4 max-lg:hidden">
        <h2 className="mb-3 flex items-center gap-2 text-[13px] font-semibold text-ink">
          <Info aria-hidden className="size-4 text-ink-3" strokeWidth={1.9} />
          {t("Как считаются направления")}
        </h2>
        {rows}
      </section>
      <details className="group rounded-[20px] border border-line bg-surface lg:hidden">
        <summary className="flex min-h-12 cursor-pointer list-none items-center gap-2 px-4 py-3 text-[14px] font-semibold text-ink [&::-webkit-details-marker]:hidden">
          <Info aria-hidden className="size-4 text-ink-3" strokeWidth={1.9} />
          <span className="flex-1">{t("Как считаются направления")}</span>
          <ChevronDown aria-hidden className="size-4 text-ink-3 transition-transform group-open:rotate-180" />
        </summary>
        <div className="px-4 pb-4">{rows}</div>
      </details>
    </>
  );
}

/** Отгрузок за период нет (весь завод) — подсказка и «Показать с начала года». */
export async function PrimaryEmpty({ choice }: { choice: PrimaryChoice }) {
  const t = await getT();
  return (
    <div className="flex flex-wrap items-center justify-between gap-3 rounded-[18px] border border-dashed border-line bg-surface px-5 py-4 text-[13px] text-ink-2">
      <span>{t("Отгрузок за период нет.")}</span>
      {choice.period !== "ytd" && (
        <Link href={`/sales/primary?${choiceQuery({ ...choice, period: "ytd", month: undefined, quarter: undefined })}`} className="inline-flex h-10 items-center rounded-xl bg-accent px-4 font-semibold text-white hover:bg-accent-strong">
          {t("Показать с начала года")}
        </Link>
      )}
    </div>
  );
}
