import { MyDay } from "@/components/me/MyDay";
import { getLocale, getT } from "@/i18n/server";
import { apiSession } from "@/lib/server-api";
import type { Me } from "@/lib/users";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Мой день · OneBase") };
}

/**
 * «Мой день» по прямой ссылке — любому вошедшему: свои рабочее время (по привязке к Verifix), задачи, календарь, документы и AI-план.
 * У роли «Сотрудник» это и есть главная («/»); блоки без привязки или прав показывают своё состояние.
 */
export default async function MyDayPage() {
  const [me, locale] = await Promise.all([apiSession<Me>("/api/auth/me"), getLocale()]);
  return <MyDay me={me} now={new Date()} locale={locale} />;
}
