import { useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { apiFetch } from '@/lib/api';
import type { DriverAssignmentSuggestion } from '@/lib/types';
import { formatCurrency, formatMeters, shortId } from '@/lib/format';
import { useDrivers, driverName } from '@/hooks/useCatalog';
import { PageHeader } from '@/components/ui/PageHeader';
import { Card, CardBody, CardHeader, CardTitle } from '@/components/ui/Card';
import { Table, THead, TBody, TR, TH, TD } from '@/components/ui/Table';
import { Button } from '@/components/ui/Button';
import { Badge } from '@/components/ui/Badge';
import { Alert } from '@/components/ui/Alert';
import { EmptyState } from '@/components/ui/EmptyState';
import { linkButtonGhost } from '@/components/ui/styles';

export default function ShipmentAssignPage() {
  const { id } = useParams();
  const { data: drivers } = useDrivers();
  const [data, setData] = useState<DriverAssignmentSuggestion | null>(null);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  const driverLabel = (driverId: string) => {
    const found = drivers.find((d) => d.id === driverId);
    return found ? driverName(found) : shortId(driverId);
  };

  async function suggest() {
    setError('');
    setLoading(true);
    try {
      const res = await apiFetch<DriverAssignmentSuggestion>(`/api/shipments/${id}/driver-suggestion`, {
        method: 'POST',
      });
      setData(res);
    } catch (e: any) {
      setError(e.message);
    } finally {
      setLoading(false);
    }
  }

  return (
    <div>
      <PageHeader
        title="Asignar conductor"
        description={id ? `Envío ${id}` : undefined}
        actions={
          <Link to="/admin/shipments" className={linkButtonGhost}>
            ← Volver a envíos
          </Link>
        }
      />

      <Card className="mb-6 max-w-3xl">
        <CardBody className="space-y-4">
          <div className="flex justify-end">
            <Button onClick={suggest} loading={loading}>
              Obtener sugerencia
            </Button>
          </div>
          {error && <Alert tone="danger">{error}</Alert>}
          {!data && !error && (
            <EmptyState
              title="Sin sugerencia todavía"
              description="Generá una sugerencia de conductor para este envío."
            />
          )}
        </CardBody>
      </Card>

      {data && (
        <Card>
          <CardHeader>
            <CardTitle>Ranking de conductores</CardTitle>
            <Badge tone="brand">Recomendado: {driverLabel(data.recommendedDriverId)}</Badge>
          </CardHeader>
          {data.ranking.length === 0 ? (
            <EmptyState
              title="Sin candidatos"
              description="No hay conductores elegibles para este envío."
            />
          ) : (
            <Table>
              <THead>
                <TR>
                  <TH>Conductor</TH>
                  <TH>Score</TH>
                  <TH>Éxitos hoy</TH>
                  <TH>Pendientes</TH>
                  <TH>En progreso</TH>
                  <TH>Distancia</TH>
                  <TH>Duración</TH>
                  <TH>Capacidad libre</TH>
                  <TH>Costo estimado</TH>
                </TR>
              </THead>
              <TBody>
                {[...data.ranking]
                  .sort((a, b) => b.score - a.score)
                  .map((item) => (
                    <TR
                      key={item.driverId}
                      className={item.driverId === data.recommendedDriverId ? 'bg-brand-50/60' : undefined}
                    >
                      <TD>
                        <span className="flex items-center gap-2">
                          {driverLabel(item.driverId)}
                          {item.driverId === data.recommendedDriverId && (
                            <Badge tone="brand">Recomendado</Badge>
                          )}
                        </span>
                      </TD>
                      <TD>{item.score.toFixed(2)}</TD>
                      <TD>{item.successAttemptsToday}</TD>
                      <TD>{item.pendingShipmentCount}</TD>
                      <TD>{item.inProgressShipmentCount}</TD>
                      <TD>{formatMeters(item.distanceMeters)}</TD>
                      <TD>{item.durationMinutes} min</TD>
                      <TD>{item.freeCapacityKg} kg</TD>
                      <TD>{formatCurrency(item.estimatedOperationCost)}</TD>
                    </TR>
                  ))}
              </TBody>
            </Table>
          )}
        </Card>
      )}
    </div>
  );
}
