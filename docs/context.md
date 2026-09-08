# Contexto de código

El proyecto utiliza Clean Architecture (API → Application → Domain; Infrastructure implementa
las abstracciones). Este documento es un mapa: indica dónde se resuelve cada cosa sin recorrer
todo el repositorio.

## Domain (`src/Domain`) — reglas de negocio

Entidades ricas (factory estática + setters privados) y value objects. Las violaciones de regla
lanzan `InvalidOperationException` (→ 409 en la API).

- **`Entities/User.cs`** — base abstracta heredada de `IdentityUser<Guid>` (integración Identity);
  jerarquía TPH: `Admin`, `Customer`, `Driver`. `Email`/`PasswordHash`/`UserName` vienen de Identity;
  `Name`/`Surname`/`CreatedAt`/`UpdatedAt` son propios.
- **`Entities/Driver.cs`** — chofer; `CurrentLocation: Coordinate?` con `SetCurrentLocation(Coordinate)`.
- **`Entities/Order.cs`** — agregado raíz de órdenes. Invariantes: exige ≥1 item, no se modifica
  tras asignarse a un envío. `AddItem`, `SetAssignedDriver`, `AssignToShipment` (internal).
- **`Entities/Shipment.cs`** — **máquina de estados del envío**: `Start/Stop/Resume/
  MarkArrivedAtDestination/Cancel/RegisterDeliveryAttempt` validan cada transición; máx. 3 intentos
  (≥5 min entre intentos) y la llegada a destino deciden `Finalized`/`DeliveryFailed`. Cada
  transición registra una entrada en `ShipmentHistory`.
- **`Entities/DeliveryAttempt.cs`** — intento de entrega; lo crea `Shipment.RegisterDeliveryAttempt`.
- **`Entities/ShipmentHistory.cs`** — auditoría de transiciones de estado.
- **`Entities/OrderItem.cs`** — línea de pedido (creación valida nombre, cantidad >0, precio ≥0 y
  `WeightKg` >0).
- **`Entities/Route.cs`** — agregado de rutas con origen `Origin: Coordinate`; `AssignDriver`,
  `AssignVehicle`, `AddStop` (prohíbe `StopOrder` duplicados). Nota: `Route.AddStop` no fija `RouteId`
  a la parada; eso lo hace `RouteStop.AssignToRoute` (invocado por `RouteService.AddStopAsync`).
- **`Entities/RouteStop.cs`** — parada ligada a un `Shipment`; `Create(shipment, coordinate, stopOrder,
  name)` fija la `Coordinate` destino y `AssignToRoute` fija la navegación.
- **`Entities/Vehicle.cs`** — vehículo con capacidad y estado activo (`SetActive`).
- **`Services/DriverScoring.cs`** — regla pura (sin I/O) que rankea candidatos: normaliza 6 métricas
  (intentos exitosos del día, envíos pendientes, en progreso, distancia, tiempo de llegada y capacidad
  libre) con pesos y las combina en un score 0..1 para sugerir el mejor conductor.
- **`Enums/ShipmentStatus.cs`** — estados: Pending, InProgress, Stopped, Arrived, Finalized,
  DeliveryFailed, Cancelled.
- **`ValueObjects/Coordinate.cs`** — record `(Latitude, Longitude)`.
- **`Common/BaseEntity.cs` y `Common/IAuditableEntity.cs`** — `Id`, `CreatedAt`, `UpdatedAt`; los
  rellena el interceptor de Infrastructure al guardar.

## Application (`src/Application`) — casos de uso

Programado contra interfaces; no conoce EF ni ASP.NET. Cada servicio orquesta:
validator FluentValidation → repositorio → `IUnitOfWork.SaveChangesAsync`.

- **`Services/*Service.cs`** — orquestadores por agregado:
  - `ShipmentService` — orquesta el ciclo de vida del envío (las *reglas* de transición están en
    `Shipment`, ver Domain). `RegisterDeliveryAttemptAsync` usa `GetByIdWithDetailsAsync`
    (carga Order + DeliveryAttempts).
  - `OrderService` — crear orden (valida cliente y admin), añadir items, asignar driver.
  - `RouteService` — crear ruta, asignar conductor/vehículo, `AddStopAsync`.
  - `RouteStopService` — crear parada por `Address` (la geocodifica con `IGeocodingClient`)
    validando que el envío exista.
  - `DriverService` — `UpdateLocationAsync`: valida la coordenada, carga el driver y fija su
    `CurrentLocation`.
  - `ShipmentAssignmentService` — `SuggestDriverAsync`: envío `Pending` con `RouteStop`, peso de la
    orden vs capacidad libre de cada candidato, distancia y duración por carretera
    (`IRouteClient.GetDrivingMetricsAsync`) y ranking final con `Domain.Services.DriverScoring`.
  - Interfaces `I*Service` junto a cada implementación.
- **`Persistence/`** — contratos (interfaces) de repositorios y `IUnitOfWork`;
  las implementaciones EF están en Infrastructure. `IUserRepository` agrega `GetDriverByIdAsync` y
  `GetDriverCandidatesAsync` (devuelve `DriverAssignmentCandidate`); `IShipmentRepository` ofrece
  `GetByIdWithAssignmentDetailsAsync` (Order→Items + RouteStop) para el scoring.
- **`Integrations/`** — contratos de servicios externos: `IGeocodingClient.GeocodeAsync(address)`
  (dirección → `Coordinate`) e `IRouteClient.GetDrivingMetricsAsync(origins, destination)`
  (distancia en metros y duración en minutos por driver, redondeada hacia arriba).
  Implementaciones ORS en Infrastructure.
- **`Dtos/`** — modelos de request por agregado (`Orders`, `Shipments`, `Routes`, `RouteStops`,
  `Drivers`, `Auth`); payload esperado por cada endpoint. `Shipments.DriverAssignmentSuggestion`
  modela la sugerencia (driver recomendado + ranking); `RouteStops.CreateRouteStopRequest` recibe
  `Address` (se geocodifica) y `Orders.CreateOrderItemRequest` incluye `WeightKg`.
- **`Validators/`** — FluentValidation `XxxRequestValidator` por request; los servicios llaman
  `ValidateAndThrowAsync` (no son decorativos). `Common/NoteValidation.cs` centraliza notas.
- **`Exceptions/NotFoundException.cs`** — entidad no encontrada (→ 404 en la API).
- **`DependencyInjection.cs`** — `AddApplication()` registra servicios + validators (escaneo de ensamblado).

## API (`src/API`) — entrada HTTP, autenticación y errores

- **`Program.cs`** — composición raíz: `AddApplication()` + `AddInfrastructure(conn)`, Identity+JwtBearer,
  policies, exception handler, OpenApi+Scalar (esquema `Bearer` exigido en la doc; se quita en
  endpoints `AllowAnonymous`), health; en Development aplica migraciones, `IdentitySeeder` y
  `DemoDataSeeder`.
- **`Controllers/`** — uno por agregado. Códigos: 201 (creación con `Location`), 204 (mutaciones),
  400/404/409/500 vía handler global:
  - `OrdersController`, `RoutesController`, `RouteStopsController` — política `AdminOnly`.
  - `ShipmentsController` — por acción: crear/cancelar/driver-suggestion `AdminOnly`; start/stop/
    resume/arrived/delivery-attempts `DriverOnly`.
  - `DriversController` — `PATCH /api/drivers/me/location` `DriverOnly`; el id del driver se toma
    del claim del token.
  - `AuthController` — `POST /api/auth/login` (anónimo) → 200 token / 400 validación / 401 credenciales.
- **`Auth/`** — `JwtOptions` (Issuer/Audience/Secret/Expiry), `JwtTokenService` (firma HMAC-SHA256 con
  claims sub, name, email, jti y roles), `LoginResponse`.
- **`Authorization/Policies.cs`** — nombres de policies `AdminOnly`/`DriverOnly`.
- **`Middleware/GlobalExceptionHandler.cs`** — mapa excepción→HTTP: Validation→400, NotFound→404,
  InvalidOperation→409, resto→500 (`application/problem+json`).
- **`Extensions/IdentitySeeder.cs`** — crea roles Admin/Driver/Customer + Admin inicial desde config.
- **Config.** `appsettings.json` define `ConnectionStrings`, `Jwt`, `Seed`, `OpenRouteService`.
  Secretos (`Jwt:Secret`, `Seed:AdminPassword`, `OpenRouteService:ApiKey`) por user-secrets,
  no versionados.

## Infrastructure y tests

- **`Persistence/LogiMatchDbContext.cs`** — hereda de `IdentityDbContext<User, IdentityRole<Guid>, Guid>`
  (tablas negocio + Identity `AspNet*`). `LogiMatchDbContextFactory.cs` para design-time.
- **`Persistence/Configurations/`** — config EF por entidad (tablas, TPH de Users, FKs, max lengths).
- **`Persistence/Interceptors/AuditableEntityInterceptor.cs`** — rellena `CreatedAt`/`UpdatedAt`
  en entidades `IAuditableEntity`.
- **`Persistence/Repositories/`** — implementaciones EF de los contratos de `Application.Persistence`,
  + `UnitOfWork`. Includes clave: Order→Items, Shipment→Order+DeliveryAttempts, Route→RouteStops;
  `UserRepository.GetDriverCandidatesAsync` agrega por driver la capacidad máx. de vehículos activos,
  entregas exitosas del día, pendientes y en-progreso (con su peso); usuarios vía `OfType<T>()` (TPH).
- **`Integrations/`** — implementaciones de los clientes externos: `OpenRouteServiceGeocodingClient`
  (`GET geocode/search`, país desde config) y `OpenRouteServiceMatrixClient`
  (`POST v2/matrix/driving-car`, chunks de ≤69 orígenes, distancias en metros y duraciones en
  minutos —redondeadas hacia arriba— extraídas de `distances` y `durations`).
- **`DependencyInjection.cs`** — `AddInfrastructure(connectionString)`: DbContext, repos, UoW y
  HttpClient tipados de ORS (geocoding + matrix; `BaseUrl` desde config).
- **`Persistence/DemoDataSeeder.cs`** — seed demo idempotente (solo Development): admin, customer y
  5 conductores con ubicación y vehículos; crea la orden objetivo sin driver asignado y otros envíos
  con estados variados (pendientes, en progreso, entregado) para ejercitar el scoring.
- **`Migrations/`** — migraciones EF del esquema: `InitialCreate` y `AddDriverScoring`
  (ubicación de conductores + `WeightKg` en items).
- **Tests** — `src/UnitTests/Domain/` (reglas por entidad, incl. `DriverScoringTests`,
  `OrderItemTests`, `DriverTests`), `src/UnitTests/Application/` (servicios con Moq y validators,
  incl. `ShipmentAssignmentServiceTests`, `DriverServiceTests`, `RouteStopServiceTests`),
  `src/UnitTests/Infrastructure/` (clients ORS con `MockHttpMessageHandler`),
  `src/IntegrationTests/` (aún sin casos).