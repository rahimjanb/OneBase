"use client";

import { createContext, useContext } from "react";

/**
 * Что можно пользователю в разделе «Продажи» сверх просмотра — для кнопок: выгрузки Excel (право sales.export). Значение кладёт layout
 * раздела из /api/auth/me; по умолчанию закрыто. Кнопка без права не показывается, а сервер всё равно отвечает 403.
 */
const SalesAccessContext = createContext({ canExport: false });

export function SalesAccessProvider({ canExport, children }: { canExport: boolean; children: React.ReactNode }) {
  return <SalesAccessContext.Provider value={{ canExport }}>{children}</SalesAccessContext.Provider>;
}

export const useSalesAccess = () => useContext(SalesAccessContext);
