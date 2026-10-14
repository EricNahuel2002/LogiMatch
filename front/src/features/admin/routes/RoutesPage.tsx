import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { apiFetch } from '@/lib/api';
import type { DriverSummary, RouteSummary, VehicleSummary } from '@/lib/types';
import { formatDateTime, shortId } from '@/lib/format';
import { routeCancellationReasonLabel } from '@/lib/route';
import { driverName, vehicleLabel } from '@/hooks/useCatalog';
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

export default function RoutesPage() {
  const { notify } = useToast();
  const [routes, setRoutes] = useState<RouteSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [drivers, setDrivers] = useState<DriverSummary[]>([]);
  const [vehicles, setVehicles] = useState<VehicleSummary[]>([]);

  const [lat, setLat] = useState('0');
  const [lng, setLng] = useState('0');
  const [shipmentId, setShipmentId] = useState('');
  const [address, setAddress] = useState('');
  const [stopName, setStopName] = useState('');
  const [stopOrder, setStopOrder] = useState('1');
  const [savingRoute, setSavingRoute] = useState(false);
  const [savingStop, setSavingStop] = useState(false);

  const loadRoutes = useCallback(async () => {
    const res = await apiFetch<RouteSummary[]>('/api/routes');
    setRoutes(res);
  }, []);

  useEffect(() => {
    (async () => {
      try {
        const [r, d, v] = await Promise.all([
          apiFetch<RouteSummary[]>('/api/routes'),
          apiFetch<DriverSummary[]>('/api/drivers'),
          apiFetch<VehicleSummary[]>('/api/vehicles'),
        ]);
        setRoutes(r);
        setDrivers(d);
        setVehicles(v);
      } catch (e: any) {
        setError(e.message);
      } finally {
        setLoading(false);
      }
    })();
  }, []);

  async function createRoute(e: React.FormEvent) {
    e.preventDefault();
    setSavingRoute(true);
    try {
      const id = await apiFetch<string>('/api/routes', {
        method: 'POST',
        body: JSON.stringify({ latitude: parseFloat(lat), longitude: parseFloat(lng) }),
      });
      notify(`Ruta creada: ${shortId(id)}`, 'success');
      await loadRoutes();
    } catch (e: any) {
      notify(e.message, 'error');
    } finally {
      setSavingRoute(false);
    }
  }

  async function createStop(e: React.FormEvent) {
    e.preventDefault();
    setSavingStop(true);
    try {
      const id = await apiFetch<string>('/api/route-stops', {
        method: 'POST',
        body: JSON.stringify({
          shipmentId,
          address,
          name: stopName || null,
          stopOrder: parseInt(stopOrder),
          distanceMeters: 0,
          tollCost: 0,
        }),
      });
      notify(`Parada creada: ${shortId(id)}`, 'success');
      setShipmentId('');
      setAddress('');
      setStopName('');
      setStopOrder('1');
    } catch (e: any) {
      notify(e.message, 'error');
    } finally {
      setSavingStop(false);
    }
  }

  const driverLabel = (driverId?: string | null) => {
    if (!driverId) return '-';
    const found = drivers.find((d) => d.id === driverId);
    return found ? driverName(found) : shortId(driverId);
  };

  const vehicleLabelById = (vehicleId?: string | null) => {
    if (!vehicleId) return '-';
    const found = vehicles.find((v) => v.id === vehicleId);
    return found ? vehicleLabel(found) : shortId(vehicleId);
  };

  return (
    <div>
      <PageHeader title="Rutas y paradas" description="Creá rutas, sus paradas y consultá las existentes." />

      <div className="mb-6 grid grid-cols-1 gap-6 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>Nueva ruta</CardTitle>
          </CardHeader>
          <CardBody>
            <form onSubmit={createRoute} className="space-y-4">
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
                <Button type="submit" loading={savingRoute}>
                  Crear ruta
                </Button>
              </div>
            </form>
          </CardBody>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Nueva parada</CardTitle>
          </CardHeader>
          <CardBody>
            <form onSubmit={createStop} className="space-y-4">
              <Field label="ID del envío" htmlFor="shipmentId" required>
                <Input
                  id="shipmentId"
                  value={shipmentId}
                  onChange={(e) => setShipmentId(e.target.value)}
                  placeholder="GUID del envío"
                />
              </Field>
              <Field label="Dirección" htmlFor="address" required>
                <Input
                  id="address"
                  value={address}
                  onChange={(e) => setAddress(e.target.value)}
                  placeholder="Dirección a geocodificar"
                />
              </Field>
              <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                <Field label="Nombre" htmlFor="stopName">
                  <Input
                    id="stopName"
                    value={stopName}
                    onChange={(e) => setStopName(e.target.value)}
                    placeholder="Opcional"
                  />
                </Field>
                <Field label="Orden de parada" htmlFor="stopOrder" required>
                  <Input
                    id="stopOrder"
                    type="number"
                    min="1"
                    step="1"
                    value={stopOrder}
                    onChange={(e) => setStopOrder(e.target.value)}
                  />
                </Field>
              </div>
              <div className="flex justify-end">
                <Button type="submit" loading={savingStop}>
                  Crear parada
                </Button>
              </div>
            </form>
          </CardBody>
        </Card>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Rutas registradas</CardTitle>
        </CardHeader>
        {loading ? (
          <div className="flex items-center gap-2 p-5 text-sm text-muted">
            <Spinner /> Cargando…
          </div>
        ) : error ? (
          <div className="p-5">
            <Alert tone="danger">{error}</Alert>
          </div>
        ) : routes.length === 0 ? (
          <EmptyState title="Sin rutas" description="Todavía no hay rutas registradas." />
        ) : (
          <Table>
            <THead>
              <TR>
                <TH>Ruta</TH>
                <TH>Estado</TH>
                <TH>Conductor</TH>
                <TH>Vehículo</TH>
                <TH>Motivo cancelación</TH>
                <TH>Creada</TH>
                <TH className="text-right">Acciones</TH>
              </TR>
            </THead>
            <TBody>
              {routes.map((route) => (
                <TR key={route.id}>
                  <TD className="font-mono text-xs">{shortId(route.id)}</TD>
                  <TD>
                    <Badge tone={route.isActive ? 'success' : 'neutral'}>
                      {route.isActive ? 'Activa' : 'Inactiva'}
                    </Badge>
                  </TD>
                  <TD>{driverLabel(route.driverId)}</TD>
                  <TD>{vehicleLabelById(route.vehicleId)}</TD>
                  <TD>{routeCancellationReasonLabel(route.cancellationReason)}</TD>
                  <TD>{formatDateTime(route.createdAt)}</TD>
                  <TD>
                    <div className="flex justify-end">
                      <Link to={`/admin/routes/${route.id}`} className={linkButtonGhost}>
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
