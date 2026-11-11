import Link from "next/link";
import { ShieldAlert } from "lucide-react";
import { LogoutLink } from "@/components/shell/AppBridge";
import { getT } from "@/i18n/server";
import { isLimited } from "@/lib/sales/access";
import { mySalesScope } from "@/lib/sales/scope-server";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Нет доступа · OneBase") };
}

export default async function NoAccessPage({ searchParams }: { searchParams: Promise<{ from?: string | string[] }> }) {
  const t = await getT();
  const from = (await searchParams).from;
  const path = Array.isArray(from) ? from[0] : from;
  // «Продажи» с охватом РМ / СВР / ТП: страница всей страны закрыта охватом, а не правом — объясняем и ведём на свою страницу.
  const scope = path?.startsWith("/sales") ? await mySalesScope() : null;
  if (isLimited(scope)) {
    return (
      <div className="mx-auto max-w-[1800px] px-4 py-16 sm:px-6">
        <div className="max-w-lg rounded-xl border border-line bg-surface p-6">
          <ShieldAlert className="size-7 text-warn" />
          <h1 className="mt-3 text-lg font-semibold text-ink">{t("Эта страница продаж вам закрыта")}</h1>
          <p className="mt-2 text-sm text-ink-2">
            {t("Она показывает всю организацию, а ваш охват в «Продажах» — своя часть оргструктуры (направление, регион или вы сами). Расширить охват может администратор пользователей.")}
          </p>
          <div className="mt-5 flex flex-wrap gap-2">
            <Link href={scope!.home} className="inline-flex h-9 items-center rounded-lg bg-accent px-4 text-sm font-semibold text-white hover:bg-accent-strong max-lg:h-11">
              {t("Мои продажи")}
            </Link>
            <Link href="/" className="inline-flex h-9 items-center rounded-lg border border-line bg-surface px-4 text-sm font-medium text-ink hover:bg-muted max-lg:h-11">
              {t("На главную")}
            </Link>
          </div>
        </div>
      </div>
    );
  }
  return (
    <div className="mx-auto max-w-[1800px] px-4 py-16 sm:px-6">
      <div className="max-w-lg rounded-xl border border-line bg-surface p-6">
        <ShieldAlert className="size-7 text-warn" />
        <h1 className="mt-3 text-lg font-semibold text-ink">{t("Недостаточно прав")}</h1>
        <p className="mt-2 text-sm text-ink-2">
          {t("У вашей учётной записи нет доступа к этому разделу. Попросите администратора выдать нужное право или войдите под другим пользователем.")}
        </p>
        <div className="mt-5 flex flex-wrap gap-2">
          {/* Не Link: Link заранее загружает адрес, а /logout при загрузке удаляет сессию. LogoutLink сначала отключает уведомления устройства. */}
          <LogoutLink href="/logout" className="inline-flex h-9 items-center rounded-lg bg-accent px-4 text-sm font-semibold text-white hover:bg-accent-strong max-lg:h-11">
            {t("Войти под другим пользователем")}
          </LogoutLink>
          <Link href="/" className="inline-flex h-9 items-center rounded-lg border border-line bg-surface px-4 text-sm font-medium text-ink hover:bg-muted max-lg:h-11">
            {t("На главную")}
          </Link>
        </div>
      </div>
    </div>
  );
}
