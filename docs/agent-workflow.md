# Agent Workflow

Este documento define cómo abordar tareas grandes o que requieran coordinación entre múltiples cambios.

## Tareas grandes

Cuando una tarea requiera muchos cambios:

1. Dividirla en fases.
2. Identificar dependencias entre las fases.
3. Definir qué archivos o módulos serán afectados.
4. Confirmar el diseño antes de realizar cambios arquitectónicos.
5. Implementar por partes.
6. Validar cada fase antes de continuar.
7. Documentar la tarea y las fases según `task.md`.

## Decisiones arquitectónicas

Si durante una tarea aparece una decisión que implique:

- modificar capas de Clean Architecture
- cambiar contratos existentes
- introducir una nueva abstracción
- modificar relaciones importantes del dominio
- cambiar infraestructura

detenerse y consultar antes de implementar.

## Contexto en tareas grandes

Incluso en tareas grandes:

1. Comenzar por el módulo afectado.
2. Inspeccionar dependencias directas.
3. Ampliar el contexto únicamente cuando sea necesario.

No recorrer todo el repositorio sin una razón concreta.