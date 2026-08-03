'use client';

import { Suspense, useState } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { AlertCircle, Eye, EyeOff, GraduationCap, Loader2 } from 'lucide-react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { BrandIllustration } from '@/components/shared/brand-illustration';
import { useAuth } from '@/context/AuthContext';
import { loginSchema, type LoginValues } from '@/schemas';

const DEMO_ACCOUNTS = [
  { role: 'Admin', email: 'admin@assignment.test' },
  { role: 'Teacher', email: 'teacher@assignment.test' },
  { role: 'Student', email: 'student@assignment.test' },
] as const;

const DEMO_PASSWORD = 'Password123!';

// `useSearchParams` forces this page into client-side rendering; Next requires a
// Suspense boundary around it during static generation. The wrapper below provides it.
export default function LoginPage() {
  return (
    <Suspense>
      <LoginForm />
    </Suspense>
  );
}

function LoginForm() {
  const { login } = useAuth();
  const router = useRouter();
  const searchParams = useSearchParams();
  const [showPassword, setShowPassword] = useState(false);
  const [failure, setFailure] = useState<string | null>(null);

  const form = useForm<LoginValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: { email: '', password: '' },
  });

  async function onSubmit(values: LoginValues) {
    setFailure(null);
    try {
      await login(values.email, values.password);
      // proxy.ts records where the visitor was headed before being redirected here.
      router.replace(searchParams.get('next') || '/');
    } catch (error) {
      setFailure(error instanceof Error ? error.message : 'Sign in failed. Please try again.');
    }
  }

  /** Fills the form so an evaluator does not have to type the demo credentials. */
  function useDemoAccount(email: string) {
    form.setValue('email', email, { shouldValidate: true });
    form.setValue('password', DEMO_PASSWORD, { shouldValidate: true });
    setFailure(null);
  }

  return (
    <div className="grid min-h-dvh lg:grid-cols-2">
      {/* Brand panel — hidden on small screens where it would only push the form down. */}
      <div className="relative hidden flex-col overflow-hidden bg-gradient-to-br from-indigo-600 via-indigo-700 to-indigo-900 p-10 lg:flex">
        {/* Faint concentric rings behind the hero, for depth. */}
        <div
          aria-hidden
          className="pointer-events-none absolute -top-24 -left-24 size-[28rem] rounded-full border border-white/10"
        />
        <div
          aria-hidden
          className="pointer-events-none absolute -top-24 -left-24 size-[20rem] rounded-full border border-white/10"
        />

        <div className="relative flex items-center gap-2.5">
          <div className="flex size-9 items-center justify-center rounded-lg bg-white/15 text-white ring-1 ring-white/20 backdrop-blur">
            <GraduationCap className="size-5" />
          </div>
          <span className="text-lg font-semibold tracking-tight text-white">Scholaris</span>
        </div>

        {/* The illustration sits in its own slot, not behind the text. */}
        <div className="relative my-2 flex flex-1 items-center">
          <BrandIllustration aria-hidden className="h-full max-h-[22rem] w-full" />
        </div>

        <div className="relative max-w-md space-y-3">
          <h2 className="text-3xl font-semibold tracking-tight text-balance text-white">
            Coursework, from set to graded.
          </h2>
          <p className="text-indigo-100/90">
            Teachers publish assignments to a class and course, students submit answers and
            attachments before the deadline, and marks and feedback flow straight back.
          </p>
        </div>

        <p className="relative mt-8 text-xs text-indigo-200/70">
          Role-based access is enforced by the API, not by this interface.
        </p>
      </div>

      {/* Form panel — a theme-aware tinted gradient so the right side never reads as
          flat black (dark) or flat gray (light): indigo-soft in light, indigo-950 in dark. */}
      <div className="relative flex items-center justify-center overflow-hidden bg-gradient-to-b from-indigo-50 to-background p-6 dark:from-indigo-950/50 dark:to-background sm:p-10">
        <div
          aria-hidden
          className="pointer-events-none absolute -top-40 right-0 size-[32rem] rounded-full bg-indigo-500/10 blur-3xl dark:bg-indigo-500/15"
        />

        <div className="relative w-full max-w-sm space-y-5">
          {/* The auth card lifts the form off the tinted background in both themes. */}
          <div className="rounded-2xl border bg-card/80 p-7 shadow-xl ring-1 ring-black/5 backdrop-blur dark:ring-white/10">
            <div className="space-y-2">
              <div className="flex items-center gap-2.5 lg:hidden">
                <div className="flex size-9 items-center justify-center rounded-lg bg-primary text-primary-foreground">
                  <GraduationCap className="size-5" />
                </div>
                <span className="text-lg font-semibold tracking-tight">Scholaris</span>
              </div>
              <h1 className="text-2xl font-semibold tracking-tight">Sign in</h1>
              <p className="text-sm text-muted-foreground">
                Use the account your school administrator created for you.
              </p>
            </div>

            {failure && (
              <Alert variant="destructive" className="mt-5">
                <AlertCircle className="size-4" />
                <AlertDescription>{failure}</AlertDescription>
              </Alert>
            )}

            <form onSubmit={form.handleSubmit(onSubmit)} className="mt-6 space-y-5" noValidate>
            <div className="space-y-2">
              <Label htmlFor="email">Email</Label>
              <Input
                id="email"
                type="email"
                autoComplete="email"
                placeholder="name@school.test"
                aria-invalid={!!form.formState.errors.email}
                {...form.register('email')}
              />
              {form.formState.errors.email && (
                <p className="text-xs text-danger">{form.formState.errors.email.message}</p>
              )}
            </div>

            <div className="space-y-2">
              <Label htmlFor="password">Password</Label>
              <div className="relative">
                <Input
                  id="password"
                  type={showPassword ? 'text' : 'password'}
                  autoComplete="current-password"
                  placeholder="••••••••"
                  aria-invalid={!!form.formState.errors.password}
                  className="pr-10"
                  {...form.register('password')}
                />
                <button
                  type="button"
                  onClick={() => setShowPassword((visible) => !visible)}
                  aria-label={showPassword ? 'Hide password' : 'Show password'}
                  className="absolute top-1/2 right-2 -translate-y-1/2 rounded-sm p-1.5 text-muted-foreground transition-colors hover:text-foreground"
                >
                  {showPassword ? <EyeOff className="size-4" /> : <Eye className="size-4" />}
                </button>
              </div>
              {form.formState.errors.password && (
                <p className="text-xs text-danger">{form.formState.errors.password.message}</p>
              )}
            </div>

            <Button type="submit" className="w-full" disabled={form.formState.isSubmitting}>
              {form.formState.isSubmitting && <Loader2 className="size-4 animate-spin" />}
              Sign in
            </Button>
          </form>
          </div>

          {/* Demo helper — a softer secondary panel, distinct from the primary auth card. */}
          <div className="space-y-3 rounded-xl border bg-muted/40 p-4">
            <p className="text-xs font-medium tracking-wide text-muted-foreground uppercase">
              Demo accounts
            </p>
            <div className="grid gap-1.5">
              {DEMO_ACCOUNTS.map((account) => (
                <button
                  key={account.email}
                  type="button"
                  onClick={() => useDemoAccount(account.email)}
                  className="flex items-center justify-between rounded-md px-2 py-1.5 text-left text-sm transition-colors hover:bg-accent"
                >
                  <span className="font-medium">{account.role}</span>
                  <span className="text-xs text-muted-foreground">{account.email}</span>
                </button>
              ))}
            </div>
            <p className="text-xs text-muted-foreground">
              Password for all three: <code className="rounded bg-muted px-1 py-0.5">{DEMO_PASSWORD}</code>
            </p>
          </div>
        </div>
      </div>
    </div>
  );
}
