import AppRoutes from '@/app/routes';
import { ToastProvider } from '@/components/ui/ToastProvider';

export default function App() {
  return (
    <ToastProvider>
      <AppRoutes />
    </ToastProvider>
  );
}
