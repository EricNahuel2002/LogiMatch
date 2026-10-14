export type ShipmentStatus =
  | 'Pending'
  | 'InProgress'
  | 'Stopped'
  | 'Arrived'
  | 'Finalized'
  | 'DeliveryFailed'
  | 'Cancelled';

export type ShipmentPriority = 'Low' | 'Normal' | 'High' | 'Urgent';

export type RouteCancellationReason =
  | 'VehicleBreakdown'
  | 'RouteAbandonment'
  | 'Emergency'
  | 'InclementWeather'
  | 'Accident';

export type DeliveryFailureReason = 'CustomerAbsent' | 'Refused' | 'BusinessClosed' | 'Other';

export type DriverIneligibilityReason =
  | 'InsufficientVehicleCapacity'
  | 'ScoreBelowMinimum'
  | 'ScoreBelowTolerance';

export interface CustomerSummary {
  id: string;
  name: string;
  surname: string;
  email?: string | null;
  phoneNumber?: string | null;
  createdAt: string;
  updatedAt?: string | null;
}

export interface DriverSummary {
  id: string;
  name: string;
  surname: string;
  email?: string | null;
  phoneNumber?: string | null;
}

export interface VehicleSummary {
  id: string;
  licensePlate: string;
  brand?: string | null;
  model?: string | null;
  capacityKg: number;
  active: boolean;
  createdAt: string;
  updatedAt?: string | null;
}

export interface DepositSummary {
  id: string;
  address: string;
  latitude: number;
  longitude: number;
  active: boolean;
  createdAt: string;
  updatedAt?: string | null;
}

export interface OrderItem {
  orderItemId: string;
  name: string;
  price: number;
  quantity: number;
  weightKg: number;
}

export interface OrderSummary {
  id: string;
  customerId: string;
  assignedDriverId?: string | null;
  deliveryWindowStartAt?: string | null;
  deliveryWindowEndAt?: string | null;
  createdAt: string;
  updatedAt?: string | null;
}

export interface OrderDetails {
  id: string;
  customerId: string;
  assignedDriverId?: string | null;
  deliveryWindowStartAt?: string | null;
  deliveryWindowEndAt?: string | null;
  items: OrderItem[];
  createdAt: string;
  updatedAt?: string | null;
}

export interface ShipmentSummary {
  id: string;
  status: ShipmentStatus;
  priority: ShipmentPriority;
  delayRiskPercentage?: number | null;
  orderId: string;
  assignedDriverId?: string | null;
  routeStopId?: string | null;
  routeId?: string | null;
  createdAt: string;
  updatedAt?: string | null;
}

export interface ShipmentDelayRiskDriver {
  driverId: string;
  name: string;
  surname: string;
}

export interface ShipmentDelayRisk {
  shipmentId: string;
  status: ShipmentStatus;
  priority: ShipmentPriority;
  delayRiskPercentage?: number | null;
  driver?: ShipmentDelayRiskDriver | null;
}

export interface ShipmentOrderItem {
  orderItemId: string;
  name: string;
  quantity: number;
  weightKg: number;
}

export interface ShipmentRouteStop {
  routeStopId?: string | null;
  stopOrder?: number | null;
  routeId?: string | null;
  name?: string | null;
  latitude?: number | null;
  longitude?: number | null;
  distanceMeters: number;
  tollCost: number;
}

export interface ShipmentDriver {
  driverId?: string | null;
  name?: string | null;
  surname?: string | null;
}

export interface ShipmentDetails {
  id: string;
  status: ShipmentStatus;
  priority: ShipmentPriority;
  delayRiskPercentage?: number | null;
  arrivedAtDestination: boolean;
  orderId: string;
  items: ShipmentOrderItem[];
  routeStop?: ShipmentRouteStop | null;
  assignedDriver?: ShipmentDriver | null;
  createdAt: string;
  updatedAt?: string | null;
}

export interface ShipmentHistory {
  id: string;
  shipmentId: string;
  status: ShipmentStatus;
  routeStopId?: string | null;
  note?: string | null;
  recordedAt: string;
}

export interface DeliveryAttempt {
  id: string;
  shipmentId: string;
  orderId: string;
  routeStopId?: string | null;
  attemptNumber: number;
  attemptedAt: string;
  succeeded: boolean;
  failureReason?: DeliveryFailureReason | null;
  note?: string | null;
}

export interface RouteStopSummary {
  id: string;
  routeId: string;
  shipmentId: string;
  name?: string | null;
  stopOrder: number;
  latitude: number;
  longitude: number;
  distanceMeters: number;
  tollCost: number;
}

export interface RouteSummary {
  id: string;
  isActive: boolean;
  cancellationReason?: RouteCancellationReason | null;
  vehicleId?: string | null;
  driverId?: string | null;
  createdAt: string;
  updatedAt?: string | null;
}

export interface RouteDetails {
  id: string;
  isActive: boolean;
  cancellationReason?: RouteCancellationReason | null;
  vehicleId?: string | null;
  driverId?: string | null;
  origin: { latitude: number; longitude: number };
  stops: RouteStopSummary[];
  createdAt: string;
  updatedAt?: string | null;
}

export interface DriverRouteCurrent {
  routeId: string;
  isActive: boolean;
  vehicleId?: string | null;
  createdAt: string;
}

export interface DriverShipment {
  shipmentId: string;
  status: ShipmentStatus;
  priority: ShipmentPriority;
  orderId: string;
  createdAt: string;
}

export interface DriverRankingItem {
  driverId: string;
  score: number;
  successAttemptsToday: number;
  pendingShipmentCount: number;
  inProgressShipmentCount: number;
  distanceMeters: number;
  durationMinutes: number;
  freeCapacityKg: number;
  estimatedOperationCost: number;
}

export interface DriverAssignmentSuggestion {
  shipmentId: string;
  recommendedDriverId: string;
  ranking: DriverRankingItem[];
  priority: ShipmentPriority;
}

export interface PlannedStop {
  shipmentId: string;
  stopOrder: number;
}

export interface RouteAssignment {
  driverId: string;
  vehicleId: string;
  depositId: string;
  loadKg: number;
  stops: PlannedStop[];
}

export interface ExcludedShipment {
  shipmentId: string;
  reason: string;
}

export interface ExcludedDriver {
  driverId: string;
  score?: number | null;
  reason: DriverIneligibilityReason;
  detail: string;
}

export interface RoutePlanningProposal {
  assignments: RouteAssignment[];
  driverCount: number;
  vehicleCount: number;
  shipmentCount: number;
  totalDistanceMeters: number;
  totalDurationMinutes: number;
  excludedShipments: ExcludedShipment[];
  excludedDrivers: ExcludedDriver[];
}

export interface CommittedRoute {
  routeId: string;
  driverId: string;
  routeStopIds: string[];
}

export interface CommitFromProposalResponse {
  routes: CommittedRoute[];
}
