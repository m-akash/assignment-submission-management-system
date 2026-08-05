'use client';

import { Suspense, useState } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import {
  AlertCircle,
  ArrowRight,
  Award,
  Backpack,
  ClipboardList,
  Eye,
  EyeOff,
  GraduationCap,
  KeyRound,
  Loader2,
  Mail,
  Send,
  UserCog,
  type LucideIcon,
} from 'lucide-react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { BrandIllustration } from '@/components/shared/brand-illustration';
import { useAuth } from '@/context/AuthContext';
import { loginSchema, type LoginValues } from '@/schemas';

/** Each demo row carries the same icon the sidebar uses for that role. */
const DEMO_ACCOUNTS: { role: string; email: string; icon: LucideIcon }[] = [
  { role: 'Admin', email: 'admin@assignment.test', icon: UserCog },
  { role: 'Teacher', email: 'teacher@assignment.test', icon: GraduationCap },
  { role: 'Student', email: 'student@assignment.test', icon: Backpack },
];

const DEMO_PASSWORD = 'Password123!';

/** What the product actually does, three lines, in the order it happens. */
const HIGHLIGHTS: { icon: LucideIcon; text: string }[] = [
  { icon: ClipboardList, text: 'Assignments published to a class and course' },
  { icon: Send, text: 'Submissions with attachments, before the deadline' },
  { icon: Award, text: 'Marks and written feedback, straight back' },
];

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
  function fillDemoAccount(email: string) {
    form.setValue('email', email, { shouldValidate: true });
    form.setValue('password', DEMO_PASSWORD, { shouldValidate: true });
    setFailure(null);
  }

  return (
    <div className="grid min-h-dvh lg:grid-cols-[1.1fr_1fr]">
      {/* Brand panel — hidden on small screens where it would only push the form down. */}
      <div className="relative hidden flex-col overflow-hidden bg-gradient-to-br from-indigo-600 via-indigo-800 to-indigo-950 p-10 lg:flex xl:p-14">
        {/* A dot grid, faded out towards the bottom-right so it never fights the text. */}
        <div
          aria-hidden
          className="pointer-events-none absolute inset-0 opacity-25 [background-image:radial-gradient(circle,rgba(255,255,255,0.6)_1px,transparent_1px)] [background-size:26px_26px] [mask-image:radial-gradient(130%_110%_at_15%_0%,black,transparent_65%)]"
        />
        <div
          aria-hidden
          className="pointer-events-none absolute -top-40 -right-32 size-[36rem] rounded-full bg-indigo-400/20 blur-3xl"
        />

        <div className="relative flex items-center gap-3">
          <div className="flex size-10 items-center justify-center rounded-xl bg-white/15 text-white ring-1 ring-white/25 backdrop-blur">
            <GraduationCap className="size-5" />
          </div>
          <span className="text-base font-semibold tracking-tight text-white">
            Assignment Management System
          </span>
        </div>

        {/* The illustration gets its own slot and shrinks before the copy does. */}
        <div className="relative flex min-h-0 flex-1 items-center justify-center py-8">
          <BrandIllustration className="h-full max-h-[20rem] w-full max-w-xl" />
        </div>

        <div className="relative max-w-md">
          <p className="text-[0.7rem] font-medium tracking-[0.2em] text-indigo-200/80 uppercase">
            Coursework, end to end
          </p>
          <h2 className="mt-3 text-[2rem] leading-[1.15] font-semibold tracking-tight text-balance text-white">
            From assigned to graded, in one place.
          </h2>

          <ul className="mt-7 space-y-3.5">
            {HIGHLIGHTS.map(({ icon: Icon, text }) => (
              <li key={text} className="flex items-center gap-3 text-sm text-indigo-100/90">
                <span className="flex size-7 shrink-0 items-center justify-center rounded-lg bg-white/10 text-indigo-100 ring-1 ring-white/15">
                  <Icon className="size-3.5" />
                </span>
                {text}
              </li>
            ))}
          </ul>
        </div>
      </div>

      {/* Form panel — a theme-aware tinted gradient so the right side never reads as
          flat black (dark) or flat gray (light): indigo-soft in light, indigo-950 in dark. */}
      <div className="relative flex items-center justify-center overflow-hidden bg-gradient-to-b from-indigo-50 to-background p-6 sm:p-10 dark:from-indigo-950/50 dark:to-background">
        <div
          aria-hidden
          className="pointer-events-none absolute -top-40 right-0 size-[32rem] rounded-full bg-indigo-500/10 blur-3xl dark:bg-indigo-500/15"
        />

        <div className="relative w-full max-w-[25rem] space-y-4">
          {/* The auth card lifts the form off the tinted background in both themes. */}
          <div className="rounded-2xl border bg-card/85 p-7 shadow-[0_28px_70px_-30px_rgba(30,27,75,0.45)] ring-1 ring-black/5 backdrop-blur sm:p-8 dark:ring-white/10">
            <div className="flex items-center gap-2.5 pb-5 lg:hidden">
              <div className="flex size-9 items-center justify-center rounded-xl bg-primary text-primary-foreground">
                <GraduationCap className="size-5" />
              </div>
              <span className="text-base font-semibold tracking-tight">
                Assignment Management System
              </span>
            </div>

            <h1 className="text-[1.75rem] leading-tight font-semibold tracking-tight">
              Welcome back
            </h1>
            <p className="mt-1.5 text-sm text-muted-foreground">
              Sign in with the account your school administrator created for you.
            </p>

            {failure && (
              <Alert variant="destructive" className="mt-5">
                <AlertCircle className="size-4" />
                <AlertDescription>{failure}</AlertDescription>
              </Alert>
            )}

            <form onSubmit={form.handleSubmit(onSubmit)} className="mt-7 space-y-4" noValidate>
              <div className="space-y-1.5">
                <Label htmlFor="email">Email</Label>
                <div className="relative">
                  <Mail
                    aria-hidden
                    className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted-foreground"
                  />
                  <Input
                    id="email"
                    type="email"
                    autoComplete="email"
                    placeholder="name@school.test"
                    aria-invalid={!!form.formState.errors.email}
                    className="h-11 pl-10"
                    {...form.register('email')}
                  />
                </div>
                {form.formState.errors.email && (
                  <p className="text-xs text-danger">{form.formState.errors.email.message}</p>
                )}
              </div>

              <div className="space-y-1.5">
                <Label htmlFor="password">Password</Label>
                <div className="relative">
                  <KeyRound
                    aria-hidden
                    className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted-foreground"
                  />
                  <Input
                    id="password"
                    type={showPassword ? 'text' : 'password'}
                    autoComplete="current-password"
                    placeholder="••••••••"
                    aria-invalid={!!form.formState.errors.password}
                    className="h-11 pr-11 pl-10"
                    {...form.register('password')}
                  />
                  <button
                    type="button"
                    onClick={() => setShowPassword((visible) => !visible)}
                    aria-label={showPassword ? 'Hide password' : 'Show password'}
                    className="absolute top-1/2 right-1.5 flex size-8 -translate-y-1/2 items-center justify-center rounded-md text-muted-foreground transition-colors hover:bg-accent hover:text-foreground"
                  >
                    {showPassword ? <EyeOff className="size-4" /> : <Eye className="size-4" />}
                  </button>
                </div>
                {form.formState.errors.password && (
                  <p className="text-xs text-danger">{form.formState.errors.password.message}</p>
                )}
              </div>

              <Button
                type="submit"
                className="mt-6 h-11 w-full text-[0.925rem]"
                disabled={form.formState.isSubmitting}
              >
                {form.formState.isSubmitting && <Loader2 className="size-4 animate-spin" />}
                Sign in
                {!form.formState.isSubmitting && (
                  <ArrowRight className="size-4 transition-transform group-hover/button:translate-x-0.5" />
                )}
              </Button>
            </form>
          </div>

          {/* Demo helper — a softer secondary panel, distinct from the primary auth card. */}
          <div className="rounded-2xl border bg-muted/40 p-2">
            <p className="px-2.5 pt-2 pb-1 text-[0.7rem] font-medium tracking-[0.14em] text-muted-foreground uppercase">
              Demo accounts
            </p>
            {DEMO_ACCOUNTS.map(({ role, email, icon: Icon }) => (
              <button
                key={email}
                type="button"
                onClick={() => fillDemoAccount(email)}
                className="group flex w-full items-center gap-2.5 rounded-xl px-2.5 py-2 text-left transition-colors hover:bg-accent"
              >
                <Icon aria-hidden className="size-4 shrink-0 text-muted-foreground" />
                <span className="text-sm font-medium">{role}</span>
                <span className="ml-auto truncate font-mono text-xs text-muted-foreground">
                  {email}
                </span>
                <ArrowRight
                  aria-hidden
                  className="size-3.5 shrink-0 text-muted-foreground opacity-0 transition-opacity group-hover:opacity-100"
                />
              </button>
            ))}
            <p className="px-2.5 pt-2 pb-1.5 text-xs text-muted-foreground">
              Password for all three:{' '}
              <code className="rounded bg-background/80 px-1.5 py-0.5 font-mono">
                {DEMO_PASSWORD}
              </code>
            </p>
          </div>
        </div>
      </div>
    </div>
  );
}
