"use client";

import { useT } from "@/i18n/client";

/** Единицы компактных сумм на языке интерфейса. */
export function useUnits() {
  const t = useT();
  return { bn: t("млрд"), mn: t("млн"), th: t("тыс.") };
}
