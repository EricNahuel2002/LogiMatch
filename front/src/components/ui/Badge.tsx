import type { ReactNode } from 'react';
import { cn } from '@/lib/cn';

export type BadgeTone = 'neutral' | 'brand' | 'success' | 'warning' | 'danger' | 'info';

const toneClasses: Record<BadgeTone, string> = {
  neutral: 'border-border bg-surface-muted text-muted',
  brand: 'border-brand-200 bg-brand-50 text-brand-700',
  success: 'border-success/30 bg-success-soft text-success-strong',
  warning: 'border-warning/30 bg-warning-soft text-warning-strong',
  danger: 'border-danger/30 bg-danger-soft text-danger-strong',
  info: 'border-info/30 bg-info-soft text-info-strong',
};

export function Badge({
  tone = 'neutral',
  className,
  children,
}: {
  tone?: BadgeTone;
  className?: string;
  children: ReactNode;
}) {
  return (
    <span
      className={cn(
        'inline-flex items-center rounded-full border px-2.5 py-0.5 text-xs font-medium',
        toneClasses[tone],
        className,
      )}
    >
      {children}
    </span>
  );
}
