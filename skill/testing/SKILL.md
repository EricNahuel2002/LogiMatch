---
name: testing
description: Crea y ejecuta tests para cambios realizados en LogiMatch utilizando los patrones existentes del proyecto.
---

# Testing

Utilizar cuando una tarea requiera crear, modificar o ejecutar tests.

## Antes de crear tests

1. Buscar tests similares.
2. Identificar el framework y helpers existentes.
3. Reutilizar fixtures, builders y mocks existentes.
4. No introducir una estrategia de testing nueva sin necesidad.

## Tests

Cubrir según corresponda:

- caso exitoso
- validaciones
- errores esperados
- casos límite

Mantener los tests enfocados en el comportamiento.

## Ejecución

Ejecutar primero los tests relacionados con el cambio.

Si el cambio puede afectar otras áreas, ejecutar posteriormente el conjunto más amplio de tests necesario.

## Resultado

Informar:

- tests creados/modificados
- tests ejecutados
- resultado
- cualquier problema detectado