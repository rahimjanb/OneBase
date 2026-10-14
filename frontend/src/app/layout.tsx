import type { Metadata, Viewport } from "next";
import { headers } from "next/headers";
import { Inter, Manrope } from "next/font/google";
import { PwaRegister } from "@/components/shell/PwaRegister";
import { themeInitScript } from "@/components/shell/ThemeToggle";
import { isFieldHost, requestHost } from "@/lib/field/host";
import "./globals.css";

const inter = Inter({ subsets: ["latin", "cyrillic"], variable: "--font-inter" });
// Заголовки — Manrope (кириллица есть).
const manrope = Manrope({ subsets: ["latin", "cyrillic"], variable: "--font-manrope" });

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
      { url: "/favicon.ico", sizes: "32x32" },
      { url: "/icons/icon-192.png", type: "image/png", sizes: "192x192" },
    ],
    apple: "/icons/apple-touch-icon.png",
  },
  // Установленное на iOS приложение: без адресной строки, строка состояния поверх тёмной шапки.
  appleWebApp: { capable: true, title: "OneBase", statusBarStyle: "black-translucent" },
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
 */
export default async function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  const field = isFieldHost(requestHost(await headers()));
  return (
    <html lang="ru" className={`${inter.variable} ${manrope.variable}`} data-brand={field ? undefined : "onebase"} suppressHydrationWarning>
      <head>
        <script dangerouslySetInnerHTML={{ __html: themeInitScript }} />
      </head>
      <body className="font-sans">
        {children}
        <PwaRegister />
      </body>
    </html>
  );
}
