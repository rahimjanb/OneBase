/**
 * Модель территории завода для цифрового двойника — перенесена из factory-3d.html без изменений геометрии (размеры приблизительные, по спутнику).
 * Координаты в метрах: x — с запада на восток, z — с севера на юг, (0,0) — северо-западный угол территории. Ключи зданий (A…J) — постоянные:
 * по ним backend хранит привязки к отделам и складам (OneBase.Application/Twin/TwinSite.cs — тот же список).
 */

export type Roof = "flat" | "gable" | "ribbed" | "skylight";

export type SiteBlock = {
  key: string;
  x: number;
  z: number;
  w: number;
  d: number;
  h: number;
  floors: number;
  roof: Roof;
  /** Офис со стеклянным фасадом (Южный корпус). */
  glass?: boolean;
};

export type SiteRoad = { x: number; z: number; w: number; d: number };

export type SiteGate = { type: "guard" | "truck"; name: string; x: number; z: number; w: number; h: number };

export const territory: [number, number][] = [
  [0, 10],
  [115, 12],
  [120, 215],
  [18, 215],
  [25, 130],
  [35, 55],
  [0, 55],
];

export const blocks: SiteBlock[] = [
  { key: "A", x: 0, z: 9, w: 47, d: 44, h: 9, floors: 1, roof: "ribbed" },
  { key: "B", x: 62, z: 14, w: 48, d: 18, h: 10.5, floors: 3, roof: "ribbed" },
  { key: "C", x: 57, z: 30, w: 27, d: 27, h: 10.5, floors: 3, roof: "gable" },
  { key: "D", x: 84, z: 31, w: 26, d: 59, h: 10.5, floors: 3, roof: "ribbed" },
  { key: "E", x: 70, z: 57, w: 16, d: 31, h: 10.5, floors: 3, roof: "gable" },
  { key: "F", x: 48, z: 53, w: 22, d: 90, h: 10.5, floors: 3, roof: "flat" },
  { key: "G", x: 47, z: 97, w: 10, d: 26, h: 10.5, floors: 3, roof: "ribbed" },
  { key: "H", x: 77, z: 88, w: 32, d: 99, h: 10.5, floors: 3, roof: "skylight" },
  { key: "I", x: 47, z: 127, w: 30, d: 66, h: 10.5, floors: 3, roof: "flat" },
  { key: "J", x: 31, z: 187, w: 84, d: 27, h: 10.5, floors: 3, roof: "flat", glass: true },
];

/** Проезд от ворот к Цеху 1 — вдоль западной стороны. */
export const roads: SiteRoad[] = [{ x: 34, z: 53, w: 12, d: 170 }];

export const gates: SiteGate[] = [
  { type: "guard", name: "Охрана", x: 31, z: 214, w: 5, h: 3.5 },
  { type: "truck", name: "Ворота для фур", x: 36, z: 214, w: 10, h: 10 },
];

/** Центр территории — куда смотрит камера по умолчанию. */
export const siteCenter = { x: 60, z: 112 };
