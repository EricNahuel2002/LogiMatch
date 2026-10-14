import type { RouteCancellationReason } from '@/lib/types';

const cancellationReasonLabels: Record<RouteCancellationReason, string> = {
  VehicleBreakdown: 'Falla del vehículo',
  RouteAbandonment: 'Abandono de ruta',
  Emergency: 'Emergencia',
  InclementWeather: 'Clima adverso',
  Accident: 'Accidente',
};

export function routeCancellationReasonLabel(reason?: string | null) {
  if (!reason) return '-';
  return cancellationReasonLabels[reason as RouteCancellationReason] ?? reason;
}

export const routeCancellationReasonOptions = Object.entries(cancellationReasonLabels).map(
  ([value, label]) => ({ value, label }),
);
