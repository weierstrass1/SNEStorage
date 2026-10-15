# Aplicador local de BPS/IPS

## Uso

Página pública **`/Tools/ApplyPatches`**, accesible desde el menú lateral como **Apply BPS/IPS Patches**.

1. Elegir un ROM SNES `.sfc` o `.smc`, sin comprimir.
2. Elegir el parche `.bps` o `.ips`, previamente extraído de su ZIP.
3. Pulsar **Apply patch & download**. Se descarga una copia con el nombre del parche. El archivo original no cambia.

No necesita sesión, endpoints nuevos, migraciones ni registros en la base de datos.

## Procesamiento y privacidad

Los inputs son elementos HTML nativos, sin `InputFile`, enlaces de datos de Blazor ni formularios enviados al servidor. JavaScript recibe los archivos y los entrega a un Web Worker del navegador. Ningún contenido de ROM, parche o resultado se manda a HTTP, SignalR o .NET por este componente. La página sigue usando la conexión habitual de Blazor para su navegación y ciclo de vida.

El motor es una implementación independiente de BPS/IPS en JavaScript. **No ejecuta `flips.exe`**: ese archivo es un programa de Windows, no un módulo para navegadores. Tampoco se añadió una dependencia NuGet/npm ni se distribuyó el ejecutable adjunto.

El worker permite cancelar el procesamiento, se termina al salir de la página y tiene un límite de 60 segundos. Los enlaces Blob se liberan al cambiar archivos o abandonar la página. Los nombres y mensajes se insertan mediante `textContent`.

## Qué se comprueba

### ROM

- Extensión `.sfc` o `.smc` y contenido no vacío.
- Tamaño entre 32 KiB y 32 MiB, admitiendo 512 bytes adicionales de cabecera de copiador.
- Bancos completos de 32 KiB tras excluir esa cabecera.
- Cabecera interna reconocible: posiciones de LoROM/HiROM/ExLoROM/ExHiROM, modo de mapeo, códigos de tamaño, vector de reset y título o pareja de checksum/complemento.
- CRC32 del archivo e integridad del checksum interno SNES con suma reflejada.

Un checksum interno incorrecto se muestra como advertencia: hacks y homebrew pueden dejarlo desactualizado. **Estas comprobaciones reconocen estructura SNES; no prueban que el juego arranque ni identifican de forma infalible su procedencia.** No se ejecuta el código del ROM. No se admiten archivos comprimidos, dumps divididos ni formatos intercalados.

### BPS

- Firma BPS1 y CRC32 del parche completo según la especificación.
- Tamaños y metadatos acotados, números y lecturas dentro del archivo.
- Tamaño y CRC32 exactos del ROM base.
- Comandos SourceRead, TargetRead, SourceCopy y TargetCopy, incluyendo copias solapadas válidas.
- Límites de escritura, longitud final y CRC32 del resultado.
- Reconocimiento del caso en que el archivo ya coincide con el resultado del parche.

### IPS

- Firma PATCH, registros normales/RLE completos y marcador EOF.
- Tamaño final opcional de tres bytes y límites de lectura/escritura.
- Límite acumulado de trabajo para evitar millones de expansiones RLE repetidas.

**IPS no guarda el checksum del ROM base ni del resultado.** Una estructura válida no permite saber si se eligió el juego, región o revisión correctos. La interfaz explica esta diferencia.

Después de aplicar cualquiera de los formatos, el resultado vuelve a pasar la comprobación estructural de ROM antes de ofrecerse para descarga.

## Cabeceras y límites

- BPS automático prueba el archivo original y una alternativa quitando la cabecera de copiador existente o añadiendo una cabecera de 512 bytes vacíos. Solo acepta una alternativa cuyo tamaño y CRC coincidan exactamente.
- IPS automático conserva el ROM como fue seleccionado. El usuario puede conservar, añadir o quitar la cabecera siguiendo las instrucciones del autor; no se intenta adivinar mediante CRC porque IPS no lo proporciona.
- No se restaura automáticamente una cabecera eliminada para BPS. El resultado usa `.smc` si contiene cabecera de copiador y `.sfc` si no.
- ROM/resultado: máximo 32 MiB más cabecera. Parche: máximo 64 MiB. Trabajo IPS acumulado: máximo 256 MiB de escritura lógica.
- Un parche BPS que exige una cabecera no vacía concreta necesitará el ROM exacto; añadir 512 bytes vacíos no reproduce información desconocida.

## Archivos

- `Components/Pages/ApplyPatches.razor`, `.razor.cs`, `.razor.css`: página, inicialización y aspecto.
- `wwwroot/js/rom-patcher.js`: controles, descarga y limpieza.
- `wwwroot/js/rom-patcher-worker.js`: lectura y procesamiento local.
- `wwwroot/js/rom-patcher-core.js`: inspección, checksums y aplicación.
- `Components/Layout/SideNavigation.razor`: enlace público.

## Referencias del formato

- [Especificación BPS de byuu, de dominio público](https://github.com/Sir-Walrus/Flips/blob/master/bps_spec.md).
- [Floating IPS](https://github.com/Sir-Walrus/Flips).
- [Cabecera interna SNES](https://snes.nesdev.org/wiki/ROM_header) y [formatos de archivos ROM](https://snes.nesdev.org/wiki/ROM_file_formats).

## Comprobación realizada al implementar

Se revisó la sintaxis de los tres módulos JavaScript y se compiló la aplicación. No se ejecutó el binario de Windows ni se usaron ROMs reales. Queda pendiente probar el flujo completo en navegador con archivos propios, especialmente parches IPS con requisitos específicos de cabecera.
