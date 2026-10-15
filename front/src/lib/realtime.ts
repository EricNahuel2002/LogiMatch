import { HubConnectionBuilder, LogLevel, type HubConnection } from '@microsoft/signalr';
import { API_BASE_URL } from '@/config/env';

export const driverHubEvents = {
  routeAssigned: 'RouteAssigned',
  routeCancelled: 'RouteCancelled',
} as const;

export const driverHubMethods = {
  updateLocation: 'UpdateLocation',
} as const;

export function createDriverHubConnection(): HubConnection {
  return new HubConnectionBuilder()
    .withUrl(`${API_BASE_URL}/hubs/drivers`, {
      accessTokenFactory: () => localStorage.getItem('token') ?? '',
    })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build();
}
