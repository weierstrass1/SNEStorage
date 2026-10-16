# Reportar recursos y comentarios

## Flujo

- Usuarios con sesión verificada ven un menú de tres puntos en la esquina superior del recurso o comentario.
- El menú contiene **Report this resource** o **Report this comment**.
- Un diálogo nativo permite abrir una categoría, seleccionar un motivo y pulsar Submit report. Todas las categorías empiezan cerradas.
- Cancel y Escape cierran el diálogo. La selección se conserva si se cancela el captcha.
- El envío usa la integración Turnstile existente, con acción `create_flag`, captcha obligatorio y antiforgery.
- Se muestra confirmación al recibir el ID del reporte. RepeatedFlag usa una pantalla distinta, con título Already reported y aviso de que no se creó un reporte adicional. Se ocultan motivos y botón de envío, dejando Close.
- Los errores de red no provocan reintentos automáticos de la escritura. Cuando el resultado es incierto, se indica no reenviar inmediatamente.
- Navegar a otra página o eliminar el control de origen cierra el diálogo y cancela las peticiones pendientes.

Los menús no se muestran sin sesión, en comentarios Deleted ni en recursos AlreadyModerated. Estas son reglas de presentación; el backend debe conservar las comprobaciones de permisos y moderación.

El resumen del menú muestra solo tres puntos, sin la flecha global de los details. Los comentarios tienen padding horizontal, separación entre tarjetas, borde suave y fondo translúcido.

## API utilizada

- GET `/api/flags/options`: diccionario de Reason con ReasonCategory cargada.
- POST `/api/flags/resource_flag/create`: JSON `{ reasonID, resourceID }`.
- POST `/api/flags/comment_flag/create`: JSON `{ reasonID, commentID }`.
- El POST devuelve el ID creado o el error con prefijo del controlador.
- `/api/profile/me` se consulta mediante la comprobación de cuenta compartida antes de enviar, para evitar usar el diálogo de una identidad anterior.

Se confirmó que el propietario corrigió ResourceID y CommentID en las búsquedas de ambos ValidateCreate. No se modificaron servicios ni controladores durante esta implementación.

## Clasificación por contexto

`wwwroot/Content/report-reasons.json` define en qué contexto aplica cada motivo. Se relaciona con la descripción del motivo normalizada, **no con IDs numéricos**, que pueden cambiar entre bases de datos.

Ejemplo:

```json
{
  "description": "Spam, repeated messages, or unsolicited promotion.",
  "contexts": ["comment"]
}
```

- `contexts`: resource, comment o ambos.
- `requiresPreviews: true`: se muestra solo si el recurso tiene previews.
- `requiresNoVersions: true`: se muestra solo si el recurso no tiene versiones.
- No se incluyen categorías sin motivos aplicables ni motivos desconocidos sin clasificación. Si se añade o cambia una descripción en DB, actualizar este JSON.
- Nombres de categoría y motivos mostrados provienen de la API; el JSON solo clasifica su aplicabilidad.
- Si previews o versiones ya se conocen en la página, se reutiliza ese contexto. Si aún no se conocen, al abrir el reporte se consulta la sección necesaria. No se añaden estas consultas a la carga inicial del recurso.
- Las opciones del catálogo se reutilizan en memoria del navegador hasta cinco minutos. No se añade caché de servidor.

El modelo Reason no incluye estos contextos: el filtrado del frontend facilita la selección, pero no impide enviar un ReasonID de otro contexto directamente a la API.

## Seal of Quality retirado

Por decisión del propietario esos motivos se retirarán de la DB. El frontend no contiene excepciones por categoría ni comprueba si un recurso tiene sello: utiliza el catálogo de la API y su clasificación de contexto.

Se actualizaron ambos scripts del catálogo a **5 categorías y 20 motivos**. Para eliminar los motivos de Quality ya insertados en producción, ejecutar manualmente el archivo completo:

[remove-quality-report-reasons-2026-10-15.sql](D:/Users/pndia/Documents/GitHub/SNEStorage/docs/remove-quality-report-reasons-2026-10-15.sql).

Es una sola instrucción para Monster.ASP/Adminer y se detiene si encuentra reportes asociados, para conservar el historial. No se ejecutó contra ninguna base de datos.

## Comprobaciones

Se comprobó sintaxis de los módulos JavaScript y compilación de la aplicación. No se enviaron reportes reales ni se ejecutaron tests. Falta comprobar el envío completo en navegador con una sesión y captcha configurados.
