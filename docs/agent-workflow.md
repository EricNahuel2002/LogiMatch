# Agent Workflow

Este archivo define cómo debe trabajar el agente para evitar cargar contexto innecesario.

## Antes de trabajar

1. Identificar el módulo o área afectada.
2. Leer `AGENTS.md`.
3. Leer solo los documentos de `docs/` relevantes.
4. Buscar las implementaciones existentes relacionadas.
5. Inspeccionar primero los archivos directamente involucrados.

## Durante el análisis

No asumir que todo el repositorio es relevante.

Prioridad de contexto:
1. Archivo o archivos objetivo.
2. Dependencias directas.
3. Interfaces/contratos relacionados.
4. Entidades y reglas de negocio relacionadas.
5. Documentación del módulo.
6. Resto del proyecto solo si es necesario.

## Durante la implementación

- Reutilizar código existente cuando corresponda.
- Evitar cambios no relacionados.
- Mantener la arquitectura.
- No introducir paquetes innecesarios.
- No refactorizar grandes partes del proyecto como efecto secundario de una tarea pequeña.

## Al terminar

Informar:
- Qué se modificó.
- Qué archivos fueron afectados.
- Decisiones relevantes.
- Tests ejecutados o pendientes.
- Riesgos o cuestiones pendientes.

## Tareas grandes

Si una tarea requiere muchos cambios:
1. Dividirla en fases.
2. Confirmar el diseño antes de una modificación arquitectónica.
3. Implementar por partes.
4. Documentar la tarea y las fases en base a la estructura dada en `task.md`.

## Regla de contexto

El agente debe buscar el contexto mínimo suficiente para tomar una decisión correcta, no el máximo contexto disponible.
