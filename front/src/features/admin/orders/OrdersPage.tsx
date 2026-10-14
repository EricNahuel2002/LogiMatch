import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { apiFetch } from '@/lib/api';
import type { OrderSummary } from '@/lib/types';
import { formatDateTime, shortId } from '@/lib/format';
import { useCustomers } from '@/hooks/useCatalog';
import { useAuthStore } from '@/hooks/useAuth';
import { PageHeader } from '@/components/ui/PageHeader';
import { Card, CardBody, CardHeader, CardTitle } from '@/components/ui/Card';
import { Table, THead, TBody, TR, TH, TD } from '@/components/ui/Table';
import { Field } from '@/components/ui/Field';
import { Input } from '@/components/ui/Input';
import { Select } from '@/components/ui/Select';
import { Button } from '@/components/ui/Button';
import { Alert } from '@/components/ui/Alert';
import { EmptyState } from '@/components/ui/EmptyState';
import { Spinner } from '@/components/ui/Spinner';
import { useToast } from '@/components/ui/useToast';
import { linkButtonGhost } from '@/components/ui/styles';

export default function OrdersPage() {
  const { notify } = useToast();
  const { user } = useAuthStore();
  const { data: customers, loading: customersLoading } = useCustomers();

  const [orders, setOrders] = useState<OrderSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [customerId, setCustomerId] = useState('');
  const [createdByAdminId, setCreatedByAdminId] = useState(user?.id ?? '');
  const [name, setName] = useState('');
  const [price, setPrice] = useState('0');
  const [qty, setQty] = useState('1');
  const [weight, setWeight] = useState('0');
  const [saving, setSaving] = useState(false);

  const loadOrders = useCallback(async () => {
    const res = await apiFetch<OrderSummary[]>('/api/orders');
    setOrders(res);
  }, []);

  useEffect(() => {
    (async () => {
      try {
        await loadOrders();
      } catch (e: any) {
        setError(e.message);
      } finally {
        setLoading(false);
      }
    })();
  }, [loadOrders]);

  async function createOrder(e: React.FormEvent) {
    e.preventDefault();
    setSaving(true);
    try {
      const orderId = await apiFetch<string>('/api/orders', {
        method: 'POST',
        body: JSON.stringify({
          customerId,
          createdByAdminId,
          items: [{ name, price: parseFloat(price), quantity: parseInt(qty), weightKg: parseFloat(weight) }],
        }),
      });
      notify(`Pedido creado: ${shortId(orderId)}`, 'success');
      setName('');
      setPrice('0');
      setQty('1');
      setWeight('0');
      await loadOrders();
    } catch (e: any) {
      notify(e.message, 'error');
    } finally {
      setSaving(false);
    }
  }

  return (
    <div>
      <PageHeader title="Pedidos" description="Creá pedidos y consultá su estado y asignaciones." />

      <Card className="mb-6 max-w-3xl">
        <CardHeader>
          <CardTitle>Nuevo pedido</CardTitle>
        </CardHeader>
        <CardBody>
          <form onSubmit={createOrder} className="space-y-5">
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              <Field label="Cliente" htmlFor="customerId" required>
                <Select
                  id="customerId"
                  value={customerId}
                  onChange={(e) => setCustomerId(e.target.value)}
                  disabled={customersLoading}
                >
                  <option value="">Seleccioná un cliente…</option>
                  {customers.map((c) => (
                    <option key={c.id} value={c.id}>
                      {c.name} {c.surname}
                    </option>
                  ))}
                </Select>
              </Field>
              <Field
                label="ID de administrador"
                htmlFor="createdByAdminId"
                required
                hint="Por defecto, tu usuario actual."
              >
                <Input
                  id="createdByAdminId"
                  value={createdByAdminId}
                  onChange={(e) => setCreatedByAdminId(e.target.value)}
                  placeholder="GUID del administrador"
                />
              </Field>
            </div>

            <p className="text-sm font-semibold text-foreground">Ítem del pedido</p>
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              <Field label="Nombre" htmlFor="itemName" required>
                <Input
                  id="itemName"
                  value={name}
                  onChange={(e) => setName(e.target.value)}
                  placeholder="Descripción del ítem"
                />
              </Field>
              <Field label="Precio" htmlFor="price" required>
                <Input
                  id="price"
                  type="number"
                  min="0"
                  step="0.01"
                  value={price}
                  onChange={(e) => setPrice(e.target.value)}
                />
              </Field>
              <Field label="Cantidad" htmlFor="qty" required>
                <Input
                  id="qty"
                  type="number"
                  min="1"
                  step="1"
                  value={qty}
                  onChange={(e) => setQty(e.target.value)}
                />
              </Field>
              <Field label="Peso (kg)" htmlFor="weight" required>
                <Input
                  id="weight"
                  type="number"
                  min="0"
                  step="0.1"
                  value={weight}
                  onChange={(e) => setWeight(e.target.value)}
                />
              </Field>
            </div>

            <div className="flex justify-end">
              <Button type="submit" loading={saving}>
                Crear pedido
              </Button>
            </div>
          </form>
        </CardBody>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Pedidos registrados</CardTitle>
        </CardHeader>
        {loading ? (
          <div className="flex items-center gap-2 p-5 text-sm text-muted">
            <Spinner /> Cargando…
          </div>
        ) : error ? (
          <div className="p-5">
            <Alert tone="danger">{error}</Alert>
          </div>
        ) : orders.length === 0 ? (
          <EmptyState title="Sin pedidos" description="Todavía no hay pedidos registrados." />
        ) : (
          <Table>
            <THead>
              <TR>
                <TH>Pedido</TH>
                <TH>Cliente</TH>
                <TH>Conductor</TH>
                <TH>Ventana de entrega</TH>
                <TH>Creado</TH>
                <TH className="text-right">Acciones</TH>
              </TR>
            </THead>
            <TBody>
              {orders.map((order) => (
                <TR key={order.id}>
                  <TD className="font-mono text-xs">{shortId(order.id)}</TD>
                  <TD className="font-mono text-xs">{shortId(order.customerId)}</TD>
                  <TD>
                    {order.assignedDriverId ? (
                      <span className="font-mono text-xs">{shortId(order.assignedDriverId)}</span>
                    ) : (
                      <span className="text-muted">Sin asignar</span>
                    )}
                  </TD>
                  <TD>
                    {order.deliveryWindowStartAt
                      ? `${formatDateTime(order.deliveryWindowStartAt)} → ${formatDateTime(order.deliveryWindowEndAt)}`
                      : '-'}
                  </TD>
                  <TD>{formatDateTime(order.createdAt)}</TD>
                  <TD>
                    <div className="flex justify-end">
                      <Link to={`/admin/orders/${order.id}`} className={linkButtonGhost}>
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
