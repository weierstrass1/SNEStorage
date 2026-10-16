# Navegación y respuesta visual de votos

Actualización posterior: producción sí conecta por WebSocket. La revisión con
mediciones está en `rendimiento-produccion-2026-10-16.md`; allí se documentan las
reducciones adicionales de interop, el monitor compartido de NewsFeed y las
limitaciones restantes de InteractiveServer.

## Páginas estáticas

La aplicación utiliza `InteractiveServer` global: la navegación interna depende
del circuito de Blazor y de la latencia hasta el servidor. No se midió el tiempo
real de producción ni se comprobó qué transporte negocia SignalR.

Se encontró un recorrido adicional en `StaticDocumentPage`: render de Loading,
import de un módulo JS, fetch del JSON en el navegador, devolución del documento
completo a .NET por SignalR y otro render con el contenido. Incluso Community
pagaba esos viajes sin consultar la DB.

Ahora el componente lee su archivo mediante `WebRootFileProvider` en
`OnInitializedAsync`. Community, Rules, Seal of Quality, About, Donations,
Privacy, Credits y las instrucciones del formulario reciben el contenido en
el render inicial. Los mismos JSON siguen siendo editables. No hay caché
compartida, consulta a DB ni una acción nueva. Las lecturas fallidas conservan
el botón de reintento. Los anclajes usan JS únicamente cuando hay un fragmento
en la dirección. About y Credits mantienen el perfil del creador como carga
separada.

Esto elimina viajes evitables; no convierte la aplicación en WebAssembly ni
elimina la latencia propia de Blazor Server. Si en producción sigue habiendo
una pausa importante, comprobar en Network la conexión `_blazor`: transporte
WebSocket frente a Long Polling, reconexiones y tiempos de los mensajes. Las
páginas públicas no esperan a PageAccessGuard para consultar la cuenta.

## Likes, dislikes y estrellas

- `optimistic-votes.js` aplica el estado visual en el navegador, en el mismo
  clic, antes del viaje a Blazor: selección, cantidades y aprobación/promedio.
- Los componentes C# también actualizan su estado antes de esperar a la API.
  Conservan el estado previo para restaurarlo cuando la petición falla.
- El módulo visual no llama a la API ni decide permisos. Las llamadas existentes
  mantienen cookies, antiforgery, CAPTCHA y validación de la respuesta.
- Una segunda pulsación sobre el mismo control se bloquea mientras se confirma
  la primera. No se encolan ni se reintentan escrituras automáticamente.
- Cambiar de like a dislike resta uno y suma otro. Quitar el voto resta uno.
  Calificar de nuevo sustituye la valoración propia sin incrementar los votos.
- Los cambios de cuenta/recurso invalidan el estado optimista anterior. Las
  respuestas tardías no deben restaurarlo sobre otra identidad.
- Noticias y estrellas ya no hacen un GET adicional en cada guardado exitoso.
  Las cifras se ajustan desde el último resumen disponible; los votos de otras
  personas llegan con la siguiente lectura, como ocurre con los comentarios.
- Noticias conserva la consulta de comprobación cuando se pierde la respuesta:
  una escritura podría haberse guardado. Los errores ambiguos explican que no
  se pudo confirmar, sin afirmar que la DB necesariamente rechazó el cambio.
- El módulo escucha los atributos de estado emitidos por Blazor para confirmar
  o deshacer su pintura inmediata. Maneja controles de respuestas por separado
  y usa texto/atributos, sin insertar HTML.
- La preselección y el hover de estrellas también responden en el navegador;
  se conserva el botón Save rating. El guardado no muestra un spinner ni cambia
  su texto a Saving.

### Guardado al navegar

Los POST de likes/dislikes de noticias y comentarios y de estrellas tienen un
AbortController propio, independiente de los mapas que se cancelan al desmontar
las vistas. La llamada JS de estos votos tampoco recibe el token de vida del
componente. La pantalla anterior ignora la respuesta tardía, pero el envío sigue
en segundo plano. Un error después de salir muestra un aviso persistente con un
enlace para revisar el voto.

Se usa keepalive para estas pequeñas peticiones y un plazo independiente de tres
minutos, que incluye la posible verificación CAPTCHA. Navegar entre secciones no
lo cancela. No es una cola duradera: cerrar el proceso del navegador antes de
iniciar el POST, perder conexión o cancelar un CAPTCHA aún puede impedir guardar.
No se repite automáticamente una escritura cuya confirmación se perdió.

## Markdown

`MarkdownText` entrega el texto codificado en una hoja oculta y una hoja vacía
para el resultado. `markdown-loader.js` carga las bibliotecas locales una vez y
formatea bloques añadidos/actualizados mediante MutationObserver. No hay import
JS, llamada de render y respuesta de Blazor por cada texto. El contenido nuevo
se formatea en la actualización del DOM antes de mostrarse; la primera carga de
bibliotecas usa un pequeño placeholder. El texto plano solo aparece como fallback
seguro si falla el formato o JavaScript está deshabilitado. Se conserva Marked,
DOMPurify, la lista de etiquetas permitidas y la protección de enlaces/imágenes.

No se modificaron servicios, controladores, DTOs, permisos ni migraciones para
estos ajustes. La compilación y las comprobaciones de sintaxis no sustituyen
una medición de latencia real en snestorage.dev.
