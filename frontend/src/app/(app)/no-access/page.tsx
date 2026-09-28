import Link from "next/link";
import { ShieldAlert } from "lucide-react";

export const metadata = { title: "Нет доступа · OneBase" };

export default function NoAccessPage() {
  return (
    <div className="mx-auto max-w-[1240px] px-4 py-16 sm:px-8">
      <div className="max-w-lg rounded-xl border border-line bg-surface p-6">
        <ShieldAlert className="size-7 text-warn" />
        <h1 className="mt-3 text-lg font-semibold text-ink">Недостаточно прав</h1>
        <p className="mt-2 text-sm text-ink-2">
          У вашей учётной записи нет доступа к этому разделу. Попросите администратора выдать нужное право или войдите под другим пользователем.
        </p>
        <div className="mt-5 flex flex-wrap gap-2">
          <Link href="/logout" className="inline-flex h-9 items-center rounded-lg bg-accent px-4 text-sm font-semibold text-white hover:bg-accent-strong">
            Войти под другим пользователем
          </Link>
          <Link href="/" className="inline-flex h-9 items-center rounded-lg border border-line bg-surface px-4 text-sm font-medium text-ink hover:bg-muted">
            На главную
          </Link>
        </div>
      </div>
    </div>
  );
}
