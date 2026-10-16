import type { DeliveryFailureReason } from '@/lib/types';
import { deliveryFailureReasonOptions } from '@/lib/shipment';
import { routeCancellationReasonOptions } from '@/lib/route';
import { Field } from '@/components/ui/Field';
import { Input } from '@/components/ui/Input';
import { Select } from '@/components/ui/Select';
import { Checkbox } from '@/components/ui/Checkbox';
import { Button } from '@/components/ui/Button';
import { Modal } from '@/components/ui/Modal';
import { useToast } from '@/components/ui/useToast';
import { DriverMap } from './DriverMap';
import type { DriverMapFocus, DriverMapFocusTarget } from './DriverMap';
import { useGeolocation } from './useGeolocation';
import { useDriverRealtime } from './useDriverRealtime';
import { useDriverRoute } from './useDriverRoute';
import { DriverTopBar } from './DriverTopBar';
import { DriverActionBar } from './DriverActionBar';
import { DriverSheet } from './DriverSheet';
import { useState } from 'react';

const DRIVER_SHEET_SNAP_POINTS: Array<number | string> = ['44px', 1];

export default function DriverHome() {
  const { notify } = useToast();

  const route = useDriverRoute(notify);

  const { position, error: geoError } = useGeolocation(route.routeActive);
  const { connected } = useDriverRealtime(route.handleRouteChanged, route.routeActive ? position : null);

  const userName = ''; // No user name in store payload used; could derive if needed, but keep minimal

  const runningAction = (action: any) =>
    route.running === `${route.currentStop?.shipmentId}:${action}`;

  const mapStops = route.mapStops;
  const [sheetSnap, setSheetSnap] = useState<number | string>(
    DRIVER_SHEET_SNAP_POINTS[DRIVER_SHEET_SNAP_POINTS.length - 1],
  );
  const [sheetContainer, setSheetContainer] = useState<HTMLDivElement | null>(null);
  const [mapFocus, setMapFocus] = useState<DriverMapFocus | null>(null);

  const focusMap = (target: DriverMapFocusTarget) =>
    setMapFocus((prev) => ({ target, nonce: (prev?.nonce ?? 0) + 1 }));

  return (
    <div className="fixed inset-0 overflow-hidden bg-surface-muted">
      <DriverTopBar connected={connected} userName={userName} error={route.error || geoError || undefined} />

      <div
        className="absolute inset-0"
        onClick={() => setSheetSnap(DRIVER_SHEET_SNAP_POINTS[0])}
      >
        {mapStops.length > 0 ? (
          <DriverMap driverPosition={position} stops={mapStops} focus={mapFocus} />
        ) : (
          <div className="absolute inset-0 flex items-center justify-center px-4 text-center text-sm text-muted">
            {route.routeLoading
              ? 'Cargando ruta...'
              : !route.currentRoute
              ? 'Sin ruta activa'
              : 'Ruta sin paradas'}
          </div>
        )}
      </div>

      <div
        ref={setSheetContainer}
        className="pointer-events-none absolute inset-x-0 bottom-0 top-[168px] z-[1000]"
      />

      {sheetContainer && (
        <DriverSheet
          container={sheetContainer}
          snapPoints={DRIVER_SHEET_SNAP_POINTS}
          activeSnap={sheetSnap}
          onSnapChange={setSheetSnap}
          currentRouteActive={route.routeActive}
          hasStops={mapStops.length > 0}
          currentStop={route.currentStop}
          upcomingStops={route.upcomingStops}
          error={route.error || geoError || undefined}
          onFocus={focusMap}
          footer={
            <DriverActionBar
              currentStopActive={Boolean(route.currentStop)}
              actions={route.currentStop ? route.actionsForStatus(route.currentStop.status) : []}
              actionLabels={route.actionLabels}
              running={(action) => runningAction(action)}
              onAction={(action) => {
                if (!route.currentStop) return;
                route.openAction(route.currentStop.shipmentId, action);
              }}
              onCancelRoute={() => route.setCancelOpen(true)}
              routeActive={route.routeActive}
              hasCurrentRoute={Boolean(route.currentRoute)}
            />
          }
        />
      )}

      <Modal
        open={route.pending !== null}
        title={route.pending ? route.actionLabels[route.pending.action] : ''}
        onClose={() => route.setPending(null)}
        footer={
          <>
            <Button variant="ghost" onClick={() => route.setPending(null)}>
              Cancelar
            </Button>
            <Button
              onClick={() =>
                route.pending &&
                route.executeAction(route.pending.shipmentId, route.pending.action)
              }
              loading={route.running !== null}
            >
              Ejecutar
            </Button>
          </>
        }
      >
        <div className="space-y-4">
          {route.pending?.action === 'delivery-attempts' && (
            <>
              <label className="flex items-center gap-2 text-sm text-foreground">
                <Checkbox
                  checked={route.succeeded}
                  onChange={(e) => route.setSucceeded(e.target.checked)}
                />
                Entrega exitosa
              </label>
              {!route.succeeded && (
                <Field label="Motivo de falla" htmlFor="failureReason" required>
                  <Select
                    id="failureReason"
                    value={route.failureReason}
                    onChange={(e) => route.setFailureReason(e.target.value as DeliveryFailureReason)}
                  >
                    {deliveryFailureReasonOptions.map((o) => (
                      <option key={o.value} value={o.value}>
                        {o.label}
                      </option>
                    ))}
                  </Select>
                </Field>
              )}
            </>
          )}
          <Field label="Nota" htmlFor="actionNote" hint="Opcional.">
            <Input
              id="actionNote"
              value={route.actionNote}
              onChange={(e) => route.setActionNote(e.target.value)}
              placeholder="Detalle de la acción"
            />
          </Field>
        </div>
      </Modal>

      <Modal
        open={route.cancelOpen}
        title="Cancelar ruta"
        onClose={() => route.setCancelOpen(false)}
        footer={
          <>
            <Button variant="ghost" onClick={() => route.setCancelOpen(false)}>
              Volver
            </Button>
            <Button variant="danger" onClick={route.cancelRoute} loading={route.cancelling}>
              Confirmar cancelación
            </Button>
          </>
        }
      >
        <div className="space-y-4">
          <Field label="Motivo" htmlFor="cancelReason" required>
            <Select
              id="cancelReason"
              value={route.cancelReason}
              onChange={(e) => route.setCancelReason(e.target.value)}
            >
              {routeCancellationReasonOptions.map((option) => (
                <option key={option.value} value={option.value}>
                  {option.label}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="Nota" htmlFor="cancelNote" hint="Opcional.">
            <Input
              id="cancelNote"
              value={route.cancelNote}
              onChange={(e) => route.setCancelNote(e.target.value)}
              placeholder="Detalle de la cancelación"
            />
          </Field>
        </div>
      </Modal>
    </div>
  );
}
