import { Button } from '@/components/ui/Button';
import type { ShipmentAction } from './useDriverRoute';

export interface DriverActionBarProps {
  currentStopActive: boolean;
  actions: ShipmentAction[];
  actionLabels: Record<ShipmentAction, string>;
  running: (action: ShipmentAction) => boolean;
  onAction: (action: ShipmentAction) => void;
  onCancelRoute: () => void;
  routeActive: boolean;
  hasCurrentRoute: boolean;
}

export function DriverActionBar({
  currentStopActive,
  actions,
  actionLabels,
  running,
  onAction,
  onCancelRoute,
  routeActive,
  hasCurrentRoute,
}: DriverActionBarProps) {
  if (!hasCurrentRoute) return null;

  return (
    <div className="flex shrink-0 flex-col gap-2 border-t border-border bg-surface px-3 pb-[calc(env(safe-area-inset-bottom,0px)+0.75rem)] pt-3">
      {routeActive && (
        <div className="flex flex-wrap gap-2">
          {currentStopActive ? (
            <>
              {actions.map((action) => (
                <Button
                  key={action}
                  variant={action === 'delivery-attempts' ? 'primary' : 'secondary'}
                  size="sm"
                  fullWidth
                  className="flex-1 min-w-[140px] sm:flex-none sm:w-auto"
                  onClick={() => onAction(action)}
                  loading={running(action)}
                >
                  {actionLabels[action]}
                </Button>
              ))}
            </>
          ) : (
            <p className="text-xs text-muted">Sin pedido pendiente para acciones inmediatas.</p>
          )}
        </div>
      )}
      <Button
        variant="danger"
        size="sm"
        fullWidth
        onClick={onCancelRoute}
        className="sm:self-end sm:w-auto"
      >
        Cancelar ruta
      </Button>
    </div>
  );
}
