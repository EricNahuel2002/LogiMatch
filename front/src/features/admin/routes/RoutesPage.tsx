import { useState } from 'react';
import { apiFetch } from '@/lib/api';
import { PageHeader } from '@/components/ui/PageHeader';
import { Card, CardBody, CardHeader, CardTitle } from '@/components/ui/Card';
import { Field } from '@/components/ui/Field';
import { Input } from '@/components/ui/Input';
import { Button } from '@/components/ui/Button';
import { useToast } from '@/components/ui/useToast';

export default function RoutesPage() {
  const { notify } = useToast();
  const [lat, setLat] = useState('0');
  const [lng, setLng] = useState('0');
  const [shipmentId, setShipmentId] = useState('');
  const [address, setAddress] = useState('');
  const [stopOrder, setStopOrder] = useState('1');
  const [savingRoute, setSavingRoute] = useState(false);
  const [savingStop, setSavingStop] = useState(false);

  async function createRoute(e: React.FormEvent) {
    e.preventDefault();
    setSavingRoute(true);
    try {
      const id = await apiFetch<string>('/api/routes', {
        method: 'POST',
        body: JSON.stringify({ latitude: parseFloat(lat), longitude: parseFloat(lng) }),
      });
      notify(`Ruta creada: ${id}`, 'success');
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
          stopOrder: parseInt(stopOrder),
          distanceMeters: 0,
          tollCost: 0,
        }),
      });
      notify(`Parada creada: ${id}`, 'success');
    } catch (e: any) {
      notify(e.message, 'error');
    } finally {
      setSavingStop(false);
    }
  }

  return (
    <div>
      <PageHeader title="Rutas y paradas" description="Creá rutas y sus paradas asociadas." />

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
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
              <div className="flex justify-end">
                <Button type="submit" loading={savingStop}>
                  Crear parada
                </Button>
              </div>
            </form>
          </CardBody>
        </Card>
      </div>
    </div>
  );
}
