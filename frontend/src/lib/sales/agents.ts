import type { AgentFlag, Period, TargetLevel } from "./types";

/** Карточка ТП в «Агентах» (GET /api/sales/agents) — те же цифры, что в карточке агента. */
export type AgentCardRow = {
  agentId: number;
  name: string;
  /** null — ТП без региона. */
  regionId: string | null;
  regionName: string;
  directionId: string | null;
  /** РМ: имя менеджера направления, без него — направление. */
  directionName: string | null;
  supervisor: string | null;
  isVacancy: boolean;
  isSalesRep: boolean;
  job: string | null;
  planKg: number | null;
  factKg: number;
  execution: number | null;
  /** Прогноз — только у идущего месяца. */
  forecastKg: number | null;
  forecastExecution: number | null;
  revenue: number;
  revenuePlan: number | null;
  revenueExecution: number | null;
  akb: number;
  visits: number;
  visitsWithOrder: number;
  orders: number;
  /** Страйк: визиты с заказом ÷ выполненные визиты. */
  conversion: number | null;
  sumPerVisit: number | null;
  categories: number;
  /** Темп к своему среднему за три прошлых месяца. */
  tempo: number | null;
  flags: AgentFlag[];
  executionLevel: TargetLevel | null;
  forecastExecutionLevel: TargetLevel | null;
};

export type AgentsView = {
  period: Period;
  targets: { conversion: number; revenuePerOutlet: number; akbPerAgent: number; categoriesPerOutlet: number };
  agents: AgentCardRow[];
  directions: { id: string; name: string }[];
  /** Регионы с ТП; у «без региона» id — пустой Guid. */
  regions: { id: string; name: string }[];
};
