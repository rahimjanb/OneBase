"use client";

import { useEffect, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { Camera, ChevronLeft, ChevronRight, ImagePlus, Loader2, Package, RefreshCw, Star, Trash2, X } from "lucide-react";
import { useT } from "@/i18n/client";
import { photoSrcSet, photoUrl, type ProductPhoto } from "@/lib/sales/product-photos";
import { usePhotoPicker, useProductPhotos } from "./PhotosContext";

/*
 * Фото товара (ТЗ «Продажи · Ассортимент — фото», раздел 7): товар — вырезка без фона, поэтому вписывается целиком (contain) на светлую
 * «студийную» подложку в обеих темах, как в макете; пока грузится — подложка пульсирует, после — плавное появление. Нет фото или файл не
 * открылся — заглушка с упаковкой (у кого есть право менять — кнопка «Добавить фото»); не открывшийся файл больше не запрашивается.
 */

const failed = new Set<string>();

/** Светлая подложка фото: товар на ней читается и в тёмной теме. */
export const photoBackdrop = "bg-[radial-gradient(120%_95%_at_50%_28%,#ffffff_0%,#edf0f5_55%,#dde2ea_100%)]";

/**
 * Заглушка «нет фото». onAdd — у кого есть право менять: заглушка становится кнопкой «Добавить фото» (внутри ссылки-карточки нажатие
 * не открывает товар). busy — фото загружается.
 */
export function NoPhoto({ icon = 24, label = false, onAdd, busy = false }: { icon?: number; label?: boolean; onAdd?: () => void; busy?: boolean }) {
  const t = useT();
  const box = (Icon: typeof Package) => (
    <span aria-hidden className="grid place-items-center rounded-xl bg-surface/60 text-ink-4" style={{ width: icon * 2, height: icon * 2 }}>
      {busy ? <Loader2 className="animate-spin" style={{ width: icon, height: icon }} /> : <Icon style={{ width: icon, height: icon }} strokeWidth={1.6} />}
    </span>
  );
  if (onAdd) {
    return (
      <button
        type="button"
        disabled={busy}
        aria-label={t("Добавить фото")}
        title={t("Добавить фото")}
        onClick={(e) => {
          e.preventDefault();
          e.stopPropagation();
          onAdd();
        }}
        className="group/add flex size-full flex-col items-center justify-center gap-1.5 bg-muted outline-none transition-colors hover:bg-accent-soft focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-accent"
      >
        {box(Camera)}
        {label && <span className="text-[10px] font-semibold text-accent-strong">{t(busy ? "Загружаем…" : "Добавить фото")}</span>}
      </button>
    );
  }
  return (
    <span role="img" aria-label={t("Нет фото")} className="flex size-full flex-col items-center justify-center gap-1.5 bg-muted">
      {box(Package)}
      {label && <span className="text-[10px] font-medium text-ink-4">{t("Нет фото")}</span>}
    </span>
  );
}

/**
 * Картинка товара во весь контейнер (contain, с отступом), с подложкой. sizes — как в srcset; priority — первые карточки: высокий приоритет
 * загрузки (ленивая загрузка остаётся: скрытый вариант раскладки, телефон или компьютер, картинку не грузит).
 */
export function ProductImage({
  photo,
  alt,
  sizes,
  priority = false,
  pad = "p-[8%]",
  emptyIcon = 24,
  emptyLabel = false,
  dim = false,
  onAdd,
  busy = false,
}: {
  photo: ProductPhoto | null | undefined;
  alt: string;
  sizes: string;
  priority?: boolean;
  pad?: string;
  emptyIcon?: number;
  emptyLabel?: boolean;
  /** Пропавший товар: фото приглушено. */
  dim?: boolean;
  /** Нет фото и можно менять — «Добавить фото». */
  onAdd?: () => void;
  busy?: boolean;
}) {
  const [state, setState] = useState<"loading" | "ready" | "error">(photo && failed.has(photo.file) ? "error" : "loading");
  const img = useRef<HTMLImageElement>(null);
  // Новое фото (загрузили, сделали главным) — заново «грузится».
  useEffect(() => {
    setState(photo && failed.has(photo.file) ? "error" : img.current?.complete && img.current.naturalWidth > 0 ? "ready" : "loading");
  }, [photo?.file]); // eslint-disable-line react-hooks/exhaustive-deps

  if (!photo || state === "error") return <NoPhoto icon={emptyIcon} label={emptyLabel} onAdd={onAdd} busy={busy} />;
  return (
    <span className={`relative block size-full overflow-hidden ${photoBackdrop} ${state === "loading" ? "animate-pulse" : ""}`}>
      <img
        ref={img}
        src={photoUrl(photo.file, 400)}
        srcSet={photoSrcSet(photo.file)}
        sizes={sizes}
        alt={alt}
        loading="lazy"
        decoding="async"
        fetchPriority={priority ? "high" : "auto"}
        draggable={false}
        onLoad={() => setState("ready")}
        onError={() => {
          failed.add(photo.file);
          console.warn("[product-photo] не открылось фото", photo.file);
          setState("error");
        }}
        className={`size-full select-none object-contain ${pad} transition-opacity duration-200 ${state === "ready" ? "opacity-100" : "opacity-0"} ${
          dim ? "grayscale-[60%] !opacity-70" : ""
        }`}
      />
      {busy && (
        <span className="absolute inset-0 grid place-items-center bg-white/50">
          <Loader2 aria-hidden className="size-6 animate-spin text-[#3D7BFF]" />
        </span>
      )}
    </span>
  );
}

/** Что показать на весь экран: фото товара (productId — живой список и действия) или готовый список; с какого начать. */
export type ViewerItem = { productId?: number; photos: ProductPhoto[]; title: string; sub?: string | null; start?: number };

/**
 * Фото на весь экран: тёмный фон, товар целиком, листание (стрелки, ← →, свайп), закрыть — крестик, Esc или свайп вниз; масштаб — щипком
 * (жест браузера). Подпись — название и артикул; «похожее фото» — пометкой. У кого есть право — «Заменить», «Добавить», «Сделать главным»,
 * «Удалить».
 */
export function PhotoViewer({ item, onClose }: { item: ViewerItem; onClose: () => void }) {
  const t = useT();
  const live = useProductPhotos(item.productId ?? -1);
  const photos = item.productId != null ? live.photos : item.photos;
  const canEdit = item.productId != null && live.canEdit;
  const [index, setIndex] = useState(item.start ?? 0);
  const [drag, setDrag] = useState(0);
  const start = useRef<{ y: number; x: number } | null>(null);
  const closeButton = useRef<HTMLButtonElement>(null);
  const count = photos.length;
  const current = photos[Math.min(index, Math.max(0, count - 1))];
  const replacing = useRef<string | undefined>(undefined);
  const picker = usePhotoPicker(async (file) => {
    const replace = replacing.current;
    if (await live.upload(file, replace)) setIndex(replace ? Math.min(index, count - 1) : 0);
  });

  useEffect(() => {
    const opener = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    closeButton.current?.focus();
    const scroller = document.querySelector<HTMLElement>("[data-app-scroll]");
    const overflow = scroller?.style.overflow;
    if (scroller) scroller.style.overflow = "hidden";
    return () => {
      if (scroller) scroller.style.overflow = overflow ?? "";
      if (opener?.isConnected) opener.focus({ preventScroll: true });
    };
  }, []);

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") onClose();
      if (count > 1 && e.key === "ArrowRight") setIndex((i) => (i + 1) % count);
      if (count > 1 && e.key === "ArrowLeft") setIndex((i) => (i - 1 + count) % count);
    };
    document.addEventListener("keydown", onKey);
    return () => document.removeEventListener("keydown", onKey);
  }, [count, onClose]);

  // Удалили последнее фото — закрыть.
  useEffect(() => {
    if (count === 0) onClose();
    else if (index > count - 1) setIndex(count - 1);
  }, [count, index, onClose]);

  if (!current) return null;
  const step = (d: number) => setIndex((i) => (i + d + count) % count);
  const action = "flex h-10 items-center gap-2 rounded-full px-4 text-[13px] font-semibold outline-none focus-visible:ring-2 focus-visible:ring-white/70 disabled:opacity-50";

  return createPortal(
    <div
      role="dialog"
      aria-modal="true"
      aria-label={item.title}
      className="fixed inset-0 z-[80] flex flex-col bg-[#000000f2] text-white"
      style={{ opacity: drag > 0 ? Math.max(0.35, 1 - drag / 400) : 1 }}
      onTouchStart={(e) => {
        start.current = e.touches.length === 1 ? { y: e.touches[0].clientY, x: e.touches[0].clientX } : null;
      }}
      onTouchMove={(e) => {
        if (!start.current || e.touches.length !== 1) {
          start.current = null;
          setDrag(0);
          return;
        }
        const dy = e.touches[0].clientY - start.current.y;
        const dx = e.touches[0].clientX - start.current.x;
        if (dy > 0 && Math.abs(dy) > Math.abs(dx)) setDrag(dy);
      }}
      onTouchEnd={(e) => {
        const begin = start.current;
        start.current = null;
        if (drag > 110) return onClose();
        setDrag(0);
        const touch = e.changedTouches[0];
        if (begin && count > 1 && touch && Math.abs(touch.clientX - begin.x) > 60 && Math.abs(touch.clientY - begin.y) < 60) step(touch.clientX < begin.x ? 1 : -1);
      }}
    >
      {picker.element}
      <div className="flex items-center justify-end p-3 pt-[max(12px,env(safe-area-inset-top))]">
        <button
          ref={closeButton}
          type="button"
          onClick={onClose}
          aria-label={t("Закрыть")}
          className="grid size-10 place-items-center rounded-full bg-white/10 outline-none hover:bg-white/20 focus-visible:ring-2 focus-visible:ring-white/70"
        >
          <X aria-hidden className="size-5" />
        </button>
      </div>
      <div className="relative flex min-h-0 flex-1 items-center justify-center px-4 [touch-action:pinch-zoom]" style={{ transform: drag ? `translateY(${drag}px)` : undefined }}>
        <img key={current.file} src={photoUrl(current.file, 800)} alt={item.title} className="max-h-full max-w-full select-none object-contain" draggable={false} />
        {live.busy && (
          <span className="absolute inset-0 grid place-items-center">
            <Loader2 aria-hidden className="size-8 animate-spin text-white" />
          </span>
        )}
        {count > 1 && (
          <>
            <button
              type="button"
              onClick={() => step(-1)}
              aria-label={t("Предыдущее фото")}
              className="absolute left-3 top-1/2 grid size-10 -translate-y-1/2 place-items-center rounded-full bg-white/10 outline-none hover:bg-white/20 focus-visible:ring-2 focus-visible:ring-white/70 max-md:hidden"
            >
              <ChevronLeft aria-hidden className="size-5" />
            </button>
            <button
              type="button"
              onClick={() => step(1)}
              aria-label={t("Следующее фото")}
              className="absolute right-3 top-1/2 grid size-10 -translate-y-1/2 place-items-center rounded-full bg-white/10 outline-none hover:bg-white/20 focus-visible:ring-2 focus-visible:ring-white/70 max-md:hidden"
            >
              <ChevronRight aria-hidden className="size-5" />
            </button>
          </>
        )}
      </div>
      <div className="px-5 pb-[max(20px,env(safe-area-inset-bottom))] pt-3 text-center">
        {count > 1 && (
          <div className="mb-3 flex justify-center gap-1.5" aria-label={t("Фото {n} из {total}", { n: index + 1, total: count })}>
            {photos.map((p, i) => (
              <button
                key={p.id}
                type="button"
                onClick={() => setIndex(i)}
                aria-label={t("Фото {n} из {total}", { n: i + 1, total: count })}
                aria-current={i === index ? "true" : undefined}
                className={`h-1.5 rounded-full transition-all ${i === index ? "w-5 bg-white" : "w-1.5 bg-white/40"}`}
              />
            ))}
          </div>
        )}
        <p className="text-[14px] font-semibold leading-snug">{item.title}</p>
        {item.sub && <p className="mt-1 text-[12px] text-white/60">{item.sub}</p>}
        {current.similar && (
          <p className="mx-auto mt-2 w-fit rounded-full bg-white/10 px-3 py-1 text-[11px] font-medium text-white/80">
            {current.similarFrom ? t("Похожее фото — товар той же линейки ({code})", { code: current.similarFrom }) : t("Похожее фото — товар той же линейки")}
          </p>
        )}
        {canEdit && (
          <div className="mt-4 flex flex-wrap justify-center gap-2">
            <button
              type="button"
              disabled={!!live.busy}
              onClick={() => {
                replacing.current = current.id;
                picker.open();
              }}
              className={`${action} ${current.similar ? "bg-[#3D7BFF] text-white" : "bg-white/10 hover:bg-white/20"}`}
            >
              <RefreshCw aria-hidden className="size-4" />
              {t(current.similar ? "Заменить своим фото" : "Заменить")}
            </button>
            <button
              type="button"
              disabled={!!live.busy}
              onClick={() => {
                replacing.current = undefined;
                picker.open();
              }}
              className={`${action} bg-white/10 hover:bg-white/20`}
            >
              <ImagePlus aria-hidden className="size-4" />
              {t("Добавить фото")}
            </button>
            {index > 0 && !current.similar && (
              <button type="button" disabled={!!live.busy} onClick={() => void live.makeMain(current.id).then((ok) => ok && setIndex(0))} className={`${action} bg-white/10 hover:bg-white/20`}>
                <Star aria-hidden className="size-4" />
                {t("Сделать главным")}
              </button>
            )}
            <button
              type="button"
              disabled={!!live.busy}
              onClick={() => {
                if (confirm(t(current.similar ? "Убрать похожее фото у этого товара?" : "Удалить это фото товара?"))) void live.remove(current.id);
              }}
              className={`${action} bg-white/10 text-[#FF8A96] hover:bg-white/20`}
            >
              <Trash2 aria-hidden className="size-4" />
              {t("Удалить")}
            </button>
          </div>
        )}
      </div>
    </div>,
    document.body,
  );
}
