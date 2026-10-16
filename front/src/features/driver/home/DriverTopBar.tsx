import { useAuthStore } from '@/hooks/useAuth';
import { useNavigate } from 'react-router-dom';
import { Badge } from '@/components/ui/Badge';
import { Button } from '@/components/ui/Button';


export interface DriverTopBarProps {
  connected: boolean;
  userName: string;
  error?: string | null;
}

export function DriverTopBar({ connected, userName, error }: DriverTopBarProps) {
  const { logout } = useAuthStore();
  const navigate = useNavigate();

  function handleLogout() {
    logout();
    navigate('/login', { replace: true });
  }

  return (
    <header className="pointer-events-none fixed inset-x-0 top-0 z-[1005] flex flex-col">
      <div className="pointer-events-auto mx-3 mt-3 flex flex-wrap items-center gap-2 rounded-lg border border-border/50 bg-surface/95 px-3 py-2 shadow-card backdrop-blur supports-[backdrop-filter]:bg-surface/90">
        <h1 className="flex-1 min-w-0 truncate text-sm font-semibold text-foreground">
          Mi ruta
        </h1>
        <Badge tone={connected ? 'success' : 'neutral'} className="shrink-0">
          {connected ? 'En vivo' : 'Sin conexión'}
        </Badge>
        {userName && (
          <span className="hidden truncate text-xs text-muted sm:block">{userName}</span>
        )}
        <Button variant="ghost" size="sm" onClick={handleLogout}>
          Cerrar sesión
        </Button>
      </div>
      {error && (
        <div className="pointer-events-auto mx-3 mt-2 rounded-md border border-danger/30 bg-danger-soft px-3 py-2 text-xs text-danger-strong shadow-card">
          {error}
        </div>
      )}
    </header>
  );
}
