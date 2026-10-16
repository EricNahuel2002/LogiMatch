import { useState, type ReactNode } from 'react';
import { Drawer } from 'vaul';

import type { DriverRouteStop } from '@/lib/types';
import { shipmentStatusLabel, shipmentStatusTone } from '@/lib/shipment';
import { formatCurrency } from '@/lib/format';
import { cn } from '@/lib/cn';
import { Badge } from '@/components/ui/Badge';
import { EmptyState } from '@/components/ui/EmptyState';

export interface DriverSheetProps {
  container: HTMLDivElement | null;
  snapPoints: Array<number | string>;
  activeSnap: number | string;
  onSnapChange: (snap: number | string) => void;
  currentRouteActive: boolean;
  hasStops: boolean;
  currentStop: DriverRouteStop | null;
  upcomingStops: DriverRouteStop[];
  error?: string | null;
  footer?: ReactNode;
}

function stopTitle(stop: DriverRouteStop) {
  return stop.name ?? 'Parada';
}

export function DriverSheet({
  container,
  snapPoints,
  activeSnap,
  onSnapChange,
  currentRouteActive,
  hasStops,
  currentStop,
  upcomingStops,
  error,
  footer,
}: DriverSheetProps) {
  const [expandedStopId, setExpandedStopId] = useState<string | null>(null);
  const isPeek = activeSnap === snapPoints[0];

  return (
    <Drawer.Root
      open
      modal={false}
      dismissible={false}
      container={container}
      snapPoints={snapPoints}
      activeSnapPoint={activeSnap}
      setActiveSnapPoint={(snap) => {
        if (typeof snap === 'number' || typeof snap === 'string') {
          onSnapChange(snap);
        }
      }}
      handleOnly={false}
    >
      <Drawer.Portal>
        <Drawer.Content className="pointer-events-auto absolute inset-x-0 bottom-0 flex h-full flex-col rounded-t-2xl border border-border bg-surface shadow-card">
          <div className="flex w-full cursor-grab touch-none flex-col items-center py-2 active:cursor-grabbing">
            <Drawer.Handle className="h-1.5 w-12 rounded-full bg-border/80" />
          </div>
          <div
            className={cn(
              'flex-1 overflow-y-auto px-4 pb-6 pt-1 transition-opacity duration-200',
              isPeek && 'pointer-events-none opacity-0',
            )}
          >
            {error && (
              <div className="mb-3 rounded-md border border-danger/30 bg-danger-soft px-3 py-2 text-xs text-danger-strong">
                {error}
              </div>
            )}
            {!currentRouteActive ? (
              <EmptyState
                title="Sin ruta activa"
                description="No tenés una ruta asignada en este momento."
              />
            ) : !hasStops ? (
              <EmptyState
                title="Ruta sin paradas"
                description="Tu ruta todavía no tiene paradas."
              />
            ) : (
              <div className="space-y-4">
                <div className="flex items-start justify-between gap-3">
                  <div className="min-w-0 flex-1">
                    <p className="text-sm font-semibold text-foreground">Pedido actual</p>
                    {currentStop ? (
                      <>
                        <p className="truncate text-sm text-muted">
                          {currentStop.customerName ?? 'Cliente'} • {stopTitle(currentStop)}
                        </p>
                      </>
                    ) : (
                      <p className="text-sm text-muted">Sin pedido pendiente</p>
                    )}
                  </div>
                  {currentStop && (
                    <Badge tone={shipmentStatusTone(currentStop.status)}>
                      {shipmentStatusLabel(currentStop.status)}
                    </Badge>
                  )}
                </div>
                {currentStop && currentStop.items.length > 0 && (
                  <div className="space-y-2 rounded-md border border-border/60 bg-surface-muted px-3 py-2">
                    <p className="text-xs font-medium text-muted">Ítems</p>
                    <ul className="space-y-1.5">
                      {currentStop.items.map((item, index) => (
                        <li
                          key={`${currentStop.routeStopId}-${index}`}
                          className="flex items-center justify-between gap-3 text-sm"
                        >
                          <span className="text-foreground">
                            {item.name} <span className="text-muted">x{item.quantity}</span>
                          </span>
                          <span className="text-muted">{formatCurrency(item.price)}</span>
                        </li>
                      ))}
                    </ul>
                  </div>
                )}
                <div>
                  <p className="text-sm font-semibold text-foreground">Envíos próximos</p>
                  {upcomingStops.length === 0 ? (
                    <p className="mt-1 text-sm text-muted">No tenés más paradas pendientes.</p>
                  ) : (
                    <ul className="mt-2 divide-y divide-border/60 overflow-hidden rounded-md border border-border/60">
                      {upcomingStops.map((stop) => {
                        const expanded = expandedStopId === stop.routeStopId;
                        return (
                          <li key={stop.routeStopId}>
                            <button
                              type="button"
                              onClick={() => setExpandedStopId(expanded ? null : stop.routeStopId)}
                              className="flex w-full items-center gap-3 px-3 py-3 text-left hover:bg-surface-muted"
                            >
                              <span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-surface-muted text-xs font-semibold text-foreground">
                                {stop.stopOrder}
                              </span>
                              <span className="min-w-0 flex-1">
                                <span className="block truncate text-sm font-medium text-foreground">
                                  {stop.customerName ?? 'Cliente'}
                                </span>
                                <span className="block truncate text-xs text-muted">
                                  {stopTitle(stop)}
                                </span>
                              </span>
                              <Badge tone={shipmentStatusTone(stop.status)}>
                                {shipmentStatusLabel(stop.status)}
                              </Badge>
                            </button>
                            {expanded && (
                              <div className="bg-surface-muted px-3 py-3">
                                {stop.items.length === 0 ? (
                                  <p className="text-sm text-muted">Sin ítems.</p>
                                ) : (
                                  <ul className="space-y-1.5">
                                    {stop.items.map((item, index) => (
                                      <li
                                        key={`${stop.routeStopId}-${index}`}
                                        className="flex items-center justify-between gap-3 text-sm"
                                      >
                                        <span className="text-foreground">
                                          {item.name} <span className="text-muted">x{item.quantity}</span>
                                        </span>
                                        <span className="text-muted">{formatCurrency(item.price)}</span>
                                      </li>
                                    ))}
                                  </ul>
                                )}
                              </div>
                            )}
                          </li>
                        );
                      })}
                    </ul>
                  )}
                </div>
              </div>
            )}
          </div>
          {footer}
        </Drawer.Content>
      </Drawer.Portal>
    </Drawer.Root>
  );
}
