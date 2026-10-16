# Carga de noticias, recursos y navegación

Revisión del 16 de octubre de 2026. Mediciones públicas sin sesión contra
snestorage.dev. No se publicaron cambios ni se enviaron votos, formularios o
descargas. Las muestras corresponden a la versión desplegada antes de las
optimizaciones locales de esta revisión.

## Observado en producción

Se confirmó mediante el registro del navegador que Blazor conecta por
**WebSocket**. No se observó fallback a Long Polling ni errores de consola en
las navegaciones revisadas. No hace falta habilitar WebSocket para corregir
este caso: ya funciona.

Mediciones aproximadas mediante navegación real en un navegador sin sesión:

| Recorrido | Tiempo hasta título visible | Tiempo hasta primer elemento |
| --- | ---: | ---: |
| Community → News | 0,43 s | 2,19 s |
| News → Resources | 0,44 s | 3,07 s |
| News → Community | 0,79 s | Contenido estático |

Son muestras puntuales e incluyen la instrumentación de automatización. No son
percentiles ni una comparación controlada antes/después. La navegación al recurso
individual terminó correctamente, pero no produjo una medición de título fiable.

También se hicieron GET públicos directos con curl, reutilizando una conexión
HTTPS cuando se indica, sin cookies:

| Petición | Tiempo total observado | Tamaño |
| --- | ---: | ---: |
| Noticias, primera petición aislada | 1,94 s | 7.220 bytes |
| Noticias, conexión reutilizada | 0,29 s y 1,20 s | 7.220 bytes |
| Lista de recursos, máximo 5 | 0,29 s | 1.373 bytes |
| Recurso 4, datos principales | 0,37 s | 1.832 bytes |
| Autores del recurso 4 | 0,37 s | 575 bytes |
| Previews del recurso 4 | 0,83 s | 924 bytes |
| Community JSON, conexión reutilizada | 1,19 s | 3.371 bytes |

El tiempo hasta el primer byte incluye red, proxy, hosting y aplicación; **no
equivale al tiempo SQL**. Un archivo estático también mostró variación, por lo
que no se puede atribuir toda la demora a las queries. Los tamaños observados
son pequeños; reducir unos campos de estos DTOs no explica por sí solo una pausa
de varios segundos.

## Cambios aplicados en frontend

1. **Un monitor de sesión por lista de noticias.** NewsFeed comparte módulo y
   estado de cuenta mediante NewsFeedContext. Cada NewsReactions conserva sus
   contadores, voto y control de errores. Las noticias individuales mantienen su
   monitor independiente. Esto elimina imports, callbacks y limpieza JS por cada
   tarjeta. Las peticiones de perfil ya estaban coalescidas; el problema no eran
   diez consultas SQL de perfil, sino consumidores e interop repetidos.
2. **Lista de recursos:** inicia HTTP antes de esperar al monitor de cuenta. Se
   quitó un viaje a JS para cancelar la lectura anterior: getProfile ya cancela
   esa lectura en el navegador antes de iniciar la nueva. Los controles sensibles
   siguen cerrados hasta confirmar la edad y el backend mantiene su validación.
3. **Recurso individual:** beginResourceRead cancela lecturas obsoletas e inicia
   el GET en la misma llamada JS. El monitor y la cuenta se inicializan en paralelo,
   sin bloquear ese GET. Se quitaron los renders iniciales redundantes de las
   siete secciones, que ya nacen con estado Loading.
4. **JavaScript de lectura más ligero:** profile.js carga la validación de uploads
   solo cuando hay un avatar que validar. News carga CAPTCHA y el monitor de
   cuenta cuando se necesitan, sin bloquear la importación usada para leer la lista.
5. **Precarga de módulos por página:** Home precarga sus dos listas; News,
   Resources y ResourceDetails precargan el módulo que usa esa vista.
6. **Observadores de DOM:** Markdown escanea una sola vez los subárboles añadidos
   por un batch y omite cambios ajenos a sus fuentes. Los votos no vuelven a pintar
   contadores confirmados que Blazor ya dibujó; solo reconcilian una interacción
   optimista pendiente.
7. **Trabajo de presentación repetido:** ResourceListRow prepara autores, tipos,
   categorías y tags al recibir otro DTO; el carrusel no vuelve a construir y
   serializar su configuración por cada render de otra sección.

## Arranque de lecturas y formularios: segunda tanda

Implementación de frontend, sin cambios de contratos del backend:

- `read-startup.js` + `BrowserReadStartup` importan el módulo y ejecutan su primer
  GET en una misma llamada JS. Se aplica a noticias, noticia individual, recursos
  del landing, lista general, recurso individual, comprobación de acceso y formularios.
  No hace falta esperar el retorno de import al servidor para iniciar HTTP.
- `beginResourceRead` inicia las siete lecturas secundarias al recibir un GET
  principal exitoso. Las invocaciones posteriores consumen esas promesas, sin
  repetir los GET. El historial completo sigue diferido hasta abrir su tab.
- El editor inicia sus colecciones y opciones juntas en el navegador, devolviendo
  los resultados de arranque en una sola llamada, y conserva el control de errores
  de cada sección. Reutiliza el ResourceReadDTO ya comprobado por PageAccessGuard.
- PageAccessGuard recibe la cuenta y, en gestión de recursos, la respuesta del
  recurso sin un viaje intermedio a Blazor. Mantiene sus comprobaciones de cuenta,
  staff y autor/submitter antes de permitir el formulario.
- El guard precarga los módulos del formulario mientras muestra Checking access.
  Tras confirmar la cuenta adelanta el catálogo público de opciones.
- `resource-options.js` comparte la petición en curso y conserva solamente un
  catálogo público exitoso durante cinco minutos en memoria de esa pestaña.
  Se utiliza en filtros, Create y Edit; los errores permiten reintentar. Recargar
  el documento descarta esa caché. No se usa almacenamiento de servidor.
- La lectura inicial ya no depende de cargar los validadores de archivos y CAPTCHA.
  Se importan al validar/enviar; las validaciones y la protección de los POST siguen activas.
- Las respuestas iniciales mayores a 12 KiB se transportan mediante IJSStreamReference
  (límite 8 MiB), evitando agruparlas en un mensaje SignalR grande. Las pequeñas
  permanecen inline. Esto también se aplica a reintentos del bootstrap del editor
  y a las nuevas comprobaciones de acceso.
- El transporte de arranque cancela lecturas al abandonar la vista y elimina sus
  referencias temporales. Los POST de votos siguen con su duración independiente.

Las promesas de las secciones pertenecen a la carga de ese recurso, no son una
caché de recursos entre visitas. No se añadieron renderizadores HTML sin sanear.
El seguimiento de los borradores y archivos permanece en los componentes actuales.

La compilación y las comprobaciones de sintaxis pasan. No hay medición posterior
en producción todavía ni se ejecutó la suite de tests. El router sigue siendo
Interactive Server: no se promete una navegación sin latencia de red.

Para la parte que corresponde al servidor, consultar
[mejoras-backend-carga-2026-10-16.md](mejoras-backend-carga-2026-10-16.md).

## Interacciones que esperaban al circuito

Se confirmó en el código que los clics del lateral, dropdowns y pestañas se enviaban
como eventos de Blazor Server. Abrir un menú exigía enviar el evento al servidor,
renderizar su estado y, en algunos casos, otra llamada JS para colocar o enfocar el
control. No había una consulta SQL en esos toggles; aun así dependían de la red.

Se trasladaron estas interacciones al navegador:

- Grupos y minimización del menú lateral, manteniendo sus enlaces siempre visibles
  y los grupos colapsados inicialmente.
- Pickers: click/hover, cierre exterior/Escape, enfoque, posición y cierre al elegir
  un videojuego. Se eliminaron la referencia .NET y los callbacks por picker.
- Tabs de Create/Edit, Previous/Next y progreso. Los inputs y archivos permanecen
  montados; una validación puede revelar su tab mediante un comando explícito.
- Overview/Version history: cambio visual local; el historial conserva su carga
  diferida y el editor solicita sus previews al abrir ese tab por primera vez.
- Abrir/cerrar filtros de recursos. Sus opciones se solicitan una vez por instancia
  de página, con reintento explícito si fallan.
- Read more de autores/tipos/categorías con un botón local al final de los elementos; razones en moderación
  mediante details; mostrar/ocultar contraseña en recuperación de cuenta.

El estado visual es propiedad del DOM. Razor declara atributos iniciales constantes
para no devolver el control a un estado anterior al llegar un render del servidor.
Los datos, búsquedas, permisos, validaciones y escrituras conservan sus responsables.
Los cambios no envían ni cancelan votos. No se guardó estado de permisos en el cliente.

Se redujeron tamaños, paddings, bordes y sombras del lateral y los pickers; se usa una
tipografía legible y compacta en navegación. Los nombres largos siguen envolviéndose y
los controles de selección mantienen objetivos de 44 px en dispositivos táctiles.
El catálogo de tipos se agrupa y ordena cuando cambian las opciones, evitando repetir
ese trabajo al editar otros campos.

Esto elimina las esperas de red para esos cambios visuales. La navegación de página
completa sigue usando el router Interactive Server; migrar los toggles no convierte
el router ni las consultas HTTP en operaciones locales. No se midió todavía esta
versión desplegada: los tiempos de la primera sección siguen siendo la referencia
anterior a estos cambios.

No se cambiaron servicios, controladores, permisos, índices o migraciones. Se añadió únicamente la caché temporal del catálogo público
en el navegador; los recursos, noticias y permisos no se guardan en esa caché. Los POST de votos siguen siendo independientes del
desmontaje de una tarjeta y conservan keepalive y aviso de error en segundo plano.
No se añadieron retrasos, transiciones que oculten la espera ni recargas completas.

## Próximas mejoras por prioridad

### 1. Agrupar la carga secundaria del recurso

Hoy el GET principal se sigue de siete acciones: autores, autores externos,
dependencias, dependencias externas, última versión, puntuación y previews.
Cada acción protegida vuelve a validar JWT y consultar acceso. En Program.cs,
OnTokenValidated llama a AccountService.ValidateJwt, que consulta SecurityStamp.
ResourceAccessService.GetViewAccess también consulta la DB. Esto importa más en
una sesión real que en las muestras anónimas anteriores.

Propuesta de contrato: **GET /api/resource/{id}/overview**, con las siete secciones
en un DTO de presentación. Mantener el GET principal reducido para mostrar el
título/descripción rápido, y conservar las acciones actuales para reintentos
individuales y otros consumidores. El historial completo seguiría cargándose
únicamente al abrir su tab.

La implementación debe comprobar acceso una vez y proyectar los campos necesarios
en SQL. No quitar validaciones ni cachear decisiones de permisos para ahorrar
tiempo. No usar Task.WhenAll sobre consultas del mismo DbContext; una proyección
con colecciones/AsSplitQuery o consultas secuenciales mantiene ese contexto seguro.
El beneficio deberá medirse: un solo HTTP no convierte automáticamente todas las
consultas SQL en una sola.

Esta acción requiere cambios de backend del propietario; no está implementada en
esta revisión. El frontend actual sigue usando los contratos existentes.

### 2. Medir el recorrido con una sesión real y los tiempos SQL

Comparar TTFB, descarga y número de solicitudes en Network, junto con duración de
los comandos EF en el hosting. Revisar conexiones/reconexiones del circuito. Los
GET anónimos no miden el coste de validar la sesión ni todas las comprobaciones de
permisos. No compartir cookies, JWT, cabeceras Authorization o connection strings.

### 3. Reducir el límite de latencia de la navegación

Routes usa InteractiveServer global. Aun con JSON local y consultas instantáneas,
el clic necesita llegar al servidor, cambiar la ruta/renderizar y devolver el DOM.
Con WebSocket funcionando, ese coste permanece. Quitar por completo ese viaje
requiere navegación y renderizado en el cliente, por ejemplo un frontend Blazor
WebAssembly que consuma las API actuales. Es un cambio de arquitectura que debe
preservar autenticación, permisos, formularios y el reproductor SPC persistente;
no es activar un ajuste de hosting ni aplicar una animación al menú.

### 4. Revisar medios de marca para la primera visita

El logo de portada original pesa aproximadamente 1,36 MB; la caja original, 2,10
MB; el sello de calidad, 1,09 MB. La caja usada por el loader es otra imagen de
359 KB, con su capa de colores de 16 KB. Variantes de resolución de pantalla y
formatos web pueden reducir transferencia/decodificación inicial. Los originales
del logo se conservaron intactos en esta revisión.

## Validación

Compilación del proyecto y comprobación de sintaxis de módulos JS. No se ejecutó
la suite de tests, no se hicieron cambios en producción y aún no se midió la
versión con estas optimizaciones desplegada.
