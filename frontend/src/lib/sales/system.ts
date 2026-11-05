import type { Period } from "./types";

/** «Система» (GET /api/sales/system): маршрут, качество дистрибуции, деньги и запас дилеров за месяц. */

export type SystemRouteRow = {
  id: string;
  name: string;
  /** Плановые визиты по отчётный день. */
  plan: number;
  doneInPlan: number;
  doneOffPlan: number;
  /** Визиты по плану с заказом этого ТП в этой точке в этот день. */
  ordersInPlan: number;
  orders: number;
  agents: SystemRouteRow[];
};

export type SystemQualityRow = {
  id: string;
  name: string;
  akb: number;
  kg: number;
  skuAvg: number | null;
  oneSku: number;
  /** Точки с не меньше coreMin SKU ядра. */
  quality: number;
  twoCategories: number;
  /** Доля веса верхних 20% точек. */
  topShare: number | null;
  topCount: number;
  /** Сколько верхних точек покупали в каждом из прошлых месяцев окна. */
  topRegular: number;
  agents: SystemQualityRow[];
};

export type SystemBucket = { label: string; from: number; to: number | null; reserveShare: number };

export type SystemDealer = {
  name: string;
  region: string | null;
  status: "active" | "gone" | "noterm" | string;
  debt: number;
  overdue: number;
  aging: number[];
  reserve: number;
  factDso: number | null;
  norm: number | null;
  defaultNorm: boolean;
  stockKg: number | null;
  stock1Kg: number | null;
  daysNow: number | null;
  days1: number | null;
  primaryKg: number | null;
  secondaryKg: number | null;
  overstock: boolean;
};

export type SystemView = {
  period: Period;
  routeTotal: SystemRouteRow;
  route: SystemRouteRow[];
  quality: {
    coreMin: number;
    coreShare: number;
    /** «ГГГГ-М», последний — выбранный месяц. */
    months: string[];
    core: { productId: number; name: string; category: string }[];
    total: SystemQualityRow;
    regions: SystemQualityRow[];
  };
  money: {
    asOf: string;
    buckets: SystemBucket[];
    shares: number[];
    debt: number;
    overdue: number;
    reserve: number;
    stockAt: string | null;
    /** Запас «на 1-е» — на эту дату; первичка и вторичка — с неё. */
    stockFrom: string;
    dealers: SystemDealer[];
  } | null;
  /** Нет права «Финансы» — денег в ответе нет. */
  moneyHidden: boolean;
  /** Оплаты дилеров не привязаны — DSO не собран. */
  moneyMissing: boolean;
};
