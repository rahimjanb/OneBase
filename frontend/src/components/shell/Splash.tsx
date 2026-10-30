import type { CSSProperties } from "react";

/**
 * Заставка 1Base при открытии OneBase: буквы влетают снизу по очереди (≈1,5 с), затем логотип «дышит» и бежит полоска.
 * Стоит первой в <body> корневого layout, а страница — за границей ожидания (<Suspense>): сервер отдаёт заставку первыми байтами,
 * пока сам ещё запрашивает API, — вместо белого экрана сразу тёмно-синий фон и анимация. Стили, скрипт и буквы (вектор) — прямо
 * в HTML, без ожидания JS-бандла и без запросов картинок. Уходит, когда страница пришла и ожила (SplashReady), но не раньше конца
 * сборки букв; не позже 8 с. До первой отрисовки страницы iOS показывает картинку запуска, Android — экран из манифеста: оба
 * того же цвета #001042 (splashScreens.json, manifest.ts).
 *
 * CSS — без inset и aspect-ratio: заставка должна работать и в старых Safari и Chrome, где приложение уже не поддерживается.
 */
export const splashCss = `
#splash {
  --splash-blue: #0581f8;
  position: fixed; top: 0; right: 0; bottom: 0; left: 0; z-index: 9999;
  display: flex; flex-direction: column; align-items: center; justify-content: center;
  background: #001042; overflow: hidden;
  transition: opacity .5s ease, visibility .5s;
}
#splash.hide { opacity: 0; visibility: hidden; pointer-events: none; }
#splash.gone { display: none; }

/* Ширина надписи — 52% экрана, но не больше 220 px; высота — по пропорции букв (WORD). Сначала постоянный размер: для браузеров без min(). */
#splash .word { position: relative; width: 220px; height: 64.4px; width: min(52vw, 220px); height: min(15.21vw, 64.4px); }
#splash .ch {
  position: absolute; top: 0; height: 100%; overflow: visible; transform-origin: 50% 50%;
  opacity: 0; transform: translateY(55%) scale(.8); filter: blur(6px);
  animation: splash-assemble .9s cubic-bezier(.22, 1.2, .36, 1) forwards;
  animation-delay: calc(.15s + var(--i) * .13s);
  will-change: transform, opacity, filter;
}
@keyframes splash-assemble {
  0% { opacity: 0; transform: translateY(55%) scale(.8); filter: blur(6px); }
  60% { opacity: 1; filter: blur(0); }
  100% { opacity: 1; transform: translateY(0) scale(1); filter: blur(0); }
}

#splash.loop .ch { animation: none; opacity: 1; filter: none; transform: none; }
#splash.loop .word { animation: splash-breathe 2.4s cubic-bezier(.45, 0, .55, 1) infinite; }
@keyframes splash-breathe { 0%, 100% { transform: scale(1); } 50% { transform: scale(1.035); } }

#splash .bar {
  position: relative; width: 96px; height: 3px; margin-top: 36px; border-radius: 3px;
  background: rgba(5, 129, 248, .35); overflow: hidden;
  opacity: 0; transition: opacity .5s ease;
}
#splash .bar::after {
  content: ""; position: absolute; top: 0; left: 0; height: 100%; width: 45%;
  border-radius: 3px; background: var(--splash-blue); transform: translateX(-110%);
}
#splash.loop .bar { opacity: 1; }
#splash.loop .bar::after { animation: splash-slide 1.4s cubic-bezier(.65, 0, .35, 1) infinite; }
@keyframes splash-slide { 0% { transform: translateX(-110%); } 100% { transform: translateX(330%); } }

@media (prefers-reduced-motion: reduce) {
  #splash .ch { animation: none; opacity: 1; transform: none; filter: none; }
  #splash.loop .word, #splash.loop .bar::after { animation: none; }
  #splash .bar { opacity: 1; }
  #splash .bar::after { width: 100%; opacity: .6; }
}
`;

/**
 * Сборка закончилась (animationend последней буквы) — цикл. hideSplash() — страница готова: заставка гаснет за 0,5 с, но не раньше
 * конца сборки; потом display:none, чтобы бесконечная анимация не крутилась под приложением. showSplash() — снова с начала.
 * Элемент ищется заново при каждом вызове: если React пересоздаст разметку, скрытие всё равно дойдёт до неё.
 * Пока заставка на экране, строка состояния (theme-color: Android, Safari) — её цвета: свой <meta> первым в <head>, браузер берёт
 * первый подходящий. Теги React не трогаем: он узнаёт их при гидратации по content и при чужом значении вставил бы вторые.
 */
const splashScript = `(function(){
var reduced=matchMedia("(prefers-reduced-motion: reduce)").matches,assembled=reduced,ready=false,timer;
var tintMeta=document.createElement("meta");tintMeta.name="theme-color";tintMeta.content="#001042";
function tint(on){if(on)document.head.insertBefore(tintMeta,document.head.firstChild);else if(tintMeta.parentNode)tintMeta.parentNode.removeChild(tintMeta)}
function el(){return document.getElementById("splash")}
function hide(){var s=el();if(!s||s.classList.contains("hide"))return;s.classList.add("hide");tint(false);setTimeout(function(){if(s.classList.contains("hide"))s.classList.add("gone")},500)}
var s=el();if(!s)return;
tint(true);
var letters=s.querySelectorAll(".ch");
letters[letters.length-1].addEventListener("animationend",function(){assembled=true;var s=el();if(s)s.classList.add("loop");if(ready)hide()});
window.hideSplash=function(){ready=true;if(assembled)hide()};
window.showSplash=function(){var s=el();if(!s)return;ready=false;assembled=reduced;s.classList.remove("loop","hide","gone");tint(true);s.querySelectorAll(".ch").forEach(function(l){l.style.animation="none";void l.offsetWidth;l.style.animation=""});clearTimeout(timer);timer=setTimeout(hide,8000)};
timer=setTimeout(hide,8000);
})()`;

/**
 * Буквы логотипа — вектор: резкие на любом экране (2×, 3×, дробные плотности Android). Контуры сняты трассировкой с
 * docs/brand/1base-logo.png (1254 px) в координатах надписи WORD; x и width — положение буквы по горизонтали.
 */
const WORD = { width: 1009.6, height: 295.4 };
const LETTERS = [
  { name: "1", x: 0, width: 134.8, d: "M109.4 0.6L90.8 0.8 86 3.1C83.4 4.3 79.4 6.2 77.2 7.2C74.9 8.3 71.1 10 68.8 11.1C66.5 12.2 64.6 13.1 64.5 13.1C64.5 13.1 59.3 15.5 49.7 20C47.8 20.9 43.6 22.9 40.4 24.4C37.2 25.9 32.7 28 30.4 29.1C28.1 30.1 24.5 31.8 22.4 32.8C15.9 35.8 14.4 36.5 11.4 37.9C3.7 41.4 1.3 42.8 0.9 43.9C0.4 45 0.6 105.1 1.1 105.9C1.7 107.1 1.6 107.1 12.9 102.6C15.5 101.5 20.2 99.7 23.4 98.4C26.6 97.1 31 95.4 33.2 94.5C35.3 93.6 39.6 91.8 42.8 90.5C46 89.2 49.9 87.6 51.5 86.8C56.3 84.7 63.3 82.1 63.8 82.3C64.8 82.7 64.8 79.8 64.9 185.9L65 288.4 65.6 288.9L66.2 289.5 99.6 289.5C134.5 289.5 134.2 289.5 134.8 288.5C135.3 287.8 135.5 3.1 135.1 1.7C134.6 0.4 135.6 0.4 109.4 0.6" },
  { name: "B", x: 164.9, width: 218.7, d: "M166.1 5.1L165.5 5.6 165.5 146.4C165.4 223.8 165.5 287.5 165.6 288C166.1 289.8 160 289.7 232.3 289.6L297 289.5 301.7 288.9C335.2 284.9 360.6 269.6 373.8 245.5C393.9 208.5 384 160.7 351.5 139.1C347.1 136.1 347.1 136.2 351.2 131.6C379.4 100 374.3 48.4 340.4 23.2C326.2 12.6 308.8 6.3 290.3 4.9C282.9 4.4 166.8 4.6 166.1 5.1M232.6 63.2C231.2 63.7 231.3 62.7 231.3 88.4C231.3 113.9 231.3 113.3 232.5 113.8C233.8 114.3 276.2 114.3 280.2 113.8C311.9 109.6 312.3 67 280.7 63.3C277 62.8 233.8 62.7 232.6 63.2M233.3 171.6C231.2 172.1 231.1 172.8 231.3 208L231.4 229.2 232 229.8L232.6 230.4 256.2 230.5C279.7 230.6 284.7 230.5 289.2 229.8C326.4 223.6 323.3 174.5 285.5 171.7C281.5 171.4 234.9 171.3 233.3 171.6" },
  { name: "a", x: 391, width: 221.4, d: "M485.9 82.2C465.3 84 447.7 91.3 430.3 105.1C386.1 140.2 378.5 214.6 414.4 260.6C446.4 301.7 509.9 307.7 546.2 273C549.3 270 549.3 269.9 549.4 280.3L549.5 288.7 550.1 289.2C550.7 289.6 551.6 289.6 581.2 289.6L611.6 289.6 612.2 289L612.8 288.4 612.8 188.1L612.8 87.9 612.2 87.3L611.7 86.8 581.2 86.7L550.6 86.6 550.1 87.2C549.5 87.8 549.5 87.8 549.4 95.4C549.2 105.6 549.3 105.5 544.6 101.1C528.8 86.5 509 80.1 485.9 82.2M496.7 138.2C461.9 142.5 442.7 177.3 457.4 209.3C475.4 248.4 533.1 244.3 546.9 203C557.8 170 530 134 496.7 138.2" },
  { name: "s", x: 622.6, width: 171.2, d: "M702 82C701.6 82.1 699.5 82.3 697.3 82.5C651.1 87.1 621.5 121.5 629.7 161.2C634.9 186.3 651 199.6 687.8 209C693.4 210.4 695.4 210.9 705.3 213.1C727.2 218.1 733.7 222.1 734.1 230.8C734.6 240.9 724.8 247.7 709.9 247.7C695.4 247.7 685.8 241.6 682.6 230.4C682.2 229 681.7 227.6 681.4 227.3C681 226.8 633.3 226.4 626.6 226.9C622.8 227.1 622.6 227.7 623.6 235.7C629 275.9 662.1 297.6 714.9 295.7C769.7 293.6 803.1 258.4 792.5 213.8C786.1 186.3 766.1 172.9 715.5 162C695.2 157.6 688.7 153.7 688.2 145.5C687.1 124.3 730.2 125 733.3 146.2C733.4 146.9 733.7 147.7 734 148.1L734.6 148.8 762 148.8C794.5 148.8 791.3 149.2 791.2 144.8C790.7 107.3 758.9 82.5 711 82C706.5 82 702.5 82 702 82" },
  { name: "e", x: 798.8, width: 210.8, d: "M897.4 81.6C879.3 82.9 864.9 87.2 849.7 96C791.1 129.8 781.4 221.7 831.3 269.2C844.9 282.1 864 291.3 882.9 294.1C941.1 302.7 987.8 278.6 1003.8 231.8C1004.6 229.6 1004.6 228.4 1003.9 227.7C1003.3 227.2 944.3 227 943.4 227.5C943.1 227.7 941.5 229.1 939.9 230.8C920.5 249.7 884.7 247 868.6 225.3C864 219.1 860.9 211.6 862.4 210.3C863 209.8 863.4 209.8 935.2 209.6C1005 209.5 1007.4 209.5 1007.9 209.1C1008.6 208.4 1008.9 206.9 1009.4 201C1016 129.6 966.3 76.6 897.4 81.6M900.6 132.9C883.4 134.7 868.8 145.3 862.9 160.2C861.6 163.6 861.9 164.6 864.4 164.7C877.8 164.7 945.8 164.6 946.3 164.5C947.9 164.1 947.5 161.7 945.3 156.9C937.5 140.5 919.6 130.9 900.6 132.9" },
];

/** Разметка заставки и её скрипт; стили (splashCss) — в <head>, чтобы были применены к первому кадру. */
export function Splash({ label }: { label: string }) {
  return (
    <>
      {/* Классы loop / hide / gone ставит скрипт — возможно, ещё до того, как React оживит страницу. */}
      <div id="splash" role="status" aria-label={label} suppressHydrationWarning>
        <div className="word" aria-hidden="true">
          {LETTERS.map((letter, i) => (
            <svg
              key={letter.name}
              className="ch"
              viewBox={`${letter.x} 0 ${letter.width} ${WORD.height}`}
              style={{ left: `${(letter.x / WORD.width) * 100}%`, width: `${(letter.width / WORD.width) * 100}%`, "--i": i } as CSSProperties}
            >
              <path d={letter.d} fill={letter.name === "1" ? "#0581f8" : "#fff"} fillRule="evenodd" />
            </svg>
          ))}
        </div>
        <div className="bar" />
      </div>
      <script dangerouslySetInnerHTML={{ __html: splashScript }} />
    </>
  );
}
