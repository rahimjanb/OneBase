import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { Card } from "@/components/ui";

export function ComingSoon({ title, subtitle, planned }: { title: string; subtitle: string; planned: string[] }) {
  return (
    <>
      <PageHeader title={title} subtitle={subtitle} />
      <PageBody>
        <Card className="max-w-2xl p-6">
          <h2 className="font-semibold">Раздел в разработке</h2>
          <p className="mt-1 text-sm text-ink-2">Здесь появится:</p>
          <ul className="mt-3 space-y-2">
            {planned.map((item) => (
              <li key={item} className="flex items-center gap-3 text-sm text-ink">
                <span className="size-1.5 shrink-0 rounded-full bg-accent" />
                {item}
              </li>
            ))}
          </ul>
        </Card>
      </PageBody>
    </>
  );
}
