"use client";

import { usePathname } from "next/navigation";
import { RecipesView } from "@/components/production/RecipesView";
import { RouteSkeleton } from "@/components/shell/RouteSkeleton";

/**
 * Пока приходят техкарты — тот же экран со скелетонами карточек (шапка, поиск и фильтры видны сразу). Эта граница ждёт и вложенные
 * страницы (карточка, форма, импорт) — для них общий макет по адресу.
 */
export default function Loading() {
  return usePathname() === "/production/recipes" ? <RecipesView data={null} /> : <RouteSkeleton />;
}
