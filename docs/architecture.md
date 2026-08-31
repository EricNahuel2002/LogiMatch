# Architecture

## Estilo

LogiMatch utiliza una variante de Clean Architecture.

Capas principales:
- Domain
- Application
- Infrastructure
- API

Flujo conceptual:

API → Application → Domain
Infrastructure implementa las abstracciones necesarias para interactuar con recursos externos.

## Reglas

- Domain no depende de Infrastructure.
- Las dependencias deben apuntar hacia las abstracciones apropiadas.
- La API no debe convertirse en el lugar donde vive la lógica de negocio.
- Antes de introducir una nueva capa, patrón o abstracción, evaluar si aporta un beneficio real.

## Regla para agentes

Antes de modificar arquitectura:
1. Identificar qué capas están involucradas.
2. Revisar los patrones existentes.
3. Explicar el impacto.
4. Pedir confirmación si el cambio es arquitectónicamente importante.

## Organización

La estructura física puede evolucionar a medida que crezcan los módulos. Preferir una organización por responsabilidad o módulo cuando el tamaño del proyecto lo justifique.

No crear carpetas únicamente para satisfacer una estructura teórica.

## Decisiones registradas

Domain no conoce JSON ni System.Text.Json.
La serialización JSON vive exclusivamente en Infrastructure.
