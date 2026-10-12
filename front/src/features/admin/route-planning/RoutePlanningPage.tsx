import { useState } from 'react';
import { apiFetch } from '@/lib/api';
import { PageHeader } from '@/components/ui/PageHeader';
import { Card, CardBody, CardHeader, CardTitle } from '@/components/ui/Card';
import { Textarea } from '@/components/ui/Textarea';
import { Button } from '@/components/ui/Button';
import { Alert } from '@/components/ui/Alert';
import { useToast } from '@/components/ui/useToast';

export default function RoutePlanningPage() {
  const { notify } = useToast();
  const [json, setJson] = useState('{\n  "driverSelections": []\n}');
  const [result, setResult] = useState<unknown>(null);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  async function preview(e: React.FormEvent) {
    e.preventDefault();
    setError('');
    setLoading(true);
    try {
      const body = JSON.parse(json);
      const res = await apiFetch('/api/route-planning/preview', {
        method: 'POST',
        body: JSON.stringify(body),
      });
      setResult(res);
      notify('Propuesta generada', 'success');
    } catch (e: any) {
      setError(e.message);
    } finally {
      setLoading(false);
    }
  }

  return (
    <div>
      <PageHeader
        title="Planificación de rutas"
        description="Generá una propuesta de rutas (solo lectura, no persiste cambios)."
      />
      <Card className="mb-6 max-w-3xl">
        <CardBody>
          <form onSubmit={preview} className="space-y-4">
            <Textarea
              value={json}
              onChange={(e) => setJson(e.target.value)}
              rows={12}
              className="font-mono text-xs"
              aria-label="Solicitud de planificación en JSON"
            />
            <div className="flex justify-end">
              <Button type="submit" loading={loading}>
                Previsualizar
              </Button>
            </div>
          </form>
        </CardBody>
      </Card>

      {error && (
        <Alert tone="danger" className="mb-6">
          {error}
        </Alert>
      )}

      {result != null && (
        <Card>
          <CardHeader>
            <CardTitle>Propuesta</CardTitle>
          </CardHeader>
          <CardBody>
            <pre className="overflow-auto rounded-md bg-surface-muted p-3 text-xs text-foreground">
              {JSON.stringify(result, null, 2)}
            </pre>
          </CardBody>
        </Card>
      )}
    </div>
  );
}
