import { useEffect, useState } from 'react';
import { apiFetch } from '@/lib/api';
import type {
  CustomerSummary,
  DepositSummary,
  DriverSummary,
  ShipmentDelayRisk,
  VehicleSummary,
} from '@/lib/types';

interface CatalogResult<T> {
  data: T[];
  loading: boolean;
  error: string;
  reload: () => void;
}

function useCatalogList<T>(path: string): CatalogResult<T> {
  const [data, setData] = useState<T[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [reloadToken, setReloadToken] = useState(0);

  useEffect(() => {
    let active = true;
    (async () => {
      try {
        const res = await apiFetch<T[]>(path);
        if (active) {
          setData(res);
          setError('');
        }
      } catch (e) {
        if (active) setError(e instanceof Error ? e.message : 'No se pudo cargar el catálogo');
      } finally {
        if (active) setLoading(false);
      }
    })();
    return () => {
      active = false;
    };
  }, [path, reloadToken]);

  const reload = () => {
    setLoading(true);
    setReloadToken((t) => t + 1);
  };

  return { data, loading, error, reload };
}

export function useCustomers() {
  return useCatalogList<CustomerSummary>('/api/customers');
}

export function useDrivers() {
  return useCatalogList<DriverSummary>('/api/drivers');
}

export function useVehicles() {
  return useCatalogList<VehicleSummary>('/api/vehicles');
}

export function useDeposits() {
  return useCatalogList<DepositSummary>('/api/deposits');
}

export function usePendingShipments() {
  return useCatalogList<ShipmentDelayRisk>('/api/shipments/pending');
}

export function driverName(driver: DriverSummary) {
  return `${driver.name} ${driver.surname}`.trim();
}

export function vehicleLabel(vehicle: VehicleSummary) {
  const detail = [vehicle.brand, vehicle.model].filter(Boolean).join(' ');
  return detail ? `${vehicle.licensePlate} — ${detail}` : vehicle.licensePlate;
}
