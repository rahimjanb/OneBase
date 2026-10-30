import type { Metadata, Viewport } from "next";
import { headers } from "next/headers";
import { Inter, Manrope, Onest } from "next/font/google";
import { Suspense } from "react";
import { PwaRegister } from "@/components/shell/PwaRegister";
import { Splash, splashCss } from "@/components/shell/Splash";
import { SplashReady } from "@/components/shell/SplashReady";
import splashScreens from "@/components/shell/splashScreens.json";
import { themeInitScript } from "@/components/shell/ThemeToggle";
import { I18nProvider } from "@/i18n/client";
import { getLang, getT } from "@/i18n/server";
import { isFieldHost, requestHost } from "@/lib/field/host";
import "./globals.css";

const inter = Inter({ subsets: ["latin", "cyrillic"], variable: "--font-inter" });
// Заголовки — Manrope (кириллица есть).
const manrope = Manrope({ subsets: ["latin", "cyrillic"], variable: "--font-manrope" });
// OneBase (главная и весь интерфейс вне Sales Base) — Onest: один шрифт для текста и заголовков, веса 400–800 (переменный).
const onest = Onest({ subsets: ["latin", "cyrillic"], variable: "--font-onest" });

/** На домене Sales Base — своё имя приложения (вкладка, установленное приложение на iOS). */
export async function generateMetadata(): Promise<Metadata> {
  const field = isFieldHost(requestHost(await headers()));
  return field
    ? {
        ...baseMetadata,
        title: "Sales Base",
        applicationName: "Sales Base",
        description: "Sales Base — управление полевыми продажами",
        icons: { icon: [{ url: "/icons/field-192.png", type: "image/png", sizes: "192x192" }], apple: "/icons/field-apple-touch-icon.png" },
        appleWebApp: { capable: true, title: "Sales Base", statusBarStyle: "black-translucent" },
      }
    : baseMetadata;
}

const baseMetadata: Metadata = {
  title: "OneBase",
  applicationName: "OneBase",
  description: "Корпоративная платформа с AI-сотрудниками",
  // Манифест (/manifest.webmanifest) Next.js подключает сам по файлу app/manifest.ts.
  icons: {
    icon: [
      // ?v= — новый адрес для кэша: Cloudflare держал /favicon.ico прежнего сайта на домене (2022 г., срок 60 дней), и на вкладке
      // в проде был чужой значок. При смене значка — увеличить номер.
      { url: "/favicon.ico?v=2", sizes: "32x32" },
      { url: "/icons/icon-192.png", type: "image/png", sizes: "192x192" },
    ],
    apple: "/icons/apple-touch-icon.png",
  },
  // Установленное на iOS приложение: без адресной строки, строка состояния поверх тёмной шапки. Пока оно запускается — картинка
  // цвета заставки 1Base под размер экрана (scripts/splash-screens.mjs), а не белый экран.
  appleWebApp: {
    capable: true,
    title: "OneBase",
    statusBarStyle: "black-translucent",
    startupImage: splashScreens.flatMap(({ width, height, ratio }) =>
      (["portrait", "landscape"] as const).map((orientation) => {
        const [w, h] = orientation === "portrait" ? [width * ratio, height * ratio] : [height * ratio, width * ratio];
        return {
          url: `/splash/startup/${w}x${h}.png`,
          media: `(device-width: ${width}px) and (device-height: ${height}px) and (-webkit-device-pixel-ratio: ${ratio}) and (orientation: ${orientation})`,
        };
      }),
    ),
  },
  // Next.js выводит только mobile-web-app-capable; iOS до 16.4 открывает приложение без адресной строки и показывает картинки запуска
  // только по прежнему apple-mobile-web-app-capable.
  other: { "apple-mobile-web-app-capable": "yes" },
  formatDetection: { telephone: false },
};

export async function generateViewport(): Promise<Viewport> {
  const requestHeaders = await headers();
  const field = isFieldHost(requestHost(requestHeaders));
  // iPhone увеличивает страницу при фокусе на поле мельче 16px; maximum-scale=1 это отключает, а масштаб жестом iOS всё равно разрешает.
  // На телефоне OneBase весь текст мельче (globals.css), поля тоже — поэтому только для iPhone; Android при фокусе не увеличивает.
  const iPhone = /iPhone|iPod/.test(requestHeaders.get("user-agent") ?? "");
  return {
    width: "device-width",
    initialScale: 1,
    ...(iPhone ? { maximumScale: 1 } : {}),
    // Контент заходит под вырез и строку состояния; отступы — через env(safe-area-inset-*) в шапке и нижней панели.
    viewportFit: "cover",
    // Клавиатура уменьшает экран приложения (Android Chrome, Firefox): шапка остаётся на месте, поле ввода — над клавиатурой,
    // нижняя панель на это время прячется (useKeyboardOpen). iOS параметр не читает.
    interactiveWidget: "resizes-content",
    // Цвет строки состояния — как у шапки на телефоне (у Sales Base — зелёная); при переключении темы его обновляет ThemeToggle.
    themeColor: field
      ? [
          { media: "(prefers-color-scheme: light)", color: "#0d2e29" },
          { media: "(prefers-color-scheme: dark)", color: "#071a17" },
        ]
      : [
          // OneBase по умолчанию в тёмной стеклянной теме — шапка тёмная при любой системной теме.
          { media: "(prefers-color-scheme: light)", color: "#050c1c" },
          { media: "(prefers-color-scheme: dark)", color: "#050c1c" },
        ],
  };
}

/**
 * data-brand="onebase" — стеклянная тёмная тема OneBase (globals.css): по умолчанию, пока пользователь не выбрал светлую. На домене
 * Sales Base её нет — там своя тема по системной.
 *
 * OneBase открывается заставкой 1Base (Splash). Страница — за <Suspense>: без него сервер молчит, пока не получит данные из API
 * (/api/auth/me, данные страницы), и всё это время виден белый экран; с ним заставка уходит первыми байтами ответа. Цена: ответ
 * начинается с 200, и redirect()/notFound() на странице срабатывают уже в браузере (вход без сессии по-прежнему решает proxy).
 */
export default async function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  const field = isFieldHost(requestHost(await headers()));
  // Язык интерфейса (кнопка RU/UZ): атрибут lang — для озвучки и переносов, провайдер — для клиентских компонентов.
  const lang = await getLang();
  const t = await getT();
  return (
    <html lang={lang} className={`${inter.variable} ${manrope.variable} ${onest.variable}`} data-brand={field ? undefined : "onebase"} suppressHydrationWarning>
      <head>
        <script dangerouslySetInnerHTML={{ __html: themeInitScript }} />
        {!field && <style dangerouslySetInnerHTML={{ __html: splashCss }} />}
      </head>
      <body className="font-sans">
        {!field && <Splash label={t("Загрузка")} />}
        <I18nProvider lang={lang}>
          {field ? (
            children
          ) : (
            <Suspense>
              {children}
              <SplashReady />
            </Suspense>
          )}
          <PwaRegister />
        </I18nProvider>
      </body>
    </html>
  );
}
