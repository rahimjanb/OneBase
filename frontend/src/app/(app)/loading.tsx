import { RouteSkeleton } from "@/components/shell/RouteSkeleton";

/**
 * Переход между разделами: заготовка страницы появляется сразу после нажатия, пока сервер собирает данные (у разделов со своей
 * заготовкой — «Продажи», HR — действует их loading.tsx).
 */
export default function Loading() {
  return <RouteSkeleton />;
}
