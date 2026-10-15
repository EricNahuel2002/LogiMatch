import { useCallback, useEffect, useMemo, useState } from 'react';
import { apiFetch, apiFetchNullable } from '@/lib/api';
import type {
  DeliveryFailureReason,
  DriverRouteCurrent,
  DriverRouteStop,
  ShipmentStatus,
} from '@/lib/types';
import { formatCurrency } from '@/lib/format';
import {
  shipmentStatusLabel,
  shipmentStatusTone,
  deliveryFailureReasonOptions,
} from '@/lib/shipment';
import { routeCancellationReasonOptions } from '@/lib/route';
import { PageHeader } from '@/components/ui/PageHeader';
import { Card, CardBody, CardHeader, CardTitle } from '@/components/ui/Card';
import { Field } from '@/components/ui/Field';
import { Input } from '@/components/ui/Input';
import { Select } from '@/components/ui/Select';
import { Checkbox } from '@/components/ui/Checkbox';
import { Button } from '@/components/ui/Button';
import { Badge } from '@/components/ui/Badge';
import { Alert } from '@/components/ui/Alert';
import { EmptyState } from '@/components/ui/EmptyState';
import { Spinner } from '@/components/ui/Spinner';
import { Modal } from '@/components/ui/Modal';
import { useToast } from '@/components/ui/useToast';
import { DriverMap } from './DriverMap';
import { useGeolocation } from './useGeolocation';
import { useDriverRealtime } from './useDriverRealtime';

type ShipmentAction = 'start' | 'stop' | 'resume' | 'arrived' | 'delivery-attempts';

const actionLabels: Record<ShipmentAction, string> = {
  start: 'Iniciar',
  stop: 'Detener',
  resume: 'Reanudar',
  arrived: 'Llegada',
  'delivery-attempts': 'Intento',
};

const terminalStatuses: ShipmentStatus[] = ['Finalized', 'DeliveryFailed', 'Cancelled'];

function isTerminal(status: ShipmentStatus) {
  return terminalStatuses.includes(status);
}

function actionsForStatus(status: ShipmentStatus): ShipmentAction[] {
  switch (status) {
    case 'Pending':
      return ['start'];
    case 'InProgress':
      return ['stop', 'arrived'];
    case 'Stopped':
      return ['resume'];
    case 'Arrived':
      return ['delivery-attempts'];
    default:
      return [];
  }
}

function stopTitle(stop: DriverRouteStop) {
  return stop.name ?? 'Parada';
}

export default function DriverHome() {
  const { notify } = useToast();

  const [currentRoute, setCurrentRoute] = useState<DriverRouteCurrent | null>(null);
  const [routeLoading, setRouteLoading] = useState(true);
  const [error, setError] = useState('');

  const [expandedStopId, setExpandedStopId] = useState<string | null>(null);

  const [running, setRunning] = useState<string | null>(null);

  const [pending, setPending] = useState<{ shipmentId: string; action: ShipmentAction } | null>(
    null,
  );
  const [actionNote, setActionNote] = useState('');
  const [succeeded, setSucceeded] = useState(true);
  const [failureReason, setFailureReason] = useState<DeliveryFailureReason>('CustomerAbsent');

  const [cancelOpen, setCancelOpen] = useState(false);
  const [cancelReason, setCancelReason] = useState('VehicleBreakdown');
  const [cancelNote, setCancelNote] = useState('');
  const [cancelling, setCancelling] = useState(false);

  const loadRoute = useCallback(async () => {
    const route = await apiFetchNullable<DriverRouteCurrent>('/api/drivers/me/routes/current');
    setCurrentRoute(route);
  }, []);

  useEffect(() => {
    (async () => {
      try {
        await loadRoute();
      } catch (e: any) {
        setError(e.message);
      } finally {
        setRouteLoading(false);
      }
    })();
  }, [loadRoute]);

  const handleRouteChanged = useCallback(() => {
    loadRoute().catch(() => {
      // La recarga por evento es best-effort; el polling manual sigue disponible.
    });
  }, [loadRoute]);

  const routeActive = Boolean(currentRoute?.isActive);
  const { position, error: geoError } = useGeolocation(routeActive);
  const { connected } = useDriverRealtime(handleRouteChanged, routeActive ? position : null);

  const currentStop = useMemo(
    () => currentRoute?.stops.find((stop) => stop.isCurrent) ?? null,
    [currentRoute],
  );

  const upcomingStops = useMemo(() => {
    if (!currentRoute) return [];
    return currentRoute.stops
      .filter((stop) => !stop.isCurrent && !isTerminal(stop.status))
      .slice()
      .sort((a, b) => a.stopOrder - b.stopOrder);
  }, [currentRoute]);

  const mapStops = useMemo(
    () => (currentStop ? [currentStop, ...upcomingStops] : upcomingStops),
    [currentStop, upcomingStops],
  );

  async function executeAction(shipmentId: string, action: ShipmentAction) {
    const key = `${shipmentId}:${action}`;
    setRunning(key);
    try {
      let body: Record<string, unknown> | undefined;
      if (action === 'stop' || action === 'resume' || action === 'arrived') {
        body = { note: actionNote || null };
      } else if (action === 'delivery-attempts') {
        body = {
          succeeded,
          failureReason: succeeded ? null : failureReason,
          note: actionNote || null,
        };
      }

      await apiFetch(`/api/shipments/${shipmentId}/${action}`, {
        method: 'POST',
        body: body ? JSON.stringify(body) : undefined,
      });

      notify(`Acción "${actionLabels[action]}" realizada`, 'success');
      setPending(null);
      await loadRoute();
    } catch (e: any) {
      notify(e.message, 'error');
    } finally {
      setRunning(null);
    }
  }

  function openAction(shipmentId: string, action: ShipmentAction) {
    if (action === 'start') {
      void executeAction(shipmentId, 'start');
      return;
    }
    setPending({ shipmentId, action });
    setActionNote('');
    setSucceeded(true);
    setFailureReason('CustomerAbsent');
  }

  async function cancelRoute() {
    setCancelling(true);
    try {
      await apiFetch('/api/drivers/me/routes/cancel', {
        method: 'POST',
        body: JSON.stringify({ reason: cancelReason, note: cancelNote || null }),
      });
      notify('Ruta cancelada', 'success');
      setCancelOpen(false);
      setCancelNote('');
      await loadRoute();
    } catch (e: any) {
      notify(e.message, 'error');
    } finally {
      setCancelling(false);
    }
  }

  return (
    <div>
      <PageHeader
        title="Panel del conductor"
        description="Gestioná tu ruta y tus entregas en tiempo real."
      />

      {error && (
        <Alert tone="danger" className="mb-6">
          {error}
        </Alert>
      )}

      {geoError && (
        <Alert tone="warning" className="mb-6">
          {geoError}
        </Alert>
      )}

      <Card className="mb-6">
        <CardHeader>
          <CardTitle>Mapa de ruta</CardTitle>
          {currentRoute && (
            <Badge tone={connected ? 'success' : 'neutral'}>
              {connected ? 'En vivo' : 'Sin conexión'}
            </Badge>
          )}
        </CardHeader>
        {routeLoading ? (
          <div className="flex items-center gap-2 p-5 text-sm text-muted">
            <Spinner /> Cargando…
          </div>
        ) : !currentRoute ? (
          <EmptyState
            title="Sin ruta activa"
            description="No tenés una ruta asignada en este momento."
          />
        ) : mapStops.length === 0 ? (
          <EmptyState title="Ruta sin paradas" description="Tu ruta todavía no tiene paradas." />
        ) : (
          <CardBody className="space-y-4">
            <DriverMap driverPosition={position} stops={mapStops} />
            <div className="flex flex-wrap items-center gap-x-6 gap-y-1 text-sm text-muted">
              <span>
                Paradas: <span className="text-foreground">{mapStops.length}</span>
              </span>
              <span>
                Próximas: <span className="text-foreground">{upcomingStops.length}</span>
              </span>
            </div>
          </CardBody>
        )}
      </Card>

      {currentRoute && (
        <Card className="mb-6">
          <CardHeader>
            <CardTitle>Pedido actual</CardTitle>
            {currentStop && (
              <Badge tone={shipmentStatusTone(currentStop.status)}>
                {shipmentStatusLabel(currentStop.status)}
              </Badge>
            )}
          </CardHeader>
          {!currentStop ? (
            <EmptyState
              title="Sin pedido pendiente"
              description="No hay paradas pendientes en tu ruta."
            />
          ) : (
            <CardBody className="space-y-4">
              <div>
                <p className="text-lg font-semibold text-foreground">
                  {currentStop.customerName ?? 'Cliente'}
                </p>
                <p className="text-sm text-muted">{stopTitle(currentStop)}</p>
              </div>
              <div className="flex flex-wrap gap-2">
                {actionsForStatus(currentStop.status).map((action) => (
                  <Button
                    key={action}
                    variant={action === 'delivery-attempts' ? 'primary' : 'secondary'}
                    onClick={() => openAction(currentStop.shipmentId, action)}
                    loading={running === `${currentStop.shipmentId}:${action}`}
                  >
                    {actionLabels[action]}
                  </Button>
                ))}
              </div>
            </CardBody>
          )}
        </Card>
      )}

      {currentRoute && (
        <Card className="mb-6">
          <CardHeader>
            <CardTitle>Envíos próximos</CardTitle>
          </CardHeader>
          {upcomingStops.length === 0 ? (
            <EmptyState title="Sin envíos próximos" description="No tenés más paradas pendientes." />
          ) : (
            <ul className="divide-y divide-border">
              {upcomingStops.map((stop) => {
                const expanded = expandedStopId === stop.routeStopId;
                return (
                  <li key={stop.routeStopId}>
                    <button
                      type="button"
                      onClick={() => setExpandedStopId(expanded ? null : stop.routeStopId)}
                      className="flex w-full items-center gap-3 px-5 py-4 text-left hover:bg-surface-muted"
                    >
                      <span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-surface-muted text-xs font-semibold text-foreground">
                        {stop.stopOrder}
                      </span>
                      <span className="min-w-0 flex-1">
                        <span className="block truncate text-sm font-medium text-foreground">
                          {stop.customerName ?? 'Cliente'}
                        </span>
                        <span className="block truncate text-xs text-muted">{stopTitle(stop)}</span>
                      </span>
                      <Badge tone={shipmentStatusTone(stop.status)}>
                        {shipmentStatusLabel(stop.status)}
                      </Badge>
                    </button>
                    {expanded && (
                      <div className="bg-surface-muted px-5 py-4">
                        {stop.items.length === 0 ? (
                          <p className="text-sm text-muted">Sin ítems.</p>
                        ) : (
                          <ul className="space-y-2">
                            {stop.items.map((item, index) => (
                              <li
                                key={`${stop.routeStopId}-${index}`}
                                className="flex items-center justify-between gap-3 text-sm"
                              >
                                <span className="text-foreground">
                                  {item.name}{' '}
                                  <span className="text-muted">x{item.quantity}</span>
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
        </Card>
      )}

      {currentRoute && (
        <div className="flex justify-end">
          <Button variant="danger" onClick={() => setCancelOpen(true)}>
            Cancelar ruta
          </Button>
        </div>
      )}

      <Modal
        open={pending !== null}
        title={pending ? actionLabels[pending.action] : ''}
        onClose={() => setPending(null)}
        footer={
          <>
            <Button variant="ghost" onClick={() => setPending(null)}>
              Cancelar
            </Button>
            <Button
              onClick={() => pending && executeAction(pending.shipmentId, pending.action)}
              loading={running !== null}
            >
              Ejecutar
            </Button>
          </>
        }
      >
        <div className="space-y-4">
          {pending?.action === 'delivery-attempts' && (
            <>
              <label className="flex items-center gap-2 text-sm text-foreground">
                <Checkbox checked={succeeded} onChange={(e) => setSucceeded(e.target.checked)} />
                Entrega exitosa
              </label>
              {!succeeded && (
                <Field label="Motivo de falla" htmlFor="failureReason" required>
                  <Select
                    id="failureReason"
                    value={failureReason}
                    onChange={(e) => setFailureReason(e.target.value as DeliveryFailureReason)}
                  >
                    {deliveryFailureReasonOptions.map((o) => (
                      <option key={o.value} value={o.value}>
                        {o.label}
                      </option>
                    ))}
                  </Select>
                </Field>
              )}
            </>
          )}
          <Field label="Nota" htmlFor="actionNote" hint="Opcional.">
            <Input
              id="actionNote"
              value={actionNote}
              onChange={(e) => setActionNote(e.target.value)}
              placeholder="Detalle de la acción"
            />
          </Field>
        </div>
      </Modal>

      <Modal
        open={cancelOpen}
        title="Cancelar ruta"
        onClose={() => setCancelOpen(false)}
        footer={
          <>
            <Button variant="ghost" onClick={() => setCancelOpen(false)}>
              Volver
            </Button>
            <Button variant="danger" onClick={cancelRoute} loading={cancelling}>
              Confirmar cancelación
            </Button>
          </>
        }
      >
        <div className="space-y-4">
          <Field label="Motivo" htmlFor="cancelReason" required>
            <Select
              id="cancelReason"
              value={cancelReason}
              onChange={(e) => setCancelReason(e.target.value)}
            >
              {routeCancellationReasonOptions.map((option) => (
                <option key={option.value} value={option.value}>
                  {option.label}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="Nota" htmlFor="cancelNote" hint="Opcional.">
            <Input
              id="cancelNote"
              value={cancelNote}
              onChange={(e) => setCancelNote(e.target.value)}
              placeholder="Detalle de la cancelación"
            />
          </Field>
        </div>
      </Modal>
    </div>
  );
}
