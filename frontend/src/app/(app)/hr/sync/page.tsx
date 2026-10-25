import { redirect } from "next/navigation";
import { VERIFIX_JOURNAL } from "@/lib/hr";

/** Прежний адрес журнала синхронизации: он теперь в «Настройки → Интеграции → HR → Verifix → Журнал синхронизации». */
export default function HrSyncMoved() {
  redirect(VERIFIX_JOURNAL);
}
