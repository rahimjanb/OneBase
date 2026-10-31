import { RouteSkeleton } from "@/components/shell/RouteSkeleton";

/** Карточка задачи, в том числе «следующая / предыдущая» — сразу макет новой страницы (RouteSkeleton), данные приходят следом. */
export default function Loading() {
  return <RouteSkeleton />;
}
