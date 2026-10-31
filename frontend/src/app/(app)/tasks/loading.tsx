import { RouteSkeleton } from "@/components/shell/RouteSkeleton";

/** Переход между страницами задач — сразу макет новой страницы (RouteSkeleton), данные приходят следом. */
export default function Loading() {
  return <RouteSkeleton />;
}
