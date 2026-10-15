import { useState } from 'react';
import type { RouteAssignment, ShipmentDelayRisk } from '@/lib/types';
import { shortId } from '@/lib/format';
import { priorityLabel, priorityTone } from '@/lib/shipment';
import { cn } from '@/lib/cn';
import { Badge } from '@/components/ui/Badge';

interface RoutePlanBranchProps {
  assignment: RouteAssignment;
  driverLabel: string;
  vehicleLabel: string;
  depositLabel: string;
  shipmentById: Map<string, ShipmentDelayRisk>;
  defaultOpen?: boolean;
}

function Chevron({ open }: { open: boolean }) {
  return (
    <svg
      viewBox="0 0 24 24"
      className={cn('h-4 w-4 shrink-0 text-muted transition-transform', open && 'rotate-90')}
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      aria-hidden="true"
    >
      <path d="M9 6l6 6-6 6" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

export function RoutePlanBranch({
  assignment,
  driverLabel,
  vehicleLabel,
  depositLabel,
  shipmentById,
  defaultOpen = false,
}: RoutePlanBranchProps) {
  const [open, setOpen] = useState(defaultOpen);

  const stops = [...assignment.stops].sort((a, b) => a.stopOrder - b.stopOrder);

  return (
    <div className="overflow-hidden rounded-md border border-border">
      <button
        type="button"
        onClick={() => setOpen((v) => !v)}
        aria-expanded={open}
        className="flex w-full items-center gap-3 bg-surface-muted/60 px-4 py-3 text-left transition-colors hover:bg-surface-muted"
      >
        <Chevron open={open} />
        <div className="min-w-0 flex-1">
          <p className="truncate text-sm font-semibold text-foreground">{driverLabel}</p>
          <p className="truncate text-xs text-muted">
            {vehicleLabel} · {depositLabel}
          </p>
        </div>
        <div className="flex shrink-0 items-center gap-2">
          <Badge tone="neutral">{stops.length} parada(s)</Badge>
          <Badge tone="brand">{assignment.loadKg} kg</Badge>
        </div>
      </button>

      {open && (
        <div className="border-t border-border px-4 py-3">
          {stops.length === 0 ? (
            <p className="text-sm text-muted">Sin paradas asignadas a esta ruta.</p>
          ) : (
            <ul className="space-y-3">
              {stops.map((stop, index) => {
                const shipment = shipmentById.get(stop.shipmentId);
                const isLast = index === stops.length - 1;
                return (
                  <li key={stop.shipmentId} className="relative flex gap-3 pl-6">
                    <span
                      className={cn(
                        'absolute left-2 top-0 w-px bg-border',
                        isLast ? 'h-3' : 'h-full',
                      )}
                      aria-hidden="true"
                    />
                    <span
                      className="absolute left-1 top-3 h-px w-2 bg-border"
                      aria-hidden="true"
                    />
                    <Badge tone="neutral" className="mt-0.5 shrink-0">
                      {stop.stopOrder}
                    </Badge>
                    <div className="min-w-0 flex-1">
                      <div className="flex flex-wrap items-center gap-2">
                        <span className="text-sm font-medium text-foreground">
                          {shipment?.customerName || `Envío ${shortId(stop.shipmentId)}`}
                        </span>
                        {shipment && (
                          <Badge tone={priorityTone(shipment.priority)}>
                            {priorityLabel(shipment.priority)}
                          </Badge>
                        )}
                      </div>
                      <div className="mt-0.5 flex flex-wrap items-center gap-x-3 gap-y-1 text-xs text-muted">
                        <span className="font-mono">{shortId(stop.shipmentId)}</span>
                        {shipment && <span>{shipment.weightKg} kg</span>}
                        {shipment?.delayRiskPercentage != null && (
                          <span>Riesgo {shipment.delayRiskPercentage}%</span>
                        )}
                      </div>
                    </div>
                  </li>
                );
              })}
            </ul>
          )}

          <p className="mt-3 border-t border-border pt-2 text-xs text-muted">
            Carga total: {assignment.loadKg} kg · Depósito: {depositLabel}
          </p>
        </div>
      )}
    </div>
  );
}
