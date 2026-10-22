import { makeT, type T } from "@/i18n/core";
import { money, monthName, num, pct } from "./format";
import type { OutstockView } from "./types";

export type Insight = { title: string; text: string };

/** Без перевода (русский) — если вызывающий не передал t. */
const plain: T = makeT(null);

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
 * t — перевод фраз на язык интерфейса (getT() на странице); без него — русский текст.
 */
export function outstockInsights(d: OutstockView, t: T = plain): Insight[] {
  const tot = d.totals;
  const i = d.insights;
  if (d.days === 0 || tot.pairsWithLoss === 0) return [];

  const out: Insight[] = [];
  const what = d.scope === "top" ? "ТОП-товарам" : d.scope === "rest" ? "товарам вне ТОПа" : "всем товарам";
  const catsText = d.selectedCategories.length ? ` (${d.selectedCategories.map((c) => `«${c}»`).join(", ")})` : "";
  const region = d.regionId ? d.regions.find((r) => r.id === d.regionId) : null;
  const where = region ? t(" в регионе {name}", { name: region.name }) : "";

  out.push({
    title: t("Коротко"),
    text:
      t("За {month} {year} по {what}{cats}{where} мы недопродали примерно на {sum} сум. ", {
        month: monthName(d.month, t),
        year: d.year,
        what: t(what),
        cats: catsText,
        where,
        sum: money(tot.lostSum, t),
      }) +
      t(
        "Это {share} от того, что реально продали. Причина одна: в магазин хотели купить товар, а у дилера на складе его не было — полки пустые, деньги ушли конкуренту. ",
        { share: pct(tot.lossShare, 1) },
      ) +
      t("Такое случилось {n} {times}: {products} {productsWord} {inRegions}.", {
        n: num(tot.pairsWithLoss),
        times: t(plural(tot.pairsWithLoss, "раз", "раза", "раз")),
        products: num(tot.productsWithLoss),
        productsWord: t(plural(tot.productsWithLoss, "товар", "товара", "товаров")),
        inRegions: t(plural(tot.regionsWithLoss, "в {n} регионе", "в {n} регионах", "в {n} регионах"), { n: num(tot.regionsWithLoss) }),
      }),
  });

  if (!region && tot.regionsWithLoss > 1 && i.topRegion) {
    const top = i.topRegion;
    let text = t("Больше всего денег потерял {name}{dealer}: {sum}, это {share} от его продаж.", {
      name: top.name,
      dealer: top.dealer ? ` (${top.dealer})` : "",
      sum: money(top.lostSum, t),
      share: pct(top.lossShare),
    });
    if (i.worstRegion) {
      const worst = i.worstRegion;
      text += t(" Хуже всего по доле — {name}: там не хватило {share} товара{nth}.", {
        name: worst.name,
        share: pct(worst.lossShare),
        nth: worst.everyNthKg ? t(", то есть примерно каждый {n}-й килограмм мы могли продать, но не продали", { n: num(worst.everyNthKg) }) : "",
      });
    } else if (top.everyNthKg) {
      text += t(" Примерно каждый {n}-й килограмм там могли продать, но не продали.", { n: num(top.everyNthKg) });
    }
    out.push({ title: t("Где теряем больше всего"), text });
  }

  if (i.topProduct) {
    const prod = i.topProduct;
    let text = i.topCategory
      ? t("Больше всего теряем на категории «{category}» — это {share} всех потерь. ", { category: i.topCategory, share: pct(i.topCategoryShare) })
      : "";
    text += t("Самый «дорогой» пропавший товар — {name}: из-за его отсутствия ушло {sum}", { name: short(prod.name), sum: money(prod.lostSum, t) });
    text +=
      prod.regions > 1
        ? t(", и не хватало его сразу {inRegions}.", {
            inRegions: t(plural(prod.regions, "в {n} регионе", "в {n} регионах", "в {n} регионах"), { n: num(prod.regions) }),
          })
        : ".";
    out.push({ title: t("Каких товаров не хватает"), text });
  }

  if (tot.corePairs > 0 && tot.pairsWithLoss > tot.corePairs) {
    const frequent = i.coreFrequent.map((name) => short(name, 48));
    out.push({
      title: t("Главное — не распыляться"),
      text:
        t(
          "Проблема сосредоточена в немногих местах. Если взять все {n} {cases} «товар закончился в регионе» и отсортировать по ущербу, то всего {core} самых крупных из них дают 80% потерь — {sum}. ",
          {
            n: num(tot.pairsWithLoss),
            cases: t(plural(tot.pairsWithLoss, "случай", "случая", "случаев")),
            core: num(tot.corePairs),
            sum: money(tot.coreSum, t),
          },
        ) +
        t("Значит, исправив эти {core} {cases}, мы вернём основную часть денег.", {
          core: num(tot.corePairs),
          cases: t(plural(tot.corePairs, "случай", "случая", "случаев")),
        }) +
        (frequent.length > 1 ? t(" Чаще всего в этом списке: {list}.", { list: frequent.join(", ") }) : ""),
    });
  }

  if (tot.zeroDays > 0) {
    if (d.factoryKnown) {
      let text =
        t(
          "В {share} дней, когда у дилера товара не было, на складе завода он был. То есть товар есть, но его вовремя не заказали или не довезли. ",
          { share: pct(i.dealerDaysShare) },
        ) +
        t("Только на этом мы потеряли {sum}. Это не нехватка производства — это вопрос заказа и доставки.", { sum: money(tot.dealerLossSum, t) });
      if (tot.factoryLossSum > 0) {
        text += t(
          " А {sum} потеряли в дни, когда на заводе тоже было пусто: тут дилер не виноват, отгружать было нечего — это вопрос к производству.",
          { sum: money(tot.factoryLossSum, t) },
        );
      }
      if (tot.unknownLossSum > 0) {
        text += t(" Ещё {sum} — дни без данных по складу завода ({n} {days}): чья это потеря, сказать нельзя.", {
          sum: money(tot.unknownLossSum, t),
          n: num(tot.unknownDays),
          days: t(plural(tot.unknownDays, "день", "дня", "дней")),
        });
      }
      text += t(" Выпуска цехов в Linko нет, поэтому остаток завода в прошлые дни восстановлен только по перемещениям, и доля завода — оценка снизу.");
      out.push({ title: t("Почему так происходит"), text });
    } else {
      out.push({
        title: t("Почему так происходит"),
        text: t("Склад завода в Linko не найден, поэтому все потери отнесены к дилерам: товар вовремя не заказали или не довезли."),
      });
    }
  }

  if (tot.chronic > 0) {
    out.push({
      title: t("Хронические дыры"),
      text:
        t("{n} {pairs} «товар × регион» стояли в нуле половину месяца и дольше — {sum}. ", {
          n: num(tot.chronic),
          pairs: t(plural(tot.chronic, "пара", "пары", "пар")),
          sum: money(tot.chronicSum, t),
        }) + t("Это не разовый сбой доставки, а товар, которого в регионе системно нет: его стоит поставить в постоянный заказ."),
    });
  }

  return out;
}
