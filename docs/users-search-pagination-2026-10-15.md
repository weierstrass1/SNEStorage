# Búsqueda de usuarios con avatares y paginación

## Contrato actual

`GET /api/user/search?likeString=nombre&maxAmount=20&page=0&withAvatar=true`

- Devuelve `List<UserNameDTO>` con ID, username, color y, cuando se solicita, Avatar.
- El límite del servidor es 20 por petición. `page` empieza en 0.
- Para listar todos, se envía likeString vacío: LikeQueryHelper omite el LIKE y mantiene el orden por username. El usuario ya ajustó el controlador a `[FromQuery] string? likeString = null` y pasa `likeString ?? string.Empty` al servicio.
- `withAvatar=true` incluye la proyección de Avatar; false la omite.
- UserController pasa los parámetros a UserService. GetLikeBySelectorQueryOperation valida la página y el offset usando long, y aplica Skip/Take en SQL.
- El usuario corrigió el ternario inicialmente invertido de withAvatar. No se editaron servicios, controlador ni DTOs desde el frontend.

## Interfaz `/Users`

- Al entrar y al vaciar el buscador se listan los usuarios por nombre. Para filtrar se requieren al menos 2 caracteres, con una espera de 350 ms al escribir. Con una sola letra se pide seguir escribiendo.
- Nombre con el color del usuario y enlace `/Profile/{id}`.
- Avatar con margen interior, carga diferida y un icono de usuario si falta o no puede cargarse. Se normaliza la ruta wwwroot usando accountAvatarUrl, como en el resto del sitio.
- Selector **Per page**: 20, 50, 100, 200 y All.
- Cada petición sigue solicitando **20 usuarios**. Para páginas mayores se añaden bloques al acercarse al final mediante scroll, con botón Load more users como alternativa.
- Previous/Next cambian la página visible cuando se completa. All continúa cargando por scroll hasta el final.
- Las páginas de 50 se ensamblan a partir de bloques de 20: el bloque que cruza el límite se conserva en memoria del navegador para empezar la siguiente página en la posición correcta.
- Los bloques ya leídos se conservan solo durante esa búsqueda, en esta vista. Cambiar el término o la cantidad por página reinicia la búsqueda. No se añadió caché de servidor.
- Menos de 20 resultados indica el final. Si el total es múltiplo exacto de 20, puede ser necesario abrir una página siguiente vacía para descubrirlo; Previous permite volver. No se solicita una consulta COUNT adicional.
- Se cancelan y descartan respuestas de búsquedas anteriores. No se reescribe el input al recibir opciones.
- Una página que repita únicamente usuarios ya cargados detiene la carga con error, evitando bucles.
- Los pickers de autores envían withAvatar=false y no solicitan más de 20 resultados.
- No se muestran correos en esta lista.

## Comprobación

Comprobación sintáctica de users.js y author-search.js y compilación del proyecto. No se ejecutaron pruebas automatizadas ni se comprobó la interacción contra la base de datos desde el navegador.
