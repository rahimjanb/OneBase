import { redirect } from "next/navigation";

/** «OneBase → Files»: раздел файлов отделов живёт на /base. */
export default function FilesRedirect() {
  redirect("/base");
}
