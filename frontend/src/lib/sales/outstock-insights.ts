import { money, monthName, num, pct } from "./format";
import type { OutstockView } from "./types";

export type Insight = { title: string; text: string };

/** Форма слова по числу: 1 товар, 2 товара, 5 товаров. */
export function plural(n: number, one: string, few: string, many: string): string {
  const a = Math.abs(Math.round(n)) % 100;
  const b = a % 10;
  if (a > 10 && a < 20) return many;
  if (b > 1 && b < 5) return few;
  if (b === 1) return one;
  return many;
}

const short = (name: string, max = 60) => (name.length > max ? `${name.slice(0, max - 1).trimEnd()}…` : name);

/**
 * «Выводы» аутстока — текст из цифр страницы: меняется вместе с фильтрами (ТОП, категории, регион).
 * Ничего не придумывает: каждая фраза — пересказ одного числа из данных.
 */
export function buildOutstockInsights(d: OutstockView): Insight[] {
  const t = d.totals;
  if (d.days === 0 || t.pairsWithLoss === 0) return [];

  const out: Insight[] = [];
  const what = d.scope === "top" ? "ТОП-товарам" : d.scope === "rest" ? "товарам вне ТОПа" : "всем товарам";
  const catsText = d.selectedCategories.length ? ` (${d.selectedCategories.map((c) => `«${c}»`).join(", ")})` : "";
  const region = d.regionId ? d.regions.find((r) => r.id === d.regionId) : null;
  const where = region ? ` в регионе ${region.name}` : "";

  out.push({
    title: "Коротко",
    text:
      `За ${monthName(d.month)} ${d.year} по ${what}${catsText}${where} мы недопродали примерно на ${money(t.lostSum)} сум. ` +
      `Это ${pct(t.lossShare, 1)} от того, что реально продали. Причина одна: в магазин хотели купить товар, а у дилера на складе его не было — ` +
      `полки пустые, деньги ушли конкуренту. Такое случилось ${num(t.pairsWithLoss)} ${plural(t.pairsWithLoss, "раз", "раза", "раз")}: ` +
      `${num(t.productsWithLoss)} ${plural(t.productsWithLoss, "товар", "товара", "товаров")} в ${num(t.regionsWithLoss)} ${plural(t.regionsWithLoss, "регионе", "регионах", "регионах")}.`,
  });

  const regions = d.byRegion.filter((r) => r.lostSum > 0);
  if (!region && regions.length > 1) {
    const top = regions[0];
    const byShare = regions.filter((r) => r.lossShare != null && r.soldKg > 0).sort((a, b) => (b.lossShare ?? 0) - (a.lossShare ?? 0))[0];
    let text = `Больше всего денег потерял ${top.name}${top.dealer ? ` (${top.dealer})` : ""}: ${money(top.lostSum)}, это ${pct(top.lossShare)} от его продаж.`;
    const worst = byShare && byShare.id !== top.id ? byShare : null;
    const share = worst?.lossShare ?? top.lossShare ?? 0;
    const nth = share > 0 ? Math.round(1 / share) : 0;
    if (worst) {
      text += ` Хуже всего по доле — ${worst.name}: там не хватило ${pct(worst.lossShare)} товара${nth >= 2 ? `, то есть примерно каждый ${num(nth)}-й килограмм мы могли продать, но не продали` : ""}.`;
    } else if (nth >= 2) {
      text += ` Примерно каждый ${num(nth)}-й килограмм там могли продать, но не продали.`;
    }
    out.push({ title: "Где теряем больше всего", text });
  }

  const cats = d.selectedCategories.length ? d.categories.filter((c) => c.selected) : d.categories;
  const cat = cats[0];
  const prod = d.byProduct[0];
  if (prod) {
    let text = cat && cats.length > 1 ? `Больше всего теряем на категории «${cat.name}» — это ${pct(cat.share)} всех потерь. ` : "";
    text += `Самый «дорогой» пропавший товар — ${short(prod.name)}: из-за его отсутствия ушло ${money(prod.lostSum)}`;
    text += prod.regions > 1 ? `, и не хватало его сразу в ${num(prod.regions)} ${plural(prod.regions, "регионе", "регионах", "регионах")}.` : ".";
    out.push({ title: "Каких товаров не хватает", text });
  }

  if (t.corePairs > 0 && t.pairsWithLoss > t.corePairs) {
    const counts = new Map<string, number>();
    for (const p of d.pairs) if (p.core) counts.set(p.product, (counts.get(p.product) ?? 0) + 1);
    const frequent = [...counts.entries()]
      .sort((a, b) => b[1] - a[1])
      .slice(0, 3)
      .map(([name]) => short(name, 48));
    out.push({
      title: "Главное — не распыляться",
      text:
        `Проблема сосредоточена в немногих местах. Если взять все ${num(t.pairsWithLoss)} ${plural(t.pairsWithLoss, "случай", "случая", "случаев")} ` +
        `«товар закончился в регионе» и отсортировать по ущербу, то всего ${num(t.corePairs)} самых крупных из них дают 80% потерь — ${money(t.coreSum)}. ` +
        `Значит, исправив эти ${num(t.corePairs)} ${plural(t.corePairs, "случай", "случая", "случаев")}, мы вернём основную часть денег.` +
        (frequent.length > 1 ? ` Чаще всего в этом списке: ${frequent.join(", ")}.` : ""),
    });
  }

  if (t.zeroDays > 0) {
    if (d.factoryKnown) {
      const share = t.dealerDays / t.zeroDays;
      let text =
        `В ${pct(share)} дней, когда у дилера товара не было, на складе завода он был. То есть товар есть, но его вовремя не заказали или не довезли. ` +
        `Только на этом мы потеряли ${money(t.dealerLossSum)}. Это не нехватка производства — это вопрос заказа и доставки.`;
      if (t.factoryLossSum > 0) {
        text += ` А ${money(t.factoryLossSum)} потеряли в дни, когда на заводе тоже было пусто: тут дилер не виноват, отгружать было нечего — это вопрос к производству.`;
      }
      text += " Выпуска цехов в Linko нет, поэтому остаток завода в прошлые дни восстановлен только по перемещениям, и доля завода — оценка снизу.";
      out.push({ title: "Почему так происходит", text });
    } else {
      out.push({
        title: "Почему так происходит",
        text: "Склад завода в Linko не найден, поэтому все потери отнесены к дилерам: товар вовремя не заказали или не довезли.",
      });
    }
  }

  return out;
}
