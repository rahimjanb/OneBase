import { redirect } from "next/navigation";
import { TwinView } from "@/components/twin/TwinView";
import { getT } from "@/i18n/server";
import { canOpen, homeFor } from "@/lib/navigation";
import { apiSession, apiTry } from "@/lib/server-api";
import { isLayer, type TwinSnapshot } from "@/lib/twin";
import type { Me } from "@/lib/users";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Цифровой двойник · OneBase") };
}

/** Сколько странице ждать снимок на сервере. */
const SERVER_WAIT_MS = 2500;

type SearchParams = Record<string, string | string[] | undefined>;
const one = (sp: SearchParams, key: string) => (Array.isArray(sp[key]) ? sp[key][0] : sp[key]);

/**
 * «Цифровой двойник» (Operational Twin, docs/twin.md): 3D-модель территории завода, здания привязаны к отделам и складам OneBase, показатели —
 * из модулей по правам пользователя (GET /api/twin). Первый снимок — с сервера, дальше страница обновляется сама.
 */
export default async function TwinPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const me = await apiSession<Me>("/api/auth/me");
  if (me && !canOpen("/twin", me.permissions)) redirect(homeFor(me.permissions));
  // Снимок обычно из кэша разделов (доли секунды); сразу после сброса кэша продаж расчёт идёт секунды — тогда страница открывается сразу,
  // а показатели догружает браузер.
  const quick = new Promise<null>((resolve) => setTimeout(() => resolve(null), SERVER_WAIT_MS));
  const [snapshot, sp] = await Promise.all([Promise.race([apiTry<TwinSnapshot>("/api/twin"), quick]), searchParams]);
  const layer = one(sp, "layer");
  return <TwinView initial={snapshot} permissions={me?.permissions ?? []} initialLayer={isLayer(layer) ? layer : "all"} initialKey={one(sp, "b") ?? null} />;
}
