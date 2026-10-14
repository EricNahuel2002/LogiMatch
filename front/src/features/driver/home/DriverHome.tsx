import { useCallback, useEffect, useState } from 'react';
import { apiFetch, apiFetchNullable } from '@/lib/api';
import type { DeliveryFailureReason, DriverRouteCurrent, DriverShipment } from '@/lib/types';
import { formatDateTime, shortId } from '@/lib/format';
import {
  priorityLabel,
  priorityTone,
  shipmentStatusLabel,
  shipmentStatusTone,
  deliveryFailureReasonOptions,
} from '@/lib/shipment';
import { routeCancellationReasonOptions } from '@/lib/route';
import { PageHeader } from '@/components/ui/PageHeader';
import { Card, CardBody, CardHeader, CardTitle } from '@/components/ui/Card';
import { Table, THead, TBody, TR, TH, TD } from '@/components/ui/Table';
import { Field } from '@/components/ui/Field';
import { Input } from '@/components/ui/Input';
import { Select } from '@/components/ui/Select';
import { Checkbox } from '@/components/ui/Checkbox';
import { Button } from '@/components/ui/Button';
import { Badge } from '@/components/ui/Badge';
import { Alert } from '@/components/ui/Alert';
import { EmptyState } from '@/components/ui/EmptyState';
import { Spinner } from '@/components/ui/Spinner';
import { useToast } from '@/components/ui/useToast';

type ShipmentAction = 'start' | 'stop' | 'resume' | 'arrived' | 'delivery-attempts';

const actionLabels: Record<ShipmentAction, string> = {
  start: 'Iniciar',
  stop: 'Detener',
  resume: 'Reanudar',
  arrived: 'Llegada',
  'delivery-attempts': 'Intento',
};

const actions: ShipmentAction[] = ['start', 'stop', 'resume', 'arrived', 'delivery-attempts'];

export default function DriverHome() {
  const { notify } = useToast();

  const [currentRoute, setCurrentRoute] = useState<DriverRouteCurrent | null>(null);
  const [routeLoading, setRouteLoading] = useState(true);
  const [shipments, setShipments] = useState<DriverShipment[]>([]);
  const [shipmentsLoading, setShipmentsLoading] = useState(true);
  const [error, setError] = useState('');

  const [lat, setLat] = useState('0');
  const [lng, setLng] = useState('0');
  const [updatingLocation, setUpdatingLocation] = useState(false);

  const [reason, setReason] = useState('VehicleBreakdown');
  const [note, setNote] = useState('');
  const [cancelling, setCancelling] = useState(false);

  const [pending, setPending] = useState<{ shipmentId: string; action: ShipmentAction } | null>(null);
  const [actionNote, setActionNote] = useState('');
  const [succeeded, setSucceeded] = useState(true);
  const [failureReason, setFailureReason] = useState<DeliveryFailureReason>('CustomerAbsent');
  const [actionLoading, setActionLoading] = useState(false);

  const load = useCallback(async () => {
    const [route, list] = await Promise.all([
      apiFetchNullable<DriverRouteCurrent>('/api/drivers/me/routes/current'),
      apiFetch<DriverShipment[]>('/api/drivers/me/shipments'),
    ]);
    setCurrentRoute(route);
    setShipments(list);
  }, []);

  useEffect(() => {
    (async () => {
      try {
        await load();
      } catch (e: any) {
        setError(e.message);
      } finally {
        setRouteLoading(false);
        setShipmentsLoading(false);
      }
    })();
  }, [load]);

  async function updateLocation(e: React.FormEvent) {
    e.preventDefault();
    setUpdatingLocation(true);
    try {
      await apiFetch('/api/drivers/me/location', {
        method: 'PATCH',
        body: JSON.stringify({ latitude: parseFloat(lat), longitude: parseFloat(lng) }),
      });
      notify('Ubicación actualizada', 'success');
    } catch (e: any) {
      notify(e.message, 'error');
    } finally {
      setUpdatingLocation(false);
    }
  }

  async function cancelRoute(e: React.FormEvent) {
    e.preventDefault();
    setCancelling(true);
    try {
      await apiFetch('/api/drivers/me/routes/cancel', {
        method: 'POST',
        body: JSON.stringify({ reason, note }),
      });
      notify('Ruta cancelada', 'success');
      setNote('');
      await load();
    } catch (e: any) {
      notify(e.message, 'error');
    } finally {
      setCancelling(false);
    }
  }

  function openAction(shipmentId: string, action: ShipmentAction) {
    setPending({ shipmentId, action });
    setActionNote('');
    setSucceeded(true);
    setFailureReason('CustomerAbsent');
  }

  async function runAction() {
    if (!pending) return;
    setActionLoading(true);
    try {
      const { shipmentId, action } = pending;
      let body: Record<string, unknown> = {};
      if (action === 'stop' || action === 'resume') body = { note: actionNote || null };
      if (action === 'arrived') body = { note: actionNote || null };
      if (action === 'delivery-attempts') {
        body = {
          succeeded,
          failureReason: succeeded ? null : failureReason,
          note: actionNote || null,
        };
      }
      await apiFetch(`/api/shipments/${shipmentId}/${action}`, {
        method: 'POST',
        body: JSON.stringify(body),
      });
      notify(`Acción "${actionLabels[action]}" realizada`, 'success');
      setPending(null);
      await load();
    } catch (e: any) {
      notify(e.message, 'error');
    } finally {
      setActionLoading(false);
    }
  }

  const selectedShipment = pending ? shipments.find((s) => s.shipmentId === pending.shipmentId) : null;
  const needsNote = pending && pending.action !== 'start';

  return (
    <div>
      <PageHeader title="Panel del conductor" description="Gestioná tu ruta, tu ubicación y tus envíos." />

      {error && (
        <Alert tone="danger" className="mb-6">
          {error}
        </Alert>
      )}

      <div className="mb-6 grid grid-cols-1 gap-6 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>Mi ruta actual</CardTitle>
            {currentRoute && (
              <Badge tone={currentRoute.isActive ? 'success' : 'neutral'}>
                {currentRoute.isActive ? 'Activa' : 'Inactiva'}
              </Badge>
            )}
          </CardHeader>
          {routeLoading ? (
            <div className="flex items-center gap-2 p-5 text-sm text-muted">
              <Spinner /> Cargando…
            </div>
          ) : currentRoute ? (
            <CardBody className="grid grid-cols-1 gap-3 text-sm sm:grid-cols-2">
              <div>
                <p className="text-muted">Ruta</p>
                <p className="font-mono text-xs text-foreground">{currentRoute.routeId}</p>
              </div>
              <div>
                <p className="text-muted">Vehículo</p>
                <p className="text-foreground">{currentRoute.vehicleId ?? 'Sin asignar'}</p>
              </div>
              <div>
                <p className="text-muted">Inicio</p>
                <p className="text-foreground">{formatDateTime(currentRoute.createdAt)}</p>
              </div>
            </CardBody>
          ) : (
            <EmptyState title="Sin ruta activa" description="No tenés una ruta asignada en este momento." />
          )}
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Actualizar ubicación</CardTitle>
          </CardHeader>
          <CardBody>
            <form onSubmit={updateLocation} className="space-y-4">
              <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                <Field label="Latitud" htmlFor="lat" required>
                  <Input
                    id="lat"
                    type="number"
                    step="any"
                    value={lat}
                    onChange={(e) => setLat(e.target.value)}
                  />
                </Field>
                <Field label="Longitud" htmlFor="lng" required>
                  <Input
                    id="lng"
                    type="number"
                    step="any"
                    value={lng}
                    onChange={(e) => setLng(e.target.value)}
                  />
                </Field>
              </div>
              <div className="flex justify-end">
                <Button type="submit" loading={updatingLocation}>
                  Actualizar ubicación
                </Button>
              </div>
            </form>
          </CardBody>
        </Card>
      </div>

      <Card className="mb-6">
        <CardHeader>
          <CardTitle>Mis envíos</CardTitle>
        </CardHeader>
        {shipmentsLoading ? (
          <div className="flex items-center gap-2 p-5 text-sm text-muted">
            <Spinner /> Cargando…
          </div>
        ) : shipments.length === 0 ? (
          <EmptyState title="Sin envíos asignados" description="No tenés envíos asignados por el momento." />
        ) : (
          <Table>
            <THead>
              <TR>
                <TH>Envío</TH>
                <TH>Estado</TH>
                <TH>Prioridad</TH>
                <TH>Pedido</TH>
                <TH>Creado</TH>
                <TH className="text-right">Acciones</TH>
              </TR>
            </THead>
            <TBody>
              {shipments.map((s) => (
                <TR key={s.shipmentId}>
                  <TD className="font-mono text-xs">{shortId(s.shipmentId)}</TD>
                  <TD>
                    <Badge tone={shipmentStatusTone(s.status)}>{shipmentStatusLabel(s.status)}</Badge>
                  </TD>
                  <TD>
                    <Badge tone={priorityTone(s.priority)}>{priorityLabel(s.priority)}</Badge>
                  </TD>
                  <TD className="font-mono text-xs">{shortId(s.orderId)}</TD>
                  <TD>{formatDateTime(s.createdAt)}</TD>
                  <TD>
                    <div className="flex flex-wrap justify-end gap-1">
                      {actions.map((action) => (
                        <Button
                          key={action}
                          variant="secondary"
                          size="sm"
                          onClick={() => openAction(s.shipmentId, action)}
                        >
                          {actionLabels[action]}
                        </Button>
                      ))}
                    </div>
                  </TD>
                </TR>
              ))}
            </TBody>
          </Table>
        )}
      </Card>

      {pending && (
        <Card className="mb-6 max-w-3xl">
          <CardHeader>
            <CardTitle>
              {actionLabels[pending.action]} · envío {shortId(pending.shipmentId)}
            </CardTitle>
            <Button variant="ghost" size="sm" onClick={() => setPending(null)}>
              Cancelar
            </Button>
          </CardHeader>
          <CardBody className="space-y-4">
            {selectedShipment && (
              <p className="text-sm text-muted">
                Estado actual: {shipmentStatusLabel(selectedShipment.status)}
              </p>
            )}
            {needsNote && (
              <Field label="Nota" htmlFor="actionNote" hint="Opcional.">
                <Input
                  id="actionNote"
                  value={actionNote}
                  onChange={(e) => setActionNote(e.target.value)}
                  placeholder="Detalle de la acción"
                />
              </Field>
            )}
            {pending.action === 'delivery-attempts' && (
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
            <div className="flex justify-end">
              <Button onClick={runAction} loading={actionLoading}>
                Ejecutar
              </Button>
            </div>
          </CardBody>
        </Card>
      )}

      <Card className="max-w-3xl">
        <CardHeader>
          <CardTitle>Cancelar ruta</CardTitle>
        </CardHeader>
        <CardBody>
          <form onSubmit={cancelRoute} className="space-y-4">
            <Field label="Motivo" htmlFor="reason" required>
              <Select id="reason" value={reason} onChange={(e) => setReason(e.target.value)}>
                {routeCancellationReasonOptions.map((option) => (
                  <option key={option.value} value={option.value}>
                    {option.label}
                  </option>
                ))}
              </Select>
            </Field>
            <Field label="Nota" htmlFor="note">
              <Input
                id="note"
                value={note}
                onChange={(e) => setNote(e.target.value)}
                placeholder="Detalle opcional"
              />
            </Field>
            <div className="flex justify-end">
              <Button type="submit" variant="danger" loading={cancelling}>
                Cancelar ruta
              </Button>
            </div>
          </form>
        </CardBody>
      </Card>
    </div>
  );
}
