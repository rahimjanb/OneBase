import { tNow } from "@/i18n/client";

/**
 * Фото товаров «Продаж» — на сервере (хранилище файлов): готовые WebP, товар вписан целиком в квадрат 160 / 400 / 800 px. Адрес картинки
 * меняется с каждой загрузкой — браузер хранит её навсегда. Первое фото товара — главное; similar — фото товара той же линейки (другой вес
 * или упаковка), пока своего нет. Менять может право «Продажи: управление» (canEdit).
 */
export type ProductPhoto = { id: string; file: string; width: number; height: number; similar: boolean; similarFrom: string | null };

export type ProductPhotos = { canEdit: boolean; products: Record<string, ProductPhoto[]> };

export const EMPTY_PHOTOS: ProductPhotos = { canEdit: false, products: {} };

export const PHOTO_SIZES = [160, 400, 800] as const;

export const photoUrl = (file: string, size: number) => `/bff/api/sales/product-photos/${file}/${size}`;

/** srcset по ширине: браузер берёт размер по плотности экрана и sizes. */
export const photoSrcSet = (file: string) => PHOTO_SIZES.map((s) => `${photoUrl(file, s)} ${s}w`).join(", ");

async function call<T>(path: string, init: RequestInit): Promise<T> {
  let response: Response;
  try {
    response = await fetch(`/bff/api/${path}`, init);
  } catch {
    throw new Error(tNow("Нет связи с сервером OneBase — фото не сохранено. Проверьте связь и повторите."));
  }
  if (!response.ok) {
    const text = await response.text();
    let message = text;
    try {
      message = JSON.parse(text).error ?? text;
    } catch {
      // не JSON
    }
    throw new Error(response.status === 403 ? tNow("Менять фото товаров может право «Продажи: управление».") : message ? tNow(message) : tNow("Ошибка {status}", { status: response.status }));
  }
  return (await response.json()) as T;
}

/** Большой снимок (телефон даёт 4–12 МБ) уменьшаем до 2400 px ещё в браузере — загрузка быстрее; сервер всё равно готовит WebP. */
export async function shrinkForUpload(file: File): Promise<File> {
  if (!/^image\/(jpeg|png|webp)$/.test(file.type) || file.size < 2_500_000) return file;
  try {
    const bitmap = await createImageBitmap(file, { imageOrientation: "from-image" });
    const scale = Math.min(1, 2400 / Math.max(bitmap.width, bitmap.height));
    if (scale === 1 && file.size < 8_000_000) return file;
    const canvas = document.createElement("canvas");
    canvas.width = Math.round(bitmap.width * scale);
    canvas.height = Math.round(bitmap.height * scale);
    canvas.getContext("2d")!.drawImage(bitmap, 0, 0, canvas.width, canvas.height);
    const type = file.type === "image/png" ? "image/png" : "image/jpeg";
    const blob = await new Promise<Blob | null>((resolve) => canvas.toBlob(resolve, type, 0.9));
    return blob ? new File([blob], file.name.replace(/\.\w+$/, type === "image/png" ? ".png" : ".jpg"), { type }) : file;
  } catch {
    return file;
  }
}

export const productPhotosApi = {
  /** Добавить фото товару; replace — какое фото заменить. Ответ — все фото товара. */
  upload: async (productId: number, file: File, replace?: string) => {
    const form = new FormData();
    form.append("file", await shrinkForUpload(file));
    if (replace) form.append("replace", replace);
    return call<ProductPhoto[]>(`sales/products/${productId}/photos`, { method: "POST", body: form });
  },
  remove: (id: string) => call<ProductPhoto[]>(`sales/product-photos/${id}`, { method: "DELETE" }),
  main: (id: string) => call<ProductPhoto[]>(`sales/product-photos/${id}/main`, { method: "POST" }),
};
