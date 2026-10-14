import type { BadgeTone } from '@/components/ui/Badge';

const statusLabels: Record<string, string> = {
  Pending: 'Pendiente',
  InProgress: 'En progreso',
  Stopped: 'Detenido',
  Arrived: 'En destino',
  Finalized: 'Finalizado',
  DeliveryFailed: 'Entrega fallida',
  Cancelled: 'Cancelado',
};

const statusTones: Record<string, BadgeTone> = {
  Pending: 'warning',
  InProgress: 'info',
  Stopped: 'warning',
  Arrived: 'brand',
  Finalized: 'success',
  DeliveryFailed: 'danger',
  Cancelled: 'neutral',
};

const priorityLabels: Record<string, string> = {
  Low: 'Baja',
  Normal: 'Normal',
  High: 'Alta',
  Urgent: 'Urgente',
};

const priorityTones: Record<string, BadgeTone> = {
  Low: 'neutral',
  Normal: 'info',
  High: 'warning',
  Urgent: 'danger',
};

export function shipmentStatusLabel(status: string) {
  return statusLabels[status] ?? status;
}

export function shipmentStatusTone(status: string): BadgeTone {
  return statusTones[status] ?? 'neutral';
}

export function priorityLabel(priority: string) {
  return priorityLabels[priority] ?? priority;
}

export function priorityTone(priority: string): BadgeTone {
  return priorityTones[priority] ?? 'neutral';
}

const deliveryFailureReasonLabels: Record<string, string> = {
  CustomerAbsent: 'Cliente ausente',
  Refused: 'Rechazado',
  BusinessClosed: 'Comercio cerrado',
  Other: 'Otro',
};

export function deliveryFailureReasonLabel(reason?: string | null) {
  if (!reason) return '-';
  return deliveryFailureReasonLabels[reason] ?? reason;
}

export const shipmentStatusOptions = Object.entries(statusLabels).map(([value, label]) => ({
  value,
  label,
}));

export const shipmentPriorityOptions = Object.entries(priorityLabels).map(([value, label]) => ({
  value,
  label,
}));

export const deliveryFailureReasonOptions = Object.entries(deliveryFailureReasonLabels).map(
  ([value, label]) => ({ value, label }),
);
