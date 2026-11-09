"use client";

import { createContext, useContext } from "react";

export type HomeKind = "my_day" | "overview" | "work";

const Context = createContext<HomeKind>("overview");

/** Какая главная у пользователя (из /api/auth/me): макет перехода на «/» рисует «Мой день» сотрудника или «Обзор» компании. */
export function HomeKindProvider({ home, children }: { home: HomeKind; children: React.ReactNode }) {
  return <Context.Provider value={home}>{children}</Context.Provider>;
}

export const useHomeKind = () => useContext(Context);
