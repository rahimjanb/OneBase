import { RouteSkeleton } from "@/components/shell/RouteSkeleton";

/** Переход между страницами «Продаж»: панель периода остаётся, ниже — сразу макет новой страницы (RouteSkeleton), данные приходят следом. */
export default function Loading() {
  return <RouteSkeleton inSales />;
}
