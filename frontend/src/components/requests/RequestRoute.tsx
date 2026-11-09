"use client";

import Link from "next/link";
import { Check, CircleDashed, Clock3, Minus, OctagonX, Route as RouteIcon, Undo2, type LucideIcon } from "lucide-react";
import { useT } from "@/i18n/client";
import { money, stampRu, type RequestDetail, type RouteStep } from "@/lib/procurement";

const title: Record<RouteStep["key"], string> = {
  head: "Руководитель отдела",
  analyst: "Аналитик",
  finance: "Финансист",
  supply_head: "Руководитель снабжения",
  director: "Директор",
  supply: "Снабжение",
};

const look: Record<RouteStep["state"], { icon: LucideIcon; ring: string; text: string }> = {
  done: { icon: Check, ring: "border-ok/40 bg-ok-soft text-ok", text: "text-ink" },
  current: { icon: Clock3, ring: "border-accent/50 bg-accent-soft text-accent-strong", text: "text-ink" },
  pending: { icon: CircleDashed, ring: "border-line bg-muted text-ink-3", text: "text-ink-2" },
  skipped: { icon: Minus, ring: "border-line bg-muted text-ink-4", text: "text-ink-3" },
  rejected: { icon: OctagonX, ring: "border-bad/40 bg-bad-soft text-bad", text: "text-ink" },
  returned: { icon: Undo2, ring: "border-warn/40 bg-warn-soft text-warn", text: "text-ink" },
};

/**
 * Маршрут согласования заявки (текущий раунд): руководитель отдела → финансист → директор (если к закупке больше лимита) → снабжение;
 * заявка «Закупка сырья» — шаги по автору из настроек снабжения (аналитик, финансист, директор). У каждого шага — кто согласовал (дата,
 * комментарий), кто решает сейчас, кто впереди или почему шаг не нужен. Финансист или директор не выбран, аналитиков нет — подсказка,
 * где это исправить.
 */
export function RequestRoute({ detail }: { detail: RequestDetail }) {
  const t = useT();
  if (!detail.route) return null;
  const limit = `${money(detail.directorLimit)} ${t("сум")}`;

  const reason = (step: RouteStep) => {
    switch (step.reason) {
      case "initiatorHead":
        return t("Не нужен: заявку создал руководитель отдела");
      case "noHeads":
        return t("Не нужен: у отдела нет руководителя");
      case "noPurchase":
        return t("Не нужен: всё выдаётся со склада");
      case "underLimit":
        return t("Не нужен: к закупке не больше {limit}", { limit });
      case "initiatorSupply":
        return t("Не нужен: заявку создал руководитель снабжения");
      case "initiatorDirector":
        return t("Не нужен: заявку создал директор");
      default:
        return t("Не нужен");
    }
  };

  const status = (step: RouteStep) => {
    const who = step.who.join(", ");
    switch (step.state) {
      case "done":
        return step.key === "supply" ? t("Заявка выполнена") : t("Согласовал: {who}", { who });
      case "rejected":
        return t("Отклонил: {who}", { who });
      case "returned":
        return t("Вернул на доработку: {who}", { who });
      case "current":
        if (step.key === "supply") return t("Снабженцы покупают по заявке");
        return who ? t("Ждёт решения: {who}", { who }) : null;
      case "pending":
        if (step.key === "supply") return t("После согласования");
        return who ? t("Впереди: {who}", { who }) : null;
      default:
        return reason(step);
    }
  };

  return (
    <section className="rounded-2xl border border-line bg-surface p-4 sm:p-5" aria-labelledby="rq-route">
      <h2 id="rq-route" className="mb-3 flex items-center gap-2 text-base font-semibold text-ink">
        <RouteIcon className="size-4 text-ink-3" aria-hidden /> {t("Маршрут согласования")}
        {detail.approvalRound > 1 && <span className="text-sm font-normal text-ink-3">· {t("раунд {n}", { n: detail.approvalRound })}</span>}
      </h2>
      {detail.rawRoute && (
        <p className="-mt-1 mb-3 text-xs text-ink-3">
          {detail.route.some((s) => s.key === "supply_head")
            ? t("Закупка сырья: согласуют руководитель снабжения, затем директор — при любой сумме.")
            : t("Закупка сырья: шаги согласования — по автору заявки, как в настройках снабжения.")}
        </p>
      )}
      <ol className="flex flex-col">
        {detail.route.map((step, i) => {
          const l = look[step.state];
          const line = status(step);
          const missing = (step.state === "current" || step.state === "pending") && step.who.length === 0 && step.key !== "supply";
          return (
            <li key={step.key} className="relative flex gap-3 pb-4 last:pb-0" aria-current={step.state === "current" ? "step" : undefined}>
              {i < detail.route!.length - 1 && <span aria-hidden className="absolute left-[13px] top-7 h-[calc(100%-1.75rem)] w-px bg-line" />}
              <span className={`z-10 grid size-7 shrink-0 place-items-center rounded-full border ${l.ring}`}>
                <l.icon className="size-3.5" aria-hidden />
              </span>
              <div className="min-w-0 flex-1 pt-0.5">
                <p className={`text-sm font-medium ${step.state === "skipped" ? "text-ink-3" : l.text}`}>{t(title[step.key])}</p>
                {line && <p className={`text-xs ${step.state === "skipped" ? "text-ink-4" : "text-ink-2"}`}>{line}</p>}
                {missing && step.key === "analyst" && (
                  <p className="mt-0.5 text-xs text-warn">
                    {t("Аналитиков нет — заявка ждёт.")}{" "}
                    <Link href="/settings/users" className="underline underline-offset-2">
                      {t("Назначьте роль «Аналитик»")}
                    </Link>
                  </p>
                )}
                {missing && step.key !== "analyst" && (
                  <p className="mt-0.5 text-xs text-warn">
                    {step.key === "finance"
                      ? t("Финансист не выбран — заявка ждёт.")
                      : step.key === "supply_head"
                        ? t("Руководитель снабжения не выбран — заявка ждёт.")
                        : t("Директор не выбран — заявка ждёт.")}{" "}
                    <Link href="/settings/departments/supply" className="underline underline-offset-2">
                      {t("Настройки → Отделы → Снабжение")}
                    </Link>
                  </p>
                )}
                {step.at && <p className="text-xs text-ink-3">{stampRu(step.at)}</p>}
                {step.comment && <p className="mt-1 whitespace-pre-line rounded-lg bg-muted px-2.5 py-1.5 text-sm text-ink-2">{step.comment}</p>}
              </div>
            </li>
          );
        })}
      </ol>
    </section>
  );
}
