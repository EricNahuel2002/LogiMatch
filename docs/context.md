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
- **`Entities/Driver.cs`** — chofer; `CurrentLocation: Coordinate?` con `SetCurrentLocation(Coordinate)`
  y `SalaryPerHour` público (se usa en la calculadora de costos de operación).
- **`Entities/Order.cs`** — agregado raíz de órdenes. Invariantes: exige ≥1 item, no se modifica
  tras asignarse a un envío. Ventana horaria de entrega opcional (`DeliveryWindowStartAt`/`EndAt`,
  hora local Argentina); si viene un extremo falta el otro o `end <= start` → excepción.
  `AddItem`, `SetAssignedDriver`, `AssignToShipment` (internal).
- **`Entities/Shipment.cs`** — **máquina de estados del envío**: `Start/Stop/Resume/
  MarkArrivedAtDestination/Cancel/RegisterDeliveryAttempt` validan cada transición; máx. 3 intentos
  (≥5 min entre intentos) y la llegada a destino deciden `Finalized`/`DeliveryFailed`. Cada
  transición registra una entrada en `ShipmentHistory`. `Requeue(reason, note)` vuelve el envío a
  `Pending` y libera el driver (lo usa la cancelación de ruta).
- **`Entities/DeliveryAttempt.cs`** — intento de entrega; lo crea `Shipment.RegisterDeliveryAttempt`.
- **`Entities/ShipmentHistory.cs`** — auditoría de transiciones de estado.
- **`Entities/OrderItem.cs`** — línea de pedido (creación valida nombre, cantidad >0, precio ≥0 y
  `WeightKg` >0).
- **`Entities/Route.cs`** — agregado de rutas con origen `Origin: Coordinate`; `AssignDriver`,
  `AssignVehicle`, `AddStop` (prohíbe `StopOrder` ≤0 o duplicados) y `Deactivate(reason)`
  (marca inactiva y guarda `CancellationReason`; falla si ya lo está). Nota: `Route.AddStop` no fija
  `RouteId` a la parada; eso lo hace `RouteStop.AssignToRoute` (invocado por `RouteService.AddStopAsync`).
- **`Entities/RouteStop.cs`** — parada ligada a un `Shipment` con `RouteId` nullable (puede existir
  sin ruta asignada); `Create(shipment, coordinate, stopOrder, name, distanceMeters, tollCost)` fija
  la `Coordinate` destino y los costos de operación (`DistanceMeters`, `TollCost`, ambos ≥0),
  `AssignToRoute` fija la navegación. La distancia/peaje alimentan la calculadora de costos.
- **`Entities/Vehicle.cs`** — vehículo con capacidad, estado activo (`SetActive`) y costos de
  operación (`FuelConsumption` en L/km, `FuelPrice` en $/L y `MaintenanceCost`, configurados en el
  factory/`SetOperatingCosts`, todos ≥0). Lleva `KilometersPerDay`, la navegación `Routes` y
  `RecalculateKilometersPerDay()`, que suma los `DistanceMeters` de los envíos llegados
  (`ArrivedAtDestination`), convierte a km y los asigna.
- **`Services/RouteOperationCostCalculator.cs`** — regla pura: costo estimado de operación del día =
  `SalaryPerHour × entregas exitosas + km × consumo × precio + mantenimiento + peaje`; valida inputs
  ≥0 vía helper `EnsureNonNegative`.
- **`Services/DriverScoring.cs`** — regla pura (sin I/O) que rankea candidatos: normaliza **7 métricas**
  (intentos exitosos del día, envíos pendientes, en progreso, distancia, tiempo de llegada, capacidad
  libre y **costo estimado de operación**) con pesos y las combina en un score 0..1 para sugerir el
  mejor conductor; `OperationCostWeight` vale lo mismo que `DistanceWeight` (5m) y el primer
  desempate es por costo (menor gana). Además provee el filtro previo de **factibilidad**
  `IsFeasible`/`FilterFeasible` (llegada `now + duración` dentro de la ventana de entrega, inclusiva),
  independiente del scoring.
- **`Enums/ShipmentStatus.cs`** — estados: Pending, InProgress, Stopped, Arrived, Finalized,
  DeliveryFailed, Cancelled.
- **`Enums/ShipmentPriority.cs`** — Low, Normal, High, Urgent. `Shipment.SetPriority` la fija y
  `Shipment.SetUrgent()` fuerza `Urgent`. El planificador la convierte en factor multiplicativo
  del costo de arco (menor factor = más prioritario), no en un peso de solución.
- **`Entities/Deposit.cs`** — punto de retorno de las rutas: `Create(address, coordinate)` valida
  `Address` no vacío (máx 200) y latitud/longitud en rango. `Active` (default `true`, índice) y
  `SetActive(bool)` marcan si el depósito admite rutas; la planificación solo elige depósitos
  activos. Entidad de solo lectura para la planificación; no tiene rutas ni paradas propias, las
  rutas terminan en su coordenada.
- **`Enums/RouteCancellationReason.cs`** — motivos de cancelación de ruta: VehicleBreakdown,
  RouteAbandonment, Emergency, InclementWeather, Accident.
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
    validando que el envío exista; registra `DistanceMeters` y `TollCost` del request.
  - `DriverService` — `UpdateLocationAsync`: valida la coordenada, carga el driver y fija su
    `CurrentLocation`. `CancelCurrentRouteAsync`: el driver cancela su ruta activa — hace `Requeue`
    de los envíos no terminales (a `Pending` y sin driver), elimina sus `RouteStop` y desactiva la
    ruta (`Route.Deactivate`); si quedan envíos por reasignar, notifica por email a los admins
    (`IEmailSender` + `IUserRepository.GetAdminsAsync`).
  - `ShipmentAssignmentService` — `SuggestDriverAsync`: envío `Pending` con `RouteStop`, peso de la
    orden vs capacidad libre de cada candidato, distancia y duración por carretera
    (`IRouteClient.GetDrivingMetricsAsync`), **filtro de factibilidad** por ventana horaria de la
    orden (hora actual en Argentina vs llegada estimada) y ranking final con
    `Domain.Services.DriverScoring`. Por candidato calcula el **costo estimado de operación**
    (`RouteOperationCostCalculator` con salario, km del día, consumos del vehículo de su ruta activa
    y peaje) que entra como peso en el score.
  - `RoutePlanningService` — **`POST /api/route-planning/preview`: propuesta read-only, no persiste
    nada.** Valida el request y carga en consultas batch (drivers, vehicles, catálogo de depósitos y
    **todos los envíos `Pending`**, una por tipo de recurso) sin N+1, arma el
    `VehicleRoutingProblem`, pide la matriz NxN a `IRouteMatrixClient` y lo resuelve con
    `IVehicleRoutingSolver`. Semántica: la administración elige `DriverId → VehicleId` y el depósito
    es **opcional** (ver `IDepositAssignmentPolicy`); el solver solo decide a qué vehículo va cada
    envío y en qué orden. El request **no lleva envíos**: se planifican todos los `Pending`, y los que
    no se pueden enrutar (sin coordenada geocodificada, fuera del horizonte o sin ventana feasible)
    se devuelven en `ExcludedShipments` en lugar de fallar; si no queda ninguno, 409. Horizonte de
    12 h **desde la hora local Argentina actual** (vía `TimeProvider`), ventanas de entrega de la
    orden convertidas a segundos relativos y clampeadas al horizonte. Antes de solver rechaza con
    `InvalidOperationException` (→ 409) recurso inexistente, envío no `Pending`, vehículo inactivo,
    **depósito inactivo**, conductor sin `CurrentLocation`, catálogo sin depósitos activos y matriz
    incompleta; los mensajes incluyen los IDs afectados. Que el solver no encuentre solución (p. ej.
    ningún vehículo tiene capacidad para todo el peso) también es 409.
    **No usa `IUnitOfWork`**: leer y devolver la propuesta es toda la operación.
  - Interfaces `I*Service` junto a cada implementación.
- **`RoutePlanning/`** — contratos de optimización, sin dependencias de EF ni ORS:
  - `VehicleRoutingProblem` + `PlanningDriverData`/`PlanningShipmentData`/`PlanningVehicleData` —
    problema de CVRPTW. Guarda el **layout de nodos** (índices de driver, envío y depósito) con sus
    lookups, de modo que el solver no necesita volver a resolver nombres a índices. Valida que toda
    ventana quepa en el horizonte con `EnsureWindowsFitHorizon` antes de llamar a OR-Tools (si no, la
    librería aborta con `ApplicationException: fail`).
  - `RouteMatrix` — matriz densa NxN de `long` con `UnreachableValue` para celdas sin dato y
    `HasCompleteData`; constructor y acceso validan rango e índice. `TimeWindow` — par
    start/end en **segundos** relativos al inicio del horizonte.
  - `IVehicleRoutingSolver`/`VehicleRoutingSolution` — frontera del solver: recibe el problema y la
    matriz, devuelve arcos planificados por ruta o `null` si no hay solución factible. La solución
    distingue `TotalDistanceMeters`/`TotalDurationMinutes` (reales, sin weighting) de los
    costos de arco ponderados por prioridad.
  - `IDepositAssignmentPolicy`/`NearestDepositAssignmentPolicy` + `DepositAssignment` — el depósito
    es **opcional en el request**: si viene, se respeta como override (el servicio ya valida que
    exista y esté activo); si viene `null`, la política elige el **depósito activo geodésicamente más
    cercano** al conductor por Haversine, con desempate por `Guid` para que el layout sea
    determinista. No usa `IRouteMatrixClient` (no hace falta una llamada extra a ORS) y solo recibe
    depósitos ya filtrados por `Active`; si no hay ninguno lanza `InvalidOperationException`.
    Registrada como **singleton** en `DependencyInjection.cs`.
- **`Persistence/`** — contratos (interfaces) de repositorios y `IUnitOfWork`;
  las implementaciones EF están en Infrastructure. `IUserRepository` agrega `GetDriverByIdAsync`,
  `GetAdminsAsync` y `GetDriverCandidatesAsync` (devuelve `DriverAssignmentCandidate` con los datos de
  operación: salario, km del día, consumos y mantenimiento del vehículo y peaje de la ruta activa);
  `IShipmentRepository` ofrece `GetByIdWithAssignmentDetailsAsync` (Order→Items + RouteStop) para el
  scoring y `GetPendingWithPlanningDetailsAsync` (proyección a `ShipmentPlanningData`: estado,
  prioridad, peso total de la orden, coordenada destino y ventana de entrega; filtra `Pending` y
  ordena de forma determinista) para la planificación;
  `IUserRepository` agrega `GetDriversByIdsAsync`, `IVehicleRepository` `GetManyByIdsAsync` e
  `IDepositRepository` **`GetAllAsync`** (catálogo completo, que el servicio filtra por `Active`;
  antes se cargaban solo los depósitos referenciados), todos
  batch para no repetir consultas por recurso; `IRouteRepository` agrega `GetActiveByDriverIdAsync`
  (ruta activa del driver con Stops→Shipment→Order) y `IRouteStopRepository` agrega `Remove`.
- **`Integrations/`** — contratos de servicios externos: `IGeocodingClient.GeocodeAsync(address)`
  (dirección → `Coordinate`), `IRouteClient.GetDrivingMetricsAsync(origins, destination)`
  (distancia en metros y duración en minutos por driver, redondeada hacia arriba) e
  **`IRouteMatrixClient.GetMatrixAsync(locations)`** (matriz NxN completa de distancias en metros y
  duraciones en segundos entre todos los nodos de planificación, con bandera de datos completos);
  implementaciones ORS en Infrastructure. `IEmailSender.SendAsync(recipients, subject, body)` notifica
  por correo (implementación MailKit en Infrastructure).
- **`Dtos/`** — modelos de request por agregado (`Orders`, `Shipments`, `Routes`, `RouteStops`,
  `Drivers`, `Auth`); payload esperado por cada endpoint. `Shipments.DriverAssignmentSuggestion`
  modela la sugerencia (driver recomendado + ranking, con `EstimatedOperationCost` en cada item);
  `RouteStops.CreateRouteStopRequest` recibe `Address` (se geocodifica) + `DistanceMeters`/`TollCost`
  y `Orders.CreateOrderItemRequest` incluye `WeightKg`;
  `Orders.CreateOrderRequest` acepta `DeliveryWindowStartAt`/`EndAt` opcionales (hora local);
  `Drivers.CancelRouteRequest` lleva `Reason` (`RouteCancellationReason`) y `Note` opcional.
  **`RoutePlanning/`**: `PlanRoutesRequest` lleva **solo** `DriverSelections`
  (`DriverSelectionRequest` con `DriverId`+`VehicleId` y `DepositId` **nullable**: si se omite, el
  planificador elige el más cercano; si se repite depósito entre rutas se deduplica el nodo);
  `RoutePlanningProposalResponse` devuelve totales, conteos y por ruta driver/vehículo/depósito, carga
  en kg y paradas (`ShipmentId` + `StopOrder`), más `ExcludedShipments` (los `Pending` que no se
  pudieron enrutar, con su motivo) y el `DepositId` realmente asignado por ruta.
- **`Validators/`** — FluentValidation `XxxRequestValidator` por request; los servicios llaman
  `ValidateAndThrowAsync` (no son decorativos). `Common/NoteValidation.cs` centraliza notas.
  `PlanRoutesRequestValidator` exige al menos una selección y drivers/vehículos no vacíos y sin
  **duplicados**; `DepositId` acepta `null` (elección automática) pero rechaza `Guid.Empty`
  (los depósitos sí se pueden compartir entre rutas).
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
  - `DriversController` — `PATCH /api/drivers/me/location` y `POST /api/drivers/me/routes/cancel`,
    ambas `DriverOnly`; el id del driver se toma del claim del token.
  - `AuthController` — `POST /api/auth/login` (anónimo) → 200 token / 400 validación / 401 credenciales.
  - `RoutePlanningController` — `POST /api/route-planning/preview`, política `AdminOnly`; devuelve
    200 con la propuesta, 400 validación, 409 conflicto de planificación. No expone endpoints CRUD de
    depósitos todavía.
- **`Auth/`** — `JwtOptions` (Issuer/Audience/Secret/Expiry), `JwtTokenService` (firma HMAC-SHA256 con
  claims sub, name, email, jti y roles), `LoginResponse`.
- **`Authorization/Policies.cs`** — nombres de policies `AdminOnly`/`DriverOnly`.
- **`Middleware/GlobalExceptionHandler.cs`** — mapa excepción→HTTP: Validation→400, NotFound→404,
  InvalidOperation→409, resto→500 (`application/problem+json`).
- **`Extensions/IdentitySeeder.cs`** — crea roles Admin/Driver/Customer + Admin inicial desde config.
- **Config.** `appsettings.json` define `ConnectionStrings`, `Jwt`, `Seed`, `OpenRouteService`,
  `Email` (Host/Port/Username/Password/From/FromName/UseSsl). Secretos
  (`Jwt:Secret`, `Seed:AdminPassword`, `OpenRouteService:ApiKey`, `Email:Username/Password`) por
  user-secrets, no versionados.

## Infrastructure y tests

- **`Persistence/LogiMatchDbContext.cs`** — hereda de `IdentityDbContext<User, IdentityRole<Guid>, Guid>`
  (tablas negocio + Identity `AspNet*`). `LogiMatchDbContextFactory.cs` para design-time.
- **`Persistence/Configurations/`** — config EF por entidad (tablas, TPH de Users, FKs, max lengths).
- **`Persistence/Interceptors/AuditableEntityInterceptor.cs`** — rellena `CreatedAt`/`UpdatedAt`
  en entidades `IAuditableEntity`.
- **`Persistence/Repositories/`** — implementaciones EF de los contratos de `Application.Persistence`,
  + `UnitOfWork`. Includes clave: Order→Items, Shipment→Order+DeliveryAttempts, Route→RouteStops;
  `UserRepository.GetDriverCandidatesAsync` agrega por driver la capacidad máx. de vehículos activos,
  entregas exitosas del día, pendientes y en-progreso (con su peso), el **km del día** (suma de
  `DistanceMeters` de envíos llegados hoy vía `ShipmentHistory`) y los datos del vehículo/peaje de la
  ruta activa; usuarios vía `OfType<T>()` (TPH).
- **`Email/`** — `MailKitEmailSender` (implementa `IEmailSender` vía SMTP) y `EmailOptions`
  (Host/Port/Username/Password/From/FromName/UseSsl).
- **`Integrations/`** — implementaciones de los clientes externos: `OpenRouteServiceGeocodingClient`
  (`GET geocode/search`, país desde config), `OpenRouteServiceMatrixClient`
  (`POST v2/matrix/driving-car`, chunks de ≤69 orígenes, distancias en metros y duraciones en
  minutos —redondeadas hacia arriba— extraídas de `distances` y `durations`) y, para la
  planificación, `OpenRouteServiceMatrixGateway` (transporte compartido:Auth, `BaseUrl` y lectura de
  la respuesta de bloque) + **`OpenRouteServiceFullMatrixClient`** (`IRouteMatrixClient`). Este
  último pide la matriz NxN en bloques de ≤50 locations (2500 celdas, bajo el límite de 3500 pares de
  ORS), recorre **solo el triángulo superior** de bloques y completa el inferior por transposición
  (válido porque distancia y duración son simétricas en los perfiles de conducción usados); son
  `blocks * (blocks + 1) / 2` requests, nunca uno por par. Ante celdas nulas escribe
  `RouteMatrix.UnreachableValue` y marca la matriz como incompleta en vez de fallar, para que el
  servicio pueda distinguir "sin datos" de "error de ORS".
- **`RoutePlanning/OrToolsVehicleRoutingSolver.cs`** — `IVehicleRoutingSolver` con Google OR-Tools.
  Monta un `RoutingModel` con **un vehículo por nodo** (cada driver es un vehículo independiente) y
  arrays `starts`/`ends` en `RoutingIndexManager`, así el depósito de cada ruta queda fijo como fin de
  arco. Costo de arco con `RegisterTransitMatrix` sobre la distancia ponderada; capacidad con
  `AddDimensionWithVehicleCapacity` en **gramas** (evita truncar pesos fraccionarios) y tiempo con
  `AddDimension` sobre las duraciones, con el cumul de inicio minimizado y rango `[0, horizonte]`.
  Timeout 10 s. El costo de arco es distancia × factor de prioridad del **nodo destino**
  (Urgent 0.5, High 0.75, Normal 1.0, Low 1.5) para que el orden priorice sin falsear los totales
  reportados; **todos los nodos no terminales son obligatorios** (no se agregan disjunctions, que
  permitirían descartar envíos). Devuelve `null` si OR-Tools no encuentra solución factible dentro
  del timeout.
- **`DependencyInjection.cs`** — `AddInfrastructure(connectionString)`: DbContext, repos, UoW,
  `EmailOptions` (sección `Email`) + `IEmailSender`/`MailKitEmailSender` y HttpClient tipados de ORS
  (geocoding + matrix; `BaseUrl` desde config), más `IRouteMatrixClient` →
  `OpenRouteServiceFullMatrixClient` y `IVehicleRoutingSolver` → `OrToolsVehicleRoutingSolver`.
- **`Persistence/DemoDataSeeder.cs`** — seed demo idempotente (solo Development): admin, customer y
  5 conductores con ubicación, `SalaryPerHour` y vehículos (con consumos, precio de combustible y
  mantenimiento), depósitos (`CentralDepositId`/`SouthDepositId`, activos); crea la orden objetivo
  sin driver asignado y otros envíos con estados variados
  (pendientes, en progreso, entregado) y paradas con `DistanceMeters`/`TollCost` para ejercitar el
  scoring.
- **`Migrations/`** — migraciones EF del esquema: `InitialCreate`, `AddDriverScoring`
  (ubicación de conductores + `WeightKg` en items), `AddOrderDeliveryWindow`
  (ventana horaria de entrega en órdenes), `AddRouteCancellation` (desactivación de rutas +
  `CancellationReason`), `MakeRouteStopRouteIdNullable` (`RouteId` opcional en paradas) y
  `AddOperatingCostsAndRouteStopCosts` (`SalaryPerHour` en drivers, costos de vehículo,
  `DistanceMeters`/`TollCost` en paradas) y `AddDeposits` (tabla `Deposits` con `Name` +
  `Coordinate`) y **`AddDepositActive`** (columna `Active` con default `true` + índice, para poder
  retirar un depósito de la planificación sin borrarlo).
- **Tests** — `src/UnitTests/Domain/` (reglas por entidad, incl. `DriverScoringTests`,
  `RouteOperationCostCalculatorTests`, `OrderItemTests`, `DriverTests`),
  `src/UnitTests/Application/` (servicios con Moq y validators,
  incl. `ShipmentAssignmentServiceTests`, `DriverServiceTests`, `RouteStopServiceTests`,
  `RoutePlanningServiceTests`, `NearestDepositAssignmentPolicyTests`, `PlanRoutesRequestValidatorTests`),
  `src/UnitTests/Infrastructure/` (clients ORS con `MockHttpMessageHandler`, incl.
  `OpenRouteServiceFullMatrixClientTests` y `OrToolsVehicleRoutingSolverTests`, este último con
  scenarios parametrizados que dimensionan las matrices según el número de nodos).
  `src/IntegrationTests/` cubre flujos reales contra SQL Server: Auth, `Orders/OrderLifecycleTests`,
  `Routes/RouteLifecycleTests`, `RouteStops/CreateRouteStopTests`, `Shipments/`
  (`ShipmentLifecycleTests`, `DriverSuggestionTests`), `Drivers/` (`UpdateLocationTests`,
  `CancelRouteTests`), `RoutePlanning/RoutePlanningPreviewTests` (propuesta happy path + que no
  persiste, asignación de todos los envíos, 409 por recurso/envío/matriz, depósito inactivo,
  elección automática del más cercano e ignorado de inactivos, 400, 401 y 403) y
  `Email/MailKitEmailSenderTests` (con `Smtp/FakeSmtpServer`). Infra:
  `ApiWebApplicationFactory` (WebApplicationFactory<Program>, base por suite reemplazando
  `IGeocodingClient`/`IRouteClient`/`IRouteMatrixClient`/`IEmailSender` por doubles `Recording*`;
  fija `Seed:AdminEmail`/`Seed:AdminPassword` y **`Seed:Password`** —este último es el que hace que
  los usuarios demo acepten `UsersPassword`—, `TestDataBuilder` y `TestDatabaseFixture`
  (collection fixture)).