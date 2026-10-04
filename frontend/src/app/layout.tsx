import type { Metadata, Viewport } from "next";
import { headers } from "next/headers";
import { Inter } from "next/font/google";
import { PwaRegister } from "@/components/shell/PwaRegister";
import { themeInitScript } from "@/components/shell/ThemeToggle";
import { isFieldHost, requestHost } from "@/lib/field/host";
import "./globals.css";

const inter = Inter({ subsets: ["latin", "cyrillic"], variable: "--font-inter" });

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

export const viewport: Viewport = {
  width: "device-width",
  initialScale: 1,
  // Контент заходит под вырез и строку состояния; отступы — через env(safe-area-inset-*) в шапке и нижней панели.
  viewportFit: "cover",
  // Цвет строки состояния — как у шапки на телефоне; при ручном переключении темы его обновляет ThemeToggle.
  themeColor: [
    { media: "(prefers-color-scheme: light)", color: "#16213a" },
    { media: "(prefers-color-scheme: dark)", color: "#0b1220" },
  ],
};

export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return (
    <html lang="ru" className={inter.variable} suppressHydrationWarning>
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
