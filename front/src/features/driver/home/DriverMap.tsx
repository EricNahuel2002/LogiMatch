import { useEffect, useMemo } from 'react';
import L from 'leaflet';
import { MapContainer, Marker, Popup, TileLayer, useMap } from 'react-leaflet';
import 'leaflet/dist/leaflet.css';
import type { DriverRouteStop } from '@/lib/types';
import type { GeoPosition } from './useGeolocation';

export interface DriverMapProps {
  driverPosition: GeoPosition | null;
  stops: DriverRouteStop[];
}

const DEFAULT_CENTER: [number, number] = [-34.6082, -58.3784];

function circleIcon(color: string, border = '#ffffff'): L.DivIcon {
  return L.divIcon({
    className: '',
    html: `<span style="display:block;width:18px;height:18px;border-radius:9999px;background:${color};border:3px solid ${border};box-shadow:0 1px 4px rgba(0,0,0,.4)"></span>`,
    iconSize: [18, 18],
    iconAnchor: [9, 9],
  });
}

function numberedIcon(label: string): L.DivIcon {
  return L.divIcon({
    className: '',
    html: `<span style="display:flex;align-items:center;justify-content:center;width:24px;height:24px;border-radius:9999px;background:#e5e7eb;border:2px solid #9ca3af;color:#374151;font-size:12px;font-weight:600">${label}</span>`,
    iconSize: [24, 24],
    iconAnchor: [12, 12],
  });
}

function destinationIcon(label: string): L.DivIcon {
  return L.divIcon({
    className: '',
    html: `<span style="display:flex;align-items:center;justify-content:center;width:28px;height:28px;border-radius:9999px 9999px 9999px 0;transform:rotate(-45deg);background:#dc2626;border:2px solid #ffffff;box-shadow:0 1px 4px rgba(0,0,0,.4)"><span style="transform:rotate(45deg);color:#fff;font-size:11px;font-weight:700">${label}</span></span>`,
    iconSize: [28, 28],
    iconAnchor: [14, 28],
  });
}

function FitBounds({ points }: { points: [number, number][] }) {
  const map = useMap();

  useEffect(() => {
    if (points.length === 0) return;
    if (points.length === 1) {
      map.setView(points[0], 15, { animate: true });
      return;
    }
    map.fitBounds(L.latLngBounds(points), { padding: [40, 40], maxZoom: 16 });
  }, [map, points]);

  return null;
}

export interface DriverMapExtendedProps extends DriverMapProps {
  className?: string;
}

export function DriverMap({ driverPosition, stops, className }: DriverMapExtendedProps) {
  const currentStop = stops.find((stop) => stop.isCurrent) ?? null;

  const stopPoints = useMemo<[number, number][]>(
    () => stops.map((stop) => [stop.latitude, stop.longitude]),
    [stops],
  );

  const center: [number, number] = stopPoints[0] ?? DEFAULT_CENTER;

  return (
    <div
      className={`absolute inset-0 isolate w-full h-full ${className ?? ''}`}
      onClick={(e) => {
        e.stopPropagation();
      }}
    >
      <MapContainer
        center={center}
        zoom={13}
        scrollWheelZoom
        zoomControl
        style={{ height: '100%', width: '100%' }}
      >
        <TileLayer
          attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
          url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
        />

        {stops.map((stop) => {
          const isCurrent = currentStop?.routeStopId === stop.routeStopId;
          return (
            <Marker
              key={stop.routeStopId}
              position={[stop.latitude, stop.longitude]}
              icon={
                isCurrent
                  ? destinationIcon(String(stop.stopOrder))
                  : numberedIcon(String(stop.stopOrder))
              }
              zIndexOffset={isCurrent ? 500 : 0}
            >
              <Popup>
                <span className="text-sm font-medium">{stop.name ?? 'Parada'}</span>
                {stop.customerName && <span className="block text-xs">{stop.customerName}</span>}
              </Popup>
            </Marker>
          );
        })}

        {driverPosition && (
          <Marker
            position={[driverPosition.latitude, driverPosition.longitude]}
            icon={circleIcon('#16a34a')}
            zIndexOffset={1000}
          >
            <Popup>Tu ubicación</Popup>
          </Marker>
        )}

        <FitBounds points={stopPoints} />
      </MapContainer>
    </div>
  );
}
