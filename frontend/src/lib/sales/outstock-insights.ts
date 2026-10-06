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
 * «Выводы» аутстока — текст из цифр сервера (OutstockView.insights и totals): меняется вместе с фильтрами (ТОП, категории, регион).
 * Здесь только шаблоны фраз: какой регион худший по доле, «каждый N-й килограмм», доля дней «потеря дилера», товары ядра — считает
 * OutstockService (DOC §9.5). Ничего не придумывает: каждая фраза — пересказ одного числа из данных.
 */
export function outstockInsights(d: OutstockView): Insight[] {
  const t = d.totals;
  const i = d.insights;
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

  if (!region && t.regionsWithLoss > 1 && i.topRegion) {
    const top = i.topRegion;
    let text = `Больше всего денег потерял ${top.name}${top.dealer ? ` (${top.dealer})` : ""}: ${money(top.lostSum)}, это ${pct(top.lossShare)} от его продаж.`;
    if (i.worstRegion) {
      const worst = i.worstRegion;
      text += ` Хуже всего по доле — ${worst.name}: там не хватило ${pct(worst.lossShare)} товара${
        worst.everyNthKg ? `, то есть примерно каждый ${num(worst.everyNthKg)}-й килограмм мы могли продать, но не продали` : ""
      }.`;
    } else if (top.everyNthKg) {
      text += ` Примерно каждый ${num(top.everyNthKg)}-й килограмм там могли продать, но не продали.`;
    }
    out.push({ title: "Где теряем больше всего", text });
  }

  if (i.topProduct) {
    const prod = i.topProduct;
    let text = i.topCategory ? `Больше всего теряем на категории «${i.topCategory}» — это ${pct(i.topCategoryShare)} всех потерь. ` : "";
    text += `Самый «дорогой» пропавший товар — ${short(prod.name)}: из-за его отсутствия ушло ${money(prod.lostSum)}`;
    text += prod.regions > 1 ? `, и не хватало его сразу в ${num(prod.regions)} ${plural(prod.regions, "регионе", "регионах", "регионах")}.` : ".";
    out.push({ title: "Каких товаров не хватает", text });
  }

  if (t.corePairs > 0 && t.pairsWithLoss > t.corePairs) {
    const frequent = i.coreFrequent.map((name) => short(name, 48));
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
      let text =
        `В ${pct(i.dealerDaysShare)} дней, когда у дилера товара не было, на складе завода он был. То есть товар есть, но его вовремя не заказали или не довезли. ` +
        `Только на этом мы потеряли ${money(t.dealerLossSum)}. Это не нехватка производства — это вопрос заказа и доставки.`;
      if (t.factoryLossSum > 0) {
        text += ` А ${money(t.factoryLossSum)} потеряли в дни, когда на заводе тоже было пусто: тут дилер не виноват, отгружать было нечего — это вопрос к производству.`;
      }
      if (t.unknownLossSum > 0) {
        text += ` Ещё ${money(t.unknownLossSum)} — дни без данных по складу завода (${num(t.unknownDays)} ${plural(t.unknownDays, "день", "дня", "дней")}): чья это потеря, сказать нельзя.`;
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

  if (t.chronic > 0) {
    out.push({
      title: "Хронические дыры",
      text:
        `${num(t.chronic)} ${plural(t.chronic, "пара", "пары", "пар")} «товар × регион» стояли в нуле половину месяца и дольше — ${money(t.chronicSum)}. ` +
        "Это не разовый сбой доставки, а товар, которого в регионе системно нет: его стоит поставить в постоянный заказ.",
    });
  }

  return out;
}
