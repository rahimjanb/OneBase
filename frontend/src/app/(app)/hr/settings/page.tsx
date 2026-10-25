import { redirect } from "next/navigation";
import { VERIFIX_SETTINGS } from "@/lib/hr";

/** Прежний адрес настроек Verifix: они теперь в «Настройки → Интеграции → HR → Verifix». */
export default function HrSettingsMoved() {
  redirect(VERIFIX_SETTINGS);
}
