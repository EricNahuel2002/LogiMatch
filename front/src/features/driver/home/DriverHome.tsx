import { useState } from 'react';
import { apiFetch } from '@/lib/api';
import { PageHeader } from '@/components/ui/PageHeader';
import { Card, CardBody, CardHeader, CardTitle } from '@/components/ui/Card';
import { Field } from '@/components/ui/Field';
import { Input } from '@/components/ui/Input';
import { Select } from '@/components/ui/Select';
import { Button } from '@/components/ui/Button';
import { useToast } from '@/components/ui/useToast';

const cancellationReasons = [
  { value: 'VehicleBreakdown', label: 'Falla del vehículo' },
  { value: 'RouteAbandonment', label: 'Abandono de ruta' },
  { value: 'Emergency', label: 'Emergencia' },
  { value: 'InclementWeather', label: 'Clima adverso' },
  { value: 'Accident', label: 'Accidente' },
];

const shipmentActions = [
  { action: 'start', label: 'Iniciar' },
  { action: 'stop', label: 'Detener' },
  { action: 'resume', label: 'Reanudar' },
  { action: 'arrived', label: 'Llegada' },
  { action: 'delivery-attempts', label: 'Intento de entrega' },
];

export default function DriverHome() {
  const { notify } = useToast();
  const [lat, setLat] = useState('0');
  const [lng, setLng] = useState('0');
  const [reason, setReason] = useState('VehicleBreakdown');
  const [note, setNote] = useState('');
  const [shipmentId, setShipmentId] = useState('');
  const [updatingLocation, setUpdatingLocation] = useState(false);
  const [cancelling, setCancelling] = useState(false);
  const [actionLoading, setActionLoading] = useState('');

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
    } catch (e: any) {
      notify(e.message, 'error');
    } finally {
      setCancelling(false);
    }
  }

  async function doAction(action: string) {
    if (!shipmentId) {
      notify('Ingresá el ID del envío', 'error');
      return;
    }
    setActionLoading(action);
    try {
      let body: Record<string, unknown> = {};
      if (action === 'stop' || action === 'resume') body = { note };
      if (action === 'arrived' || action === 'delivery-attempts') body = { succeeded: true, note };
      await apiFetch(`/api/shipments/${shipmentId}/${action}`, {
        method: 'POST',
        body: JSON.stringify(body),
      });
      notify('Acción realizada correctamente', 'success');
    } catch (e: any) {
      notify(e.message, 'error');
    } finally {
      setActionLoading('');
    }
  }

  return (
    <div>
      <PageHeader title="Panel del conductor" description="Actualizá tu ubicación y gestioná tus envíos." />

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
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

        <Card>
          <CardHeader>
            <CardTitle>Cancelar ruta</CardTitle>
          </CardHeader>
          <CardBody>
            <form onSubmit={cancelRoute} className="space-y-4">
              <Field label="Motivo" htmlFor="reason" required>
                <Select id="reason" value={reason} onChange={(e) => setReason(e.target.value)}>
                  {cancellationReasons.map((option) => (
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

      <Card className="mt-6">
        <CardHeader>
          <CardTitle>Acciones sobre envíos</CardTitle>
        </CardHeader>
        <CardBody className="space-y-4">
          <Field label="ID del envío" htmlFor="shipmentId">
            <Input
              id="shipmentId"
              value={shipmentId}
              onChange={(e) => setShipmentId(e.target.value)}
              placeholder="GUID del envío"
            />
          </Field>
          <div className="flex flex-wrap gap-2">
            {shipmentActions.map((item) => (
              <Button
                key={item.action}
                variant="secondary"
                onClick={() => doAction(item.action)}
                loading={actionLoading === item.action}
              >
                {item.label}
              </Button>
            ))}
          </div>
        </CardBody>
      </Card>
    </div>
  );
}
