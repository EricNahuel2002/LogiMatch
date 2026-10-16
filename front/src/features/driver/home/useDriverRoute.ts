import { useCallback, useEffect, useMemo, useState } from 'react';
import { apiFetch, apiFetchNullable } from '@/lib/api';
import type {
  DeliveryFailureReason,
  DriverRouteCurrent,
  DriverRouteStop,
  ShipmentStatus,
} from '@/lib/types';

export type ShipmentAction = 'start' | 'stop' | 'resume' | 'arrived' | 'delivery-attempts';

const actionLabels: Record<ShipmentAction, string> = {
  start: 'Iniciar recorrido',
  stop: 'Pausar',
  resume: 'Reanudar',
  arrived: 'Marcar llegada',
  'delivery-attempts': 'Intentar entrega',
};

const terminalStatuses: ShipmentStatus[] = ['Finalized', 'DeliveryFailed', 'Cancelled'];

function isTerminal(status: ShipmentStatus) {
  return terminalStatuses.includes(status);
}

function actionsForStatus(status: ShipmentStatus): ShipmentAction[] {
  switch (status) {
    case 'Pending':
      return ['start'];
    case 'InProgress':
      return ['stop', 'arrived'];
    case 'Stopped':
      return ['resume'];
    case 'Arrived':
      return ['delivery-attempts'];
    default:
      return [];
  }
}

export interface PendingAction {
  shipmentId: string;
  action: ShipmentAction;
}

export interface UseDriverRouteResult {
  currentRoute: DriverRouteCurrent | null;
  routeLoading: boolean;
  error: string;
  currentStop: DriverRouteStop | null;
  upcomingStops: DriverRouteStop[];
  mapStops: DriverRouteStop[];
  running: string | null;
  pending: PendingAction | null;
  actionNote: string;
  succeeded: boolean;
  failureReason: DeliveryFailureReason;
  cancelOpen: boolean;
  cancelReason: string;
  cancelNote: string;
  cancelling: boolean;
  routeActive: boolean;
  loadRoute: () => Promise<void>;
  handleRouteChanged: () => void;
  executeAction: (shipmentId: string, action: ShipmentAction) => Promise<void>;
  openAction: (shipmentId: string, action: ShipmentAction) => void;
  cancelRoute: () => Promise<void>;
  setActionNote: (value: string) => void;
  setSucceeded: (value: boolean) => void;
  setFailureReason: (value: DeliveryFailureReason) => void;
  setCancelOpen: (value: boolean) => void;
  setCancelReason: (value: string) => void;
  setCancelNote: (value: string) => void;
  setPending: (value: PendingAction | null) => void;
  actionsForStatus: (status: ShipmentStatus) => ShipmentAction[];
  actionLabels: Record<ShipmentAction, string>;
}

export function useDriverRoute(notify: (message: string, tone?: 'success' | 'error') => void): UseDriverRouteResult {
  const [currentRoute, setCurrentRoute] = useState<DriverRouteCurrent | null>(null);
  const [routeLoading, setRouteLoading] = useState(true);
  const [error, setError] = useState('');

  const [running, setRunning] = useState<string | null>(null);

  const [pending, setPending] = useState<PendingAction | null>(null);
  const [actionNote, setActionNote] = useState('');
  const [succeeded, setSucceeded] = useState(true);
  const [failureReason, setFailureReason] = useState<DeliveryFailureReason>('CustomerAbsent');

  const [cancelOpen, setCancelOpen] = useState(false);
  const [cancelReason, setCancelReason] = useState('VehicleBreakdown');
  const [cancelNote, setCancelNote] = useState('');
  const [cancelling, setCancelling] = useState(false);

  const loadRoute = useCallback(async () => {
    const route = await apiFetchNullable<DriverRouteCurrent>('/api/drivers/me/routes/current');
    setCurrentRoute(route);
  }, []);

  useEffect(() => {
    (async () => {
      try {
        await loadRoute();
      } catch (e: any) {
        setError(e.message);
      } finally {
        setRouteLoading(false);
      }
    })();
  }, [loadRoute]);

  const handleRouteChanged = useCallback(() => {
    loadRoute().catch(() => {
      // best-effort
    });
  }, [loadRoute]);

  const routeActive = Boolean(currentRoute?.isActive);

  const currentStop = useMemo(
    () => currentRoute?.stops.find((stop) => stop.isCurrent) ?? null,
    [currentRoute],
  );

  const upcomingStops = useMemo(() => {
    if (!currentRoute) return [];
    return currentRoute.stops
      .filter((stop) => !stop.isCurrent && !isTerminal(stop.status))
      .slice()
      .sort((a, b) => a.stopOrder - b.stopOrder);
  }, [currentRoute]);

  const mapStops = useMemo(
    () => (currentStop ? [currentStop, ...upcomingStops] : upcomingStops),
    [currentStop, upcomingStops],
  );

  const executeAction = useCallback(
    async (shipmentId: string, action: ShipmentAction) => {
      const key = `${shipmentId}:${action}`;
      setRunning(key);
      try {
        let body: Record<string, unknown> | undefined;
        if (action === 'stop' || action === 'resume' || action === 'arrived') {
          body = { note: actionNote || null };
        } else if (action === 'delivery-attempts') {
          body = {
            succeeded,
            failureReason: succeeded ? null : failureReason,
            note: actionNote || null,
          };
        }

        await apiFetch(`/api/shipments/${shipmentId}/${action}`, {
          method: 'POST',
          body: body ? JSON.stringify(body) : undefined,
        });

        notify(`Acción "${actionLabels[action]}" realizada`, 'success');
        setPending(null);
        await loadRoute();
      } catch (e: any) {
        notify(e.message, 'error');
      } finally {
        setRunning(null);
      }
    },
    [actionNote, succeeded, failureReason, notify, loadRoute],
  );

  const openAction = useCallback((shipmentId: string, action: ShipmentAction) => {
    if (action === 'start') {
      void executeAction(shipmentId, 'start');
      return;
    }
    setPending({ shipmentId, action });
    setActionNote('');
    setSucceeded(true);
    setFailureReason('CustomerAbsent');
  }, [executeAction]);

  const cancelRoute = useCallback(async () => {
    setCancelling(true);
    try {
      await apiFetch('/api/drivers/me/routes/cancel', {
        method: 'POST',
        body: JSON.stringify({ reason: cancelReason, note: cancelNote || null }),
      });
      notify('Ruta cancelada', 'success');
      setCancelOpen(false);
      setCancelNote('');
      await loadRoute();
    } catch (e: any) {
      notify(e.message, 'error');
    } finally {
      setCancelling(false);
    }
  }, [cancelReason, cancelNote, notify, loadRoute]);

  return {
    currentRoute,
    routeLoading,
    error,
    currentStop,
    upcomingStops,
    mapStops,
    running,
    pending,
    actionNote,
    succeeded,
    failureReason,
    cancelOpen,
    cancelReason,
    cancelNote,
    cancelling,
    routeActive,
    loadRoute,
    handleRouteChanged,
    executeAction,
    openAction,
    cancelRoute,
    setActionNote,
    setSucceeded,
    setFailureReason,
    setCancelOpen,
    setCancelReason,
    setCancelNote,
    setPending,
    actionsForStatus,
    actionLabels,
  };
}
