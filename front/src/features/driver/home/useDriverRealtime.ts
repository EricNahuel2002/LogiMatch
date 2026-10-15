import { useEffect, useRef, useState } from 'react';
import { HubConnectionState } from '@microsoft/signalr';
import { createDriverHubConnection, driverHubEvents, driverHubMethods } from '@/lib/realtime';
import type { GeoPosition } from './useGeolocation';

const SEND_MIN_INTERVAL_MS = 30_000;
const SEND_MIN_DISTANCE_M = 25;
const EARTH_RADIUS_M = 6_371_000;

function distanceMeters(from: GeoPosition, to: GeoPosition): number {
  const dLat = ((to.latitude - from.latitude) * Math.PI) / 180;
  const dLon = ((to.longitude - from.longitude) * Math.PI) / 180;
  const lat1 = (from.latitude * Math.PI) / 180;
  const lat2 = (to.latitude * Math.PI) / 180;
  const h =
    Math.sin(dLat / 2) ** 2 + Math.cos(lat1) * Math.cos(lat2) * Math.sin(dLon / 2) ** 2;
  return 2 * EARTH_RADIUS_M * Math.asin(Math.sqrt(h));
}

interface UseDriverRealtimeResult {
  connected: boolean;
}

/**
 * Keeps a SignalR connection to the driver hub alive: reloads the route when the backend assigns
 * or cancels one, and pushes the device location while a route is active (throttled to one update
 * every 30 seconds or 25 meters).
 */
export function useDriverRealtime(
  onRouteChanged: () => void,
  position: GeoPosition | null,
): UseDriverRealtimeResult {
  const connectionRef = useRef<ReturnType<typeof createDriverHubConnection> | null>(null);
  const handlerRef = useRef(onRouteChanged);
  const lastSentRef = useRef<{ position: GeoPosition; at: number } | null>(null);
  const [connected, setConnected] = useState(false);

  useEffect(() => {
    handlerRef.current = onRouteChanged;
  }, [onRouteChanged]);

  useEffect(() => {
    const connection = createDriverHubConnection();
    connectionRef.current = connection;

    const handleRouteChanged = () => handlerRef.current();

    connection.on(driverHubEvents.routeAssigned, handleRouteChanged);
    connection.on(driverHubEvents.routeCancelled, handleRouteChanged);
    connection.onreconnecting(() => setConnected(false));
    connection.onreconnected(() => setConnected(true));
    connection.onclose(() => setConnected(false));

    connection
      .start()
      .then(() => setConnected(true))
      .catch(() => setConnected(false));

    return () => {
      connection.off(driverHubEvents.routeAssigned, handleRouteChanged);
      connection.off(driverHubEvents.routeCancelled, handleRouteChanged);
      connectionRef.current = null;
      void connection.stop();
    };
  }, []);

  useEffect(() => {
    const connection = connectionRef.current;
    if (!connected || !connection || !position) return;
    if (connection.state !== HubConnectionState.Connected) return;

    const last = lastSentRef.current;
    const now = Date.now();

    if (last) {
      const moved = distanceMeters(last.position, position) >= SEND_MIN_DISTANCE_M;
      const elapsed = now - last.at >= SEND_MIN_INTERVAL_MS;
      if (!moved && !elapsed) return;
    }

    lastSentRef.current = { position, at: now };
    connection
      .invoke(driverHubMethods.updateLocation, position.latitude, position.longitude)
      .catch(() => {
        // Best-effort: the next tick (30s / 25m) retries.
      });
  }, [connected, position]);

  return { connected };
}
