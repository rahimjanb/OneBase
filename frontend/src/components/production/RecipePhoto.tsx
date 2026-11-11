"use client";

import { useRouter } from "next/navigation";
import { useEffect, useId, useRef, useState, useTransition } from "react";
import { Camera, ImageOff, ImagePlus, Images, Loader2, RefreshCw, Trash2, Upload } from "lucide-react";
import { showTaskToast } from "@/components/tasks/TaskToast";
import { tNow, useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import type { RecipePhoto } from "@/lib/production";
import { PHOTO_ACCEPT, photoFileError, photoSrc, photoSrcSet } from "@/lib/recipe-cards";

/**
 * Фото продукта техкарты: превью в списке (плавное появление, средний цвет фото — подложка, пока грузится; не загрузилось — «Не удалось
 * загрузить фото»), заглушка «Фото не добавлено», загрузка с прогрессом прямо из списка и блок «Фото продукта» в карточке и форме техкарты
 * (заменить, удалить с подтверждением, перетащить файл, на телефоне — камера или галерея). Фото одно на код — у всех версий.
 */

/** Отправка файла с прогрессом (XHR: у fetch нет прогресса отправки). Ответ сервера — фото; ошибка — Error с текстом для человека. */
function sendPhoto(recipeId: string, file: File, onProgress: (p: number) => void): Promise<RecipePhoto> {
  return new Promise((resolve, reject) => {
    const xhr = new XMLHttpRequest();
    xhr.open("POST", `/bff/api/production/recipes/${recipeId}/photo`);
    xhr.upload.onprogress = (e) => e.lengthComputable && onProgress(Math.min(0.97, e.loaded / e.total));
    xhr.onload = () => {
      let body: { error?: string; detail?: string; title?: string } & Partial<RecipePhoto> = {};
      try {
        body = JSON.parse(xhr.responseText);
      } catch {
        // не JSON
      }
      if (xhr.status >= 200 && xhr.status < 300) resolve(body as RecipePhoto);
      else if (xhr.status === 403) reject(new Error(tNow("Недостаточно прав.")));
      else if (xhr.status === 413) reject(new Error(tNow("Фото — до 10 МБ.")));
      else {
        const message = body.error ?? body.detail ?? body.title;
        reject(new Error(message ? tNow(message) : tNow("Фото не загрузилось ({status})", { status: xhr.status })));
      }
    };
    xhr.onerror = () => reject(new Error(tNow("Нет связи с сервером OneBase — фото не загрузилось. Проверьте связь и попробуйте ещё раз.")));
    const form = new FormData();
    form.append("file", file);
    xhr.send(form);
  });
}

/**
 * Загрузка и удаление фото техкарты: проверка файла (тип, до 10 МБ), прогресс, после успеха — свежие данные страницы и сообщение «Фото
 * обновлено». busy — пока идёт отправка или обновляются данные (до появления фото на странице).
 */
export function usePhotoUpload(recipeId: string | null) {
  const t = useT();
  const router = useRouter();
  const [progress, setProgress] = useState<number | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [refreshing, startRefresh] = useTransition();

  const upload = async (file: File) => {
    if (!recipeId) return;
    const invalid = photoFileError(file);
    setError(invalid ? t(invalid) : null);
    if (invalid) return;
    setProgress(0);
    try {
      await sendPhoto(recipeId, file, setProgress);
      setProgress(1);
      startRefresh(() => router.refresh());
      showTaskToast({ tone: "ok", text: t("Фото обновлено") });
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setProgress(null);
    }
  };

  const remove = async () => {
    if (!recipeId) return;
    setError(null);
    setProgress(1);
    try {
      await bff(`production/recipes/${recipeId}/photo`, { method: "DELETE" });
      startRefresh(() => router.refresh());
      showTaskToast({ tone: "ok", text: t("Фото удалено") });
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setProgress(null);
    }
  };

  return { upload, remove, progress: progress ?? (refreshing ? 1 : null), busy: progress !== null || refreshing, error, setError };
}

/** Скрытое поле выбора файла; open() — открыть выбор (камера — сразу камера телефона). */
export function usePicker(onFile: (file: File) => void) {
  const gallery = useRef<HTMLInputElement>(null);
  const camera = useRef<HTMLInputElement>(null);
  const change = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    e.target.value = ""; // тот же файл ещё раз — снова событие
    if (file) onFile(file);
  };
  const inputs = (
    <>
      <input ref={gallery} type="file" accept={PHOTO_ACCEPT} onChange={change} className="hidden" tabIndex={-1} aria-hidden />
      <input ref={camera} type="file" accept="image/*" capture="environment" onChange={change} className="hidden" tabIndex={-1} aria-hidden />
    </>
  );
  return { inputs, open: () => gallery.current?.click(), openCamera: () => camera.current?.click() };
}

/** Полоса прогресса поверх фото или заглушки. */
export function PhotoProgress({ value, label }: { value: number; label?: string }) {
  const t = useT();
  return (
    <span role="progressbar" aria-label={label ?? t("Загрузка фото")} aria-valuemin={0} aria-valuemax={100} aria-valuenow={Math.round(value * 100)}
      className="absolute inset-x-3 bottom-3 z-20 block h-1.5 overflow-hidden rounded-full bg-black/40 backdrop-blur-sm">
      <span className="block h-full rounded-full bg-accent transition-[width] duration-200" style={{ width: `${Math.max(6, value * 100)}%` }} />
    </span>
  );
}

/**
 * Превью фото: srcset 480/960, лениво, без сдвига вёрстки (размер задаёт контейнер); до загрузки — средний цвет фото, затем плавное
 * появление; не загрузилось — «Не удалось загрузить фото» (compact — только иконка и короткая подпись, для квадрата на телефоне).
 */
export function PhotoImage({ photo, alt, sizes, className = "", zoom = false, compact = false, full = false }: {
  photo: RecipePhoto;
  alt: string;
  sizes: string;
  className?: string;
  /** Карточка в сетке: фото чуть увеличивается при наведении на карточку (group). */
  zoom?: boolean;
  compact?: boolean;
  /** Оригинал (форма техкарты), а не превью. */
  full?: boolean;
}) {
  const t = useT();
  const ref = useRef<HTMLImageElement>(null);
  const [state, setState] = useState<"loading" | "ok" | "failed">("loading");
  // Картинка могла загрузиться (или не загрузиться) до гидратации — onLoad тогда уже был: проверяем сами; новое фото — снова «грузится».
  useEffect(() => {
    const img = ref.current;
    setState(img?.complete ? (img.naturalWidth > 0 ? "ok" : "failed") : "loading");
  }, [photo.id]);

  if (state === "failed") {
    return (
      <span className={`flex flex-col items-center justify-center gap-1.5 bg-muted text-center ${className}`} role="img" aria-label={t("Не удалось загрузить фото")}>
        <ImageOff aria-hidden className={compact ? "size-5 text-ink-3" : "size-[22px] text-ink-3"} />
        <span className={compact ? "px-1 text-[10px] font-semibold leading-tight text-ink-3" : "text-[13px] font-semibold text-ink-2"}>
          {compact ? t("Фото не загрузилось") : t("Не удалось загрузить фото")}
        </span>
      </span>
    );
  }
  return (
    <span className={`block overflow-hidden ${className}`} style={{ backgroundColor: photo.tint ?? "var(--color-muted)" }}>
      <span className={`block size-full ${zoom ? "transition-transform duration-300 ease-out group-hover:scale-[1.03]" : ""}`}>
        {/* eslint-disable-next-line @next/next/no-img-element -- превью из своего API через /bff (cookie), оптимизатор Next не нужен */}
        <img
          ref={ref}
          src={full ? photoSrc(photo, 1600) : photoSrc(photo, 960)}
          srcSet={full ? undefined : photoSrcSet(photo)}
          sizes={full ? undefined : sizes}
          alt={alt}
          loading={full ? "eager" : "lazy"}
          decoding="async"
          onLoad={() => setState("ok")}
          onError={() => setState("failed")}
          className={`size-full object-cover transition-opacity duration-200 ${state === "ok" ? "opacity-100" : "opacity-0"}`}
        />
      </span>
    </span>
  );
}

/**
 * Блок «Фото продукта» в карточке и форме техкарты: превью 4:3; «Заменить» и «Удалить» (с подтверждением); на компьютере файл можно
 * перетащить (подсветка пунктиром), на телефоне — «Камера» или «Галерея»; во время загрузки — полоса прогресса; ошибка — под блоком.
 * Режим pending (новая техкарта, ещё не сохранена): файл выбирается и показывается сразу, отправляется после сохранения формы.
 */
export function RecipePhotoBlock({
  recipeId,
  photo,
  name,
  canEdit,
  pending,
  className = "",
}: {
  recipeId: string | null;
  photo: RecipePhoto | null | undefined;
  name: string;
  canEdit: boolean;
  /** Новая техкарта: выбранный файл и его смена — без отправки. */
  pending?: { file: File | null; onChange: (file: File | null) => void };
  className?: string;
}) {
  const t = useT();
  const titleId = useId();
  const live = usePhotoUpload(pending ? null : recipeId);
  const [confirm, setConfirm] = useState(false);
  const [drag, setDrag] = useState(false);
  const [pendingError, setPendingError] = useState<string | null>(null);
  const [preview, setPreview] = useState<string | null>(null);

  const accept = (file: File) => {
    if (!pending) return void live.upload(file);
    const invalid = photoFileError(file);
    setPendingError(invalid ? t(invalid) : null);
    if (!invalid) pending.onChange(file);
  };
  const picker = usePicker(accept);

  // Предпросмотр выбранного, но ещё не отправленного файла (новая техкарта).
  const pendingFile = pending?.file ?? null;
  useEffect(() => {
    if (!pendingFile) return setPreview(null);
    const url = URL.createObjectURL(pendingFile);
    setPreview(url);
    return () => URL.revokeObjectURL(url);
  }, [pendingFile]);

  const busy = live.busy;
  const error = pending ? pendingError : live.error;
  const has = pending ? !!pendingFile : !!photo;
  const dropProps = canEdit && !busy
    ? {
        onDragOver: (e: React.DragEvent) => {
          if (![...e.dataTransfer.types].includes("Files")) return;
          e.preventDefault();
          setDrag(true);
        },
        onDragLeave: (e: React.DragEvent) => {
          if (!e.currentTarget.contains(e.relatedTarget as Node | null)) setDrag(false);
        },
        onDrop: (e: React.DragEvent) => {
          e.preventDefault();
          setDrag(false);
          const file = e.dataTransfer.files?.[0];
          if (file) accept(file);
        },
      }
    : {};

  const remove = () => {
    setConfirm(false);
    if (pending) {
      pending.onChange(null);
      setPendingError(null);
    } else void live.remove();
  };

  const btn = "inline-flex h-9 items-center gap-1.5 rounded-lg border border-line bg-surface px-3 text-[13px] font-semibold text-ink hover:bg-muted disabled:opacity-50";

  return (
    <section aria-labelledby={titleId} className={className}>
      <h2 id={titleId} className="mb-2.5 text-base font-semibold text-ink">
        {t("Фото продукта")}
      </h2>
      <div
        {...dropProps}
        className={`relative aspect-[4/3] w-full overflow-hidden rounded-[14px] border bg-muted ${drag ? "border-2 border-dashed border-accent" : "border-line"}`}
      >
        {pending && preview ? (
          // eslint-disable-next-line @next/next/no-img-element -- локальный файл (blob:), ещё не отправлен
          <img src={preview} alt={name || t("Фото продукта")} className="size-full object-cover" />
        ) : !pending && photo ? (
          <PhotoImage photo={photo} alt={name} sizes="320px" full className="size-full" />
        ) : (
          <div className="flex size-full flex-col items-center justify-center gap-2 px-4 text-center" aria-label={t("Фото не добавлено")}>
            <span className="grid size-[52px] place-items-center rounded-2xl border border-line-strong bg-surface">
              <ImagePlus aria-hidden className="size-[22px] text-ink-3" />
            </span>
            <p className="text-[13px] font-semibold text-ink-2">{t("Фото не добавлено")}</p>
            {canEdit && (
              <>
                <button type="button" onClick={picker.open} disabled={busy} className="inline-flex items-center gap-1.5 rounded-lg bg-accent-soft px-3 py-1.5 text-[12px] font-semibold text-accent-strong disabled:opacity-50 max-md:hidden">
                  <Upload aria-hidden className="size-[13px]" /> {t("Загрузить фото")}
                </button>
                <p className="text-[11px] text-ink-4 max-md:hidden">{t("или перетащите файл сюда · JPEG, PNG, WebP, HEIC до 10 МБ")}</p>
              </>
            )}
          </div>
        )}
        {drag && (
          <div className="pointer-events-none absolute inset-0 z-10 grid place-items-center bg-surface-solid/95 text-[13px] font-semibold text-accent-strong">
            <span className="flex flex-col items-center gap-2">
              <Upload aria-hidden className="size-5" /> {t("Отпустите, чтобы загрузить")}
            </span>
          </div>
        )}
        {live.progress !== null && <PhotoProgress value={live.progress} />}
      </div>

      {canEdit && (
        <div className="mt-2.5 flex flex-wrap items-center gap-2">
          {/* Телефон: камера или галерея. */}
          <button type="button" onClick={picker.openCamera} disabled={busy} className={`${btn} md:hidden`}>
            <Camera aria-hidden className="size-4" /> {t("Камера")}
          </button>
          <button type="button" onClick={picker.open} disabled={busy} className={`${btn} md:hidden`}>
            <Images aria-hidden className="size-4" /> {t("Галерея")}
          </button>
          {has && (
            <button type="button" onClick={picker.open} disabled={busy} className={`${btn} max-md:hidden`}>
              {busy ? <Loader2 aria-hidden className="size-4 animate-spin" /> : <RefreshCw aria-hidden className="size-4" />} {t("Заменить")}
            </button>
          )}
          {has &&
            (confirm ? (
              <span className="flex flex-wrap items-center gap-2" role="group" aria-label={t("Удалить фото?")}>
                <span className="text-[13px] text-ink-2">{t("Удалить фото у всех версий?")}</span>
                <button type="button" onClick={remove} disabled={busy} className="inline-flex h-9 items-center gap-1.5 rounded-lg bg-bad px-3 text-[13px] font-semibold text-white disabled:opacity-50">
                  {t("Удалить")}
                </button>
                <button type="button" onClick={() => setConfirm(false)} className={btn}>
                  {t("Отмена")}
                </button>
              </span>
            ) : (
              <button type="button" onClick={() => (pending ? remove() : setConfirm(true))} disabled={busy} className={`${btn} text-bad`}>
                <Trash2 aria-hidden className="size-4" /> {t("Удалить")}
              </button>
            ))}
        </div>
      )}
      {error && (
        <p role="alert" className="mt-2 text-[13px] text-bad">
          {error}
        </p>
      )}
      {pending && pendingFile && <p className="mt-2 text-[12px] text-ink-3">{t("Фото сохранится вместе с техкартой.")}</p>}
      {!pending && <p className="mt-2 text-[12px] text-ink-3">{t("Одно фото на продукт — у всех версий техкарты. Кадр 4:3 вырезается по центру.")}</p>}
      {picker.inputs}
    </section>
  );
}

/** Загрузить фото новой техкарты сразу после её создания (форма). Ошибка — текст, техкарта уже создана. */
export async function uploadAfterCreate(recipeId: string, file: File): Promise<string | null> {
  try {
    await sendPhoto(recipeId, file, () => {});
    return null;
  } catch (e) {
    return e instanceof Error ? e.message : String(e);
  }
}
