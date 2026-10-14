import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { apiFetch } from '@/lib/api';
import type { DriverAssignmentSuggestion, ShipmentDelayRisk, ShipmentSummary } from '@/lib/types';
import { formatDateTime, shortId } from '@/lib/format';
import {
  priorityLabel,
  priorityTone,
  shipmentStatusLabel,
  shipmentStatusTone,
  shipmentStatusOptions,
  shipmentPriorityOptions,
} from '@/lib/shipment';
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
import { linkButtonPrimary, linkButtonGhost } from '@/components/ui/styles';

export default function ShipmentsPage() {
  const { notify } = useToast();
  const [pending, setPending] = useState<ShipmentDelayRisk[]>([]);
  const [all, setAll] = useState<ShipmentSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [orderId, setOrderId] = useState('');
  const [urgent, setUrgent] = useState(false);
  const [creating, setCreating] = useState(false);
  const [statusFilter, setStatusFilter] = useState('');
  const [priorityFilter, setPriorityFilter] = useState('');

  const loadPending = useCallback(async () => {
    const res = await apiFetch<ShipmentDelayRisk[]>('/api/shipments/pending');
    setPending(res);
  }, []);

  const loadAll = useCallback(async () => {
    const res = await apiFetch<ShipmentSummary[]>('/api/shipments');
    setAll(res);
  }, []);

  useEffect(() => {
    (async () => {
      try {
        await Promise.all([loadPending(), loadAll()]);
      } catch (e: any) {
        setError(e.message);
      } finally {
        setLoading(false);
      }
    })();
  }, [loadPending, loadAll]);

  async function createShipment(e: React.FormEvent) {
    e.preventDefault();
    setCreating(true);
    try {
      const id = await apiFetch<string>(`/api/shipments?orderId=${orderId}&urgent=${urgent}`, {
        method: 'POST',
      });
      notify(`Envío creado: ${shortId(id)}`, 'success');
      setOrderId('');
      setUrgent(false);
      await Promise.all([loadPending(), loadAll()]);
    } catch (e: any) {
      notify(e.message, 'error');
    } finally {
      setCreating(false);
    }
  }

  async function suggest(id: string) {
    try {
      const res = await apiFetch<DriverAssignmentSuggestion>(`/api/shipments/${id}/driver-suggestion`, {
        method: 'POST',
      });
      const driver = res.ranking.find((r) => r.driverId === res.recommendedDriverId);
      notify(
        driver
          ? `Conductor sugerido: ${shortId(driver.driverId)} (score ${driver.score.toFixed(2)})`
          : 'Sugerencia de conductor obtenida',
        'success',
      );
    } catch (e: any) {
      notify(e.message, 'error');
    }
  }

  const filtered = all.filter(
    (s) =>
      (!statusFilter || s.status === statusFilter) &&
      (!priorityFilter || s.priority === priorityFilter),
  );

  return (
    <div>
      <PageHeader title="Envíos" description="Creá envíos y gestioná los pendientes y el historial." />

      <Card className="mb-6 max-w-3xl">
        <CardHeader>
          <CardTitle>Nuevo envío</CardTitle>
        </CardHeader>
        <CardBody>
          <form onSubmit={createShipment} className="flex flex-col gap-4 sm:flex-row sm:items-end">
            <Field label="ID del pedido" htmlFor="orderId" required className="flex-1">
              <Input
                id="orderId"
                value={orderId}
                onChange={(e) => setOrderId(e.target.value)}
                placeholder="GUID del pedido"
              />
            </Field>
            <label className="flex h-10 items-center gap-2 text-sm text-foreground">
              <Checkbox checked={urgent} onChange={(e) => setUrgent(e.target.checked)} />
              Urgente
            </label>
            <Button type="submit" loading={creating}>
              Crear envío
            </Button>
          </form>
        </CardBody>
      </Card>

      <Card className="mb-6">
        <CardHeader>
          <CardTitle>Envíos pendientes</CardTitle>
        </CardHeader>
        {loading ? (
          <div className="flex items-center gap-2 p-5 text-sm text-muted">
            <Spinner /> Cargando…
          </div>
        ) : error ? (
          <div className="p-5">
            <Alert tone="danger">{error}</Alert>
          </div>
        ) : pending.length === 0 ? (
          <EmptyState title="Sin envíos pendientes" description="No hay envíos pendientes por asignar." />
        ) : (
          <Table>
            <THead>
              <TR>
                <TH>Envío</TH>
                <TH>Prioridad</TH>
                <TH>Riesgo</TH>
                <TH className="text-right">Acciones</TH>
              </TR>
            </THead>
            <TBody>
              {pending.map((p) => (
                <TR key={p.shipmentId}>
                  <TD className="font-mono text-xs">{shortId(p.shipmentId)}</TD>
                  <TD>
                    <Badge tone={priorityTone(p.priority)}>{priorityLabel(p.priority)}</Badge>
                  </TD>
                  <TD>{p.delayRiskPercentage != null ? `${p.delayRiskPercentage}%` : '-'}</TD>
                  <TD>
                    <div className="flex justify-end gap-2">
                      <Button variant="secondary" size="sm" onClick={() => suggest(p.shipmentId)}>
                        Sugerir conductor
                      </Button>
                      <Link to={`/admin/shipments/${p.shipmentId}/assign`} className={linkButtonPrimary}>
                        Asignar
                      </Link>
                    </div>
                  </TD>
                </TR>
              ))}
            </TBody>
          </Table>
        )}
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Todos los envíos</CardTitle>
          <div className="flex flex-wrap items-center gap-2">
            <Select
              aria-label="Filtrar por estado"
              value={statusFilter}
              onChange={(e) => setStatusFilter(e.target.value)}
              className="h-9 w-44"
            >
              <option value="">Todos los estados</option>
              {shipmentStatusOptions.map((o) => (
                <option key={o.value} value={o.value}>
                  {o.label}
                </option>
              ))}
            </Select>
            <Select
              aria-label="Filtrar por prioridad"
              value={priorityFilter}
              onChange={(e) => setPriorityFilter(e.target.value)}
              className="h-9 w-40"
            >
              <option value="">Todas las prioridades</option>
              {shipmentPriorityOptions.map((o) => (
                <option key={o.value} value={o.value}>
                  {o.label}
                </option>
              ))}
            </Select>
          </div>
        </CardHeader>
        {loading ? (
          <div className="flex items-center gap-2 p-5 text-sm text-muted">
            <Spinner /> Cargando…
          </div>
        ) : filtered.length === 0 ? (
          <EmptyState title="Sin envíos" description="No hay envíos que coincidan con el filtro." />
        ) : (
          <Table>
            <THead>
              <TR>
                <TH>Envío</TH>
                <TH>Estado</TH>
                <TH>Prioridad</TH>
                <TH>Pedido</TH>
                <TH>Riesgo</TH>
                <TH>Creado</TH>
                <TH className="text-right">Acciones</TH>
              </TR>
            </THead>
            <TBody>
              {filtered.map((s) => (
                <TR key={s.id}>
                  <TD className="font-mono text-xs">{shortId(s.id)}</TD>
                  <TD>
                    <Badge tone={shipmentStatusTone(s.status)}>{shipmentStatusLabel(s.status)}</Badge>
                  </TD>
                  <TD>
                    <Badge tone={priorityTone(s.priority)}>{priorityLabel(s.priority)}</Badge>
                  </TD>
                  <TD className="font-mono text-xs">{shortId(s.orderId)}</TD>
                  <TD>{s.delayRiskPercentage != null ? `${s.delayRiskPercentage}%` : '-'}</TD>
                  <TD>{formatDateTime(s.createdAt)}</TD>
                  <TD>
                    <div className="flex justify-end">
                      <Link to={`/admin/shipments/${s.id}`} className={linkButtonGhost}>
                        Ver detalle
                      </Link>
                    </div>
                  </TD>
                </TR>
              ))}
            </TBody>
          </Table>
        )}
      </Card>
    </div>
  );
}
