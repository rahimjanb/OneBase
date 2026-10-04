"use client";

import Link from "next/link";
import { useEffect, useRef, useState } from "react";
import { Camera, MapPin, Play, Square, X } from "lucide-react";
import { geoLabel, visitResultLabel } from "@/lib/field/labels";
import type { FieldVisitResult, FieldVisitRow } from "@/lib/field/types";
import { Chip, buttonClass, inputClass } from "./ui";
import { locate } from "@/lib/geo";
import { fieldApi, useAction } from "./hooks";

const results: FieldVisitResult[] = ["Order", "Sale", "Refusal", "Revisit", "NoDecisionMaker", "Closed", "Other"];

function Elapsed({ since }: { since: string }) {
  // Время — только в браузере: на сервере и в браузере минуты могли разойтись (ошибка гидратации).
  const [now, setNow] = useState<number | null>(null);
  useEffect(() => {
    setNow(Date.now());
    const t = setInterval(() => setNow(Date.now()), 15000);
    return () => clearInterval(t);
  }, []);
  if (now === null) return <span>…</span>;
  const minutes = Math.max(0, Math.round((now - new Date(since).getTime()) / 60000));
  return <span>{minutes} мин</span>;
}

async function uploadPhoto(visitId: string, file: File) {
  const form = new FormData();
  form.append("file", file);
  const res = await fetch(`/bff/api/field/visits/${visitId}/photo`, { method: "POST", body: form });
  if (!res.ok) {
    let message = `Фото не загрузилось (${res.status})`;
    try {
      message = ((await res.json()) as { error?: string }).error ?? message;
    } catch {
      // не JSON
    }
    throw new Error(message);
  }
}

/**
 * Визит в точке: прибыл → «Начать визит» (координаты из браузера, расстояние до точки считает сервер) → результат →
 * «Завершить». Плохой GPS не мешает: визит начнётся с отметкой «нет GPS».
 */
export function VisitPanel({
  marketId,
  marketName,
  routePointId,
  active,
  canStart,
  geoRadiusM,
  onFinished,
}: {
  marketId: number;
  marketName: string;
  routePointId?: string | null;
  active: FieldVisitRow | null;
  canStart: boolean;
  geoRadiusM: number;
  /** После завершения или отмены визита (например, закрыть шторку маршрута). */
  onFinished?: () => void;
}) {
  const { busy, error, run } = useAction();
  const [result, setResult] = useState<FieldVisitResult | null>(null);
  const [amount, setAmount] = useState("");
  const [comment, setComment] = useState("");
  const [revisit, setRevisit] = useState("");
  const [photo, setPhoto] = useState<File | null>(null);
  const fileRef = useRef<HTMLInputElement>(null);
  const [locating, setLocating] = useState(false);
  // Браузер запретил геолокацию: объясняем, почему визит без отметки места, и где включить.
  const [geoNote, setGeoNote] = useState<"denied" | null>(null);

  if (active && active.marketId !== marketId) {
    return (
      <div className="rounded-xl border border-warn/40 bg-warn-soft px-4 py-3 text-sm text-ink">
        Сейчас идёт визит в «{active.marketName}».{" "}
        <Link href={`/field/customers/${active.marketId}`} className="font-medium text-accent-strong underline">
          Завершите его
        </Link>
        , чтобы начать новый.
      </div>
    );
  }

  if (!active) {
    if (!canStart) return null;
    return (
      <div className="space-y-2">
        <button
          type="button"
          disabled={busy !== null || locating}
          onClick={async () => {
            setLocating(true);
            const { position: pos, reason } = await locate();
            setLocating(false);
            setGeoNote(reason === "denied" || reason === "dismissed" ? "denied" : null);
            await run("start", () =>
              fieldApi.post("visits/start", {
                marketId,
                routePointId: routePointId ?? null,
                latitude: pos?.latitude ?? null,
                longitude: pos?.longitude ?? null,
                accuracyM: pos?.accuracy ?? null,
              }),
            );
          }}
          className={`${buttonClass.primary} h-14 w-full text-base lg:h-12`}
        >
          <Play className="size-5" />
          {locating ? "Определяем местоположение…" : busy === "start" ? "Начинаем…" : "Начать визит"}
        </button>
        <p className="flex items-center gap-1.5 text-xs text-ink-3">
          <MapPin className="size-3.5" /> Координаты сверяются с точкой: в радиусе {geoRadiusM} м — «на месте». Без GPS визит тоже начнётся.
        </p>
        {error && <p className="rounded-lg bg-bad-soft px-3 py-2 text-sm text-bad">{error}</p>}
      </div>
    );
  }

  const [geoText, geoTone] = geoLabel[active.geoStatus];
  return (
    <div className="space-y-4 rounded-xl border-2 border-accent/50 bg-accent-soft/40 p-4">
      {geoNote === "denied" && active.geoStatus === "NoGps" && (
        <p className="rounded-lg bg-warn-soft px-3 py-2 text-sm text-ink">
          Геолокация не разрешена — визит идёт без отметки места. Разрешить её можно в профиле: «Уведомления и геолокация».
        </p>
      )}
      <div className="flex flex-wrap items-center gap-2">
        <span className="relative flex size-2.5">
          <span className="absolute inline-flex h-full w-full animate-ping rounded-full bg-accent opacity-60" />
          <span className="relative inline-flex size-2.5 rounded-full bg-accent" />
        </span>
        <span className="font-semibold text-ink">Идёт визит</span>
        <span className="text-sm text-ink-2">
          · <Elapsed since={active.startedAt} />
        </span>
        <Chip tone={geoTone}>
          {geoText}
          {active.distanceM != null ? ` · ${Math.round(active.distanceM)} м` : ""}
        </Chip>
      </div>

      <div>
        <div className="mb-2 text-sm font-medium text-ink">Результат визита</div>
        <div className="grid grid-cols-2 gap-2 sm:grid-cols-3">
          {results.map((r) => (
            <button
              key={r}
              type="button"
              onClick={() => setResult(r)}
              className={`h-12 rounded-xl border px-2 text-sm font-medium transition-colors ${result === r ? "border-accent bg-accent text-white" : "border-line bg-surface text-ink active:bg-muted"}`}
            >
              {visitResultLabel[r][0]}
            </button>
          ))}
        </div>
      </div>

      {(result === "Order" || result === "Sale") && (
        <label className="block">
          <span className="mb-1.5 block text-sm text-ink-2">Сумма, сум (необязательно)</span>
          <input inputMode="numeric" value={amount} onChange={(e) => setAmount(e.target.value.replace(/[^\d]/g, ""))} placeholder="например, 1 250 000" className={inputClass} />
        </label>
      )}

      {result === "Revisit" && (
        <label className="block">
          <span className="mb-1.5 block text-sm text-ink-2">Когда вернуться (по умолчанию — через 2 дня)</span>
          <input type="date" value={revisit} onChange={(e) => setRevisit(e.target.value)} className={inputClass} />
        </label>
      )}

      <label className="block">
        <span className="mb-1.5 block text-sm text-ink-2">Комментарий (необязательно)</span>
        <textarea value={comment} onChange={(e) => setComment(e.target.value)} rows={2} maxLength={2000} className={`${inputClass} h-auto py-2.5`} placeholder="что важно знать супервайзеру" />
      </label>

      <div className="flex items-center gap-2">
        <input ref={fileRef} type="file" accept="image/*" capture="environment" className="hidden" onChange={(e) => setPhoto(e.target.files?.[0] ?? null)} />
        <button type="button" onClick={() => fileRef.current?.click()} className={buttonClass.outline}>
          <Camera className="size-4" /> {photo ? "Фото выбрано" : "Фото"}
        </button>
        {photo && (
          <button type="button" onClick={() => setPhoto(null)} className={buttonClass.ghost} aria-label="Убрать фото">
            <X className="size-4" />
          </button>
        )}
      </div>

      {error && <p className="rounded-lg bg-bad-soft px-3 py-2 text-sm text-bad">{error}</p>}

      <div className="flex gap-2">
        <button
          type="button"
          disabled={!result || busy !== null}
          onClick={() =>
            run("finish", async () => {
              await fieldApi.post(`visits/${active.id}/finish`, {
                result,
                amount: amount ? Number(amount) : null,
                comment: comment || null,
                revisitDate: revisit || null,
              });
              // Визит уже завершён: ошибка фото не должна оставлять его «идущим» в интерфейсе.
              if (photo) {
                try {
                  await uploadPhoto(active.id, photo);
                } catch (e) {
                  window.alert(`Визит завершён, но фото не загрузилось: ${e instanceof Error ? e.message : String(e)}`);
                }
              }
            }, { onDone: () => onFinished?.() })
          }
          className={`${buttonClass.primary} h-12 flex-1 text-base`}
        >
          <Square className="size-4" />
          {busy === "finish" ? "Сохраняем…" : result ? "Завершить визит" : "Выберите результат"}
        </button>
        <button
          type="button"
          disabled={busy !== null}
          onClick={() => {
            if (confirm(`Отменить визит в «${marketName}»? Он не будет засчитан.`)) run("cancel", () => fieldApi.post(`visits/${active.id}/cancel`), { onDone: () => onFinished?.() });
          }}
          className={`${buttonClass.outline} h-12`}
        >
          Отменить
        </button>
      </div>
    </div>
  );
}
