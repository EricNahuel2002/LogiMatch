import type { ComponentPropsWithoutRef } from 'react';
import { cn } from '@/lib/cn';
import { controlClasses } from './styles';

export function Select({ className, children, ...props }: ComponentPropsWithoutRef<'select'>) {
  return (
    <select className={cn(controlClasses, 'pr-8', className)} {...props}>
      {children}
    </select>
  );
}
