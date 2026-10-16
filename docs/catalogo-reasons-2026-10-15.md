# Catálogo de motivos de reporte

Preparado el 15 de octubre de 2026 a partir de `wwwroot/Content/site-rules.json` y `wwwroot/Content/seal-of-quality.json`, y del esquema actual de Reason/ReasonCategory.

## Archivo para insertar

[Abrir el script SQL](D:/Users/pndia/Documents/GitHub/SNEStorage/docs/reasons-catalog-2026-10-15.sql).

**Para el editor web de Monster.ASP/Adminer:** usar [esta versión de una sola instrucción](D:/Users/pndia/Documents/GitHub/SNEStorage/docs/reasons-catalog-monsterasp-2026-10-15.sql). Incluye el mismo catálogo y la misma transacción dentro de `EXEC sys.sp_executesql N'...'`, con las comillas escapadas. Hay que pegar el archivo completo, incluido el cierre final.

El error 1087 «Must declare the table variable @Categories» significa que la instrucción no ve la declaración de esa variable. Puede ocurrir cuando el editor envía las instrucciones como lotes separados o cuando solo se selecciona una parte del script. La variante conserva declaraciones y usos dentro de un único lote ejecutado por SQL Server. [Documentación de sp_executesql y ámbito del lote](https://learn.microsoft.com/en-us/sql/relational-databases/system-stored-procedures/sp-executesql-transact-sql?view=sql-server-ver17#remarks).

Son **5 categorías y 20 motivos**, con textos en inglés para la interfaz. El script usa las tablas finales `dbo.reason_category` y `dbo.reason`, sus columnas mapeadas y los IDs Identity generados por SQL Server.

### Cómo usarlo

1. Tener aplicadas las migraciones del esquema hasta ReasonCategory2. Este script inserta datos; no crea tablas ni reemplaza migraciones.
2. En el editor SQL de Monster.ASP o SSMS, seleccionar la base de SNEStorage.
3. Abrir la versión correspondiente al editor y ejecutarla completa. En Monster.ASP usar la variante indicada arriba, sin partirla por instrucciones.
4. Revisar los dos resultados: cantidades insertadas y catálogo con sus IDs reales, prioridades, contexto sugerido y referencias a las reglas.
5. Recargar la caché de reasons: actualmente vive 24 horas. Si ya estaba cargada, reiniciar la aplicación la vacía; si no, la siguiente lectura usará los datos nuevos.

El archivo **no se ejecutó** contra ninguna DB. No añade ni modifica tablas, usuarios, recursos ni flags. Inserta únicamente categorías/motivos que falten; no cambia filas existentes ni fija IDs manuales. Se puede volver a ejecutar sin insertar duplicados con el mismo nombre de categoría y descripción de motivo. Textos distintos con el mismo significado requieren revisión manual: el script no puede reconocer equivalencias semánticas.

Si encuentra varias categorías con un mismo nombre propuesto, o varios motivos iguales en una categoría, aborta en vez de escoger un ID arbitrariamente. La inserción se hace dentro de una transacción.

**Importante sobre las migraciones actuales:** ReasonCategory añade reason_category_id obligatorio con valor 0 y crea su FK antes de insertar categorías. Si ya existen filas en reason, esa migración puede fallar. Una migración posterior o este catálogo no pueden corregir un paso anterior que todavía no logró aplicarse. En ese caso hay que asignar una categoría válida a esos motivos durante la migración, antes de exigir la FK. No se modificaron migraciones en esta tarea.

## Criterio de prioridad

| Prioridad | Revisión sugerida |
| --- | --- |
| 5 | Urgente: posible material ilegal, enlaces maliciosos o phishing |
| 4 | Alta: ROMs no permitidos, redistribución sin permiso, acoso |
| 3 | Media: engaño, advertencias omitidas, autoría, dependencias y dependencias |
| 2 | Normal: spam, clasificación, enlaces ajenos al contexto, documentación o previews requeridas |
| 1 | Baja: información vaga y comentarios sin contenido útil |

**Priority ordena la atención del staff, no decide un castigo.** Los motivos describen una sospecha que debe revisarse; contexto, intención, impacto y reincidencia siguen siendo decisiones humanas. No se prohíben palabras aisladas, toda conversación política/religiosa ni todo homebrew.

## Catálogo

Las referencias A–F corresponden al orden actual de Site rules; las referencias SQ corresponden al documento separado Seal of Quality. El SQL incluye también los IDs de regla estables para encontrar cada origen aunque se reordenen las secciones.

| Categoría | Motivo resumido | Prioridad | Reglas |
| --- | --- | --- | --- |
| Safety and distribution | Posible material ilegal | 5 | C.1 |
| Safety and distribution | Enlaces maliciosos o phishing | 5 | C.2 |
| Safety and distribution | ROM en lugar de parche; excepción para homebrew sin infracción | 4 | C.3 |
| Safety and distribution | Redistribución sin permiso | 4 | C.4 |
| Safety and distribution | Enlaces sospechosos o ajenos al contexto | 2 | C.2, D.1 |
| Conduct and discussions | Acoso o intimidación | 4 | A.1 |
| Conduct and discussions | Lenguaje usado para provocar o insultar | 3 | A.3 |
| Conduct and discussions | Spam, repetición o promoción no solicitada | 2 | D.1 |
| Conduct and discussions | Discusión sensible fuera de contexto | 2 | D.2 |
| Conduct and discussions | Comentario vago, sin sentido o ajeno al tema | 1 | D.1, D.3 |
| Resource content and previews | Archivos que no corresponden a la descripción | 3 | B.1 |
| Resource content and previews | Advertencias de contenido omitidas o incorrectas | 3 | B.2 |
| Resource content and previews | Recurso sin relación con SNES | 2 | B.3 |
| Resource content and previews | Previews ajenas al recurso o inapropiadas | 2 | B.4 |
| Authorship and resource information | Autores o colaboradores sin acreditar | 3 | C.5 |
| Authorship and resource information | Videojuego, tipos o categorías incorrectos | 2 | B.5 |
| Authorship and resource information | Dependencias requeridas omitidas o incorrectas | 2 | B.6 |
| Authorship and resource information | Información vaga, incompleta o imprecisa | 1 | B.6 |
| Dependencies and visibility | Dependencia privada o inaccesible tras cambiar visibilidad | 3 | E.3 |
| Dependencies and visibility | Recurso sin versiones | 3 | E.4 |

### Qué no se convirtió en un motivo independiente

- Por decisión del propietario se retiraron todos los motivos de Seal of Quality del catálogo y se eliminarán de la DB. El frontend no contiene excepciones por esa categoría. Para retirar los ya insertados está `docs/remove-quality-report-reasons-2026-10-15.sql`, que aborta si existen reportes asociados.


- A.2, contenido del perfil/avatar: actualmente solo hay acciones para reportar recursos o comentarios. No hay un objetivo de reporte para perfiles. Conviene añadir ese motivo cuando exista el contexto correspondiente.
- E.1 y E.2: explican permisos y derechos de acceso/gestión, no son infracciones por sí mismos.
- F.1: la reincidencia influye en la decisión del staff; no debe confundirse con el motivo concreto del reporte.
- Solicitar el sello es opcional; no pedirlo no incumple ninguna regla.
- Beneficios del sello y solicitudes de retirada/borrado excepcional: son información o trámites con el staff, no motivos para reportar cualquier recurso.
- Un recurso borrado ya no tiene un ID válido contra el que crear el reporte actual. Las restricciones de borrado deben aplicarse en backend y conservar historial si se necesita auditar su incumplimiento.
- No se convirtió una preferencia artística personal en una infracción. El estándar artístico corresponde al criterio del staff.
- No se agregó un motivo genérico «Other»: el DTO actual no permite adjuntar una explicación libre, por lo que ese reporte sería difícil de investigar.

## Observaciones del código actual

### Estructura de reasons/categorías

- ReasonCategory y Reason están registrados en OnModelCreating, sus nombres de tabla/columnas coinciden con el script y Reason incluye la FK de categoría.
- La caché de reasons carga ReasonCategory, por lo que `/api/flags/options` dispone de nombre e ID de categoría para agruparlos.
- No hay restricción única de nombres/descripciones en DB. El script evita duplicados de su propio catálogo, pero otros procesos todavía pueden crearlos.
- Borrar una categoría hace cascada a sus motivos; borrar motivos hace cascada a Flags. Con los enlaces de flags existentes, la operación puede eliminar historial o bloquearse por las FKs restantes. Para retirar opciones, conviene no borrar categorías/motivos con historial; si se necesita, diseñar un estado Active en lugar de reutilizar IDs.
- Priority tiene Range(1,5) en el modelo, pero SQL manual no ejecuta DataAnnotations. Los valores del catálogo están en ese rango; si se desea imponerlo a todas las escrituras SQL hace falta un CHECK en DB.
- Los contextos Resources/Comments del resultado SQL son documentación: **el modelo actual no los guarda ni los valida**. No se añadieron columnas. Actualmente se puede enviar cualquier ReasonID existente a cualquiera de las dos acciones. Si se quiere restringirlo, hace falta representarlo en backend.

### Fallos de flags que siguen presentes en esta copia

1. **FullGet sigue público** en FlagController y el servicio no comprueba staff. Expone identidad del reportante y motivo.
2. **Corregido durante la integración del frontend:** ambos ValidateCreate ahora usan ResourceID/CommentID para buscar el objeto y conservan ReasonID para consultar el motivo.
3. **No se comprueba acceso al recurso privado ni AlreadyModerated** al crear reportes.
4. **FlagFilterDTO sigue declarando campos públicos**, sin get/set. No se enlazan como las propiedades esperadas desde query.
5. **ResolvedAt no se actualiza** y FindEdit sigue usando NewNotFound en lugar de FlagNotFound.
6. **FlagReadDTO todavía no contiene el ID/tipo del objetivo**, necesario para abrirlo desde el panel.
7. El chequeo AnyAsync de repetición sigue separado del insert, sin garantía atómica para usuario + objetivo.

No se modificaron servicios, controladores, modelos, DTOs, reglas ni migraciones durante esta tarea. El catálogo no corrige esos problemas de ejecución. El informe anterior con propuestas está en `docs/revision-flags-news-likes-2026-10-15.md`.
