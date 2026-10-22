import Link from "next/link";
import { getT } from "@/i18n/server";

/** Объекта нет или он вне зоны пользователя — backend отвечает 404, о чужих данных не сообщаем. */
export default async function NotFound() {
  const t = await getT();
  return (
    <div className="mx-auto max-w-lg px-4 pt-10 text-center">
      <h1 className="text-lg font-semibold text-ink">{t("Не найдено")}</h1>
      <p className="mt-2 text-sm text-ink-2">{t("Такой записи нет или она не в вашей зоне.")}</p>
      <Link href="/field" className="mt-5 inline-flex h-11 items-center rounded-lg bg-accent px-5 text-sm font-semibold text-white">
        {t("На главную")}
      </Link>
    </div>
  );
}
