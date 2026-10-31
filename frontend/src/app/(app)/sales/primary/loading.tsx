import { RouteSkeleton } from "@/components/shell/RouteSkeleton";

/** «Первичка»: республика, экспорт — сразу макет новой страницы (RouteSkeleton), данные приходят следом. */
export default function Loading() {
  return <RouteSkeleton inSales />;
}
