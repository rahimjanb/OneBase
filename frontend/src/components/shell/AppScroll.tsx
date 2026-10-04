"use client";

import { useEffect, useLayoutEffect, useRef, useState, useTransition } from "react";
import { usePathname, useRouter } from "next/navigation";
import { RefreshCw } from "lucide-react";

/**
 * Оболочка как у приложения: верхняя и нижняя панели стоят на месте, прокручивается только содержимое — элемент
 * с атрибутом data-app-scroll. Документ не прокручивается (globals.css), поэтому то, что браузер делал для документа,
 * делаем здесь: прокрутку наверх при переходе, восстановление позиции по «Назад», обновление жестом «потянуть вниз».
 */
const scroller = () => document.querySelector<HTMLElement>("[data-app-scroll]");

/**
 * Новая страница открывается сверху; по «Назад»/«Вперёд» возвращается позиция, на которой ушли. Смена только параметров
 * (?date=, фильтры) позицию не трогает — как в Next.js для документа.
 */
export function ScrollManager() {
  const pathname = usePathname();
  const positions = useRef(new Map<string, number>());
  // Пока идёт восстановление после «Назад»/«Вперёд» (метка времени окончания): позицию не сбрасываем и не записываем.
  const restoreUntil = useRef(0);
  const lastLocation = useRef<string | null>(null);
  // Куда вернуться по «Назад»/«Вперёд», если страница придёт с сервера позже: до этого Next.js держит на экране прежнюю.
  const pending = useRef<{ key: string; target: number; deadline: number } | null>(null);
  // Номер перехода: ожидание высоты для прежней страницы не трогает следующую.
  const generation = useRef(0);

  useEffect(() => {
    const el = scroller();
    if (!el) return;
    let frame = 0;
    const key = () => window.location.pathname + window.location.search;
    // Переход по ссылке: Next.js рисует новую страницу раньше, чем меняет адрес, и сброс прокрутки записался бы как
    // позиция прежней. Поэтому позицию страницы, с которой уходят, фиксируем в момент нажатия — до смены адреса.
    let frozen: { key: string; until: number } | null = null;
    const onClick = (e: MouseEvent) => {
      if (e.button !== 0 || e.metaKey || e.ctrlKey || e.shiftKey || e.altKey) return;
      const link = e.target instanceof Element ? e.target.closest<HTMLAnchorElement>("a[href]") : null;
      if (!link || link.target === "_blank") return;
      positions.current.set(key(), el.scrollTop);
      frozen = { key: key(), until: performance.now() + 3000 };
    };
    const onScroll = () => {
      if (performance.now() < restoreUntil.current || frame) return;
      frame = requestAnimationFrame(() => {
        frame = 0;
        const current = key();
        if (frozen && frozen.key === current && performance.now() < frozen.until) return;
        positions.current.set(current, el.scrollTop);
      });
    };
    // «Назад»/«Вперёд»: позицию возвращаем сами. Next.js дорисовывает страницу асинхронно и может ещё раз перерисовать
    // старую, поэтому позиция удерживается несколько кадров после первой удачной установки (до 1,5 с). Любое касание
    // (в том числе нижней панели — переход на другую страницу) и колесо отменяют.
    const onPop = () => {
      const target = positions.current.get(key()) ?? 0;
      const started = performance.now();
      pending.current = { key: key(), target, deadline: started + 20_000 };
      restoreUntil.current = started + 1500;
      let applied = 0;
      let cancelled = false;
      const stop = () => {
        cancelled = true;
        restoreUntil.current = 0;
      };
      window.addEventListener("pointerdown", stop, { capture: true, once: true });
      el.addEventListener("wheel", stop, { passive: true, once: true });
      const finish = () => {
        restoreUntil.current = 0;
        window.removeEventListener("pointerdown", stop, { capture: true });
        el.removeEventListener("wheel", stop);
      };
      // Страницу из кэша React вставляет ещё до этого обработчика, поэтому ждать изменений нельзя — позицию ставим сразу,
      // как хватит высоты. Если страница идёт с сервера, её позицию поставит ожидание ниже (pending).
      const step = () => {
        if (cancelled) return finish();
        const now = performance.now();
        if (el.scrollHeight - el.clientHeight >= target) {
          if (Math.abs(el.scrollTop - target) > 1) el.scrollTop = target;
          applied ||= now;
        }
        if ((applied && now - applied > 400) || now - started > 1500) return finish();
        requestAnimationFrame(step);
      };
      requestAnimationFrame(step);
    };
    el.addEventListener("scroll", onScroll, { passive: true });
    document.addEventListener("click", onClick, true);
    window.addEventListener("popstate", onPop);
    return () => {
      el.removeEventListener("scroll", onScroll);
      document.removeEventListener("click", onClick, true);
      window.removeEventListener("popstate", onPop);
      if (frame) cancelAnimationFrame(frame);
    };
  }, []);

  // Новая страница (ссылка, переход из кода) — сверху; страница, к которой вернулись по «Назад», — на прежней позиции,
  // даже если пришла с сервера через несколько секунд. Сверяемся с адресом в браузере, а не с промежуточным значением
  // usePathname: во время перехода Next.js может кратко отдать прежний путь.
  useLayoutEffect(() => {
    const el = scroller();
    const current = window.location.pathname;
    const previous = lastLocation.current;
    lastLocation.current = current;
    if (!el || previous === null || previous === current) return;
    const run = ++generation.current;
    const wanted = pending.current;
    pending.current = null;
    if (wanted && wanted.key === current + window.location.search && performance.now() < wanted.deadline) {
      // Позицию ставим, когда хватит высоты: сначала может прийти заготовка страницы (loading, aria-busy), данные — позже.
      // Ждём, пока заготовка уйдёт и данные дорисуются (ещё до 1,5 с), но не дольше 10 с. Страница стала короче не больше
      // чем на полэкрана — ставим как можно ближе; намного короче или не дождались — остаёмся наверху.
      // Касание, колесо и переход на другую страницу отменяют.
      const until = performance.now() + 10_000;
      let loadedAt = 0;
      let cancelled = false;
      const stop = () => {
        cancelled = true;
      };
      window.addEventListener("pointerdown", stop, { capture: true, once: true });
      el.addEventListener("wheel", stop, { passive: true, once: true });
      const step = () => {
        const now = performance.now();
        const live = !cancelled && run === generation.current;
        const room = el.scrollHeight - el.clientHeight;
        const loading = !!el.querySelector('[aria-busy="true"]');
        if (!loading && !loadedAt) loadedAt = now;
        if (live && room < wanted.target && now < until && (loading || now - loadedAt < 1500)) {
          requestAnimationFrame(step);
          return;
        }
        window.removeEventListener("pointerdown", stop, { capture: true });
        el.removeEventListener("wheel", stop);
        if (!live || loading || room + el.clientHeight / 2 < wanted.target) return;
        restoreUntil.current = Math.max(restoreUntil.current, now + 150);
        el.scrollTop = wanted.target;
      };
      step();
      return;
    }
    if (performance.now() < restoreUntil.current) return;
    el.scrollTop = 0;
  }, [pathname]);

  return null;
}

/**
 * «Потянуть вниз — обновить» в области прокрутки: в установленном приложении другой кнопки обновления нет, а системный
 * жест браузера пропадает, когда документ не прокручивается. Только на сенсорных экранах.
 */
export function PullToRefresh() {
  const router = useRouter();
  const [pull, setPull] = useState(0);
  const [pending, startTransition] = useTransition();

  useEffect(() => {
    const el = scroller();
    if (!el || !window.matchMedia("(pointer: coarse)").matches) return;
    let startY = 0;
    let tracking = false;
    let distance = 0;
    // Касание во вложенной вертикальной прокрутке (список в карточке, выпадающий список), прокрученной не до верха, —
    // жест её, а не обновления страницы.
    const inScrolledChild = (target: EventTarget | null) => {
      for (let node = target instanceof Element ? target : null; node && node !== el; node = node.parentElement) {
        if (node.scrollTop > 0 && node.scrollHeight > node.clientHeight + 1 && /(auto|scroll)/.test(getComputedStyle(node).overflowY)) return true;
      }
      return false;
    };
    const onStart = (e: TouchEvent) => {
      // Не мешаем жестам карты и элементов со своей прокруткой (data-no-ptr) и не обновляем под открытой шторкой.
      const inside = e.target instanceof Element && e.target.closest(".leaflet-container, [data-no-ptr]");
      tracking = !inside && el.scrollTop <= 0 && e.touches.length === 1 && !document.querySelector("[role=dialog]") && !inScrolledChild(e.target);
      startY = e.touches[0]?.clientY ?? 0;
      distance = 0;
    };
    const onMove = (e: TouchEvent) => {
      if (!tracking) return;
      const dy = (e.touches[0]?.clientY ?? 0) - startY;
      if (dy <= 0 || el.scrollTop > 0) {
        if (distance) setPull((distance = 0));
        return;
      }
      distance = Math.min(dy * 0.5, 90);
      setPull(distance);
    };
    const onEnd = () => {
      if (!tracking) return;
      tracking = false;
      // Без связи refresh перезагрузил бы страницу с потерей введённого — тогда не обновляем.
      if (distance >= 60 && navigator.onLine) startTransition(() => router.refresh());
      distance = 0;
      setPull(0);
    };
    el.addEventListener("touchstart", onStart, { passive: true });
    el.addEventListener("touchmove", onMove, { passive: true });
    el.addEventListener("touchend", onEnd);
    el.addEventListener("touchcancel", onEnd);
    return () => {
      el.removeEventListener("touchstart", onStart);
      el.removeEventListener("touchmove", onMove);
      el.removeEventListener("touchend", onEnd);
      el.removeEventListener("touchcancel", onEnd);
    };
  }, [router]);

  if (pull === 0 && !pending) return null;
  const ready = pull >= 60;
  return (
    <div className="pointer-events-none absolute inset-x-0 top-2 z-20 flex justify-center" aria-live="polite">
      <span
        className="grid size-9 place-items-center rounded-full border border-line bg-surface text-accent-strong shadow-md"
        style={{ transform: pending ? undefined : `translateY(${Math.max(0, pull - 30)}px)`, opacity: pending ? 1 : Math.min(1, pull / 50) }}
      >
        <RefreshCw className={`size-4 ${pending ? "animate-spin" : ""}`} style={pending ? undefined : { transform: `rotate(${pull * 4}deg)` }} />
        <span className="sr-only">{pending ? "Обновляем" : ready ? "Отпустите, чтобы обновить" : "Потяните, чтобы обновить"}</span>
      </span>
    </div>
  );
}

/**
 * Открыта экранная клавиатура — нижнюю панель прячем, как в приложениях. Клавиатура — это фокус в текстовом поле
 * и заметно уменьшившийся экран: на Android фокус остаётся в поле и после того, как клавиатуру закрыли кнопкой «назад»,
 * а поля даты и времени открывают выбор, а не клавиатуру.
 */
export function useKeyboardOpen() {
  const [open, setOpen] = useState(false);
  useEffect(() => {
    if (!window.matchMedia("(pointer: coarse)").matches) return;
    const nonText = ["checkbox", "radio", "button", "submit", "reset", "range", "color", "file", "image", "date", "time", "datetime-local", "month", "week"];
    const editable = (t: Element | null) => {
      if (!(t instanceof HTMLElement)) return false;
      if (t.isContentEditable || t.tagName === "TEXTAREA") return true;
      return t.tagName === "INPUT" && !nonText.includes((t as HTMLInputElement).type);
    };
    // Высота без клавиатуры — наибольшая в этой ориентации; клавиатура съедает заметно больше, чем строка адреса.
    const height = () => Math.min(window.innerHeight, window.visualViewport?.height ?? window.innerHeight);
    let full = height();
    let timer: ReturnType<typeof setTimeout> | undefined;
    // Фокус переходит между полями через focusout → focusin, клавиатура выезжает с анимацией — проверяем чуть позже.
    const check = () => {
      clearTimeout(timer);
      timer = setTimeout(() => {
        const h = height();
        full = Math.max(full, h);
        setOpen(editable(document.activeElement) && full - h > 120);
      }, 120);
    };
    // Поворот — новая высота «без клавиатуры». Ориентация — экрана устройства: медиазапрос (orientation) считает по окну,
    // и на маленьком телефоне его переворачивает сама клавиатура (окно становится шире, чем выше).
    const rotate = () => {
      full = 0;
      check();
    };
    const orientation = typeof screen !== "undefined" ? screen.orientation : undefined;
    document.addEventListener("focusin", check);
    document.addEventListener("focusout", check);
    window.visualViewport?.addEventListener("resize", check);
    window.addEventListener("resize", check);
    if (orientation) orientation.addEventListener("change", rotate);
    else window.addEventListener("orientationchange", rotate);
    return () => {
      clearTimeout(timer);
      document.removeEventListener("focusin", check);
      document.removeEventListener("focusout", check);
      window.visualViewport?.removeEventListener("resize", check);
      window.removeEventListener("resize", check);
      if (orientation) orientation.removeEventListener("change", rotate);
      else window.removeEventListener("orientationchange", rotate);
    };
  }, []);
  return open;
}
