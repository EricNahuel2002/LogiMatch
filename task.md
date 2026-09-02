# LogiMatch — Registro de tareas

## Última tarea completada

- Implementar reglas de negocio de dominio (Order, OrderItem, DeliveryAttempt, Shipment, Vehicle) en 3 fases: dominio rico, infraestructura y tests.

## Última fase completada

- **Fase 1 — Dominio rico (reglas de negocio)**: `ShipmentStatus.DeliveryFailed`, `Vehicle.Active` + `SetActive`, `Order.Create/AddItem/SetAssignedDriver/AssignToShipment`, `OrderItem.Create`, `DeliveryAttempt.Create`, `Shipment.Start/Stop/Cancel/MarkArrivedAtDestination/RegisterDeliveryAttempt`, transiciones con `ShipmentHistory`. Build de Domain OK (0 warnings/errors). Método interno renombrado a `MarkFinalized` (evita colisión con `Object.Finalize`).

- **Fase 2 — Infraestructura**: `DeliveryAttemptConfiguration` (ShipmentId requerida, `Cascade`, inverse nav `s.DeliveryAttempts`) y `VehicleConfiguration` (`Active` con `HasDefaultValue(true)`). Migración `AddVehicleAvailability` (add `Active` y `ArrivedAtDestination`, alter `ShipmentId` no-null + FK cascade). Build OK (0 warnings/errors).

- **Fase 3 — Tests**: `OrderTests`, `VehicleTests`, `ShipmentTests` y `DeliveryAttemptTests` (24 tests) cubren las 9 reglas. Verificación final: build de la solución 0 warnings/errors y 24/24 tests verdes (+1 placeholder de IntegrationTests).

## Fase actual

Ninguna fase actual.

## Decisiones importantes de la tarea

- Dominio rico: las reglas viven en métodos de las entidades con setters privados (patrón EF: ctor `private` + factory estática).
- Máximo de 3 intentos y 5 minutos de separación se cuentan por `Shipment`.
- Nuevo flag `ArrivedAtDestination` en `Shipment` para modelar la llegada al destino.
- Nuevo valor `DeliveryFailed` en `ShipmentStatus` (se persiste como string: sin cambio de schema).
- Violaciones de regla → `InvalidOperationException` con mensaje descriptivo.
- Las transiciones de estado registran entradas en `ShipmentHistory`.

## Fase siguiente

Sin fases siguientes.