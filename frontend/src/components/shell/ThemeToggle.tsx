"use client";

import { Moon, Sun } from "lucide-react";
import { useEffect, useState } from "react";
import { useT } from "@/i18n/client";

const KEY = "onebase-theme";

/**
 * Скрипт в <head>: применяет сохранённую тему до отрисовки, без мигания. Цвет строки состояния — от оболочки (--color-status-bar,
 * у стеклянной темы OneBase шапка прозрачная; иначе --color-sidebar), а на страницах без неё (вход) — от продукта страницы.
 * Тег заставки (с property, Splash.tsx) не перекрашивается: пока она на экране, строка состояния — её цвета.
 */
export const themeInitScript = `try{var t=localStorage.getItem("${KEY}");if(t){document.documentElement.dataset.theme=t;document.addEventListener("DOMContentLoaded",function(){var s=document.querySelector("[data-app-shell]")||document.querySelector("[data-product]");var st=s&&getComputedStyle(s);var c=st&&(st.getPropertyValue("--color-status-bar").trim()||st.getPropertyValue("--color-sidebar").trim())||(t==="dark"?"#050c1c":"#16213a");document.querySelectorAll('meta[name="theme-color"]:not([property])').forEach(function(m){m.setAttribute("content",c)})})}}catch(e){}`;

/** Тема сейчас: выбранная, иначе у OneBase — тёмная стеклянная (по умолчанию), у Sales Base — как в системе. */
export function currentTheme(): "light" | "dark" {
  const explicit = document.documentElement.dataset.theme;
  if (explicit === "light" || explicit === "dark") return explicit;
  if (document.documentElement.dataset.brand === "onebase") return "dark";
  return window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";
}

/** Включить тему и запомнить выбор; цвет строки состояния установленного приложения — как у шапки выбранной темы (у Sales Base — свой). */
export function applyTheme(next: "light" | "dark") {
  document.documentElement.dataset.theme = next;
  try {
    localStorage.setItem(KEY, next);
  } catch {
    // хранилище недоступно — тема действует до перезагрузки
  }
  const shell = document.querySelector<HTMLElement>("[data-app-shell]");
  const style = shell && getComputedStyle(shell);
  const color =
    (style && (style.getPropertyValue("--color-status-bar").trim() || style.getPropertyValue("--color-sidebar").trim())) || (next === "dark" ? "#050c1c" : "#16213a");
  for (const meta of document.querySelectorAll('meta[name="theme-color"]')) meta.setAttribute("content", color);
}

/** Кнопка темы в шапке: цвет берёт от шапки (на телефоне она тёмная, на больших экранах — светлая). */
export function ThemeToggle() {
  const t = useT();
  const [theme, setTheme] = useState<"light" | "dark" | null>(null);

  useEffect(() => setTheme(currentTheme()), []);

  function toggle() {
    const next = currentTheme() === "dark" ? "light" : "dark";
    applyTheme(next);
    setTheme(next);
  }

  return (
    <button
      type="button"
      onClick={toggle}
      aria-label={theme === "dark" ? t("Светлая тема") : t("Тёмная тема")}
      className="grid size-10 place-items-center rounded-full text-current transition-colors duration-150 hover:bg-white/10 hover:text-white focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent lg:size-9 lg:rounded-[9px] lg:text-ink-3 lg:hover:bg-muted lg:hover:text-ink"
    >
      {theme === "dark" ? <Sun className="size-4" strokeWidth={1.75} /> : <Moon className="size-4" strokeWidth={1.75} />}
    </button>
  );
}
