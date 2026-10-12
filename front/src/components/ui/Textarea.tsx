import type { ComponentPropsWithoutRef } from 'react';
import { cn } from '@/lib/cn';
import { controlClasses } from './styles';

export function Textarea({ className, ...props }: ComponentPropsWithoutRef<'textarea'>) {
  return <textarea className={cn(controlClasses, 'resize-y', className)} {...props} />;
}
