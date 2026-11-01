import { RouteSkeleton } from "@/components/shell/RouteSkeleton";

/** Переход между страницами заявок — сразу макет новой страницы (RouteSkeleton), данные приходят следом. */
export default function Loading() {
  return <RouteSkeleton />;
}
