"use client";

import Link from "next/link";
import { useEffect, useRef, useState } from "react";
import { Camera, MapPin, Play, Square, X } from "lucide-react";
import { useT } from "@/i18n/client";
import type { T } from "@/i18n/core";
import { geoLabel, visitResultLabel } from "@/lib/field/labels";
import type { FieldVisitResult, FieldVisitRow } from "@/lib/field/types";
import { Chip, buttonClass, inputClass } from "./ui";
import { locate } from "@/lib/geo";
import { fieldApi, useAction } from "./hooks";

const results: FieldVisitResult[] = ["Order", "Sale", "Refusal", "Revisit", "NoDecisionMaker", "Closed", "Other"];

function Elapsed({ since }: { since: string }) {
  const t = useT();
  // Время — только в браузере: на сервере и в браузере минуты могли разойтись (ошибка гидратации).
  const [now, setNow] = useState<number | null>(null);
  useEffect(() => {
    setNow(Date.now());
    const timer = setInterval(() => setNow(Date.now()), 15000);
    return () => clearInterval(timer);
  }, []);
  if (now === null) return <span>…</span>;
  const minutes = Math.max(0, Math.round((now - new Date(since).getTime()) / 60000));
  return <span>{t("{n} мин", { n: minutes })}</span>;
}

async function uploadPhoto(visitId: string, file: File, t: T) {
  const form = new FormData();
  form.append("file", file);
  const res = await fetch(`/bff/api/field/visits/${visitId}/photo`, { method: "POST", body: form });
  if (!res.ok) {
    let message = t("Фото не загрузилось ({status})", { status: res.status });
    try {
      const error = ((await res.json()) as { error?: string }).error;
      if (error) message = t(error); // текст сервера — на языке интерфейса
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
  const t = useT();
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
        {t("Сейчас идёт визит в «{name}».", { name: active.marketName })}{" "}
        <Link href={`/field/customers/${active.marketId}`} className="font-medium text-accent-strong underline">
          {t("Завершите его")}
        </Link>
        {t(", чтобы начать новый.")}
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
          {locating ? t("Определяем местоположение…") : busy === "start" ? t("Начинаем…") : t("Начать визит")}
        </button>
        <p className="flex items-center gap-1.5 text-xs text-ink-3">
          <MapPin className="size-3.5" /> {t("Координаты сверяются с точкой: в радиусе {m} м — «на месте». Без GPS визит тоже начнётся.", { m: geoRadiusM })}
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
          {t("Геолокация не разрешена — визит идёт без отметки места. Разрешить её можно в профиле: «Уведомления и геолокация».")}
        </p>
      )}
      <div className="flex flex-wrap items-center gap-2">
        <span className="relative flex size-2.5">
          <span className="absolute inline-flex h-full w-full animate-ping rounded-full bg-accent opacity-60" />
          <span className="relative inline-flex size-2.5 rounded-full bg-accent" />
        </span>
        <span className="font-semibold text-ink">{t("Идёт визит")}</span>
        <span className="text-sm text-ink-2">
          · <Elapsed since={active.startedAt} />
        </span>
        <Chip tone={geoTone}>
          {t(geoText)}
          {active.distanceM != null ? ` · ${t("{n} м", { n: Math.round(active.distanceM) })}` : ""}
        </Chip>
      </div>

      <div>
        <div className="mb-2 text-sm font-medium text-ink">{t("Результат визита")}</div>
        <div className="grid grid-cols-2 gap-2 sm:grid-cols-3">
          {results.map((r) => (
            <button
              key={r}
              type="button"
              onClick={() => setResult(r)}
              className={`h-12 rounded-xl border px-2 text-sm font-medium transition-colors ${result === r ? "border-accent bg-accent text-white" : "border-line bg-surface text-ink active:bg-muted"}`}
            >
              {t(visitResultLabel[r][0])}
            </button>
          ))}
        </div>
      </div>

      {(result === "Order" || result === "Sale") && (
        <label className="block">
          <span className="mb-1.5 block text-sm text-ink-2">{t("Сумма, сум (необязательно)")}</span>
          <input inputMode="numeric" value={amount} onChange={(e) => setAmount(e.target.value.replace(/[^\d]/g, ""))} placeholder={t("например, 1 250 000")} className={inputClass} />
        </label>
      )}

      {result === "Revisit" && (
        <label className="block">
          <span className="mb-1.5 block text-sm text-ink-2">{t("Когда вернуться (по умолчанию — через 2 дня)")}</span>
          <input type="date" value={revisit} onChange={(e) => setRevisit(e.target.value)} className={inputClass} />
        </label>
      )}

      <label className="block">
        <span className="mb-1.5 block text-sm text-ink-2">{t("Комментарий (необязательно)")}</span>
        <textarea value={comment} onChange={(e) => setComment(e.target.value)} rows={2} maxLength={2000} className={`${inputClass} h-auto py-2.5`} placeholder={t("что важно знать супервайзеру")} />
      </label>

      <div className="flex items-center gap-2">
        <input ref={fileRef} type="file" accept="image/*" capture="environment" className="hidden" onChange={(e) => setPhoto(e.target.files?.[0] ?? null)} />
        <button type="button" onClick={() => fileRef.current?.click()} className={buttonClass.outline}>
          <Camera className="size-4" /> {photo ? t("Фото выбрано") : t("Фото")}
        </button>
        {photo && (
          <button type="button" onClick={() => setPhoto(null)} className={buttonClass.ghost} aria-label={t("Убрать фото")}>
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
                  await uploadPhoto(active.id, photo, t);
                } catch (e) {
                  window.alert(t("Визит завершён, но фото не загрузилось: {error}", { error: e instanceof Error ? e.message : String(e) }));
                }
              }
            }, { onDone: () => onFinished?.() })
          }
          className={`${buttonClass.primary} h-12 flex-1 text-base`}
        >
          <Square className="size-4" />
          {busy === "finish" ? t("Сохраняем…") : result ? t("Завершить визит") : t("Выберите результат")}
        </button>
        <button
          type="button"
          disabled={busy !== null}
          onClick={() => {
            if (confirm(t("Отменить визит в «{name}»? Он не будет засчитан.", { name: marketName }))) run("cancel", () => fieldApi.post(`visits/${active.id}/cancel`), { onDone: () => onFinished?.() });
          }}
          className={`${buttonClass.outline} h-12`}
        >
          {t("Отменить")}
        </button>
      </div>
    </div>
  );
}
