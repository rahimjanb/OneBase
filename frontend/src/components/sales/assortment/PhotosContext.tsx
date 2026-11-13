"use client";

import { createContext, useCallback, useContext, useRef, useState } from "react";
import { X } from "lucide-react";
import { useT } from "@/i18n/client";
import { EMPTY_PHOTOS, productPhotosApi, type ProductPhoto, type ProductPhotos } from "@/lib/sales/product-photos";

/*
 * Фото товаров страницы (ассортимент, категория, артикул): что пришло с сервера и что поменяли здесь же — без перезагрузки. Действия
 * (добавить, заменить, удалить, сделать главным) — у кого есть право «Продажи: управление»; ошибка — плашка внизу экрана.
 */

type Busy = { productId: number; what: "upload" | "remove" | "main" } | null;

type Ctx = {
  photos: ProductPhotos;
  busy: Busy;
  run: (productId: number, what: NonNullable<Busy>["what"], action: () => Promise<ProductPhoto[]>) => Promise<boolean>;
};

const PhotosCtx = createContext<Ctx | null>(null);

export function ProductPhotosProvider({ initial, children }: { initial: ProductPhotos | null; children: React.ReactNode }) {
  const t = useT();
  const [photos, setPhotos] = useState<ProductPhotos>(initial ?? EMPTY_PHOTOS);
  const [busy, setBusy] = useState<Busy>(null);
  const [error, setError] = useState<string | null>(null);
  const timer = useRef<number | null>(null);

  const run = useCallback<Ctx["run"]>(async (productId, what, action) => {
    setBusy({ productId, what });
    setError(null);
    try {
      const list = await action();
      setPhotos((p) => ({ ...p, products: { ...p.products, [String(productId)]: list } }));
      return true;
    } catch (e) {
      setError((e as Error).message);
      if (timer.current) window.clearTimeout(timer.current);
      timer.current = window.setTimeout(() => setError(null), 8000);
      return false;
    } finally {
      setBusy(null);
    }
  }, []);

  return (
    <PhotosCtx.Provider value={{ photos, busy, run }}>
      {children}
      {error && (
        <div role="alert" className="fixed bottom-24 left-1/2 z-[90] flex max-w-[92vw] -translate-x-1/2 items-center gap-3 rounded-xl border border-bad/30 bg-surface-solid px-4 py-2.5 text-sm text-bad shadow-lg lg:bottom-6">
          <span>{error}</span>
          <button type="button" onClick={() => setError(null)} aria-label={t("Закрыть")} className="text-ink-3 hover:text-ink">
            <X className="size-4" />
          </button>
        </div>
      )}
    </PhotosCtx.Provider>
  );
}

/** Все фото страницы: товар Linko → фото (обложка категории — первый продаваемый товар с фото). */
export function useAllProductPhotos(): Record<string, ProductPhoto[]> {
  return useContext(PhotosCtx)?.photos.products ?? {};
}

/** Фото товара и действия с ними. Вне провайдера (другие страницы) — фото нет, менять нельзя. */
export function useProductPhotos(productId: number) {
  const ctx = useContext(PhotosCtx);
  const list = ctx?.photos.products[String(productId)] ?? [];
  const canEdit = !!ctx?.photos.canEdit;
  const busy = ctx?.busy?.productId === productId ? ctx.busy.what : null;
  return {
    photos: list,
    canEdit,
    busy,
    upload: (file: File, replace?: string) => ctx?.run(productId, "upload", () => productPhotosApi.upload(productId, file, replace)) ?? Promise.resolve(false),
    remove: (id: string) => ctx?.run(productId, "remove", () => productPhotosApi.remove(id)) ?? Promise.resolve(false),
    makeMain: (id: string) => ctx?.run(productId, "main", () => productPhotosApi.main(id)) ?? Promise.resolve(false),
  };
}

/** Выбор файла фото: скрытое поле и открыть его (камера или галерея на телефоне). */
export function usePhotoPicker(onFile: (file: File) => void) {
  const input = useRef<HTMLInputElement>(null);
  const element = (
    <input
      ref={input}
      type="file"
      accept="image/jpeg,image/png,image/webp,image/heic,image/heif"
      className="hidden"
      // Нажатие на скрытое поле всплывало бы до строки таблицы или карточки — и открывало бы товар.
      onClick={(e) => e.stopPropagation()}
      onChange={(e) => {
        const file = e.target.files?.[0];
        e.target.value = "";
        if (file) onFile(file);
      }}
    />
  );
  return { open: () => input.current?.click(), element };
}
