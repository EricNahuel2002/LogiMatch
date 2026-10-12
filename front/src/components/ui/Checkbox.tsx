import type { ComponentPropsWithoutRef } from 'react';
import { cn } from '@/lib/cn';

export function Checkbox({ className, ...props }: ComponentPropsWithoutRef<'input'>) {
  return (
    <input
      type="checkbox"
      className={cn(
        'h-4 w-4 rounded border-border text-brand-600 focus:ring-brand-500/40',
        className,
      )}
      {...props}
    />
  );
}
