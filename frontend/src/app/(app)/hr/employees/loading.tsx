import { RouteSkeleton } from "@/components/shell/RouteSkeleton";

/** Список сотрудников ↔ карточка сотрудника — сразу макет новой страницы (RouteSkeleton), данные приходят следом. */
export default function Loading() {
  return <RouteSkeleton />;
}
