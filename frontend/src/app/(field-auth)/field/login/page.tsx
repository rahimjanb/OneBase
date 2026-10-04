import { headers } from "next/headers";
import { CalendarCheck, ChartColumn, Route, Sparkles } from "lucide-react";
import { LoginForm } from "@/app/login/LoginForm";
import { fieldHomePath, isFieldHost, requestHost } from "@/lib/field/host";

export const metadata = { title: "Вход · Sales Base" };

const features = [
  { icon: Route, title: "Маршрут на день", text: "Точки из плана Linko, задач и тех, кого пора навестить." },
  { icon: CalendarCheck, title: "Визиты у точки", text: "Отметка по геолокации, результат визита и фото." },
  { icon: ChartColumn, title: "План и KPI", text: "Темп продаж, прогноз месяца и покрытие точек." },
  { icon: Sparkles, title: "AI-планирование", text: "Кого навестить и что проверить, решение за супервайзером." },
];

/**
 * Вход в Sales Base — своя страница: на домене Sales Base открывается по «/login» (proxy), в OneBase — /field/login.
 * Форма и проверка пароля — общие с OneBase; после входа — главная Sales Base или запрошенная страница.
 */
export default async function FieldLoginPage({ searchParams }: { searchParams: Promise<{ next?: string }> }) {
  const { next } = await searchParams;
  const host = requestHost(await headers());
  const home = fieldHomePath(host);

  return (
    <main data-product="field" className="flex min-h-dvh flex-col bg-page lg:flex-row">
      {/* Бренд: сверху на телефоне, слева на большом экране */}
      <section className="relative overflow-hidden bg-sidebar px-6 pb-20 pt-[calc(env(safe-area-inset-top)+2.5rem)] text-sidebar-text lg:flex lg:w-[46%] lg:max-w-[640px] lg:flex-col lg:justify-between lg:px-12 lg:py-12">
        <RouteArt />
        <div className="relative flex items-center gap-3">
          <img src="/icons/field-192.png" alt="" width={48} height={48} className="size-12 rounded-2xl" />
          <div className="leading-tight">
            <div className="text-xl font-semibold text-white">Sales Base</div>
            <div className="text-sm text-sidebar-muted">Field Sales Management</div>
          </div>
        </div>

        <p className="relative mt-6 max-w-sm text-sm text-sidebar-text lg:hidden">Маршруты, визиты, задачи и KPI полевой команды.</p>

        <div className="relative hidden lg:block">
          <h2 className="max-w-md text-[28px] font-semibold leading-tight text-white">Полевые продажи в одном приложении</h2>
          <ul className="mt-8 space-y-5">
            {features.map((f) => (
              <li key={f.title} className="flex gap-3.5">
                <span className="grid size-10 shrink-0 place-items-center rounded-xl bg-white/10 text-white">
                  <f.icon className="size-5" strokeWidth={1.75} />
                </span>
                <span>
                  <span className="block text-sm font-semibold text-white">{f.title}</span>
                  <span className="block text-sm text-sidebar-muted">{f.text}</span>
                </span>
              </li>
            ))}
          </ul>
        </div>

        <p className="relative hidden text-xs text-sidebar-muted lg:block">Часть OneBase · продажи и точки из Linko</p>
      </section>

      {/* Форма: на телефоне карточка заходит на тёмный блок */}
      <section className="relative z-10 -mt-12 flex flex-1 items-start justify-center px-4 pb-10 lg:mt-0 lg:items-center lg:px-10">
        <div className="w-full max-w-sm">
          <div className="rounded-xl shadow-[0_12px_40px_-12px_rgb(0_0_0/0.25)] lg:shadow-none">
            <LoginForm next={next ?? home} product="field" title="Вход в Sales Base" hint="Логин и пароль выдаёт РМ или супервайзер." />
          </div>
          <p className="mt-4 text-center text-xs text-ink-3">С телефона: откройте сайт в браузере и выберите «Установить приложение».</p>
          {!isFieldHost(host) && (
            <p className="mt-2 text-center text-xs">
              <a href="/login" className="text-ink-3 underline-offset-2 hover:text-ink hover:underline">
                Вход в OneBase
              </a>
            </p>
          )}
        </div>
      </section>
    </main>
  );
}

/** Декор: пунктирный маршрут с точками визитов. */
function RouteArt() {
  return (
    <svg aria-hidden="true" viewBox="0 0 400 400" className="pointer-events-none absolute -right-16 -top-10 h-[320px] w-[320px] text-accent opacity-40 lg:-right-24 lg:bottom-0 lg:top-auto lg:h-[520px] lg:w-[520px]">
      <path d="M40 330 C 110 300, 90 220, 170 210 S 260 120, 330 70" fill="none" stroke="currentColor" strokeWidth="3" strokeDasharray="2 10" strokeLinecap="round" />
      <path d="M60 120 C 120 160, 200 120, 250 170 S 330 280, 370 300" fill="none" stroke="currentColor" strokeWidth="2" strokeDasharray="1 9" strokeLinecap="round" opacity="0.6" />
      {[
        [40, 330],
        [170, 210],
        [330, 70],
        [250, 170],
      ].map(([x, y]) => (
        <g key={`${x}-${y}`}>
          <circle cx={x} cy={y} r="14" fill="currentColor" opacity="0.18" />
          <circle cx={x} cy={y} r="5" fill="currentColor" />
        </g>
      ))}
    </svg>
  );
}
