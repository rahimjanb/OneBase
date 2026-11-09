import "server-only";
import { getT } from "@/i18n/server";
import { API_URL } from "@/lib/server-api";

type RecoveryMode = { isolated: boolean };

/** Режим меняется только перезапуском API — спрашиваем не чаще раза в минуту на процесс Next.js, а не на каждой странице. */
const TTL_MS = 60_000;
let cached: { mode: RecoveryMode; at: number } | null = null;

async function recoveryMode(): Promise<RecoveryMode> {
  if (cached && Date.now() - cached.at < TTL_MS) return cached.mode;
  try {
    const response = await fetch(`${API_URL}/api/recovery/mode`, { cache: "no-store", signal: AbortSignal.timeout(2000) });
    if (response.ok) {
      const mode = { isolated: Boolean(((await response.json()) as RecoveryMode).isolated) };
      cached = { mode, at: Date.now() };
      return mode;
    }
  } catch {
    // API не ответил — решает последний известный ответ.
  }
  return cached?.mode ?? { isolated: false };
}

/**
 * Плашка восстановленной копии для проверки (Recovery:Isolated на API): на всех страницах — вход, OneBase, Sales Base. Клики проходят
 * сквозь неё, верх шапки остаётся доступен.
 */
export async function RecoveryBanner() {
  if (!(await recoveryMode()).isolated) return null;
  const t = await getT();
  return (
    <div
      role="status"
      className="pointer-events-none fixed inset-x-0 top-0 z-[2147483647] flex justify-center px-3 pt-[env(safe-area-inset-top)]"
    >
      <span className="rounded-b-lg bg-amber-400 px-3 py-1 text-center text-xs font-semibold text-amber-950 shadow-lg">
        {t("Копия для проверки: внешние интеграции выключены")}
      </span>
    </div>
  );
}
