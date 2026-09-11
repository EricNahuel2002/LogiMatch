---
name: implement-feature
description: Implementa una nueva funcionalidad siguiendo el flujo de análisis del proyecto LogiMatch y respetando Clean Architecture.
---

# Implement Feature

Utiliza este workflow cuando debas implementar una funcionalidad o modificar comportamiento existente.

## 1. Analizar contexto

1. Leer únicamente la documentación relevante de `docs/`.
2. Leer `context.md`.
3. Determinar qué capa, entidad o módulo está involucrado.
4. Identificar archivos directamente relacionados.

No recorrer todo el repositorio salvo que sea necesario.

## 2. Analizar implementación existente

Antes de crear algo nuevo:

- Buscar servicios similares.
- Buscar entidades similares.
- Buscar DTOs similares.
- Buscar validaciones similares.
- Buscar patrones existentes.
- Reutilizar infraestructura existente cuando corresponda.

## 3. Implementar

Respetar:

- Clean Architecture
- SOLID
- DRY
- KISS
- separación de responsabilidades
- patrones existentes
- reglas del proyecto

Si falta información para tomar una decisión importante, preguntar antes de cambiar la arquitectura.
Si la propuesta contradice las reglas del proyecto, adviértelo.
No asumir requisitos que no fueron definidos.
Modificar únicamente los archivos necesarios.

## 4. Validar

Después de implementar:

- Revisar errores de compilación.
- Ejecutar los tests relevantes.
- Revisar que no se hayan modificado archivos no relacionados.

## 5. Resultado

Explicar brevemente:

- qué se hizo
- por qué
- qué archivos fueron modificados
- qué validaciones/tests se ejecutaron