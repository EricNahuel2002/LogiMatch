import { useEffect, useState } from 'react';
import { apiFetch } from '@/lib/api';
import { PageHeader } from '@/components/ui/PageHeader';
import { Card, CardHeader, CardTitle } from '@/components/ui/Card';
import { Table, THead, TBody, TR, TH, TD } from '@/components/ui/Table';
import { Badge } from '@/components/ui/Badge';
import { Alert } from '@/components/ui/Alert';
import { EmptyState } from '@/components/ui/EmptyState';
import { Spinner } from '@/components/ui/Spinner';
import { shipmentStatusLabel, shipmentStatusTone, priorityLabel, priorityTone } from '@/lib/shipment';

interface ShipmentDelayRisk {
  shipmentId: string;
  status: string;
  priority: string;
  delayRiskPercentage?: number;
  driver?: { driverId: string; name: string; surname: string };
}

function StatCard({ label, value }: { label: string; value: number }) {
  return (
    <Card className="p-5">
      <p className="text-sm text-muted">{label}</p>
      <p className="mt-1 text-3xl font-semibold text-foreground">{value}</p>
    </Card>
  );
}

export default function AdminDashboard() {
  const [items, setItems] = useState<ShipmentDelayRisk[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  useEffect(() => {
    (async () => {
      try {
        const res = await apiFetch<ShipmentDelayRisk[]>('/api/shipments/pending');
        setItems(res);
      } catch (e: any) {
        setError(e.message);
      } finally {
        setLoading(false);
      }
    })();
  }, []);

  const urgentCount = items.filter((it) => it.priority === 'Urgent').length;
  const highRiskCount = items.filter((it) => (it.delayRiskPercentage ?? 0) >= 50).length;

  return (
    <div>
      <PageHeader title="Panel" description="Resumen operativo de los envíos pendientes." />

      <div className="mb-6 grid grid-cols-1 gap-4 sm:grid-cols-3">
        <StatCard label="Envíos pendientes" value={items.length} />
        <StatCard label="Prioridad urgente" value={urgentCount} />
        <StatCard label="Riesgo alto (≥50%)" value={highRiskCount} />
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Envíos pendientes con riesgo de demora</CardTitle>
        </CardHeader>
        {loading ? (
          <div className="flex items-center gap-2 p-5 text-sm text-muted">
            <Spinner /> Cargando…
          </div>
        ) : error ? (
          <div className="p-5">
            <Alert tone="danger">{error}</Alert>
          </div>
        ) : items.length === 0 ? (
          <EmptyState
            title="Sin envíos pendientes"
            description="No hay envíos pendientes con riesgo de demora por el momento."
          />
        ) : (
          <Table>
            <THead>
              <TR>
                <TH>Envío</TH>
                <TH>Estado</TH>
                <TH>Prioridad</TH>
                <TH>Riesgo</TH>
                <TH>Conductor</TH>
              </TR>
            </THead>
            <TBody>
              {items.map((it) => (
                <TR key={it.shipmentId}>
                  <TD className="font-mono text-xs">{it.shipmentId}</TD>
                  <TD>
                    <Badge tone={shipmentStatusTone(it.status)}>
                      {shipmentStatusLabel(it.status)}
                    </Badge>
                  </TD>
                  <TD>
                    <Badge tone={priorityTone(it.priority)}>{priorityLabel(it.priority)}</Badge>
                  </TD>
                  <TD>{it.delayRiskPercentage != null ? `${it.delayRiskPercentage}%` : '-'}</TD>
                  <TD>{it.driver ? `${it.driver.name} ${it.driver.surname}` : '-'}</TD>
                </TR>
              ))}
            </TBody>
          </Table>
        )}
      </Card>
    </div>
  );
}
