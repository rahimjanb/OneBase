import { RouteSkeleton } from "@/components/shell/RouteSkeleton";

/** Переход в другой раздел — сразу макет новой страницы (RouteSkeleton), данные приходят следом. */
export default function Loading() {
  return <RouteSkeleton />;
}
