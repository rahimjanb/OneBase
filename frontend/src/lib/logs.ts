export type LogSource = "linko" | "sync" | "system";

export type LogEntry = {
  id: number;
  timestamp: string;
  /** Warning | Error | Critical */
  level: string;
  source: LogSource;
  category: string;
  message: string;
  exception: string | null;
  traceId: string | null;
};

export type LogsView = {
  days: number;
  items: LogEntry[];
  hasMore: boolean;
  counts: Record<LogSource, { errors: number; warnings: number }>;
};

export const sourceLabels: Record<LogSource, string> = {
  linko: "Интеграция Linko",
  sync: "Синхронизация",
  system: "Система",
};
