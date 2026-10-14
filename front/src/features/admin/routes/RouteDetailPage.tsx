import { useCallback, useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { apiFetch } from '@/lib/api';
import type { DriverSummary, RouteDetails, VehicleSummary } from '@/lib/types';
import { formatDateTime, formatCurrency, formatMeters, shortId } from '@/lib/format';
import { routeCancellationReasonLabel } from '@/lib/route';
import { driverName, vehicleLabel } from '@/hooks/useCatalog';
import { PageHeader } from '@/components/ui/PageHeader';
import { Card, CardBody, CardHeader, CardTitle } from '@/components/ui/Card';
import { Table, THead, TBody, TR, TH, TD } from '@/components/ui/Table';
import { Field } from '@/components/ui/Field';
import { Input } from '@/components/ui/Input';
import { Select } from '@/components/ui/Select';
import { Button } from '@/components/ui/Button';
import { Badge } from '@/components/ui/Badge';
import { Alert } from '@/components/ui/Alert';
import { EmptyState } from '@/components/ui/EmptyState';
import { Spinner } from '@/components/ui/Spinner';
import { useToast } from '@/components/ui/useToast';
import { linkButtonGhost } from '@/components/ui/styles';

export default function RouteDetailPage() {
  const { id } = useParams();
  const { notify } = useToast();

  const [route, setRoute] = useState<RouteDetails | null>(null);
  const [drivers, setDrivers] = useState<DriverSummary[]>([]);
  const [vehicles, setVehicles] = useState<VehicleSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  const [driverId, setDriverId] = useState('');
  const [savingDriver, setSavingDriver] = useState(false);
  const [vehicleId, setVehicleId] = useState('');
  const [savingVehicle, setSavingVehicle] = useState(false);
  const [routeStopId, setRouteStopId] = useState('');
  const [savingStop, setSavingStop] = useState(false);

  const loadRoute = useCallback(async () => {
    if (!id) return;
    const res = await apiFetch<RouteDetails>(`/api/routes/${id}`);
    setRoute(res);
    setDriverId(res.driverId ?? '');
    setVehicleId(res.vehicleId ?? '');
  }, [id]);

  useEffect(() => {
    (async () => {
      try {
        const [, d, v] = await Promise.all([
          loadRoute(),
          apiFetch<DriverSummary[]>('/api/drivers'),
          apiFetch<VehicleSummary[]>('/api/vehicles'),
        ]);
        setDrivers(d);
        setVehicles(v);
      } catch (e: any) {
        setError(e.message);
      } finally {
        setLoading(false);
      }
    })();
  }, [loadRoute]);

  async function assignDriver(e: React.FormEvent) {
    e.preventDefault();
    if (!id) return;
    setSavingDriver(true);
    try {
      await apiFetch(`/api/routes/${id}/driver`, {
        method: 'PATCH',
        body: JSON.stringify({ driverId }),
      });
      notify('Conductor asignado a la ruta', 'success');
      await loadRoute();
    } catch (e: any) {
      notify(e.message, 'error');
    } finally {
      setSavingDriver(false);
    }
  }

  async function assignVehicle(e: React.FormEvent) {
    e.preventDefault();
    if (!id) return;
    setSavingVehicle(true);
    try {
      await apiFetch(`/api/routes/${id}/vehicle`, {
        method: 'PATCH',
        body: JSON.stringify({ vehicleId }),
      });
      notify('Vehículo asignado a la ruta', 'success');
      await loadRoute();
    } catch (e: any) {
      notify(e.message, 'error');
    } finally {
      setSavingVehicle(false);
    }
  }

  async function attachStop(e: React.FormEvent) {
    e.preventDefault();
    if (!id) return;
    setSavingStop(true);
    try {
      await apiFetch(`/api/routes/${id}/stops/${routeStopId}`, { method: 'POST' });
      notify('Parada adjuntada a la ruta', 'success');
      setRouteStopId('');
      await loadRoute();
    } catch (e: any) {
      notify(e.message, 'error');
    } finally {
      setSavingStop(false);
    }
  }

  return (
    <div>
      <PageHeader
        title="Detalle de ruta"
        description={id}
        actions={
          <Link to="/admin/routes" className={linkButtonGhost}>
            ← Volver a rutas
          </Link>
        }
      />

      {loading ? (
        <div className="flex items-center gap-2 text-sm text-muted">
          <Spinner /> Cargando…
        </div>
      ) : error ? (
        <Alert tone="danger">{error}</Alert>
      ) : route ? (
        <div className="space-y-6">
          <Card>
            <CardHeader>
              <CardTitle>Datos generales</CardTitle>
              <Badge tone={route.isActive ? 'success' : 'neutral'}>
                {route.isActive ? 'Activa' : 'Inactiva'}
              </Badge>
            </CardHeader>
            <CardBody className="grid grid-cols-1 gap-4 text-sm sm:grid-cols-2 lg:grid-cols-3">
              <div>
                <p className="text-muted">Origen</p>
                <p className="text-foreground">
                  {route.origin.latitude}, {route.origin.longitude}
                </p>
              </div>
              <div>
                <p className="text-muted">Creada</p>
                <p className="text-foreground">{formatDateTime(route.createdAt)}</p>
              </div>
              {route.cancellationReason && (
                <div>
                  <p className="text-muted">Motivo de cancelación</p>
                  <p className="text-foreground">
                    {routeCancellationReasonLabel(route.cancellationReason)}
                  </p>
                </div>
              )}
            </CardBody>
          </Card>

          <div className="grid grid-cols-1 gap-6 lg:grid-cols-3">
            <Card>
              <CardHeader>
                <CardTitle>Asignar conductor</CardTitle>
              </CardHeader>
              <CardBody>
                <form onSubmit={assignDriver} className="space-y-4">
                  <Field label="Conductor" htmlFor="routeDriver" required>
                    <Select
                      id="routeDriver"
                      value={driverId}
                      onChange={(e) => setDriverId(e.target.value)}
                    >
                      <option value="">Seleccioná un conductor…</option>
                      {drivers.map((d) => (
                        <option key={d.id} value={d.id}>
                          {driverName(d)}
                        </option>
                      ))}
                    </Select>
                  </Field>
                  <div className="flex justify-end">
                    <Button type="submit" loading={savingDriver} disabled={!driverId}>
                      Asignar
                    </Button>
                  </div>
                </form>
              </CardBody>
            </Card>

            <Card>
              <CardHeader>
                <CardTitle>Asignar vehículo</CardTitle>
              </CardHeader>
              <CardBody>
                <form onSubmit={assignVehicle} className="space-y-4">
                  <Field label="Vehículo" htmlFor="routeVehicle" required>
                    <Select
                      id="routeVehicle"
                      value={vehicleId}
                      onChange={(e) => setVehicleId(e.target.value)}
                    >
                      <option value="">Seleccioná un vehículo…</option>
                      {vehicles.map((v) => (
                        <option key={v.id} value={v.id}>
                          {vehicleLabel(v)}
                        </option>
                      ))}
                    </Select>
                  </Field>
                  <div className="flex justify-end">
                    <Button type="submit" loading={savingVehicle} disabled={!vehicleId}>
                      Asignar
                    </Button>
                  </div>
                </form>
              </CardBody>
            </Card>

            <Card>
              <CardHeader>
                <CardTitle>Adjuntar parada</CardTitle>
              </CardHeader>
              <CardBody>
                <form onSubmit={attachStop} className="space-y-4">
                  <Field
                    label="ID de parada"
                    htmlFor="routeStopId"
                    required
                    hint="GUID de la parada a asignar."
                  >
                    <Input
                      id="routeStopId"
                      value={routeStopId}
                      onChange={(e) => setRouteStopId(e.target.value)}
                      placeholder="GUID de la parada"
                    />
                  </Field>
                  <div className="flex justify-end">
                    <Button type="submit" loading={savingStop} disabled={!routeStopId}>
                      Adjuntar
                    </Button>
                  </div>
                </form>
              </CardBody>
            </Card>
          </div>

          <Card>
            <CardHeader>
              <CardTitle>Paradas de la ruta</CardTitle>
            </CardHeader>
            {route.stops.length === 0 ? (
              <EmptyState title="Sin paradas" description="Esta ruta todavía no tiene paradas asignadas." />
            ) : (
              <Table>
                <THead>
                  <TR>
                    <TH>Orden</TH>
                    <TH>Parada</TH>
                    <TH>Envío</TH>
                    <TH>Dirección</TH>
                    <TH>Distancia</TH>
                    <TH>Peaje</TH>
                  </TR>
                </THead>
                <TBody>
                  {[...route.stops]
                    .sort((a, b) => a.stopOrder - b.stopOrder)
                    .map((stop) => (
                      <TR key={stop.id}>
                        <TD>{stop.stopOrder}</TD>
                        <TD className="font-mono text-xs">{shortId(stop.id)}</TD>
                        <TD className="font-mono text-xs">{shortId(stop.shipmentId)}</TD>
                        <TD>{stop.name ?? '-'}</TD>
                        <TD>{formatMeters(stop.distanceMeters)}</TD>
                        <TD>{formatCurrency(stop.tollCost)}</TD>
                      </TR>
                    ))}
                </TBody>
              </Table>
            )}
          </Card>
        </div>
      ) : null}
    </div>
  );
}
