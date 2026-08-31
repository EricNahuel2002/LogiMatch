# Contexto de código

## Estructura principal

El proyecto utiliza Clean Architecture, organizado en las siguientes capas:

- **API**: punto de entrada de la aplicación. Maneja las requests HTTP, responses, autenticación/autorización y configuración relacionada con la API.
- **Application**: contiene los casos de uso de la aplicación, validaciones, DTOs, interfaces y servicios de aplicación.
- **Domain**: contiene las entidades, value objects y reglas de negocio.
- **Infrastructure**: implementa detalles externos como persistencia de datos, migraciones, acceso a servicios externos y librerías específicas de infraestructura.

