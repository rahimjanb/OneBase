import type { Metadata } from "next";
import { Inter } from "next/font/google";
import "./globals.css";

const inter = Inter({ subsets: ["latin", "cyrillic"], variable: "--font-inter" });

export const metadata: Metadata = {
  title: "OneBase",
  description: "Корпоративная платформа с AI-сотрудниками",
};

export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return (
    <html lang="ru" className={inter.variable}>
      <body className="font-sans">{children}</body>
    </html>
  );
}
