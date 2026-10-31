"use client";

import { useCallback, useEffect, useRef } from "react";
import { usePathname, useSearchParams } from "next/navigation";

const START = "onebase:nav-start";

/** Переход из кода (router.push / refresh): то же, что нажатие на ссылку. */
export function startNavPending() {
  window.dispatchEvent(new Event(START));
}

/**
 * Если за это время адрес не сменился, макета у новой страницы нет (loading.tsx не накрывает переход) и на экране прежняя: она
 * приглушается. Переходы с макетом (RouteSkeleton) меняют адрес сразу — они проходят без приглушения.
 */
const DIM_AFTER = 150;
/** Переход так и не случился (ссылку перехватил обработчик, сервер не ответил) — приглушение снимается само. */
const GIVE_UP = 15_000;

const scroller = () => document.querySelector<HTMLElement>("[data-app-scroll]");

/**
 * Отклик на переход без полоски загрузки, как у браузера. Страницы разделов открываются сразу макетом (loading.tsx → RouteSkeleton),
 * данные приходят следом. Где макета нет — смена фильтра или месяца на той же странице, переход между чатами, — прежнее содержимое
 * приглушается до прихода нового (data-nav-pending на <main>, globals.css). Ещё — обработчик касаний: без него iOS Safari не включает
 * :active, и кнопки не отзываются на нажатие.
 */
export function NavPending() {
  const pathname = usePathname();
  const search = useSearchParams();
  const timers = useRef<number[]>([]);

  const finish = useCallback(() => {
    timers.current.forEach((id) => window.clearTimeout(id));
    timers.current = [];
    scroller()?.removeAttribute("data-nav-pending");
  }, []);

  const start = useCallback(() => {
    finish();
    timers.current.push(
      window.setTimeout(() => scroller()?.setAttribute("data-nav-pending", ""), DIM_AFTER),
      window.setTimeout(finish, GIVE_UP),
    );
  }, [finish]);

  useEffect(() => {
    const touch = () => {};
    const onClick = (e: MouseEvent) => {
      if (e.button !== 0 || e.metaKey || e.ctrlKey || e.shiftKey || e.altKey) return;
      const link = e.target instanceof Element ? e.target.closest<HTMLAnchorElement>("a[href]") : null;
      if (!link || (link.target && link.target !== "_self") || link.hasAttribute("download")) return;
      const url = new URL(link.href, window.location.href);
      if (url.origin !== window.location.origin) return;
      // Та же страница (или только якорь) — перехода нет.
      if (url.pathname === window.location.pathname && url.search === window.location.search) return;
      start();
    };
    document.addEventListener("touchstart", touch, { passive: true });
    // Перехват: обработчик next/link отменяет переход браузера (preventDefault) раньше, чем нажатие дошло бы до document.
    document.addEventListener("click", onClick, true);
    window.addEventListener(START, start);
    return () => {
      document.removeEventListener("touchstart", touch);
      document.removeEventListener("click", onClick, true);
      window.removeEventListener(START, start);
    };
  }, [start]);

  // Адрес сменился — на экране новая страница или её макет.
  useEffect(finish, [pathname, search, finish]);

  useEffect(() => finish, [finish]);

  return null;
}
