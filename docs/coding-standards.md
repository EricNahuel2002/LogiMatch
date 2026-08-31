# Coding Standards

## General

- Preferir claridad sobre código excesivamente compacto.
- Usar nombres descriptivos.
- Mantener métodos pequeños.
- Mantener clases con una responsabilidad clara.
- Evitar comentarios que simplemente repitan el código.
- Mantener nullability habilitado.
- Usar async/await cuando corresponda.

## Diseño

Priorizar:
- SOLID
- DRY
- KISS
- Separation of Concerns

No agregar complejidad innecesaria. Si dos soluciones son válidas, elegir la más sencilla que mantenga buenas prácticas.

Evitar:
- Abstracciones prematuras.
- Patrones aplicados sin necesidad.
- Duplicación de lógica.
- Soluciones rápidas que dificulten futuras modificaciones.

## Regla práctica

Antes de crear una nueva clase, servicio, interfaz o abstracción:
1. Buscar si ya existe algo equivalente.
2. Evaluar si puede reutilizarse.
3. Crear una nueva abstracción solo si mejora realmente el diseño.
