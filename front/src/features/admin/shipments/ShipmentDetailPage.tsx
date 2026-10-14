import { useCallback, useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { apiFetch } from '@/lib/api';
import type { DeliveryAttempt, ShipmentDetails, ShipmentHistory } from '@/lib/types';
import { formatDateTime, formatMeters, formatCurrency } from '@/lib/format';
import {
  deliveryFailureReasonLabel,
  priorityLabel,
  priorityTone,
  shipmentStatusLabel,
  shipmentStatusTone,
} from '@/lib/shipment';
import { PageHeader } from '@/components/ui/PageHeader';
import { Card, CardBody, CardHeader, CardTitle } from '@/components/ui/Card';
import { Table, THead, TBody, TR, TH, TD } from '@/components/ui/Table';
import { Field } from '@/components/ui/Field';
import { Input } from '@/components/ui/Input';
import { Button } from '@/components/ui/Button';
import { Badge } from '@/components/ui/Badge';
import { Alert } from '@/components/ui/Alert';
import { EmptyState } from '@/components/ui/EmptyState';
import { Spinner } from '@/components/ui/Spinner';
import { useToast } from '@/components/ui/useToast';
import { linkButtonGhost } from '@/components/ui/styles';

export default function ShipmentDetailPage() {
  const { id } = useParams();
  const { notify } = useToast();

  const [details, setDetails] = useState<ShipmentDetails | null>(null);
  const [history, setHistory] = useState<ShipmentHistory[]>([]);
  const [attempts, setAttempts] = useState<DeliveryAttempt[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [note, setNote] = useState('');
  const [cancelling, setCancelling] = useState(false);

  const load = useCallback(async () => {
    if (!id) return;
    const [d, h, a] = await Promise.all([
      apiFetch<ShipmentDetails>(`/api/shipments/${id}`),
      apiFetch<ShipmentHistory[]>(`/api/shipments/${id}/history`),
      apiFetch<DeliveryAttempt[]>(`/api/shipments/${id}/delivery-attempts`),
    ]);
    setDetails(d);
    setHistory(h);
    setAttempts(a);
  }, [id]);

  useEffect(() => {
    (async () => {
      try {
        await load();
      } catch (e: any) {
        setError(e.message);
      } finally {
        setLoading(false);
      }
    })();
  }, [load]);

  async function cancelShipment(e: React.FormEvent) {
    e.preventDefault();
    if (!id) return;
    setCancelling(true);
    try {
      await apiFetch(`/api/shipments/${id}/cancel`, {
        method: 'POST',
        body: JSON.stringify({ note: note || null }),
      });
      notify('Envío cancelado', 'success');
      setNote('');
      await load();
    } catch (e: any) {
      notify(e.message, 'error');
    } finally {
      setCancelling(false);
    }
  }

  const cancellable = details && !['Finalized', 'DeliveryFailed', 'Cancelled'].includes(details.status);

  return (
    <div>
      <PageHeader
        title="Detalle del envío"
        description={id}
        actions={
          <Link to="/admin/shipments" className={linkButtonGhost}>
            ← Volver a envíos
          </Link>
        }
      />

      {loading ? (
        <div className="flex items-center gap-2 text-sm text-muted">
          <Spinner /> Cargando…
        </div>
      ) : error ? (
        <Alert tone="danger">{error}</Alert>
      ) : details ? (
        <div className="space-y-6">
          <Card>
            <CardHeader>
              <CardTitle>Datos generales</CardTitle>
              <div className="flex items-center gap-2">
                <Badge tone={shipmentStatusTone(details.status)}>
                  {shipmentStatusLabel(details.status)}
                </Badge>
                <Badge tone={priorityTone(details.priority)}>{priorityLabel(details.priority)}</Badge>
              </div>
            </CardHeader>
            <CardBody className="grid grid-cols-1 gap-4 text-sm sm:grid-cols-2">
              <div>
                <p className="text-muted">Pedido</p>
                <p className="font-mono text-xs text-foreground">{details.orderId}</p>
              </div>
              <div>
                <p className="text-muted">Conductor</p>
                <p className="text-foreground">
                  {details.assignedDriver?.driverId
                    ? `${details.assignedDriver.name ?? ''} ${details.assignedDriver.surname ?? ''}`.trim()
                    : 'Sin asignar'}
                </p>
              </div>
              <div>
                <p className="text-muted">Riesgo de demora</p>
                <p className="text-foreground">
                  {details.delayRiskPercentage != null ? `${details.delayRiskPercentage}%` : '-'}
                </p>
              </div>
              <div>
                <p className="text-muted">Llegó a destino</p>
                <p className="text-foreground">{details.arrivedAtDestination ? 'Sí' : 'No'}</p>
              </div>
              <div>
                <p className="text-muted">Creado</p>
                <p className="text-foreground">{formatDateTime(details.createdAt)}</p>
              </div>
              {details.routeStop && (
                <div>
                  <p className="text-muted">Parada</p>
                  <p className="text-foreground">
                    {details.routeStop.name ?? 'Sin nombre'} · orden {details.routeStop.stopOrder ?? '-'}
                    {' · '}
                    {formatMeters(details.routeStop.distanceMeters)} · {formatCurrency(details.routeStop.tollCost)}
                  </p>
                </div>
              )}
            </CardBody>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>Ítems del pedido</CardTitle>
            </CardHeader>
            {details.items.length === 0 ? (
              <CardBody className="text-sm text-muted">Sin ítems.</CardBody>
            ) : (
              <Table>
                <THead>
                  <TR>
                    <TH>Nombre</TH>
                    <TH>Cantidad</TH>
                    <TH>Peso (kg)</TH>
                  </TR>
                </THead>
                <TBody>
                  {details.items.map((item) => (
                    <TR key={item.orderItemId}>
                      <TD>{item.name}</TD>
                      <TD>{item.quantity}</TD>
                      <TD>{item.weightKg}</TD>
                    </TR>
                  ))}
                </TBody>
              </Table>
            )}
          </Card>

          <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
            <Card>
              <CardHeader>
                <CardTitle>Historial</CardTitle>
              </CardHeader>
              {history.length === 0 ? (
                <EmptyState title="Sin historial" description="No hay eventos registrados." />
              ) : (
                <Table>
                  <THead>
                    <TR>
                      <TH>Fecha</TH>
                      <TH>Estado</TH>
                      <TH>Nota</TH>
                    </TR>
                  </THead>
                  <TBody>
                    {history.map((h) => (
                      <TR key={h.id}>
                        <TD>{formatDateTime(h.recordedAt)}</TD>
                        <TD>
                          <Badge tone={shipmentStatusTone(h.status)}>
                            {shipmentStatusLabel(h.status)}
                          </Badge>
                        </TD>
                        <TD>{h.note ?? '-'}</TD>
                      </TR>
                    ))}
                  </TBody>
                </Table>
              )}
            </Card>

            <Card>
              <CardHeader>
                <CardTitle>Intentos de entrega</CardTitle>
              </CardHeader>
              {attempts.length === 0 ? (
                <EmptyState title="Sin intentos" description="No hay intentos de entrega registrados." />
              ) : (
                <Table>
                  <THead>
                    <TR>
                      <TH>N.º</TH>
                      <TH>Fecha</TH>
                      <TH>Resultado</TH>
                      <TH>Motivo</TH>
                    </TR>
                  </THead>
                  <TBody>
                    {attempts.map((a) => (
                      <TR key={a.id}>
                        <TD>{a.attemptNumber}</TD>
                        <TD>{formatDateTime(a.attemptedAt)}</TD>
                        <TD>
                          <Badge tone={a.succeeded ? 'success' : 'danger'}>
                            {a.succeeded ? 'Exitoso' : 'Fallido'}
                          </Badge>
                        </TD>
                        <TD>{a.succeeded ? '-' : deliveryFailureReasonLabel(a.failureReason)}</TD>
                      </TR>
                    ))}
                  </TBody>
                </Table>
              )}
            </Card>
          </div>

          {cancellable && (
            <Card className="max-w-3xl">
              <CardHeader>
                <CardTitle>Cancelar envío</CardTitle>
              </CardHeader>
              <CardBody>
                <form onSubmit={cancelShipment} className="space-y-4">
                  <Field label="Nota" htmlFor="cancelNote" hint="Opcional.">
                    <Input
                      id="cancelNote"
                      value={note}
                      onChange={(e) => setNote(e.target.value)}
                      placeholder="Motivo de la cancelación"
                    />
                  </Field>
                  <div className="flex justify-end">
                    <Button type="submit" variant="danger" loading={cancelling}>
                      Cancelar envío
                    </Button>
                  </div>
                </form>
              </CardBody>
            </Card>
          )}
        </div>
      ) : null}
    </div>
  );
}
