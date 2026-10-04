"use client";

import { Moon, Sun } from "lucide-react";
import { useEffect, useState } from "react";

const KEY = "onebase-theme";

/**
 * Скрипт в <head>: применяет сохранённую тему до отрисовки, без мигания. Цвет строки состояния — от оболочки, а на
 * страницах без неё (вход) — от продукта страницы (Sales Base — зелёный).
 */
export const themeInitScript = `try{var t=localStorage.getItem("${KEY}");if(t){document.documentElement.dataset.theme=t;document.addEventListener("DOMContentLoaded",function(){var s=document.querySelector("[data-app-shell]")||document.querySelector("[data-product]");var c=s&&getComputedStyle(s).getPropertyValue("--color-sidebar").trim()||(t==="dark"?"#0b1220":"#16213a");document.querySelectorAll('meta[name="theme-color"]').forEach(function(m){m.setAttribute("content",c)})})}}catch(e){}`;

function currentTheme(): "light" | "dark" {
  const explicit = document.documentElement.dataset.theme;
  if (explicit === "light" || explicit === "dark") return explicit;
  return window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";
}

/** Кнопка темы в шапке: цвет берёт от шапки (на телефоне она тёмная, на больших экранах — светлая). */
export function ThemeToggle() {
  const [theme, setTheme] = useState<"light" | "dark" | null>(null);

  useEffect(() => setTheme(currentTheme()), []);

  function toggle() {
    const next = currentTheme() === "dark" ? "light" : "dark";
    document.documentElement.dataset.theme = next;
    try {
      localStorage.setItem(KEY, next);
    } catch {
      // хранилище недоступно — тема действует до перезагрузки
    }
    // Цвет строки состояния установленного приложения — как у шапки выбранной темы (у Sales Base — свой, зелёный).
    const shell = document.querySelector<HTMLElement>("[data-app-shell]");
    const color = (shell && getComputedStyle(shell).getPropertyValue("--color-sidebar").trim()) || (next === "dark" ? "#0b1220" : "#16213a");
    for (const meta of document.querySelectorAll('meta[name="theme-color"]')) meta.setAttribute("content", color);
    setTheme(next);
  }

  return (
    <button
      type="button"
      onClick={toggle}
      aria-label={theme === "dark" ? "Светлая тема" : "Тёмная тема"}
      className="grid size-10 place-items-center rounded-full text-current transition-colors hover:bg-white/10 hover:text-white lg:size-9 lg:hover:bg-muted lg:hover:text-ink"
    >
      {theme === "dark" ? <Sun className="size-4" strokeWidth={1.75} /> : <Moon className="size-4" strokeWidth={1.75} />}
    </button>
  );
}
