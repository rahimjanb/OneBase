"use client";

import { useRef, useState } from "react";
import { ImagePlus, RefreshCw } from "lucide-react";
import { useT } from "@/i18n/client";
import type { ProductPhotos } from "@/lib/sales/product-photos";
import { PhotoViewer, ProductImage } from "./photo";
import { ProductPhotosProvider, usePhotoPicker, useProductPhotos } from "./PhotosContext";

/**
 * Фото на экране артикула («где продаётся, а где нет»): главное фото и миниатюры остальных; нажатие — просмотр на весь экран с листанием
 * (и действиями с фото, если есть право). Нет фото — заглушка, у кого есть право — «Добавить фото»; похожее фото — с пометкой и
 * «Заменить своим фото». children — статус и «ТОП» справа от фото.
 */
export function ProductPhotoPanel({ photos, ...props }: { photos: ProductPhotos | null; productId: number; code: string | null; name: string; children?: React.ReactNode }) {
  return (
    <ProductPhotosProvider initial={photos}>
      <Panel {...props} />
    </ProductPhotosProvider>
  );
}

function Panel({ productId, code, name, children }: { productId: number; code: string | null; name: string; children?: React.ReactNode }) {
  const t = useT();
  const { photos, canEdit, busy, upload } = useProductPhotos(productId);
  const [open, setOpen] = useState<number | null>(null);
  // Какое фото заменить — ссылкой, не состоянием: файл выбирают раньше, чем страница перерисуется.
  const replace = useRef<string | undefined>(undefined);
  const picker = usePhotoPicker((file) => void upload(file, replace.current));
  const sub = code ? t("Артикул {code}", { code }) : null;
  const main = photos[0];
  const pick = (id?: string) => {
    replace.current = id;
    picker.open();
  };
  const small = "inline-flex h-8 items-center gap-1.5 rounded-lg border border-line bg-surface px-2.5 text-[12px] font-semibold text-ink-2 outline-none hover:bg-muted focus-visible:ring-2 focus-visible:ring-accent disabled:opacity-50";

  return (
    <div className="mb-4 flex flex-wrap items-center gap-4">
      {picker.element}
      {main ? (
        <button
          type="button"
          onClick={() => setOpen(0)}
          aria-label={t("Фото: {name}", { name })}
          className="size-24 shrink-0 cursor-zoom-in overflow-hidden rounded-2xl border border-line-strong outline-none focus-visible:ring-2 focus-visible:ring-accent md:size-28"
        >
          <ProductImage photo={main} alt="" sizes="112px" priority pad="p-2" busy={busy === "upload"} />
        </button>
      ) : (
        <span className="size-24 shrink-0 overflow-hidden rounded-2xl border border-line md:size-28">
          <ProductImage photo={null} alt="" sizes="112px" emptyIcon={22} emptyLabel onAdd={canEdit ? () => pick() : undefined} busy={busy === "upload"} />
        </span>
      )}
      {photos.length > 1 && (
        <div className="flex gap-2">
          {photos.slice(1).map((p, i) => (
            <button
              key={p.id}
              type="button"
              onClick={() => setOpen(i + 1)}
              aria-label={t("Фото {n} из {total}", { n: i + 2, total: photos.length })}
              className="size-12 shrink-0 cursor-zoom-in overflow-hidden rounded-xl border border-line outline-none focus-visible:ring-2 focus-visible:ring-accent"
            >
              <ProductImage photo={p} alt="" sizes="48px" pad="p-1" />
            </button>
          ))}
        </div>
      )}
      <div className="flex min-w-0 flex-col gap-2">
        <div className="flex flex-wrap items-center gap-2 text-sm text-ink-2">{children}</div>
        {main?.similar && (
          <p className="text-[12px] text-ink-3">
            {main.similarFrom ? t("Похожее фото — товар той же линейки ({code})", { code: main.similarFrom }) : t("Похожее фото — товар той же линейки")}
          </p>
        )}
        {canEdit && (
          <div className="flex flex-wrap gap-2">
            {main && (
              <button type="button" disabled={!!busy} onClick={() => pick(main.id)} className={small}>
                <RefreshCw aria-hidden className="size-3.5" />
                {t(main.similar ? "Заменить своим фото" : "Заменить фото")}
              </button>
            )}
            <button type="button" disabled={!!busy} onClick={() => pick()} className={small}>
              <ImagePlus aria-hidden className="size-3.5" />
              {t("Добавить фото")}
            </button>
          </div>
        )}
      </div>
      {open != null && <PhotoViewer item={{ productId, photos, title: name, sub, start: open }} onClose={() => setOpen(null)} />}
    </div>
  );
}
