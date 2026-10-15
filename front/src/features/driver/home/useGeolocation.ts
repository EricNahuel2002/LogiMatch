import { useEffect, useState } from 'react';

export interface GeoPosition {
  latitude: number;
  longitude: number;
  accuracy?: number;
}

export interface GeolocationState {
  position: GeoPosition | null;
  error: string | null;
}

function isSupported() {
  return typeof navigator !== 'undefined' && 'geolocation' in navigator;
}

export function useGeolocation(enabled: boolean): GeolocationState {
  const supported = isSupported();
  const [position, setPosition] = useState<GeoPosition | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!enabled || !supported) return;

    const watchId = navigator.geolocation.watchPosition(
      (result) => {
        setError(null);
        setPosition({
          latitude: result.coords.latitude,
          longitude: result.coords.longitude,
          accuracy: result.coords.accuracy,
        });
      },
      (geoError) => {
        setError(
          geoError.code === geoError.PERMISSION_DENIED
            ? 'Permiso de ubicación denegado. Tu posición no se actualizará.'
            : 'No se pudo obtener tu ubicación.',
        );
      },
      { enableHighAccuracy: true, maximumAge: 15000, timeout: 20000 },
    );

    return () => navigator.geolocation.clearWatch(watchId);
  }, [enabled, supported]);

  return {
    position: supported ? position : null,
    error: supported ? error : 'Tu dispositivo no soporta geolocalización.',
  };
}
