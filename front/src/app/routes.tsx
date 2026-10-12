import { Routes, Route, Navigate, Outlet } from 'react-router-dom';
import LoginPage from '@/features/auth/LoginPage';
import AdminDashboard from '@/features/admin/dashboard/AdminDashboard';
import OrdersPage from '@/features/admin/orders/OrdersPage';
import ShipmentsPage from '@/features/admin/shipments/ShipmentsPage';
import RoutesPage from '@/features/admin/routes/RoutesPage';
import RoutePlanningPage from '@/features/admin/route-planning/RoutePlanningPage';
import ShipmentAssignPage from '@/features/admin/shipments/ShipmentAssignPage';
import DriverHome from '@/features/driver/home/DriverHome';
import { AppShell } from '@/components/layout/AppShell';
import { useAuthStore } from '@/hooks/useAuth';

function RequireAuth({ roles }: { roles?: string[] }) {
  const { token, user } = useAuthStore();
  if (!token) return <Navigate to="/login" replace />;
  if (roles && roles.length > 0) {
    const has = user?.roles?.some((r) => roles.includes(r));
    if (!has) return <Navigate to="/" replace />;
  }
  return <Outlet />;
}

export default function AppRoutes() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />

      <Route element={<RequireAuth roles={['Admin']} />}>
        <Route element={<AppShell />}>
          <Route path="/admin/dashboard" element={<AdminDashboard />} />
          <Route path="/admin/orders" element={<OrdersPage />} />
          <Route path="/admin/shipments" element={<ShipmentsPage />} />
          <Route path="/admin/shipments/:id/assign" element={<ShipmentAssignPage />} />
          <Route path="/admin/routes" element={<RoutesPage />} />
          <Route path="/admin/route-planning" element={<RoutePlanningPage />} />
        </Route>
      </Route>

      <Route element={<RequireAuth roles={['Driver']} />}>
        <Route element={<AppShell />}>
          <Route path="/driver" element={<DriverHome />} />
        </Route>
      </Route>

      <Route path="/" element={<Navigate to="/login" replace />} />
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
