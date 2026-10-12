import { useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { apiFetch } from '@/lib/api';
import { PageHeader } from '@/components/ui/PageHeader';
import { Card, CardBody } from '@/components/ui/Card';
import { Button } from '@/components/ui/Button';
import { Alert } from '@/components/ui/Alert';
import { EmptyState } from '@/components/ui/EmptyState';
import { linkButtonGhost } from '@/components/ui/styles';

export default function ShipmentAssignPage() {
  const { id } = useParams();
  const [data, setData] = useState<unknown>(null);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  async function suggest() {
    setError('');
    setLoading(true);
    try {
      const res = await apiFetch(`/api/shipments/${id}/driver-suggestion`, { method: 'POST' });
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
      <Card className="max-w-3xl">
        <CardBody className="space-y-4">
          <div className="flex justify-end">
            <Button onClick={suggest} loading={loading}>
              Obtener sugerencia
            </Button>
          </div>
          {error && <Alert tone="danger">{error}</Alert>}
          {data != null ? (
            <pre className="overflow-auto rounded-md bg-surface-muted p-3 text-xs text-foreground">
              {JSON.stringify(data, null, 2)}
            </pre>
          ) : (
            !error && (
              <EmptyState
                title="Sin sugerencia todavía"
                description="Generá una sugerencia de conductor para este envío."
              />
            )
          )}
        </CardBody>
      </Card>
    </div>
  );
}
