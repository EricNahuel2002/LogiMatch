import type { ReactNode } from 'react';
import { cn } from '@/lib/cn';

export function Table({ className, children }: { className?: string; children: ReactNode }) {
  return (
    <div className="w-full overflow-x-auto">
      <table className={cn('w-full border-collapse text-left text-sm', className)}>{children}</table>
    </div>
  );
}

export function THead({ children }: { children: ReactNode }) {
  return (
    <thead className="bg-surface-muted text-xs uppercase tracking-wide text-muted">{children}</thead>
  );
}

export function TBody({ children }: { children: ReactNode }) {
  return <tbody className="divide-y divide-border">{children}</tbody>;
}

export function TR({ className, children }: { className?: string; children: ReactNode }) {
  return <tr className={cn('transition-colors hover:bg-surface-muted/60', className)}>{children}</tr>;
}

export function TH({ className, children }: { className?: string; children: ReactNode }) {
  return (
    <th scope="col" className={cn('whitespace-nowrap px-4 py-3 font-medium', className)}>
      {children}
    </th>
  );
}

export function TD({ className, children }: { className?: string; children: ReactNode }) {
  return <td className={cn('px-4 py-3 text-foreground', className)}>{children}</td>;
}
