"use client";

import { useEffect, useRef, useState } from "react";
import { ArrowRight, Info, X } from "lucide-react";
import { useT } from "@/i18n/client";
import type { HrOverview } from "@/lib/hr";
import { focus } from "./bits";

/**
 * Пояснение под блоками: одна строка, полные правила — в поповере по «Подробнее о статусах». В поповере же — кто не попал в «по графику»
 * (выходной, без графика) и сотрудники, пропавшие из последней выгрузки Verifix.
 */
export function StatusNote({ o }: { o: HrOverview }) {
  const t = useT();
  const [open, setOpen] = useState(false);
  const box = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;
    const onDown = (e: PointerEvent) => !box.current?.contains(e.target as Node) && setOpen(false);
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && setOpen(false);
    document.addEventListener("pointerdown", onDown);
    document.addEventListener("keydown", onKey);
    return () => {
      document.removeEventListener("pointerdown", onDown);
      document.removeEventListener("keydown", onKey);
    };
  }, [open]);

  const extra = [
    o.dayOff > 0 && t("Выходной по графику: {n}", { n: o.dayOff }),
    o.noSchedule > 0 && t("Без графика в Verifix: {n}", { n: o.noSchedule }),
    o.missingInVerifix > 0 && t("Не пришли в последней выгрузке Verifix (записи сохранены): {n}", { n: o.missingInVerifix }),
  ].filter(Boolean) as string[];

  return (
    <div ref={box} className="relative flex items-start gap-2 text-xs text-(--ts-subtle)">
      <Info aria-hidden className="mt-px size-3.5 shrink-0" />
      <p className="min-w-0">
        <span className="max-md:hidden">
          {t("«Отсутствует» — рабочий день по графику, нет отпуска/больничного, смена началась, отметок нет. Одна отметка — ещё не рабочий день.")}
        </span>
        <span className="md:hidden">{t("«Отсутствует» — смена началась, нет документа и ни одной отметки.")}</span>{" "}
        <button
          type="button"
          aria-expanded={open}
          aria-controls="hr-status-rules"
          onClick={() => setOpen((v) => !v)}
          className={`inline-flex cursor-pointer items-center gap-1 rounded font-medium text-(--ts-muted) hover:text-(--ts-text-2) ${focus}`}
        >
          <span className="max-md:hidden">{t("Подробнее о статусах")}</span>
          <span className="md:hidden">{t("Подробнее")}</span>
          <ArrowRight aria-hidden className="size-3" />
        </button>
      </p>
      {open && (
        <div
          id="hr-status-rules"
          role="dialog"
          aria-label={t("О статусах")}
          className="absolute bottom-[calc(100%+8px)] left-0 z-40 w-[min(560px,calc(100vw-32px))] rounded-[12px] border border-(--ts-border) bg-(--ts-surface) p-4 text-[13px] leading-relaxed text-(--ts-text-2) shadow-[0_12px_32px_#0e13221f]"
        >
          <button
            type="button"
            aria-label={t("Закрыть")}
            onClick={() => setOpen(false)}
            className={`absolute right-2 top-2 grid size-7 cursor-pointer place-items-center rounded-[7px] text-(--ts-subtle) hover:bg-(--ts-surface-2) ${focus}`}
          >
            <X className="size-4" />
          </button>
          <p className="pr-6">
            {t(
              "«Отсутствует» ставится, только если день рабочий по графику Verifix, нет отпуска, больничного, командировки или отгула, прошло начало смены с допуском и отметки за это время загружены полностью. Иначе — «ожидается», «нет графика» или «не ясно». Одна отметка — ещё не рабочий день: приход засчитывается по табелю Verifix или отметке «Приход».",
            )}
          </p>
          <p className="mt-2">{t("Явка — на месте (вовремя и опоздавшие) от «по графику»: выходные и дни без графика в неё не входят.")}</p>
          {extra.length > 0 && <p className="mt-2 text-(--ts-muted)">{extra.join(". ")}.</p>}
        </div>
      )}
    </div>
  );
}
