/** «ДМП» (GET /api/sales/dmp): стеллажи в магазинах — приход на склады дилеров, остатки, установки и заказы магазинов. Даты — «ГГГГ-ММ-ДД». */

/** Заказы магазина за окно: число заказов, вес и вес категории стеллажа (возвраты вычтены). */
export type DmpOrders = { orders: number; kg: number; focusKg: number };

export type DmpMonthKg = {
  year: number;
  month: number;
  kg: number;
  focusKg: number;
};

export type DmpRegion = {
  region: string;
  received: number;
  receivedDates: string[];
  left: number;
  installed: number;
  issued: number;
  shops: number;
};

export type DmpShop = {
  marketId: number;
  market: string;
  region: string;
  agentId: number | null;
  agent: string | null;
  /** Первая установка (без неё — первая выдача). */
  date: string;
  /** Установлено за вычетом возвратов. */
  qty: number;
  /** Выдано, доставка не подтверждена. */
  issued: number;
  /** Заказ, которым привезли стеллаж. */
  fill: DmpOrders;
  /** Дней от установки до даты данных — длина окон «до» и «после». */
  days: number;
  before: DmpOrders;
  after: DmpOrders;
  lastOrder: string | null;
  idleDays: number | null;
  months: DmpMonthKg[];
};

export type DmpView = {
  asOf: string;
  products: string[];
  /** Категория отчёта, под которую стоит стеллаж (Twizos — «Придзел»); null — не настроена. */
  focusCategory: string | null;
  received: number;
  left: number;
  factoryLeft: number;
  installed: number;
  issued: number;
  shops: number;
  regions: DmpRegion[];
  shopRows: DmpShop[];
  months: DmpMonthKg[];
};
