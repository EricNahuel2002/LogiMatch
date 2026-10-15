import { useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { apiFetch } from '@/lib/api';
import type {
  CommitFromProposalResponse,
  RoutePlanningProposal,
} from '@/lib/types';
import { formatKg, formatMeters, shortId } from '@/lib/format';
import {
  useDeposits,
  useDrivers,
  usePendingShipments,
  useVehicles,
  driverName,
  vehicleLabel,
} from '@/hooks/useCatalog';
import { priorityLabel, priorityTone } from '@/lib/shipment';
import { PageHeader } from '@/components/ui/PageHeader';
import { Card, CardBody, CardHeader, CardTitle } from '@/components/ui/Card';
import { Table, THead, TBody, TR, TH, TD } from '@/components/ui/Table';
import { Field } from '@/components/ui/Field';
import { Select } from '@/components/ui/Select';
import { Button } from '@/components/ui/Button';
import { Badge } from '@/components/ui/Badge';
import { Alert } from '@/components/ui/Alert';
import { EmptyState } from '@/components/ui/EmptyState';
import { Spinner } from '@/components/ui/Spinner';
import { useToast } from '@/components/ui/useToast';
import { linkButtonPrimary } from '@/components/ui/styles';
import { RoutePlanBranch } from './components/RoutePlanBranch';

interface SelectionRow {
  key: number;
  driverId: string;
  vehicleId: string;
  depositId: string;
}

let nextKey = 1;

function newRow(): SelectionRow {
  return { key: nextKey++, driverId: '', vehicleId: '', depositId: '' };
}

export default function RoutePlanningPage() {
  const { notify } = useToast();
  const { data: drivers, loading: driversLoading } = useDrivers();
  const { data: vehicles, loading: vehiclesLoading } = useVehicles();
  const { data: deposits } = useDeposits();
  const {
    data: pending,
    loading: pendingLoading,
    error: pendingError,
    reload: reloadPending,
  } = usePendingShipments();

  const [rows, setRows] = useState<SelectionRow[]>([newRow()]);
  const [loading, setLoading] = useState(false);
  const [committing, setCommitting] = useState(false);
  const [error, setError] = useState('');
  const [proposal, setProposal] = useState<RoutePlanningProposal | null>(null);
  const [committed, setCommitted] = useState<CommitFromProposalResponse | null>(null);

  const ready = rows.length > 0 && rows.every((r) => r.driverId && r.vehicleId);

  const shipmentById = useMemo(
    () => new Map(pending.map((s) => [s.shipmentId, s])),
    [pending],
  );

  const totalPendingWeightKg = useMemo(
    () => pending.reduce((sum, s) => sum + (s.weightKg ?? 0), 0),
    [pending],
  );

  const totalSelectedCapacityKg = useMemo(
    () =>
      rows.reduce(
        (sum, r) => sum + (vehicles.find((v) => v.id === r.vehicleId)?.capacityKg ?? 0),
        0,
      ),
    [rows, vehicles],
  );

  function updateRow(key: number, patch: Partial<SelectionRow>) {
    setRows((prev) => prev.map((r) => (r.key === key ? { ...r, ...patch } : r)));
  }

  function addRow() {
    setRows((prev) => [...prev, newRow()]);
  }

  function removeRow(key: number) {
    setRows((prev) => (prev.length > 1 ? prev.filter((r) => r.key !== key) : prev));
  }

  async function preview(e: React.FormEvent) {
    e.preventDefault();
    setError('');
    setCommitted(null);
    setLoading(true);
    try {
      const driverSelections = rows.map((r) => ({
        driverId: r.driverId,
        vehicleId: r.vehicleId,
        depositId: r.depositId || null,
      }));
      const res = await apiFetch<RoutePlanningProposal>('/api/route-planning/preview', {
        method: 'POST',
        body: JSON.stringify({ driverSelections }),
      });
      setProposal(res);
      notify('Propuesta generada', 'success');
    } catch (e: any) {
      setProposal(null);
      setError(e.message);
    } finally {
      setLoading(false);
    }
  }

  async function commit() {
    if (!proposal) return;
    setCommitting(true);
    try {
      const assignments = proposal.assignments.map((a) => ({
        driverId: a.driverId,
        vehicleId: a.vehicleId,
        depositId: a.depositId,
        stops: a.stops.map((s) => ({ shipmentId: s.shipmentId, stopOrder: s.stopOrder })),
      }));
      const res = await apiFetch<CommitFromProposalResponse>('/api/routes/commit-from-proposal', {
        method: 'POST',
        body: JSON.stringify({ assignments }),
      });
      setCommitted(res);
      notify(`Se crearon ${res.routes.length} ruta(s)`, 'success');
      reloadPending();
    } catch (e: any) {
      notify(e.message, 'error');
    } finally {
      setCommitting(false);
    }
  }

  const driverLabel = (driverId: string) => {
    const found = drivers.find((d) => d.id === driverId);
    return found ? driverName(found) : shortId(driverId);
  };

  const vehicleLabelById = (vehicleId: string) => {
    const found = vehicles.find((v) => v.id === vehicleId);
    return found ? vehicleLabel(found) : shortId(vehicleId);
  };

  const depositLabel = (depositId: string) => {
    const found = deposits.find((d) => d.id === depositId);
    return found ? found.address : shortId(depositId);
  };

  return (
    <div>
      <PageHeader
        title="Planificación de rutas"
        description="Elegí conductores y vehículos, previsualizá la propuesta y persistila como rutas."
      />

      <div className="mb-6 grid grid-cols-1 gap-6 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>Recursos a planificar</CardTitle>
            <Button variant="secondary" size="sm" onClick={addRow}>
              + Agregar conductor
            </Button>
          </CardHeader>
          <CardBody>
            <form onSubmit={preview} className="space-y-5">
              <div className="space-y-4">
                {rows.map((row, index) => {
                  const selectedVehicle = vehicles.find((v) => v.id === row.vehicleId);
                  return (
                  <div
                    key={row.key}
                    className="grid grid-cols-1 gap-4 rounded-md border border-border p-4 sm:grid-cols-2"
                  >
                    <Field label={`Conductor ${index + 1}`} required>
                      <Select
                        value={row.driverId}
                        onChange={(e) => updateRow(row.key, { driverId: e.target.value })}
                        disabled={driversLoading}
                      >
                        <option value="">Seleccioná…</option>
                        {drivers.map((d) => (
                          <option key={d.id} value={d.id}>
                            {driverName(d)}
                          </option>
                        ))}
                      </Select>
                    </Field>
                    <Field label="Vehículo" required>
                      <Select
                        value={row.vehicleId}
                        onChange={(e) => updateRow(row.key, { vehicleId: e.target.value })}
                        disabled={vehiclesLoading}
                      >
                        <option value="">Seleccioná…</option>
                        {vehicles.map((v) => (
                          <option key={v.id} value={v.id}>
                            {vehicleLabel(v)}
                          </option>
                        ))}
                      </Select>
                    </Field>
                    <Field label="Depósito" hint="Automático (más cercano) si se deja vacío.">
                      <Select
                        value={row.depositId}
                        onChange={(e) => updateRow(row.key, { depositId: e.target.value })}
                      >
                        <option value="">Automático</option>
                        {deposits.map((d) => (
                          <option key={d.id} value={d.id}>
                            {d.address}
                          </option>
                        ))}
                      </Select>
                    </Field>
                    <div className="flex items-end justify-between gap-4 sm:col-span-2">
                      <p className="text-xs text-muted">
                        Capacidad máx. del vehículo:{' '}
                        <span className="font-medium text-foreground">
                          {selectedVehicle ? formatKg(selectedVehicle.capacityKg) : '—'}
                        </span>
                      </p>
                      <Button
                        variant="ghost"
                        size="sm"
                        onClick={() => removeRow(row.key)}
                        disabled={rows.length === 1}
                      >
                        Quitar
                      </Button>
                    </div>
                  </div>
                  );
                })}

                <p className="text-xs text-muted">
                  Capacidad total de vehículos seleccionados:{' '}
                  <span className="font-medium text-foreground">
                    {formatKg(totalSelectedCapacityKg)}
                  </span>
                </p>
              </div>

              <div className="flex justify-end">
                <Button type="submit" loading={loading} disabled={!ready}>
                  Previsualizar propuesta
                </Button>
              </div>
            </form>
          </CardBody>
        </Card>

        <Card className="flex flex-col">
          <CardHeader>
            <CardTitle>Envíos pendientes</CardTitle>
            <Badge tone="neutral">{pending.length} envío(s)</Badge>
          </CardHeader>
          {pendingLoading ? (
            <div className="flex items-center gap-2 p-5 text-sm text-muted">
              <Spinner /> Cargando…
            </div>
          ) : pendingError ? (
            <div className="p-5">
              <Alert tone="danger">{pendingError}</Alert>
            </div>
          ) : pending.length === 0 ? (
            <EmptyState
              title="Sin envíos pendientes"
              description="No hay envíos pendientes para planificar."
            />
          ) : (
            <>
              <div className="max-h-96 overflow-auto">
                <Table>
                  <THead>
                    <TR>
                      <TH>Envío</TH>
                      <TH>Cliente</TH>
                      <TH>Prioridad</TH>
                      <TH>Riesgo</TH>
                      <TH className="text-right">Peso</TH>
                    </TR>
                  </THead>
                  <TBody>
                    {pending.map((s) => (
                      <TR key={s.shipmentId}>
                        <TD className="font-mono text-xs">{shortId(s.shipmentId)}</TD>
                        <TD className="text-sm">{s.customerName || '-'}</TD>
                        <TD>
                          <Badge tone={priorityTone(s.priority)}>{priorityLabel(s.priority)}</Badge>
                        </TD>
                        <TD>{s.delayRiskPercentage != null ? `${s.delayRiskPercentage}%` : '-'}</TD>
                        <TD className="text-right text-sm">{formatKg(s.weightKg)}</TD>
                      </TR>
                    ))}
                  </TBody>
                </Table>
              </div>
              <div className="flex items-center justify-between border-t border-border px-5 py-3 text-sm">
                <span className="text-muted">Peso total de pendientes</span>
                <span className="font-semibold text-foreground">
                  {pending.length} envío(s) · {formatKg(totalPendingWeightKg)}
                </span>
              </div>
            </>
          )}
        </Card>
      </div>

      {error && (
        <Alert tone="danger" className="mb-6">
          {error}
        </Alert>
      )}

      {committed && (
        <Card className="mb-6">
          <CardHeader>
            <CardTitle>Rutas creadas</CardTitle>
            <Badge tone="success">{committed.routes.length} ruta(s)</Badge>
          </CardHeader>
          <CardBody className="space-y-2 text-sm">
            {committed.routes.map((r) => (
              <div key={r.routeId} className="flex flex-wrap items-center gap-3">
                <span className="font-mono text-xs">{shortId(r.routeId)}</span>
                <span className="text-muted">
                  {r.routeStopIds.length} parada(s) · conductor {driverLabel(r.driverId)}
                </span>
                <Link to={`/admin/routes/${r.routeId}`} className={linkButtonPrimary}>
                  Ver ruta
                </Link>
              </div>
            ))}
          </CardBody>
        </Card>
      )}

      {proposal && (
        <div className="space-y-6">
          <Card>
            <CardHeader>
              <CardTitle>Resumen de la propuesta</CardTitle>
              <Button onClick={commit} loading={committing}>
                Persistir propuesta
              </Button>
            </CardHeader>
            <CardBody className="grid grid-cols-2 gap-4 text-sm sm:grid-cols-3 lg:grid-cols-5">
              <div>
                <p className="text-muted">Conductores</p>
                <p className="text-lg font-semibold text-foreground">{proposal.driverCount}</p>
              </div>
              <div>
                <p className="text-muted">Vehículos</p>
                <p className="text-lg font-semibold text-foreground">{proposal.vehicleCount}</p>
              </div>
              <div>
                <p className="text-muted">Envíos</p>
                <p className="text-lg font-semibold text-foreground">{proposal.shipmentCount}</p>
              </div>
              <div>
                <p className="text-muted">Distancia total</p>
                <p className="text-lg font-semibold text-foreground">
                  {formatMeters(proposal.totalDistanceMeters)}
                </p>
              </div>
              <div>
                <p className="text-muted">Duración total</p>
                <p className="text-lg font-semibold text-foreground">
                  {proposal.totalDurationMinutes} min
                </p>
              </div>
            </CardBody>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>Rutas propuestas</CardTitle>
              <Badge tone="neutral">{proposal.assignments.length} ruta(s)</Badge>
            </CardHeader>
            {proposal.assignments.length === 0 ? (
              <EmptyState
                title="Sin rutas propuestas"
                description="No se pudo asignar ningún envío con los recursos elegidos."
              />
            ) : (
              <CardBody className="space-y-3">
                {proposal.assignments.map((a, index) => (
                  <RoutePlanBranch
                    key={a.driverId}
                    assignment={a}
                    driverLabel={driverLabel(a.driverId)}
                    vehicleLabel={vehicleLabelById(a.vehicleId)}
                    depositLabel={depositLabel(a.depositId)}
                    shipmentById={shipmentById}
                    defaultOpen={index === 0}
                  />
                ))}
              </CardBody>
            )}
          </Card>

          {(proposal.excludedShipments.length > 0 || proposal.excludedDrivers.length > 0) && (
            <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
              <Card>
                <CardHeader>
                  <CardTitle>Envíos excluidos</CardTitle>
                </CardHeader>
                {proposal.excludedShipments.length === 0 ? (
                  <CardBody className="text-sm text-muted">Ninguno.</CardBody>
                ) : (
                  <Table>
                    <THead>
                      <TR>
                        <TH>Envío</TH>
                        <TH>Motivo</TH>
                      </TR>
                    </THead>
                    <TBody>
                      {proposal.excludedShipments.map((s) => (
                        <TR key={s.shipmentId}>
                          <TD className="font-mono text-xs">{shortId(s.shipmentId)}</TD>
                          <TD>{s.reason}</TD>
                        </TR>
                      ))}
                    </TBody>
                  </Table>
                )}
              </Card>

              <Card>
                <CardHeader>
                  <CardTitle>Conductores excluidos</CardTitle>
                </CardHeader>
                {proposal.excludedDrivers.length === 0 ? (
                  <CardBody className="text-sm text-muted">Ninguno.</CardBody>
                ) : (
                  <Table>
                    <THead>
                      <TR>
                        <TH>Conductor</TH>
                        <TH>Score</TH>
                        <TH>Motivo</TH>
                      </TR>
                    </THead>
                    <TBody>
                      {proposal.excludedDrivers.map((d) => (
                        <TR key={d.driverId}>
                          <TD>{driverLabel(d.driverId)}</TD>
                          <TD>{d.score != null ? d.score.toFixed(2) : '-'}</TD>
                          <TD>{d.detail}</TD>
                        </TR>
                      ))}
                    </TBody>
                  </Table>
                )}
              </Card>
            </div>
          )}
        </div>
      )}

      {loading && (
        <div className="mt-4 flex items-center gap-2 text-sm text-muted">
          <Spinner /> Calculando propuesta…
        </div>
      )}
    </div>
  );
}
