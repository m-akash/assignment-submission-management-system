'use client';

import { Suspense, useEffect, useState } from 'react';
import Link from 'next/link';
import { useRouter, useSearchParams } from 'next/navigation';
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import {
  AlertCircle,
  ArrowRight,
  CheckCircle2,
  Eye,
  EyeOff,
  KeyRound,
  Loader2,
  ShieldCheck,
} from 'lucide-react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { BrandWordmark } from '@/components/shared/brand-wordmark';
import { ApiError, apiGet, apiPost, toQuery } from '@/lib/api';
import { formatDateTime } from '@/lib/format';
import { setPasswordSchema, type SetPasswordValues } from '@/schemas';
import type { PasswordSetupStatus } from '@/types/api';

const SETUP_URL = '/api/v1/auth/set-password';

// `useSearchParams` forces client-side rendering; Next requires a Suspense boundary
// around it during static generation. Same wrapper the login page uses.
export default function SetPasswordPage() {
  return (
    <Suspense>
      <SetPasswordForm />
    </Suspense>
  );
}

function SetPasswordForm() {
  const router = useRouter();
  const token = useSearchParams().get('token') ?? '';

  // The link is checked before the form is shown, so an expired link says so instead of
  // being discovered after the user has typed a password twice.
  const [status, setStatus] = useState<PasswordSetupStatus | null>(null);
  const [checking, setChecking] = useState(true);
  const [failure, setFailure] = useState<string | null>(null);
  const [done, setDone] = useState(false);
  const [showPassword, setShowPassword] = useState(false);

  const form = useForm<SetPasswordValues>({
    resolver: zodResolver(setPasswordSchema),
    defaultValues: { newPassword: '', confirmPassword: '' },
  });

  useEffect(() => {
    if (!token) {
      setStatus({ isUsable: false, fullName: null, expiresAtUtc: null });
      setChecking(false);
      return;
    }

    let active = true;
    apiGet<PasswordSetupStatus>(`${SETUP_URL}${toQuery({ token })}`)
      .then((result) => {
        if (active) setStatus(result);
      })
      .catch(() => {
        // A network or server failure is not the same as a dead link, but from here the
        // only honest thing to say is that the link cannot be used right now.
        if (active) setStatus({ isUsable: false, fullName: null, expiresAtUtc: null });
      })
      .finally(() => {
        if (active) setChecking(false);
      });

    return () => {
      active = false;
    };
  }, [token]);

  async function onSubmit(values: SetPasswordValues) {
    setFailure(null);
    try {
      await apiPost(SETUP_URL, { token, newPassword: values.newPassword });
      setDone(true);
      // A short pause so the confirmation is actually read before the redirect.
      setTimeout(() => router.replace('/login'), 2500);
    } catch (error) {
      setFailure(
        error instanceof ApiError
          ? error.message
          : 'Could not set your password. Please try again.',
      );
    }
  }

  return (
    <div className="relative flex min-h-dvh items-center justify-center overflow-hidden bg-linear-to-b from-primary/8 to-background p-6 sm:p-10 dark:from-primary/12">
      <div
        aria-hidden
        className="pointer-events-none absolute -top-40 right-0 size-128 rounded-full bg-primary/10 blur-3xl dark:bg-primary/15"
      />

      <div className="relative w-full max-w-[25rem]">
        <div className="rounded-2xl border bg-card/90 p-7 shadow-xl backdrop-blur sm:p-8">
          <BrandWordmark className="pb-5" />

          {checking ? (
            <div className="flex items-center gap-2.5 py-6 text-sm text-muted-foreground">
              <Loader2 className="size-4 animate-spin" />
              Checking your link…
            </div>
          ) : done ? (
            <>
              <div className="flex size-11 items-center justify-center rounded-xl bg-success/10 text-success">
                <CheckCircle2 className="size-5" />
              </div>
              <h1 className="mt-4 text-[1.75rem] leading-tight font-semibold tracking-tight">
                Password set
              </h1>
              <p className="mt-1.5 text-sm text-muted-foreground">
                You can now sign in with your new password. Taking you to the sign-in page…
              </p>
              <Button asChild className="mt-6 h-11 w-full text-[0.925rem]">
                <Link href="/login">
                  Go to sign in
                  <ArrowRight className="size-4" />
                </Link>
              </Button>
            </>
          ) : !status?.isUsable ? (
            <>
              <div className="flex size-11 items-center justify-center rounded-xl bg-danger/10 text-danger">
                <AlertCircle className="size-5" />
              </div>
              <h1 className="mt-4 text-[1.75rem] leading-tight font-semibold tracking-tight">
                This link cannot be used
              </h1>
              <p className="mt-1.5 text-sm text-muted-foreground">
                Password setup links work once and expire after a couple of days. Ask your
                school administrator to send you a new one.
              </p>
              <Button asChild variant="outline" className="mt-6 h-11 w-full text-[0.925rem]">
                <Link href="/login">Back to sign in</Link>
              </Button>
            </>
          ) : (
            <>
              <h1 className="text-[1.75rem] leading-tight font-semibold tracking-tight">
                {status.fullName ? `Welcome, ${status.fullName}` : 'Choose your password'}
              </h1>
              <p className="mt-1.5 text-sm text-muted-foreground">
                Choose a password to finish setting up your account. Nobody else knows it —
                not even your administrator.
              </p>

              {status.expiresAtUtc && (
                <p className="mt-3 flex items-start gap-2 text-xs text-muted-foreground">
                  <ShieldCheck aria-hidden className="mt-0.5 size-3.5 shrink-0" />
                  This link works once and expires on {formatDateTime(status.expiresAtUtc)}.
                </p>
              )}

              {failure && (
                <Alert variant="destructive" className="mt-5">
                  <AlertCircle className="size-4" />
                  <AlertDescription>{failure}</AlertDescription>
                </Alert>
              )}

              <form onSubmit={form.handleSubmit(onSubmit)} className="mt-7 space-y-4" noValidate>
                <div className="space-y-1.5">
                  <Label htmlFor="newPassword">New password</Label>
                  <div className="relative">
                    <KeyRound
                      aria-hidden
                      className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted-foreground"
                    />
                    <Input
                      id="newPassword"
                      type={showPassword ? 'text' : 'password'}
                      autoComplete="new-password"
                      placeholder="••••••••"
                      aria-invalid={!!form.formState.errors.newPassword}
                      className="h-11 pr-11 pl-10"
                      {...form.register('newPassword')}
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
                  {form.formState.errors.newPassword && (
                    <p className="text-xs text-danger">
                      {form.formState.errors.newPassword.message}
                    </p>
                  )}
                </div>

                <div className="space-y-1.5">
                  <Label htmlFor="confirmPassword">Confirm password</Label>
                  <div className="relative">
                    <KeyRound
                      aria-hidden
                      className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted-foreground"
                    />
                    <Input
                      id="confirmPassword"
                      type={showPassword ? 'text' : 'password'}
                      autoComplete="new-password"
                      placeholder="••••••••"
                      aria-invalid={!!form.formState.errors.confirmPassword}
                      className="h-11 pl-10"
                      {...form.register('confirmPassword')}
                    />
                  </div>
                  {form.formState.errors.confirmPassword && (
                    <p className="text-xs text-danger">
                      {form.formState.errors.confirmPassword.message}
                    </p>
                  )}
                </div>

                <Button
                  type="submit"
                  className="mt-6 h-11 w-full text-[0.925rem]"
                  disabled={form.formState.isSubmitting}
                >
                  {form.formState.isSubmitting && <Loader2 className="size-4 animate-spin" />}
                  Set password and continue
                  {!form.formState.isSubmitting && <ArrowRight className="size-4" />}
                </Button>
              </form>
            </>
          )}
        </div>
      </div>
    </div>
  );
}
