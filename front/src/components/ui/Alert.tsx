import type { ReactNode } from 'react';
import { cn } from '@/lib/cn';

export type AlertTone = 'info' | 'success' | 'warning' | 'danger';

const toneClasses: Record<AlertTone, string> = {
  info: 'border-info/30 bg-info-soft text-info-strong',
  success: 'border-success/30 bg-success-soft text-success-strong',
  warning: 'border-warning/30 bg-warning-soft text-warning-strong',
  danger: 'border-danger/30 bg-danger-soft text-danger-strong',
};

export interface AlertProps {
  tone?: AlertTone;
  title?: string;
  className?: string;
  children: ReactNode;
}

export function Alert({ tone = 'info', title, className, children }: AlertProps) {
  return (
    <div
      role="alert"
      className={cn('rounded-md border px-4 py-3 text-sm', toneClasses[tone], className)}
    >
      {title && <p className="font-semibold">{title}</p>}
      <div className={cn(title && 'mt-0.5')}>{children}</div>
    </div>
  );
}
