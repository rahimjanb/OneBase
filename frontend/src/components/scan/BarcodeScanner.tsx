"use client";

import { useEffect, useRef, useState } from "react";
import { Flashlight, FlashlightOff, Keyboard, Loader2, X } from "lucide-react";
import { useT } from "@/i18n/client";
import { cameraProblem, cleanCode, createConfirmer, scanFormats } from "@/lib/barcode";

type Detected = { rawValue: string; format: string };
type Detector = { detect(source: HTMLVideoElement): Promise<Detected[]> };
type NativeDetector = { new (options: { formats: string[] }): Detector; getSupportedFormats(): Promise<string[]> };

/**
 * Распознавание: встроенное в браузер (Chrome на Android и др.), если оно читает товарные и складские коды; иначе — ZXing (Safari на iPhone,
 * Firefox), его файл WebAssembly — из public/zxing, не с внешнего CDN. Загружается только когда открыт сканер.
 */
async function createDetector(): Promise<Detector> {
  const native = (globalThis as { BarcodeDetector?: NativeDetector }).BarcodeDetector;
  if (native?.getSupportedFormats) {
    try {
      const supported = await native.getSupportedFormats();
      const formats = scanFormats.filter((f) => supported.includes(f));
      if (formats.includes("ean_13") && formats.includes("code_128")) return new native({ formats });
    } catch {
      // встроенное не отвечает — ZXing ниже
    }
  }
  const { BarcodeDetector, prepareZXingModule } = await import("barcode-detector/ponyfill");
  await prepareZXingModule({
    overrides: { locateFile: (path: string, prefix: string) => (path.endsWith(".wasm") ? `/zxing/${path}` : prefix + path) },
    fireImmediately: true,
  });
  return new BarcodeDetector({ formats: [...scanFormats] });
}

/**
 * Сканер штрихкода камерой: на телефоне — на весь экран, на компьютере — окном. Задняя камера, рамка, фонарик (если телефон его даёт),
 * «Ввести код» — вручную. Линейный код принимается после двух одинаковых чтений подряд, QR и DataMatrix — сразу; телефон коротко вибрирует,
 * камера выключается, onCode получает код. Нет разрешения, камеры или https — понятное сообщение и ручной ввод.
 */
export function BarcodeScanner({ open, onClose, onCode }: { open: boolean; onClose: () => void; onCode: (code: string) => void }) {
  const t = useT();
  const video = useRef<HTMLVideoElement>(null);
  const track = useRef<MediaStreamTrack | null>(null);
  const deliver = useRef(onCode);
  deliver.current = onCode;
  const [state, setState] = useState<"camera" | "engine" | "scanning" | "error">("camera");
  const [problem, setProblem] = useState<string | null>(null);
  const [torch, setTorch] = useState<boolean | null>(null);
  const [manual, setManual] = useState(false);
  const [typed, setTyped] = useState("");

  useEffect(() => {
    if (!open) return;
    let stopped = false;
    let stream: MediaStream | null = null;
    let timer = 0;
    const stop = () => {
      stopped = true;
      window.clearTimeout(timer);
      stream?.getTracks().forEach((x) => x.stop());
      track.current = null;
    };
    setState("camera");
    setProblem(null);
    setTorch(null);
    setManual(false);
    setTyped("");
    void (async () => {
      const v = video.current;
      if (!v) return;
      try {
        if (!window.isSecureContext || !navigator.mediaDevices?.getUserMedia) throw new Error("insecure");
        stream = await navigator.mediaDevices.getUserMedia({ video: { facingMode: { ideal: "environment" }, width: { ideal: 1280 }, height: { ideal: 720 } }, audio: false });
        if (stopped) return stop();
        v.srcObject = stream;
        await v.play();
        track.current = stream.getVideoTracks()[0] ?? null;
        const caps = track.current?.getCapabilities?.() as { torch?: boolean } | undefined;
        if (caps?.torch) setTorch(false);
      } catch (e) {
        if (stopped) return;
        stop();
        setProblem(t(cameraProblem(e, window.isSecureContext)));
        setState("error");
        return;
      }
      let detector: Detector;
      try {
        setState("engine");
        detector = await createDetector();
      } catch {
        if (stopped) return;
        stop();
        setProblem(t("Не удалось загрузить распознавание штрихкодов. Проверьте связь и попробуйте снова."));
        setState("error");
        return;
      }
      if (stopped) return;
      setState("scanning");
      const confirm = createConfirmer();
      const tick = async () => {
        if (stopped) return;
        try {
          if (v.readyState >= 2) {
            for (const found of await detector.detect(v)) {
              const code = cleanCode(found.rawValue);
              const sure = code && confirm(code, found.format);
              if (sure) {
                stop();
                navigator.vibrate?.(60);
                deliver.current(sure);
                return;
              }
            }
          }
        } catch {
          // кадр не прочитан — следующий
        }
        timer = window.setTimeout(() => void tick(), 120);
      };
      void tick();
    })();
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && onClose();
    document.addEventListener("keydown", onKey);
    return () => {
      stop();
      document.removeEventListener("keydown", onKey);
    };
    // t и onClose — с родителя, на камеру не влияют.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open]);

  const toggleTorch = async () => {
    if (torch === null || !track.current) return;
    try {
      await track.current.applyConstraints({ advanced: [{ torch: !torch } as MediaTrackConstraintSet] });
      setTorch(!torch);
    } catch {
      setTorch(null);
    }
  };
  const submit = (e: React.FormEvent) => {
    e.preventDefault();
    const code = cleanCode(typed);
    if (code) onCode(code);
  };

  if (!open) return null;
  const round = "grid size-12 place-items-center rounded-full bg-white/15 text-white backdrop-blur hover:bg-white/25";
  return (
    <div role="dialog" aria-modal="true" aria-label={t("Сканер штрихкода")} className="fixed inset-0 z-[60] flex bg-black text-white sm:items-center sm:justify-center sm:bg-black/80 sm:p-6">
      <div className="relative flex w-full flex-col overflow-hidden sm:aspect-[4/3] sm:max-w-2xl sm:rounded-2xl sm:bg-black">
        <video ref={video} playsInline muted className="absolute inset-0 size-full object-cover" />

        {/* Рамка: снаружи затемнено, по углам — скобки. */}
        {state !== "error" && (
          <div className="pointer-events-none absolute inset-0 grid place-items-center" aria-hidden>
            <div className="relative h-[30%] min-h-36 w-[80%] max-w-md rounded-2xl shadow-[0_0_0_200vmax_rgb(0_0_0/0.45)]">
              {["left-0 top-0 border-l-4 border-t-4 rounded-tl-2xl", "right-0 top-0 border-r-4 border-t-4 rounded-tr-2xl", "bottom-0 left-0 border-b-4 border-l-4 rounded-bl-2xl", "bottom-0 right-0 border-b-4 border-r-4 rounded-br-2xl"].map((c) => (
                <span key={c} className={`absolute size-8 border-white [filter:drop-shadow(0_0_2px_rgb(0_0_0/0.7))] ${c}`} />
              ))}
              {state === "scanning" && <span className="absolute inset-x-4 top-1/2 h-0.5 -translate-y-1/2 animate-pulse rounded-full bg-accent shadow-[0_0_12px_var(--color-accent)]" />}
            </div>
          </div>
        )}

        <div className="relative z-10 flex items-center justify-between gap-3 p-4 pt-[calc(1rem+env(safe-area-inset-top))]">
          <span className="text-base font-semibold drop-shadow">{t("Сканер штрихкода")}</span>
          <button type="button" onClick={onClose} aria-label={t("Закрыть")} className={round}>
            <X className="size-6" aria-hidden />
          </button>
        </div>

        <div className="relative z-10 flex flex-1 items-center justify-center px-6">
          {(state === "camera" || state === "engine") && (
            <p className="flex items-center gap-2 rounded-xl bg-black/50 px-4 py-2.5 text-sm">
              <Loader2 className="size-4 animate-spin" aria-hidden /> {state === "camera" ? t("Включаем камеру…") : t("Загружаем распознавание…")}
            </p>
          )}
          {state === "error" && problem && <p className="max-w-sm rounded-2xl bg-white/10 px-5 py-4 text-center text-[15px] leading-snug">{problem}</p>}
        </div>

        <div className="relative z-10 space-y-3 p-4 pb-[calc(1rem+env(safe-area-inset-bottom))]">
          {state === "scanning" && !manual && <p className="text-center text-sm text-white/85 drop-shadow">{t("Наведите камеру на штрихкод — он прочитается сам")}</p>}
          {manual ? (
            <form onSubmit={submit} className="flex gap-2">
              <label className="min-w-0 flex-1">
                <span className="sr-only">{t("Код")}</span>
                <input
                  autoFocus
                  value={typed}
                  onChange={(e) => setTyped(e.target.value)}
                  placeholder={t("Штрихкод или артикул")}
                  className="h-12 w-full rounded-xl border border-white/20 bg-black/60 px-4 text-base text-white placeholder:text-white/50 focus:border-accent focus:outline-none"
                />
              </label>
              <button type="submit" disabled={!cleanCode(typed)} className="h-12 rounded-xl bg-accent px-5 text-sm font-semibold text-white disabled:opacity-50">
                {t("Найти")}
              </button>
            </form>
          ) : (
            <div className="flex items-center justify-center gap-4">
              {torch !== null && (
                <button type="button" onClick={() => void toggleTorch()} aria-pressed={torch} aria-label={torch ? t("Выключить фонарик") : t("Включить фонарик")} className={`${round} ${torch ? "bg-white text-black hover:bg-white" : ""}`}>
                  {torch ? <FlashlightOff className="size-5" aria-hidden /> : <Flashlight className="size-5" aria-hidden />}
                </button>
              )}
              <button type="button" onClick={() => setManual(true)} className="inline-flex h-12 items-center gap-2 rounded-full bg-white/15 px-5 text-sm font-semibold backdrop-blur hover:bg-white/25">
                <Keyboard className="size-5" aria-hidden /> {t("Ввести код")}
              </button>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
