---
name: create-api-endpoint
description: Crea o modifica endpoints de ASP.NET Core siguiendo la arquitectura y patrones existentes de LogiMatch.
---

# Create API Endpoint

Utilizar cuando se necesite crear o modificar un endpoint HTTP.

## Antes de implementar

Determinar:

- recurso involucrado
- endpoint HTTP necesario
- capa Application correspondiente
- entidad involucrada
- DTOs existentes
- validaciones existentes
- patrón utilizado por endpoints similares

## Implementación

Seguir el patrón existente del proyecto para:

1. DTO
2. Validator
3. Application service / use case
4. Controller
5. Persistencia
6. Tests

No crear nuevas abstracciones si existe una solución equivalente en el proyecto.

## Validación

Comprobar:

- comportamiento HTTP correcto
- validaciones
- casos exitosos
- casos de error
- tests correspondientes

No modificar endpoints no relacionados.