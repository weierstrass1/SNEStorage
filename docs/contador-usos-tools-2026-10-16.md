# Contador global de usos de herramientas

## Comportamiento implementado

- Se cuenta una operación completada, no una visita ni un clic fallido.
- Apply IPS/BPS registra una unidad al generar correctamente el ROM parcheado.
- Descargar de nuevo el mismo resultado no suma otra unidad.
- El contador aparece dentro del botón de ejecución como `Uses: 1,234`.
- SPC Player queda excluido.
- Los archivos continúan en el navegador. Solo se envía una petición sin cuerpo
  para incrementar un total agregado; no nombres de archivos, ROMs ni patches.
- El total empieza en cero al habilitar el contador. No se pueden reconstruir
  usos anteriores que nunca se registraron.

## Contrato que espera el frontend

`GET /api/tools/apply-ips-bps/usage`

Devuelve HTTP 200 con `{ "count": 1234 }`.

`POST /api/tools/apply-ips-bps/usage`

Sin cuerpo. Devuelve HTTP 200 con `{ "count": 1235 }`. El frontend utiliza
el helper antiforgery existente y la cookie del mismo origen. Ambas acciones
deben admitir visitantes sin sesión; GET usa la política `reads` y POST `writes`.

## Persistencia en archivo, sin DB ni migraciones

Por elección del autor se utiliza un archivo de texto en el servidor:

`<ContentRootPath>/Storage/ToolUsage/apply-ips-bps.txt`

La carpeta se crea automáticamente cuando hay una consulta. El contador ausente
se considera cero y el archivo se escribe al completar el primer uso.

ToolUsageService toma un bloqueo exclusivo sobre `apply-ips-bps.lock` para
leer/incrementar. El bloqueo también protege procesos superpuestos durante un
reciclado de IIS, siempre que compartan la misma carpeta. El número nuevo se
escribe primero en `apply-ips-bps.pending`; solo después se reemplaza el archivo
principal. Una escritura fallida no trunca el total anterior. Si el archivo
principal tiene datos inválidos se devuelve un error, no se reinicia el contador.

El controlador usa ServiceResultContext/GetActionResult para errores, sin
intervención de DbContext ni DBHelperService. Los fallos de almacenamiento
devuelven 503. La ruta de herramienta es fija, no se transforma una clave del
visitante en una ruta de disco.

### MonsterASP y publicaciones

- La identidad de la aplicación necesita permiso de lectura/escritura en la carpeta.
- Los archivos de Storage/ToolUsage se excluyen de publicación y ya están
  cubiertos por la exclusión de Storage en .gitignore.
- Si el despliegue elimina archivos adicionales en el destino, preservar esa
  carpeta. Para aislarla del despliegue, configurar `ToolUsage:Directory` con una
  ruta absoluta persistente, fuera de wwwroot y del directorio reemplazado.
- Los reinicios conservan el total mientras se conserve el archivo. Borrarlo o
  desplegar en una carpeta vacía empieza un contador nuevo.
- Si hay varias máquinas, necesitan una carpeta compartida compatible con bloqueo
  de archivos para mantener un total único. Un archivo por máquina no lo mantiene.

## Fallos y límites

- Si falla el contador, el parche y su descarga siguen funcionando.
- No se reintenta automáticamente un POST: si se aplicó pero se perdió la
  respuesta, reenviarlo podría duplicar ese uso.
- El total es de usos reportados por el navegador, no de usuarios únicos ni una
  prueba de operaciones verificadas en servidor. El servidor no puede comprobar
  el parche sin recibir los archivos; un cliente modificado podría inventar usos.
- Antiforgery y rate limiting reducen abuso, no convierten este número en una
  estadística imposible de manipular.
- Privacy describe el contador agregado sin envío de archivos. El contacto
  apunta al Discord de SNEStorage.

## Archivos de implementación

- `Services/ToolUsageService.cs`: lectura, incremento y persistencia.
- `Controllers/ToolController.cs`: acciones públicas con límites de peticiones
  y el antiforgery global para escrituras.
- `DTOs/ToolUsageReadDTO.cs`: respuesta con el total.
- `Program.cs`: registro de ToolUsageService.
- `wwwroot/js/tool-usage.js`: consulta inicial y registro de un uso completado.
- `wwwroot/js/rom-patcher.js`: registra solo después de producir el resultado.
- `Components/Pages/ApplyPatches.*`: contador dentro del botón.
