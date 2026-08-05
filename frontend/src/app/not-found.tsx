import Link from 'next/link';
import { ArrowLeft, Compass } from 'lucide-react';
import { Button } from '@/components/ui/button';

export default function NotFound() {
  return (
    <div className="relative flex min-h-dvh flex-col items-center justify-center overflow-hidden p-6 text-center">
      <div
        aria-hidden
        className="pointer-events-none absolute -top-40 size-128 rounded-full bg-primary/10 blur-3xl"
      />

      <div className="relative flex max-w-md flex-col items-center gap-5">
        <div className="flex size-14 items-center justify-center rounded-2xl border bg-card text-primary shadow-sm">
          <Compass className="size-6" />
        </div>
        <div className="space-y-2">
          <p className="eyebrow">Error 404</p>
          <h1 className="text-2xl font-semibold">Page not found</h1>
          <p className="text-sm text-muted-foreground">
            The page you were looking for is not here, or you may not have access to it.
          </p>
        </div>
        <Button asChild size="lg">
          <Link href="/">
            <ArrowLeft className="size-4" />
            Back to dashboard
          </Link>
        </Button>
      </div>
    </div>
  );
}
