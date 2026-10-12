import { useState } from 'react';
import { apiFetch } from '@/lib/api';
import { PageHeader } from '@/components/ui/PageHeader';
import { Card, CardBody, CardHeader, CardTitle } from '@/components/ui/Card';
import { Field } from '@/components/ui/Field';
import { Input } from '@/components/ui/Input';
import { Button } from '@/components/ui/Button';
import { useToast } from '@/components/ui/useToast';

export default function OrdersPage() {
  const { notify } = useToast();
  const [customerId, setCustomerId] = useState('');
  const [createdByAdminId, setCreatedByAdminId] = useState('');
  const [name, setName] = useState('');
  const [price, setPrice] = useState('0');
  const [qty, setQty] = useState('1');
  const [weight, setWeight] = useState('0');
  const [loading, setLoading] = useState(false);

  async function createOrder(e: React.FormEvent) {
    e.preventDefault();
    setLoading(true);
    try {
      const orderId = await apiFetch<string>('/api/orders', {
        method: 'POST',
        body: JSON.stringify({
          customerId,
          createdByAdminId,
          items: [{ name, price: parseFloat(price), quantity: parseInt(qty), weightKg: parseFloat(weight) }],
        }),
      });
      notify(`Pedido creado: ${orderId}`, 'success');
      setName('');
      setPrice('0');
      setQty('1');
      setWeight('0');
    } catch (e: any) {
      notify(e.message, 'error');
    } finally {
      setLoading(false);
    }
  }

  return (
    <div>
      <PageHeader title="Pedidos" description="Registrá un pedido con su primer ítem." />
      <Card className="max-w-3xl">
        <CardHeader>
          <CardTitle>Nuevo pedido</CardTitle>
        </CardHeader>
        <CardBody>
          <form onSubmit={createOrder} className="space-y-5">
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              <Field label="ID de cliente" htmlFor="customerId" required>
                <Input
                  id="customerId"
                  value={customerId}
                  onChange={(e) => setCustomerId(e.target.value)}
                  placeholder="GUID del cliente"
                />
              </Field>
              <Field label="ID de administrador" htmlFor="createdByAdminId" required>
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
              <Button type="submit" loading={loading}>
                Crear pedido
              </Button>
            </div>
          </form>
        </CardBody>
      </Card>
    </div>
  );
}
