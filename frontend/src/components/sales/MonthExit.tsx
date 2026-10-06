"use client";

import { usePathname } from "next/navigation";
import { createContext, useContext, useEffect, useMemo, useState } from "react";

/** Куда уходить со страницы при смене месяца: страница (path) и адрес без параметров (to). */
type Exit = { path: string; to: string };

const ExitContext = createContext<{ exit: Exit | null; report: (exit: Exit | null) => void } | null>(null);

/**
 * Сброс при смене месяца (DOC-filters §12): со страницы ТП или магазина переключатель периода уходит к региону (нет региона — к республике).
 * Переключатель — в полосе раздела (layout), а регион знает страница: она сообщает (ReportMonthExit), переключатель читает (useMonthExit).
 */
export function MonthExitProvider({ children }: { children: React.ReactNode }) {
  const [exit, setExit] = useState<Exit | null>(null);
  const value = useMemo(() => ({ exit, report: setExit }), [exit]);
  return <ExitContext.Provider value={value}>{children}</ExitContext.Provider>;
}

/** Страница ТП или магазина сообщает, куда уходить при смене месяца; ничего не рисует. При уходе со страницы сообщение снимается. */
export function ReportMonthExit({ to }: { to: string }) {
  const report = useContext(ExitContext)?.report;
  const pathname = usePathname();
  useEffect(() => {
    report?.({ path: pathname, to });
    return () => report?.(null);
  }, [report, pathname, to]);
  return null;
}

/** Адрес, куда уходить при смене месяца с текущей страницы; null — остаться на странице. */
export function useMonthExit(pathname: string): string | null {
  const exit = useContext(ExitContext)?.exit;
  return exit && exit.path === pathname ? exit.to : null;
}
