"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { Loader2, ShieldCheck } from "lucide-react";
import { primary } from "@/components/ai/form";
import { ErrorNote } from "@/components/tasks/TaskBadges";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import type { QualitySettings } from "@/lib/quality";

/**
 * «Настройки → Отделы → Снабжение → Контроль качества»: кто решает «допустить / забраковать», блокирует и снимает блокировку партий. Выбрать
 * можно сотрудника с правом «Качество сырья» (роль «Лаборатория»); технический администратор сюда не попадает. Правит — право «Настройки
 * снабжения».
 */
export function QualitySettingsSection({ settings }: { settings: QualitySettings }) {
  const t = useT();
  const router = useRouter();
  const [chosen, setChosen] = useState<string[]>(settings.approvers.map((a) => a.id));
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);
  // Уже назначенный, у которого сняли право, остаётся в списке — его можно убрать.
  const people = [...settings.candidates, ...settings.approvers.filter((a) => !settings.candidates.some((c) => c.id === a.id))];
  const changed = chosen.length !== settings.approvers.length || chosen.some((id) => !settings.approvers.some((a) => a.id === id));

  const save = async () => {
    setError(null);
    setSaved(false);
    setBusy(true);
    try {
      await bff("quality/settings", { method: "PUT", body: JSON.stringify({ approverIds: chosen }) });
      setSaved(true);
      router.refresh();
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setBusy(false);
    }
  };

  return (
    <section className="rounded-xl border border-line bg-surface p-5" aria-labelledby="supply-quality">
      <h2 id="supply-quality" className="flex items-center gap-2 text-base font-semibold text-ink">
        <ShieldCheck className="size-4 text-ink-3" aria-hidden /> {t("Контроль качества")}
      </h2>
      <p className="mt-1 text-sm text-ink-2">
        {t(
          "Кто решает по партиям сырья: допустить или забраковать, заблокировать и снять блокировку. Документы и показатели вносит лаборатория, а решение — только назначенные здесь: право само по себе не даёт решать.",
        )}
      </p>
      {people.length === 0 ? (
        <p className="mt-3 rounded-lg bg-muted px-3 py-2 text-sm text-ink-3">
          {t("Пока некого назначить: дайте сотруднику роль «Лаборатория» в «Пользователях».")}
        </p>
      ) : settings.canEdit ? (
        <ul className="mt-3 grid gap-1 sm:grid-cols-2">
          {people.map((p) => (
            <li key={p.id}>
              <label className="flex items-center gap-2 rounded-lg px-2 py-1.5 text-sm text-ink hover:bg-muted">
                <input
                  type="checkbox"
                  checked={chosen.includes(p.id)}
                  onChange={() => setChosen((all) => (all.includes(p.id) ? all.filter((x) => x !== p.id) : [...all, p.id]))}
                  className="size-4 accent-[var(--color-accent)]"
                />
                {p.name}
              </label>
            </li>
          ))}
        </ul>
      ) : (
        <p className="mt-3 text-sm text-ink">
          {settings.approvers.length > 0 ? settings.approvers.map((a) => a.name).join(", ") : t("Никто не назначен — партии на контроле ждут решения.")}
        </p>
      )}
      {settings.canEdit && people.length > 0 && chosen.length === 0 && (
        <p className="mt-2 text-xs text-warn">{t("Никто не назначен — партии на контроле будут ждать решения.")}</p>
      )}
      {settings.canEdit && people.length > 0 && (
        <div className="mt-3 flex flex-wrap items-center gap-3">
          <button type="button" onClick={() => void save()} disabled={busy || !changed} className={primary}>
            {busy && <Loader2 className="size-4 animate-spin" aria-hidden />}
            {t("Сохранить")}
          </button>
          {saved && !changed && <span className="text-sm text-ok">{t("Сохранено")}</span>}
        </div>
      )}
      <div className="mt-2">
        <ErrorNote text={error} />
      </div>
    </section>
  );
}
