import type { ComponentPropsWithoutRef } from 'react';
import { cn } from '@/lib/cn';
import { controlClasses } from './styles';

export function Input({ className, ...props }: ComponentPropsWithoutRef<'input'>) {
  return <input className={cn(controlClasses, className)} {...props} />;
}
