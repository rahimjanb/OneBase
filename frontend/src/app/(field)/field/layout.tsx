import { headers } from "next/headers";
import { FieldShell } from "@/components/field/FieldShell";
import { LogoutLink } from "@/components/shell/AppBridge";
import { FieldAccessError, fieldGet, fieldMe } from "@/lib/field/api";
import { FIELD_LOGOUT_PATH, oneBaseHref, requestHost } from "@/lib/field/host";
import type { FieldMe } from "@/lib/field/types";

export const metadata = { title: { template: "%s · Sales Base", default: "Sales Base" } };

/**
 * Sales Base — отдельная оболочка (не «Отделы» OneBase): навигация по роли, нижняя панель на телефоне.
 * Кто пользователь в Sales Base, решает backend (/api/field/me); без карточки участника — объяснение, а не пустые экраны.
 */
export default async function FieldLayout({ children }: { children: React.ReactNode }) {
  let me: FieldMe;
  try {
    me = await fieldMe();
  } catch (error) {
    if (!(error instanceof FieldAccessError)) throw error;
    // На домене Sales Base «/» — снова Sales Base: OneBase — по основному домену, если он задан.
    const oneBase = oneBaseHref(requestHost(await headers()), "/");
    return (
      <main data-product="field" className="grid min-h-dvh place-items-center bg-page px-4">
        <div className="w-full max-w-md rounded-2xl border border-line bg-surface p-6 text-center">
          <img src="/icons/field-192.png" alt="" className="mx-auto size-14 rounded-2xl" />
          <h1 className="mt-4 text-lg font-semibold text-ink">Нет доступа к Sales Base</h1>
          <p className="mt-2 text-sm text-ink-2">{error.message}</p>
          <div className="mt-5 flex justify-center gap-2">
            <LogoutLink href={FIELD_LOGOUT_PATH} className="inline-flex h-11 items-center rounded-lg border border-line px-4 text-sm text-ink hover:bg-muted">
              Войти другим пользователем
            </LogoutLink>
            {oneBase && (
              <a href={oneBase} className="inline-flex h-11 items-center rounded-lg bg-accent px-4 text-sm font-semibold text-white">
                OneBase
              </a>
            )}
          </div>
        </div>
      </main>
    );
  }

  return <FieldShell me={me}>{children}</FieldShell>;
}
