import { useCallback, useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { apiFetch } from '@/lib/api';
import type { OrderDetails } from '@/lib/types';
import { formatDateTime, formatCurrency } from '@/lib/format';
import { useDrivers, driverName } from '@/hooks/useCatalog';
import { PageHeader } from '@/components/ui/PageHeader';
import { Card, CardBody, CardHeader, CardTitle } from '@/components/ui/Card';
import { Table, THead, TBody, TR, TH, TD } from '@/components/ui/Table';
import { Field } from '@/components/ui/Field';
import { Input } from '@/components/ui/Input';
import { Select } from '@/components/ui/Select';
import { Button } from '@/components/ui/Button';
import { Alert } from '@/components/ui/Alert';
import { Spinner } from '@/components/ui/Spinner';
import { useToast } from '@/components/ui/useToast';
import { linkButtonGhost } from '@/components/ui/styles';

export default function OrderDetailPage() {
  const { id } = useParams();
  const { notify } = useToast();
  const { data: drivers, loading: driversLoading } = useDrivers();

  const [order, setOrder] = useState<OrderDetails | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  const [name, setName] = useState('');
  const [price, setPrice] = useState('0');
  const [qty, setQty] = useState('1');
  const [weight, setWeight] = useState('0');
  const [savingItem, setSavingItem] = useState(false);

  const [driverId, setDriverId] = useState('');
  const [savingDriver, setSavingDriver] = useState(false);

  const loadOrder = useCallback(async () => {
    if (!id) return;
    const res = await apiFetch<OrderDetails>(`/api/orders/${id}`);
    setOrder(res);
    setDriverId(res.assignedDriverId ?? '');
  }, [id]);

  useEffect(() => {
    (async () => {
      try {
        await loadOrder();
      } catch (e: any) {
        setError(e.message);
      } finally {
        setLoading(false);
      }
    })();
  }, [loadOrder]);

  async function addItem(e: React.FormEvent) {
    e.preventDefault();
    if (!id) return;
    setSavingItem(true);
    try {
      await apiFetch(`/api/orders/${id}/items`, {
        method: 'POST',
        body: JSON.stringify({
          name,
          price: parseFloat(price),
          quantity: parseInt(qty),
          weightKg: parseFloat(weight),
        }),
      });
      notify('Ítem agregado', 'success');
      setName('');
      setPrice('0');
      setQty('1');
      setWeight('0');
      await loadOrder();
    } catch (e: any) {
      notify(e.message, 'error');
    } finally {
      setSavingItem(false);
    }
  }

  async function assignDriver(e: React.FormEvent) {
    e.preventDefault();
    if (!id) return;
    setSavingDriver(true);
    try {
      await apiFetch(`/api/orders/${id}/driver`, {
        method: 'PATCH',
        body: JSON.stringify({ driverId: driverId || null }),
      });
      notify('Conductor asignado', 'success');
      await loadOrder();
    } catch (e: any) {
      notify(e.message, 'error');
    } finally {
      setSavingDriver(false);
    }
  }

  return (
    <div>
      <PageHeader
        title="Detalle del pedido"
        description={id}
        actions={
          <Link to="/admin/orders" className={linkButtonGhost}>
            ← Volver a pedidos
          </Link>
        }
      />

      {loading ? (
        <div className="flex items-center gap-2 text-sm text-muted">
          <Spinner /> Cargando…
        </div>
      ) : error ? (
        <Alert tone="danger">{error}</Alert>
      ) : order ? (
        <div className="space-y-6">
          <Card>
            <CardHeader>
              <CardTitle>Datos generales</CardTitle>
            </CardHeader>
            <CardBody className="grid grid-cols-1 gap-4 text-sm sm:grid-cols-2">
              <div>
                <p className="text-muted">Cliente</p>
                <p className="font-mono text-xs text-foreground">{order.customerId}</p>
              </div>
              <div>
                <p className="text-muted">Conductor</p>
                <p className="text-foreground">
                  {order.assignedDriverId ?? 'Sin asignar'}
                </p>
              </div>
              <div>
                <p className="text-muted">Ventana de entrega</p>
                <p className="text-foreground">
                  {order.deliveryWindowStartAt
                    ? `${formatDateTime(order.deliveryWindowStartAt)} → ${formatDateTime(order.deliveryWindowEndAt)}`
                    : '-'}
                </p>
              </div>
              <div>
                <p className="text-muted">Creado</p>
                <p className="text-foreground">{formatDateTime(order.createdAt)}</p>
              </div>
            </CardBody>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>Ítems</CardTitle>
            </CardHeader>
            {order.items.length === 0 ? (
              <CardBody className="text-sm text-muted">Sin ítems registrados.</CardBody>
            ) : (
              <Table>
                <THead>
                  <TR>
                    <TH>Nombre</TH>
                    <TH>Precio</TH>
                    <TH>Cantidad</TH>
                    <TH>Peso (kg)</TH>
                  </TR>
                </THead>
                <TBody>
                  {order.items.map((item) => (
                    <TR key={item.orderItemId}>
                      <TD>{item.name}</TD>
                      <TD>{formatCurrency(item.price)}</TD>
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
                <CardTitle>Agregar ítem</CardTitle>
              </CardHeader>
              <CardBody>
                <form onSubmit={addItem} className="space-y-4">
                  <Field label="Nombre" htmlFor="itemName" required>
                    <Input id="itemName" value={name} onChange={(e) => setName(e.target.value)} />
                  </Field>
                  <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
                    <Field label="Precio" htmlFor="itemPrice" required>
                      <Input
                        id="itemPrice"
                        type="number"
                        min="0"
                        step="0.01"
                        value={price}
                        onChange={(e) => setPrice(e.target.value)}
                      />
                    </Field>
                    <Field label="Cantidad" htmlFor="itemQty" required>
                      <Input
                        id="itemQty"
                        type="number"
                        min="1"
                        step="1"
                        value={qty}
                        onChange={(e) => setQty(e.target.value)}
                      />
                    </Field>
                    <Field label="Peso (kg)" htmlFor="itemWeight" required>
                      <Input
                        id="itemWeight"
                        type="number"
                        min="0"
                        step="0.1"
                        value={weight}
                        onChange={(e) => setWeight(e.target.value)}
                      />
                    </Field>
                  </div>
                  <div className="flex justify-end">
                    <Button type="submit" loading={savingItem}>
                      Agregar ítem
                    </Button>
                  </div>
                </form>
              </CardBody>
            </Card>

            <Card>
              <CardHeader>
                <CardTitle>Asignar conductor</CardTitle>
              </CardHeader>
              <CardBody>
                <form onSubmit={assignDriver} className="space-y-4">
                  <Field label="Conductor" htmlFor="driverId">
                    <Select
                      id="driverId"
                      value={driverId}
                      onChange={(e) => setDriverId(e.target.value)}
                      disabled={driversLoading}
                    >
                      <option value="">Sin asignar</option>
                      {drivers.map((d) => (
                        <option key={d.id} value={d.id}>
                          {driverName(d)}
                        </option>
                      ))}
                    </Select>
                  </Field>
                  <div className="flex justify-end">
                    <Button type="submit" loading={savingDriver}>
                      Guardar asignación
                    </Button>
                  </div>
                </form>
              </CardBody>
            </Card>
          </div>
        </div>
      ) : null}
    </div>
  );
}
