import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { apiFetch } from '@/lib/api';
import { PageHeader } from '@/components/ui/PageHeader';
import { Card, CardBody, CardHeader, CardTitle } from '@/components/ui/Card';
import { Table, THead, TBody, TR, TH, TD } from '@/components/ui/Table';
import { Field } from '@/components/ui/Field';
import { Input } from '@/components/ui/Input';
import { Checkbox } from '@/components/ui/Checkbox';
import { Button } from '@/components/ui/Button';
import { Badge } from '@/components/ui/Badge';
import { Alert } from '@/components/ui/Alert';
import { EmptyState } from '@/components/ui/EmptyState';
import { Spinner } from '@/components/ui/Spinner';
import { useToast } from '@/components/ui/useToast';
import { linkButtonPrimary } from '@/components/ui/styles';
import { priorityLabel, priorityTone } from '@/lib/shipment';

interface ShipmentDelayRisk {
  shipmentId: string;
  status: string;
  priority: string;
  delayRiskPercentage?: number;
}

export default function ShipmentsPage() {
  const { notify } = useToast();
  const [pending, setPending] = useState<ShipmentDelayRisk[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [orderId, setOrderId] = useState('');
  const [urgent, setUrgent] = useState(false);
  const [creating, setCreating] = useState(false);
  const [suggestion, setSuggestion] = useState<unknown>(null);

  async function loadPending() {
    const res = await apiFetch<ShipmentDelayRisk[]>('/api/shipments/pending');
    setPending(res);
  }

  useEffect(() => {
    (async () => {
      try {
        await loadPending();
      } catch (e: any) {
        setError(e.message);
      } finally {
        setLoading(false);
      }
    })();
  }, []);

  async function createShipment(e: React.FormEvent) {
    e.preventDefault();
    setCreating(true);
    try {
      const id = await apiFetch<string>(`/api/shipments?orderId=${orderId}&urgent=${urgent}`, {
        method: 'POST',
      });
      notify(`Envío creado: ${id}`, 'success');
      await loadPending();
    } catch (e: any) {
      notify(e.message, 'error');
    } finally {
      setCreating(false);
    }
  }

  async function suggest(id: string) {
    try {
      const res = await apiFetch(`/api/shipments/${id}/driver-suggestion`, { method: 'POST' });
      setSuggestion(res);
      notify('Sugerencia de conductor obtenida', 'success');
    } catch (e: any) {
      notify(e.message, 'error');
    }
  }

  return (
    <div>
      <PageHeader title="Envíos" description="Creá envíos y gestioná los pendientes." />

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

      <Card>
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
                  <TD className="font-mono text-xs">{p.shipmentId}</TD>
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

      {suggestion != null && (
        <Card className="mt-6">
          <CardHeader>
            <CardTitle>Sugerencia de conductor</CardTitle>
          </CardHeader>
          <CardBody>
            <pre className="overflow-auto rounded-md bg-surface-muted p-3 text-xs text-foreground">
              {JSON.stringify(suggestion, null, 2)}
            </pre>
          </CardBody>
        </Card>
      )}
    </div>
  );
}
