/**
 * Цифровой двойник (Operational Twin): типы снимка GET /api/twin и чистые функции вида — цвет здания в слое, подписи на зданиях, KPI
 * предприятия, карточка здания и «Проблемы и предупреждения». Числа приходят с backend (сервисы разделов, права пользователя); здесь только
 * раскраска по правилам и подписи. Где данных в OneBase нет — «Нет данных», где нет права — «Нет доступа»; ничего не досчитывается.
 * Без импортов: модуль проверяется node --test (src/lib/twin.test.mjs). Подписи — через t (русский текст — ключ словаря).
 */

export type T = (text: string, vars?: Record<string, string | number>) => string;

// ---------- Снимок (camelCase, как отдаёт backend) ----------

export type TwinState = "ok" | "noaccess" | "nodata" | "error";
export type Part<D> = { state: TwinState; data: D | null };

export type TwinLink = { kind: "department" | "warehouse" | "plant"; id: string; code: string | null; name: string };
export type TwinObject = { key: string; name: string; defaultName: string; links: TwinLink[]; updatedAt: string | null };

export type SalesLevel = "Good" | "Warning" | "Bad";
export type TwinSales = {
  year: number;
  month: number;
  closed: boolean;
  dataThrough: string;
  factKg: number;
  planKg: number | null;
  /** Доли: 0,62 = 62 %. */
  execution: number | null;
  forecastExecution: number | null;
  level: SalesLevel | null;
  levelBasis: "forecast" | "execution";
  revenue: number;
  akb: number;
  critical: number;
  risk: number;
  vacancies: number;
};
export type TwinPrimary = {
  year: number;
  month: number;
  kg: number;
  sum: number;
  planKg: number | null;
  execution: number | null;
  forecastExecution: number | null;
  hasPlan: boolean;
  asOf: string | null;
};
export type TwinHr = {
  seesAll: boolean;
  isToday: boolean;
  active: number;
  scheduled: number;
  present: number;
  late: number;
  absent: number;
  onLeave: number;
  /** 0–100. */
  presentPct: number | null;
  syncedAt: string | null;
};
export type TwinTasks = { open: number; overdue: number; review: number; blocked: number; dueThisWeek: number; doneThisMonth: number };
export type TwinSupply = {
  readsAll: boolean;
  open: number;
  approval: number;
  urgent: number;
  revision: number;
  raw: number;
  overdueToBuy: number | null;
  spendMonth: number | null;
  spendFrom: string;
  spendTo: string;
};
export type TwinStock = {
  warehouseId: string | null;
  warehouseName: string | null;
  items: number;
  low: number;
  empty: number;
  withReserve: number;
  toIssue: number;
  rawLow: number | null;
};
export type TwinPlant = {
  stockId: number;
  name: string;
  kg: number | null;
  daysOfCover: number | null;
  skus: number;
  deficit: number;
  overstock: number;
  dead: number;
  snapshotDate: string;
  syncedAt: string | null;
};
export type TwinFinance = {
  asOf: string;
  debt: number;
  overdue: number;
  /** Доля 0–1. */
  overdueShare: number;
  overdueDealers: number;
  paidMonth: number;
  shippedMonth: number;
};
export type TwinPromo = { month: string; total: number; promos: number; toSales: number | null };
export type TwinAlert = { id: string; category: string; severity: "Critical" | "Warning"; title: string; href: string | null };

export type TwinDeptTasks = { open: number; overdue: number; review: number; blocked: number };
export type TwinDeptHr = { divisions: number; employees: number; scheduled: number; present: number; late: number; absent: number; presentPct: number | null };
export type TwinDeptSupply = { open: number; approval: number; urgent: number; revision: number; raw: number; overdue: number | null; purchaseTotal: number };
export type TwinDeptSpend = { total: number; lines: number };
export type TwinDepartment = {
  id: string;
  code: string;
  name: string;
  tasks: Part<TwinDeptTasks>;
  hr: Part<TwinDeptHr>;
  supply: Part<TwinDeptSupply>;
  spend: Part<TwinDeptSpend>;
  users: number | null;
};

export type TwinSnapshot = {
  generatedAt: string;
  canEdit: boolean;
  objects: TwinObject[];
  departments: TwinDepartment[];
  sales: Part<TwinSales>;
  primary: Part<TwinPrimary>;
  hr: Part<TwinHr>;
  tasks: Part<TwinTasks>;
  supply: Part<TwinSupply>;
  stock: Part<TwinStock>;
  plant: Part<TwinPlant>;
  finance: Part<TwinFinance>;
  promo: Part<TwinPromo>;
  alerts: Part<{ items: TwinAlert[] }>;
};

export type TwinOption = { id: string; code: string | null; name: string };
export type TwinOptions = { departments: TwinOption[]; warehouses: TwinOption[]; plants: TwinOption[] };

// ---------- Слои и цвета ----------

export type LayerKey = "all" | "production" | "warehouse" | "supply" | "finance" | "hr" | "sales" | "marketing" | "tasks";

export const LAYERS: { key: LayerKey; label: string }[] = [
  { key: "all", label: "Все" },
  { key: "production", label: "Производство" },
  { key: "warehouse", label: "Склад" },
  { key: "supply", label: "Снабжение" },
  { key: "finance", label: "Финансы" },
  { key: "hr", label: "HR" },
  { key: "sales", label: "Продажи" },
  { key: "marketing", label: "Маркетинг" },
  { key: "tasks", label: "Задачи" },
];

export const isLayer = (value: string | null | undefined): value is LayerKey => LAYERS.some((l) => l.key === value);

/**
 * ok / warn / bad — статус по правилу раздела; info — данные есть, нормы нет (только сведения); nodata — данных нет, нет доступа или
 * источник не ответил; off — здание не относится к слою (или не привязано).
 */
export type Tone = "ok" | "warn" | "bad" | "info" | "nodata" | "off";

const RANK: Record<Tone, number> = { bad: 5, warn: 4, ok: 3, info: 2, nodata: 1, off: 0 };

export const worst = (tones: Tone[]): Tone => tones.reduce<Tone>((w, t) => (RANK[t] > RANK[w] ? t : w), "off");

/** Подписи легенды — по порядку важности. */
export const LEGEND: { tone: Tone; label: string }[] = [
  { tone: "bad", label: "Требует действия" },
  { tone: "warn", label: "Внимание" },
  { tone: "ok", label: "В норме" },
  { tone: "info", label: "Сведения без нормы" },
  { tone: "nodata", label: "Нет данных" },
];

/** Пороги, которых нет в разделах (остальные — правила разделов): просрочено не меньше этой доли открытых задач — красный. */
export const TASKS_BAD_SHARE = 0.25;

/** Сколько находок AI отдела показывать в карточке здания; остальные — ссылкой на AI Dashboard. */
export const AI_PER_DEPARTMENT = 3;

/** Явка — как в HR и на главной: от 80% — зелёный, 70–79% — жёлтый, ниже — красный. */
export const attendanceTone = (pct: number): Tone => (pct >= 80 ? "ok" : pct >= 70 ? "warn" : "bad");

/** Уровень выполнения с сервера (ExecutionLevelOf раздела «Продажи»: от 90% — Good, от 60% — Warning). */
export const levelTone = (level: SalesLevel | null): Tone => (level === "Good" ? "ok" : level === "Warning" ? "warn" : level === "Bad" ? "bad" : "info");

export function tasksTone(t: TwinDeptTasks): Tone {
  if (t.overdue <= 0) return "ok";
  return t.open > 0 && t.overdue / t.open >= TASKS_BAD_SHARE ? "bad" : "warn";
}

// ---------- Форматы ----------

const nbsp = " ";
const group = (n: number, digits = 0) =>
  n.toLocaleString("ru-RU", { minimumFractionDigits: 0, maximumFractionDigits: digits }).replace(/ |\s/g, nbsp);

export const int = (n: number) => group(Math.round(n));
/** Доля 0–1 → «62,4%». */
export const share = (v: number, digits = 1) => `${group(v * 100, digits)}%`;
/** Проценты 0–100 → «92,3%». */
export const pct = (v: number, digits = 1) => `${group(v, digits)}%`;
/** «2026-10-30» → «30.10». */
export const dayMonth = (iso: string) => `${iso.slice(8, 10)}.${iso.slice(5, 7)}`;
export const tons = (kg: number) => `${group(kg / 1000, kg >= 100_000 ? 0 : 1)}${nbsp}т`;

/** 1 250 000 000 → «1,3 млрд сум»; единицы — через словарь. */
export function sum(value: number, t: T): string {
  const abs = Math.abs(value);
  const short = (n: number) => group(n, 1);
  if (abs >= 1_000_000_000) return `${short(value / 1_000_000_000)}${nbsp}${t("млрд")}`;
  if (abs >= 1_000_000) return `${short(value / 1_000_000)}${nbsp}${t("млн")}`;
  if (abs >= 10_000) return `${short(value / 1_000)}${nbsp}${t("тыс.")}`;
  return `${int(value)}${nbsp}${t("сум")}`;
}

// ---------- Что в здании ----------

export type Contents = { departments: TwinDepartment[]; warehouses: TwinLink[]; plants: TwinLink[] };

export function contentsOf(object: TwinObject, s: TwinSnapshot): Contents {
  const byId = new Map(s.departments.map((d) => [d.id, d]));
  return {
    departments: object.links.filter((l) => l.kind === "department").map((l) => byId.get(l.id)).filter((d): d is TwinDepartment => !!d),
    warehouses: object.links.filter((l) => l.kind === "warehouse"),
    plants: object.links.filter((l) => l.kind === "plant"),
  };
}

/** Текст вместо значения: нет права, нет данных, источник не ответил. */
export const stateText = (state: TwinState, t: T) =>
  state === "noaccess" ? t("Нет доступа") : state === "error" ? t("Не загрузилось") : t("Нет данных");

// ---------- Сигналы слоя ----------

export type Signal = { tone: Tone; value: string; source: string };
export type BuildingSignal = { tone: Tone; value: string | null; items: Signal[] };

const missing = (state: TwinState, source: string, t: T): Signal => ({ tone: "nodata", value: stateText(state, t), source });

function tasksSignal(d: TwinDepartment, t: T): Signal {
  const p = d.tasks;
  if (p.state !== "ok" || !p.data) return missing(p.state, d.name, t);
  const x = p.data;
  const value = x.overdue > 0 ? t("Просрочено: {n}", { n: x.overdue }) : x.open > 0 ? t("В работе: {n}", { n: x.open }) : t("Нет открытых задач");
  return { tone: tasksTone(x), value, source: d.name };
}

function hrSignal(d: TwinDepartment, t: T): Signal {
  const p = d.hr;
  if (p.state !== "ok" || !p.data) return missing(p.state, d.name, t);
  if (p.data.presentPct == null) return { tone: "info", value: t("Сотрудников: {n}", { n: p.data.employees }), source: d.name };
  return { tone: attendanceTone(p.data.presentPct), value: t("Явка {pct}", { pct: pct(p.data.presentPct) }), source: d.name };
}

function salesSignal(s: TwinSnapshot, source: string, t: T): Signal {
  const p = s.sales;
  if (p.state !== "ok" || !p.data) return missing(p.state, source, t);
  const x = p.data;
  const basis = x.levelBasis === "forecast" ? x.forecastExecution : x.execution;
  if (x.planKg == null || basis == null) return { tone: "info", value: t("Плана нет"), source };
  return {
    tone: levelTone(x.level),
    value: x.levelBasis === "forecast" ? t("Прогноз {pct}", { pct: share(basis) }) : t("План {pct}", { pct: share(basis) }),
    source,
  };
}

function deptSupplySignal(d: TwinDepartment, t: T): Signal {
  const p = d.supply;
  if (p.state !== "ok" || !p.data) return missing(p.state, d.name, t);
  const x = p.data;
  if (x.overdue != null && x.overdue > 0) return { tone: "bad", value: t("Просрочено к закупке: {n}", { n: x.overdue }), source: d.name };
  if (x.urgent > 0) return { tone: "warn", value: t("Срочных заявок: {n}", { n: x.urgent }), source: d.name };
  return { tone: "ok", value: x.open > 0 ? t("Заявок: {n}", { n: x.open }) : t("Нет открытых заявок"), source: d.name };
}

/** Отдел снабжения отвечает за закупки всей компании — его здание показывает путь закупки целиком. */
function companySupplySignal(s: TwinSnapshot, source: string, t: T): Signal {
  const p = s.supply;
  if (p.state !== "ok" || !p.data) return missing(p.state, source, t);
  const x = p.data;
  if (x.overdueToBuy != null && x.overdueToBuy > 0) return { tone: "bad", value: t("Просрочено к закупке: {n}", { n: x.overdueToBuy }), source };
  if (x.urgent > 0) return { tone: "warn", value: t("Срочных заявок: {n}", { n: x.urgent }), source };
  return { tone: "ok", value: x.approval > 0 ? t("На согласовании: {n}", { n: x.approval }) : t("Заявок: {n}", { n: x.open }), source };
}

function stockSignal(s: TwinSnapshot, link: TwinLink, t: T): Signal {
  const p = s.stock;
  if (p.state !== "ok" || !p.data) return missing(p.state, link.name, t);
  const x = p.data;
  // Остатки отдаются по складу по умолчанию (он пока один); другой склад — без данных, а не чужие цифры.
  if (x.warehouseId !== link.id) return missing("nodata", link.name, t);
  if (x.empty > 0) return { tone: "bad", value: t("Нет в наличии: {n}", { n: x.empty }), source: link.name };
  if (x.low > 0) return { tone: "warn", value: t("Ниже минимума: {n}", { n: x.low }), source: link.name };
  return { tone: "ok", value: t("Позиций: {n}", { n: x.items }), source: link.name };
}

function plantSignal(s: TwinSnapshot, link: TwinLink, t: T): Signal {
  const p = s.plant;
  if (p.state !== "ok" || !p.data) return missing(p.state, link.name, t);
  const x = p.data;
  if (String(x.stockId) !== link.id) return missing("nodata", link.name, t);
  if (x.deficit > 0) return { tone: "warn", value: t("Дефицит: {n} SKU", { n: x.deficit }), source: link.name };
  return {
    tone: "ok",
    value: x.daysOfCover != null ? t("Покрытие {n} дн.", { n: int(x.daysOfCover) }) : x.kg != null ? tons(x.kg) : t("SKU: {n}", { n: x.skus }),
    source: link.name,
  };
}

function financeSignal(s: TwinSnapshot, source: string, t: T): Signal {
  const p = s.finance;
  if (p.state !== "ok" || !p.data) return missing(p.state, source, t);
  const x = p.data;
  if (x.overdue > 0) return { tone: "bad", value: t("ПДЗ {sum}", { sum: sum(x.overdue, t) }), source };
  return { tone: "ok", value: t("Долг {sum}", { sum: sum(x.debt, t) }), source };
}

function spendSignal(d: TwinDepartment, t: T): Signal {
  const p = d.spend;
  if (p.state !== "ok" || !p.data) return missing(p.state, d.name, t);
  return { tone: "info", value: t("Закупки {sum}", { sum: sum(p.data.total, t) }), source: d.name };
}

function promoSignal(s: TwinSnapshot, source: string, t: T): Signal {
  const p = s.promo;
  if (p.state !== "ok" || !p.data) return missing(p.state, source, t);
  return { tone: "info", value: t("Акции {sum}", { sum: sum(p.data.total, t) }), source };
}

function layerItems(c: Contents, s: TwinSnapshot, layer: Exclude<LayerKey, "all">, t: T): Signal[] {
  const ds = c.departments;
  switch (layer) {
    case "tasks":
      return ds.map((d) => tasksSignal(d, t));
    case "hr":
      return ds.map((d) => (d.code === "hr" && s.hr.state === "ok" && s.hr.data?.presentPct != null
        ? { tone: attendanceTone(s.hr.data.presentPct), value: t("Явка {pct}", { pct: pct(s.hr.data.presentPct) }), source: d.name }
        : hrSignal(d, t)));
    case "sales":
      return ds.filter((d) => d.code === "sales").map((d) => salesSignal(s, d.name, t));
    case "supply":
      return ds.map((d) => (d.code === "supply" ? companySupplySignal(s, d.name, t) : deptSupplySignal(d, t)));
    case "warehouse":
      return [...c.warehouses.map((l) => stockSignal(s, l, t)), ...c.plants.map((l) => plantSignal(s, l, t))];
    case "production":
      return ds.filter((d) => d.code === "production").map((d) => ({ tone: "nodata" as Tone, value: t("Нет данных о выпуске"), source: d.name }));
    case "finance":
      return ds.map((d) => (d.code === "finance" ? financeSignal(s, d.name, t) : spendSignal(d, t)));
    case "marketing":
      return ds.filter((d) => d.code === "marketing").map((d) => promoSignal(s, d.name, t));
  }
}

/**
 * Цвет и подпись здания в слое: худший из сигналов отделов и складов здания, подпись — его значение. Здание вне слоя — off; не привязанное —
 * off и «Не привязано». «Все» — худшее по всем слоям.
 */
export function signalOf(object: TwinObject, s: TwinSnapshot, layer: LayerKey, t: T): BuildingSignal {
  if (object.links.length === 0) return { tone: "off", value: t("Не привязано"), items: [] };
  const c = contentsOf(object, s);
  const items =
    layer === "all"
      ? LAYERS.filter((l) => l.key !== "all").flatMap((l) => layerItems(c, s, l.key as Exclude<LayerKey, "all">, t))
      : layerItems(c, s, layer, t);
  if (items.length === 0) return { tone: "off", value: null, items };
  const tone = worst(items.map((i) => i.tone));
  return { tone, value: items.find((i) => i.tone === tone)?.value ?? null, items };
}

// ---------- KPI предприятия ----------

export type Kpi = { key: string; label: string; value: string; sub: string | null; tone: Tone; href: string | null; state: TwinState };

/** Верхняя полоса: показатели предприятия. Без права на раздел плитки нет; выпуска в OneBase нет — плитка «Нет данных» есть всегда. */
export function enterpriseKpis(s: TwinSnapshot, t: T): Kpi[] {
  const list: Kpi[] = [];
  const push = (k: Kpi) => k.state !== "noaccess" && list.push(k);
  const none = (key: string, label: string, state: TwinState, href: string | null): Kpi => ({ key, label, value: stateText(state, t), sub: null, tone: "nodata", href, state });

  const sales = s.sales;
  if (sales.state === "ok" && sales.data) {
    const x = sales.data;
    push({
      key: "sales",
      label: t("Продажи · план месяца"),
      value: x.execution != null ? share(x.execution) : t("Плана нет"),
      sub: !x.closed && x.forecastExecution != null
        ? t("прогноз {pct} · по {date}", { pct: share(x.forecastExecution), date: dayMonth(x.dataThrough) })
        : x.closed
          ? t("месяц закрыт")
          : t("данные по {date}", { date: dayMonth(x.dataThrough) }),
      tone: x.planKg == null ? "info" : levelTone(x.level),
      href: "/sales",
      state: "ok",
    });
    push({ key: "revenue", label: t("Выручка месяца"), value: sum(x.revenue, t), sub: t("АКБ {n}", { n: int(x.akb) }), tone: "info", href: "/sales", state: "ok" });
  } else push(none("sales", t("Продажи · план месяца"), sales.state, "/sales"));

  const primary = s.primary;
  if (primary.state === "ok" && primary.data) {
    const x = primary.data;
    const basis = x.forecastExecution ?? x.execution;
    push({
      key: "primary",
      label: t("Отгрузки завода"),
      value: tons(x.kg),
      sub: x.hasPlan && basis != null ? t("{pct} плана", { pct: share(basis) }) : t("плана первички нет"),
      tone: "info",
      href: "/sales/primary",
      state: "ok",
    });
  } else push(none("primary", t("Отгрузки завода"), primary.state, "/sales/primary"));

  push({ key: "output", label: t("Выпуск продукции"), value: t("Нет данных"), sub: t("в OneBase не ведётся"), tone: "nodata", href: null, state: "nodata" });

  const hr = s.hr;
  if (hr.state === "ok" && hr.data && hr.data.presentPct != null) {
    const x = hr.data;
    push({
      key: "hr",
      label: x.seesAll ? t("Явка сегодня") : t("Явка отдела сегодня"),
      value: pct(x.presentPct!),
      sub: t("{present} из {scheduled}", { present: int(x.present), scheduled: int(x.scheduled) }),
      tone: attendanceTone(x.presentPct!),
      href: "/hr",
      state: "ok",
    });
  } else push(none("hr", t("Явка сегодня"), hr.state === "ok" ? "nodata" : hr.state, "/hr"));

  const tasks = s.tasks;
  if (tasks.state === "ok" && tasks.data) {
    const x = tasks.data;
    push({
      key: "tasks",
      label: t("Активные задачи"),
      value: int(x.open),
      sub: x.overdue > 0 ? t("просрочено {n}", { n: x.overdue }) : t("без просрочек"),
      tone: tasksTone(x),
      href: "/tasks",
      state: "ok",
    });
  } else push(none("tasks", t("Активные задачи"), tasks.state, "/tasks"));

  const supply = s.supply;
  if (supply.state === "ok" && supply.data) {
    const x = supply.data;
    push({
      key: "supply",
      label: t("Заявки на закупку"),
      value: int(x.open),
      sub: x.overdueToBuy != null && x.overdueToBuy > 0 ? t("просрочено к закупке {n}", { n: x.overdueToBuy }) : t("на согласовании {n}", { n: x.approval }),
      tone: x.overdueToBuy != null && x.overdueToBuy > 0 ? "bad" : x.urgent > 0 ? "warn" : "ok",
      href: x.readsAll ? "/procurement/buy" : "/requests",
      state: "ok",
    });
  } else push(none("supply", t("Заявки на закупку"), supply.state, "/requests"));

  const stock = s.stock;
  if (stock.state === "ok" && stock.data) {
    const x = stock.data;
    push({
      key: "stock",
      label: t("Склад снабжения"),
      value: x.low > 0 ? t("{n} ниже мин.", { n: x.low }) : t("в норме"),
      sub: x.empty > 0 ? t("нет в наличии {n}", { n: x.empty }) : t("позиций {n}", { n: int(x.items) }),
      tone: x.empty > 0 ? "bad" : x.low > 0 ? "warn" : "ok",
      href: "/procurement/stock?filter=low",
      state: "ok",
    });
  } else push(none("stock", t("Склад снабжения"), stock.state, "/procurement/stock"));

  const finance = s.finance;
  if (finance.state === "ok" && finance.data) {
    const x = finance.data;
    push({
      key: "finance",
      label: t("ПДЗ дилеров"),
      value: sum(x.overdue, t),
      sub: t("{pct} долга", { pct: share(x.overdueShare) }),
      tone: x.overdue > 0 ? "bad" : "ok",
      href: "/sales/dso",
      state: "ok",
    });
  } else push(none("finance", t("ПДЗ дилеров"), finance.state, "/sales/dso"));

  return list;
}

// ---------- Карточка здания ----------

export type Metric = { label: string; value: string; state: TwinState; tone?: Tone; hint?: string; href?: string };
export type Section = { key: string; title: string; kind: "department" | "warehouse" | "plant" | "domain"; href?: string; metrics: Metric[] };
export type Problem = { tone: "bad" | "warn"; text: string; source: string; href: string | null };
export type Card = { sections: Section[]; problems: Problem[] };

export type CardOptions = {
  /** Куда ведёт «Открыть раздел» отдела (по правам пользователя). */
  departmentHref: (code: string) => string;
};

const ok = (label: string, value: string, extra: Omit<Metric, "label" | "value" | "state"> = {}): Metric => ({ label, value, state: "ok", ...extra });
const absent = (label: string, state: TwinState, t: T, hint?: string): Metric => ({ label, value: stateText(state, t), state, tone: "nodata", hint });

function departmentMetrics(d: TwinDepartment, s: TwinSnapshot, t: T): Metric[] {
  const m: Metric[] = [];
  // Люди
  if (d.hr.state === "ok" && d.hr.data) {
    const h = d.hr.data;
    m.push(ok(t("Сотрудники"), int(h.employees), { hint: t("Verifix · подразделений: {n}", { n: h.divisions }), href: "/hr/employees" }));
    m.push(h.presentPct != null
      ? ok(t("Явка сегодня"), pct(h.presentPct), { tone: attendanceTone(h.presentPct), hint: t("на месте {present} из {scheduled} · опоздали {late}", { present: h.present, scheduled: h.scheduled, late: h.late }), href: "/hr/attendance" })
      : ok(t("Явка сегодня"), t("Нет графика"), { tone: "info" }));
  } else {
    m.push(absent(t("Сотрудники"), d.hr.state, t, d.hr.state === "nodata" ? t("подразделения Verifix не привязаны к отделу") : undefined));
  }
  if (d.users != null) m.push(ok(t("Пользователи OneBase"), int(d.users), { href: "/settings/users" }));

  // Задачи
  if (d.tasks.state === "ok" && d.tasks.data) {
    const x = d.tasks.data;
    m.push(ok(t("Активные задачи"), int(x.open), { hint: t("на проверке {review} · заблокировано {blocked}", { review: x.review, blocked: x.blocked }), href: `/tasks/${d.code}` }));
    m.push(ok(t("Просроченные задачи"), int(x.overdue), { tone: x.overdue > 0 ? tasksTone(x) : undefined, href: `/tasks/${d.code}` }));
  } else {
    m.push(absent(t("Активные задачи"), d.tasks.state, t));
  }

  // Снабжение отдела
  if (d.supply.state === "ok" && d.supply.data) {
    const x = d.supply.data;
    const href = s.supply.data?.readsAll ? `/requests/all?department=${encodeURIComponent(d.code)}&status=open` : "/requests";
    m.push(ok(t("Заявки на снабжение"), int(x.open), { hint: t("на согласовании {approval} · срочных {urgent}", { approval: x.approval, urgent: x.urgent }), href }));
    m.push(ok(t("Потребность в сырье"), int(x.raw), { hint: t("открытых заявок со «Сырьём»"), href }));
    m.push(x.overdue == null
      ? absent(t("Просрочено к закупке"), "noaccess", t)
      : ok(t("Просрочено к закупке"), int(x.overdue), { tone: x.overdue > 0 ? "bad" : undefined, href: "/procurement/buy" }));
  } else {
    m.push(absent(t("Заявки на снабжение"), d.supply.state, t));
  }

  // Деньги: бюджетов в OneBase нет; расходы — только закупки через снабжение.
  m.push(absent(t("Бюджет подразделения"), "nodata", t, t("бюджеты в OneBase не ведутся")));
  m.push(d.spend.state === "ok" && d.spend.data
    ? ok(t("Расходы: закупки месяца"), sum(d.spend.data.total, t), { tone: "info", hint: t("только закупки через снабжение"), href: "/procurement/reports" })
    : absent(t("Расходы: закупки месяца"), d.spend.state, t));
  return m;
}

function domainSection(d: TwinDepartment, s: TwinSnapshot, t: T): Section | null {
  switch (d.code) {
    case "production": {
      const m: Metric[] = [
        absent(t("Производственный план"), "nodata", t, t("в OneBase не ведётся")),
        absent(t("Фактический выпуск"), "nodata", t),
        absent(t("Выполнение плана"), "nodata", t),
        absent(t("Производственная загрузка"), "nodata", t),
      ];
      const p = s.primary;
      m.push(p.state === "ok" && p.data
        ? ok(t("Отгрузки завода (первичка)"), tons(p.data.kg), {
            tone: "info",
            hint: p.data.hasPlan && (p.data.forecastExecution ?? p.data.execution) != null
              ? t("{pct} плана первички · это отгрузки, не выпуск", { pct: share((p.data.forecastExecution ?? p.data.execution)!) })
              : t("это отгрузки, не выпуск"),
            href: "/sales/primary",
          })
        : absent(t("Отгрузки завода (первичка)"), p.state, t));
      const raw = s.stock.data?.rawLow;
      if (s.stock.state === "ok" && raw != null) m.push(ok(t("«Сырьё» на складе ниже минимума"), int(raw), { tone: raw > 0 ? "warn" : undefined, href: "/procurement/stock?filter=low" }));
      return { key: "domain-production", title: t("Выпуск продукции"), kind: "domain", metrics: m };
    }
    case "sales": {
      const p = s.sales;
      if (p.state !== "ok" || !p.data) return { key: "domain-sales", title: t("Продажи месяца"), kind: "domain", href: "/sales", metrics: [absent(t("Выполнение плана"), p.state, t)] };
      const x = p.data;
      return {
        key: "domain-sales",
        title: t("Продажи месяца"),
        kind: "domain",
        href: "/sales",
        metrics: [
          x.execution != null ? ok(t("Выполнение плана"), share(x.execution), { tone: x.levelBasis === "execution" ? levelTone(x.level) : undefined, hint: x.planKg != null ? t("факт {fact} из {plan}", { fact: tons(x.factKg), plan: tons(x.planKg) }) : undefined }) : absent(t("Выполнение плана"), "nodata", t, t("плана на месяц нет")),
          x.forecastExecution != null ? ok(t("Прогноз на конец месяца"), share(x.forecastExecution), { tone: levelTone(x.level) }) : absent(t("Прогноз на конец месяца"), "nodata", t),
          ok(t("Выручка месяца"), sum(x.revenue, t)),
          ok(t("АКБ"), int(x.akb)),
          ok(t("Проблемные агенты"), int(x.critical + x.risk), { tone: x.critical > 0 ? "bad" : x.risk > 0 ? "warn" : "ok", hint: t("критично {c} · риск {r} · вакансий {v}", { c: x.critical, r: x.risk, v: x.vacancies }), href: "/sales/problems" }),
        ],
      };
    }
    case "finance": {
      const p = s.finance;
      if (p.state !== "ok" || !p.data)
        return {
          key: "domain-finance",
          title: t("Дебиторка дилеров"),
          kind: "domain",
          href: "/sales/dso",
          metrics: [
            absent(t("Дебиторская задолженность"), p.state, t, p.state === "nodata" ? t("реестр оплат дилеров не загружен") : undefined),
            absent(t("Бюджеты и расходы компании"), "nodata", t, t("в OneBase не ведутся")),
          ],
        };
      const x = p.data;
      return {
        key: "domain-finance",
        title: t("Дебиторка дилеров"),
        kind: "domain",
        href: "/sales/dso",
        metrics: [
          ok(t("Дебиторская задолженность"), sum(x.debt, t)),
          ok(t("Просрочено (ПДЗ)"), sum(x.overdue, t), { tone: x.overdue > 0 ? "bad" : undefined, hint: t("{pct} долга · дилеров {n}", { pct: share(x.overdueShare), n: x.overdueDealers }) }),
          ok(t("Оплачено за месяц"), sum(x.paidMonth, t)),
          ok(t("Отгружено за месяц"), sum(x.shippedMonth, t)),
          absent(t("Бюджеты и расходы компании"), "nodata", t, t("в OneBase не ведутся")),
        ],
      };
    }
    case "marketing": {
      const p = s.promo;
      return {
        key: "domain-marketing",
        title: t("Маркетинг и акции"),
        kind: "domain",
        href: "/sales/promo",
        metrics: [
          absent(t("Кампании и лиды"), "nodata", t, t("в OneBase не ведутся")),
          absent(t("Рекламные расходы"), "nodata", t),
          p.state === "ok" && p.data
            ? ok(t("Акции: скидки месяца"), sum(p.data.total, t), { tone: "info", hint: p.data.toSales != null ? t("{pct} от продаж · акций {n}", { pct: share(p.data.toSales), n: p.data.promos }) : t("акций {n}", { n: p.data.promos }) })
            : absent(t("Акции: скидки месяца"), p.state, t),
        ],
      };
    }
    case "supply": {
      const p = s.supply;
      if (p.state !== "ok" || !p.data) return { key: "domain-supply", title: t("Снабжение компании"), kind: "domain", href: "/procurement", metrics: [absent(t("Открытые заявки"), p.state, t)] };
      const x = p.data;
      return {
        key: "domain-supply",
        title: t("Снабжение компании"),
        kind: "domain",
        href: x.readsAll ? "/procurement" : "/requests",
        metrics: [
          ok(t("Открытые заявки"), int(x.open), { hint: t("срочных {urgent} · на доработке {revision}", { urgent: x.urgent, revision: x.revision }) }),
          ok(t("На согласовании"), int(x.approval)),
          x.overdueToBuy == null ? absent(t("Просрочено к закупке"), "noaccess", t) : ok(t("Просрочено к закупке"), int(x.overdueToBuy), { tone: x.overdueToBuy > 0 ? "bad" : undefined, href: "/procurement/buy" }),
          x.spendMonth == null ? absent(t("Закупки за месяц"), "noaccess", t) : ok(t("Закупки за месяц"), sum(x.spendMonth, t), { tone: "info", href: "/procurement/reports" }),
        ],
      };
    }
    case "hr": {
      const p = s.hr;
      if (p.state !== "ok" || !p.data) return { key: "domain-hr", title: t("Персонал компании"), kind: "domain", href: "/hr", metrics: [absent(t("Явка сегодня"), p.state, t)] };
      const x = p.data;
      return {
        key: "domain-hr",
        title: x.seesAll ? t("Персонал компании") : t("Персонал вашего отдела"),
        kind: "domain",
        href: "/hr",
        metrics: [
          x.presentPct != null ? ok(t("Явка сегодня"), pct(x.presentPct), { tone: attendanceTone(x.presentPct) }) : ok(t("Явка сегодня"), t("Нет графика"), { tone: "info" }),
          ok(t("Сотрудники"), int(x.active)),
          ok(t("Опоздали"), int(x.late), { tone: x.late > 0 ? "warn" : undefined, href: "/hr/deviations" }),
          ok(t("Отсутствуют"), int(x.absent), { tone: x.absent > 0 ? "warn" : undefined, href: "/hr/deviations" }),
        ],
      };
    }
    default:
      return null;
  }
}

function warehouseSection(link: TwinLink, s: TwinSnapshot, t: T): Section {
  const p = s.stock;
  const title = t("Склад снабжения · {name}", { name: link.name });
  if (p.state !== "ok" || !p.data || p.data.warehouseId !== link.id) {
    return { key: `wh-${link.id}`, title, kind: "warehouse", href: "/procurement/stock", metrics: [absent(t("Остатки"), p.state === "ok" ? "nodata" : p.state, t)] };
  }
  const x = p.data;
  return {
    key: `wh-${link.id}`,
    title,
    kind: "warehouse",
    href: "/procurement/stock",
    metrics: [
      ok(t("Позиций на складе"), int(x.items)),
      ok(t("Ниже минимума"), int(x.low), { tone: x.low > 0 ? "warn" : undefined, href: "/procurement/stock?filter=low" }),
      ok(t("Нет в наличии"), int(x.empty), { tone: x.empty > 0 ? "bad" : undefined, href: "/procurement/stock?filter=empty" }),
      x.rawLow == null ? absent(t("«Сырьё» ниже минимума"), "nodata", t) : ok(t("«Сырьё» ниже минимума"), int(x.rawLow), { tone: x.rawLow > 0 ? "warn" : undefined }),
      ok(t("Заявок к выдаче"), int(x.toIssue), { href: "/procurement/issue" }),
      absent(t("Стоимость запасов"), "nodata", t, t("склад ведётся в количестве, без оценки")),
    ],
  };
}

function plantSection(link: TwinLink, s: TwinSnapshot, t: T): Section {
  const p = s.plant;
  const title = t("Готовая продукция · {name}", { name: link.name });
  if (p.state !== "ok" || !p.data || String(p.data.stockId) !== link.id) {
    return { key: `pl-${link.id}`, title, kind: "plant", href: "/sales/stock?scope=plant", metrics: [absent(t("Остаток"), p.state === "ok" ? "nodata" : p.state, t)] };
  }
  const x = p.data;
  return {
    key: `pl-${link.id}`,
    title,
    kind: "plant",
    href: "/sales/stock?scope=plant",
    metrics: [
      x.kg != null ? ok(t("Остаток"), tons(x.kg)) : absent(t("Остаток"), "nodata", t),
      x.daysOfCover != null ? ok(t("Дней покрытия"), int(x.daysOfCover)) : absent(t("Дней покрытия"), "nodata", t),
      ok(t("SKU с дефицитом"), int(x.deficit), { tone: x.deficit > 0 ? "warn" : undefined, hint: t("меньше 15 дней продаж") }),
      ok(t("Затоварка"), int(x.overstock), { hint: t("больше 30 дней продаж") }),
      ok(t("Без продаж"), int(x.dead)),
    ],
  };
}

function problemsOf(c: Contents, s: TwinSnapshot, t: T, departmentHref: (code: string) => string): Problem[] {
  const list: Problem[] = [];
  for (const d of c.departments) {
    const x = d.tasks.data;
    if (d.tasks.state === "ok" && x) {
      if (x.overdue > 0) list.push({ tone: tasksTone(x) === "bad" ? "bad" : "warn", text: t("Просроченных задач: {n}", { n: x.overdue }), source: d.name, href: `/tasks/${d.code}` });
      if (x.blocked > 0) list.push({ tone: "warn", text: t("Заблокированных задач: {n}", { n: x.blocked }), source: d.name, href: `/tasks/${d.code}` });
    }
    const sp = d.supply.data;
    if (d.supply.state === "ok" && sp) {
      if (sp.overdue != null && sp.overdue > 0) list.push({ tone: "bad", text: t("Просрочена закупка по заявкам: {n}", { n: sp.overdue }), source: d.name, href: "/procurement/buy" });
      if (sp.revision > 0) list.push({ tone: "warn", text: t("Заявок на доработке: {n}", { n: sp.revision }), source: d.name, href: "/requests" });
    }
    const h = d.hr.data;
    if (d.hr.state === "ok" && h) {
      if (h.absent > 0) list.push({ tone: "warn", text: t("Отсутствуют сегодня: {n}", { n: h.absent }), source: d.name, href: "/hr/deviations" });
      if (h.late > 0) list.push({ tone: "warn", text: t("Опоздали сегодня: {n}", { n: h.late }), source: d.name, href: "/hr/deviations" });
    }
    if (d.code === "sales" && s.sales.state === "ok" && s.sales.data) {
      const x2 = s.sales.data;
      const basis = x2.levelBasis === "forecast" ? x2.forecastExecution : x2.execution;
      if (x2.level === "Bad" && basis != null)
        list.push({ tone: "bad", text: x2.levelBasis === "forecast" ? t("Прогноз продаж — {pct} плана", { pct: share(basis) }) : t("Продажи — {pct} плана", { pct: share(basis) }), source: d.name, href: "/sales" });
      if (x2.critical > 0) list.push({ tone: "warn", text: t("Агентов в критической зоне: {n}", { n: x2.critical }), source: d.name, href: "/sales/problems" });
    }
    if (d.code === "finance" && s.finance.state === "ok" && s.finance.data && s.finance.data.overdue > 0)
      list.push({ tone: "bad", text: t("ПДЗ дилеров: {sum}", { sum: sum(s.finance.data.overdue, t) }), source: d.name, href: "/sales/dso" });
    if (d.code === "supply" && s.supply.state === "ok" && s.supply.data?.overdueToBuy)
      list.push({ tone: "bad", text: t("Просрочено к закупке по компании: {n}", { n: s.supply.data.overdueToBuy }), source: d.name, href: "/procurement/buy" });
  }
  for (const l of c.warehouses) {
    const x = s.stock.data;
    if (s.stock.state !== "ok" || !x || x.warehouseId !== l.id) continue;
    if (x.empty > 0) list.push({ tone: "bad", text: t("Нет в наличии: {n} поз.", { n: x.empty }), source: l.name, href: "/procurement/stock?filter=empty" });
    if (x.low > 0) list.push({ tone: "warn", text: t("Ниже минимума: {n} поз.", { n: x.low }), source: l.name, href: "/procurement/stock?filter=low" });
  }
  for (const l of c.plants) {
    const x = s.plant.data;
    if (s.plant.state === "ok" && x && String(x.stockId) === l.id && x.deficit > 0)
      list.push({ tone: "warn", text: t("Дефицит готовой продукции: {n} SKU", { n: x.deficit }), source: l.name, href: "/sales/stock?scope=plant" });
  }
  // Находки AI: категория — код отдела; по отделу — самые важные, остальные — одной строкой со ссылкой на AI Dashboard.
  for (const d of c.departments) {
    const found = (s.alerts.data?.items ?? []).filter((a) => a.category === d.code);
    for (const a of found.slice(0, AI_PER_DEPARTMENT))
      list.push({ tone: a.severity === "Critical" ? "bad" : "warn", text: t(a.title), source: t("AI · {name}", { name: d.name }), href: a.href ?? departmentHref(d.code) });
    if (found.length > AI_PER_DEPARTMENT)
      list.push({ tone: "warn", text: t("Ещё находок AI: {n}", { n: found.length - AI_PER_DEPARTMENT }), source: t("AI · {name}", { name: d.name }), href: "/ai" });
  }
  return list.sort((a, b) => (a.tone === b.tone ? 0 : a.tone === "bad" ? -1 : 1));
}

/** Карточка здания: раздел каждого отдела (свои показатели и показатели его направления), склады и «Проблемы и предупреждения». */
export function cardOf(object: TwinObject, s: TwinSnapshot, t: T, o: CardOptions): Card {
  const c = contentsOf(object, s);
  const sections: Section[] = [];
  for (const d of c.departments) {
    const domain = domainSection(d, s, t);
    if (domain) sections.push(domain);
    sections.push({ key: `dept-${d.id}`, title: t("Отдел «{name}»", { name: d.name }), kind: "department", href: o.departmentHref(d.code), metrics: departmentMetrics(d, s, t) });
  }
  for (const l of c.warehouses) sections.push(warehouseSection(l, s, t));
  for (const l of c.plants) sections.push(plantSection(l, s, t));
  return { sections, problems: problemsOf(c, s, t, o.departmentHref) };
}

/** «Обновлено 2 мин назад» — по часам браузера. */
export function ago(iso: string, now: number, t: T): string {
  const minutes = Math.floor((now - Date.parse(iso)) / 60_000);
  if (minutes < 1) return t("только что");
  if (minutes < 60) return t("{n} мин назад", { n: minutes });
  return t("{n} ч назад", { n: Math.floor(minutes / 60) });
}
