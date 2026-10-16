# Reproductor SPC y biblioteca local

## Uso

- Abrir Tools → SPC Player (`/Tools/SPCPlayer`). No requiere cuenta.
- Elegir varios SPC o arrastrarlos al área de importación. Se comprueba la extensión, tamaño y contenido mediante inspectSpc, como en las previews.
- Cada archivo se guarda en IndexedDB, en este navegador y origen. No se transmite al servidor ni pasa por Blazor/.NET.
- Se deduplican archivos por SHA-256. El título, juego y duración proceden de las etiquetas SPC cuando existen; se usa el nombre del archivo si no hay título.
- Play/Pause, Stop, Previous/Next, volumen, shuffle y repeat (off/all/one).
- Para configurar repeticiones, cada fila de la biblioteca tiene un botón visible **Playback**. El editor guía: elegir duración, elegir cuántas veces y pulsar **Apply to this song**. Las opciones dicen 1 time, 2 times, etc., e incluyen un resumen de la duración total. El contador representa reproducciones totales: 2 significa dos en total.
- Si no hay duración utilizable, se explica por qué el contador está deshabilitado y se ofrece **Set a length to enable repetitions**, que activa la duración personalizada. Si Repeat current song está activo, aparece un aviso y un botón para desactivarlo y respetar el contador de esa canción.
- Los botones de cada fila permiten reproducir, subir/bajar en el orden y eliminar. Clear playlist exige confirmación dentro de la vista.
- Se leen tiempos ID666 y xid6 (intro, bucle, final, fade y contador de bucles). No se presenta esto como detección del final musical. Si las etiquetas son inexactas, cada canción tiene un botón de ajustes con Use SPC tags, Custom length y Until I skip, más un contador de reproducciones o bucles. Con xid6 se amplía el bucle sin reiniciar la intro; sin bucle conocido se repite el snapshot completo según la duración elegida.
- Los SPC sin duración se reproducen hasta Next/Stop por defecto. Se puede habilitar expresamente una duración predeterminada en Playback & storage. Se respeta el fade indicado en el archivo; la duración personalizada usa un fade breve.
- Las canciones ya importadas actualizan sus metadatos al reproducirlas, conservando sus archivos y preferencias. No se requiere volver a importarlas ni cambiar el esquema de IndexedDB.
- Al salir de la vista se muestra el reproductor flotante. Se puede desplegar la cola, pausar/cambiar de pista o detener y ocultar el reproductor.
- El nuevo botón Minimize player añade un tercer tamaño: una barra de tres iconos (restaurar, play/pausa y cerrar). Conserva el estado anterior de la cola al restaurar.
- La barra de posición es arrastrable, con saltos de un segundo. El salto se aplica al soltar; no fuerza Play cuando estaba pausado. El avance del emulador cede periódicamente al navegador para permitir Stop y la interacción durante saltos largos. Con duración desconocida, la ventana del cursor se amplía conforme avanza la reproducción.
- La biblioteca y preferencias sobreviven a la recarga. La reproducción requiere Play tras abrir un documento nuevo; no se promete audio ininterrumpido durante F5, login/logout con navegación completa, cierre de la pestaña o suspensión del navegador/dispositivo.
- Borrar datos del sitio borra la biblioteca. No hay sincronización entre dispositivos/cuentas; conservar los SPC originales.

## Almacenamiento y límites

Base IndexedDB `snestorage-spc-library`, versión 1:

- tracks: metadatos, orden y hash como ID.
- files: Blob original, separado para no leer todos los archivos al restaurar la biblioteca.
- settings: volumen, repetición, shuffle, duración predeterminada y canción seleccionada.

Metadatos y archivo se añaden/borran en la misma transacción de IndexedDB. La interfaz solo anuncia que un archivo se guardó tras completar esa transacción. Si el navegador bloquea el almacenamiento o se agota el espacio se muestra un error; no se simula un guardado permanente.

Límites: 5 MB por archivo, 1.000 canciones y 100 MB por biblioteca. Los permisos y cuotas reales del navegador pueden ser menores. Las importaciones se procesan secuencialmente y se preservan los archivos ya guardados si uno falla. Solo se lee el Blob de la canción elegida al reproducir.

## Integración

- `Components/Pages/SPCPlayer.razor` y su code-behind: página pública y montaje de los controles nativos.
- `Components/Shared/SpcPlaylistHost.razor` y code-behind: montaje persistente del dock en MainLayout, fuera de PageAccessGuard.
- `wwwroot/js/spc-playlist.js?v=4`: estado del documento, controles, cola y reproducción.
- `wwwroot/js/spc-timing.js`: lectura de metadatos de duración. Especificación: https://snesmusic.org/files/spc_file_format.txt. Las etiquetas describen tiempos de reproducción; un dump puede tener datos incorrectos o carecer de una duración.
- `wwwroot/js/spc-library.js`: almacenamiento local.
- `wwwroot/js/spc-engine.js`: se extrajeron el loader/AudioContext y la limpieza del motor existente, sin cambiar el WASM ni el proveedor.
- `wwwroot/js/audio-focus.js`: un propietario de audio por documento. Otra preview pausa la playlist; reproducir la playlist detiene la preview anterior. El audio HTML de otros elementos también interrumpe al propietario.
- `audio-preview.js?v=4`: usa el motor compartido; conserva sus controles y su limpieza al salir de la preview.
- `css/spc-player.css?v=4`: estilos acotados por clase para página/dock, modo mínimo, cursor, móvil y foco por teclado.

Las selecciones, nombres y errores de archivos se insertan mediante textContent. Solo las plantillas e iconos constantes usan innerHTML. El motor no se carga antes de Play. Un único temporizador de progreso funciona mientras la playlist reproduce; desmontar la página no lo cancela. Se cancela al pausar, terminar o cerrar el documento. La cola del dock no se genera mientras está oculta o colapsada.

## Menú lateral

- Community está colapsado inicialmente. Community y Donations siempre son visibles; Users se muestra al expandir.
- Tools está colapsado inicialmente y oculta todos sus enlaces. SPC Player aparece antes de Apply IPS/BPS.

## Comprobación

Compilación del proyecto y comprobación sintáctica de los módulos JavaScript. No se ejecutaron tests ni reproducción real de SPC en el navegador durante esta implementación. El comportamiento de audio, almacenamiento, navegación y tamaños móviles requiere comprobación en el navegador del usuario.
