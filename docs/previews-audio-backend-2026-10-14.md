# Previews de audio: guía de backend

Fecha: 2026-10-14. El frontend ya admite audio común, SPC y BRR en Create, Edit y
la vista individual. **Este documento es una propuesta para aplicar tú; no se ha
modificado el backend ni la base de datos.** BNK queda fuera por ahora.

**Estado de la revisión posterior:** ya están implementados en FileService el
contexto de audio, su límite de 5 MiB, InspectAudio e InspectSnesSound. ValidateFile
ya llama al inspector SPC/BRR antes del inspector de audio común. El código de abajo
queda como referencia; no hace falta volver a pegarlo. No se han consultado los
registros de FileType ni se ha realizado una subida contra la aplicación.
Queda el ajuste de metadata del apartado 6 para distinguir audio/video OGG.

El límite sigue siendo **20 previews, 5 MiB por archivo**, contando imágenes,
videos y sonidos juntos. Los avatares siguen aceptando solo imágenes; el archivo
principal del recurso sigue siendo un comprimido. No necesitas nuevos NuGets.

## 1. Añadir los MIME a FileType

| Extensiones | Name | Value |
|---|---|---|
| .mp3 | MP3 | audio/mpeg |
| .wav | WAV | audio/wav |
| .ogg, .oga, .opus | OGG AUDIO | audio/ogg |
| .flac | FLAC | audio/flac |
| .m4a | M4A | audio/mp4 |
| .aac | AAC | audio/aac |
| .weba | WEBA | audio/webm |
| .spc | SPC | audio/x-spc |
| .brr | BRR | audio/x-brr |

SPC y BRR usan tipos propios del proyecto; el navegador no los reproduce de forma
nativa. OGG puede contener audio o video: conserva también el registro `video/ogg`.

Puedes ejecutar este SQL en la DB local y en producción. Inserta solo valores
que no existen, deja los IDs a la columna identity y no modifica registros actuales:

```sql
INSERT INTO dbo.file_type ([name], [value])
SELECT types.[name], types.[value]
FROM (VALUES
    ('MP3', 'audio/mpeg'),
    ('WAV', 'audio/wav'),
    ('OGG AUDIO', 'audio/ogg'),
    ('FLAC', 'audio/flac'),
    ('M4A', 'audio/mp4'),
    ('AAC', 'audio/aac'),
    ('WEBA', 'audio/webm'),
    ('SPC', 'audio/x-spc'),
    ('BRR', 'audio/x-brr')
) AS types ([name], [value])
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.file_type existing WHERE existing.[value] = types.[value]
);
```

Después refresca la caché de FileType o reinicia la aplicación. Los nombres caben
en `varchar(10)` y los MIME en `varchar(50)`: no hace falta cambiar ese esquema.

El selector normaliza el ContentType de estos archivos a los valores de la tabla,
porque Windows/navegadores pueden enviar SPC/BRR como application/octet-stream.
Esto solo prepara la petición: **la validación real sigue haciéndose en el servidor**.
Clientes externos deben enviar los MIME admitidos; no se acepta cualquier audio/*.

## 2. FileService.ValidateContext

Añade este helper pequeño a FileService:

```csharp
private static bool IsPreviewAudio(string mime)
{
    return mime is "audio/mpeg" or "audio/wav" or "audio/ogg" or
        "audio/flac" or "audio/mp4" or "audio/aac" or "audio/webm" or
        "audio/x-spc" or "audio/x-brr";
}
```

En el switch de ValidateContext, cambia únicamente Previews:

```csharp
FileContext.Previews =>
    contentType.StartsWith("image/", StringComparison.Ordinal) ||
    contentType.StartsWith("video/", StringComparison.Ordinal) ||
    IsPreviewAudio(contentType),
```

## 3. FileService.ValidateFile

Después de obtener expectedMime y de declarar isImage/isVideo, añade:

```csharp
bool isAudio = IsPreviewAudio(expectedMime);
```

La comprobación del tamaño debe incluir audio:

```csharp
if ((isImage || isVideo || isAudio) && form.Length > MAX_MEDIA_FILE_SIZE)
    return ServiceResult.MediaSizeSurpassTheLimit.Return(_logger);
```

Reemplaza la selección del inspector por:

```csharp
OneOf<string, ServiceResultContext> inspectionResult;

if (isImage)
    inspectionResult = InspectImage(stream, ct);
else if (isVideo)
    inspectionResult = await InspectVideo(stream, ct);
else if (expectedMime is "audio/x-spc" or "audio/x-brr")
    inspectionResult = await InspectSnesSound(stream, expectedMime, ct);
else if (isAudio)
    inspectionResult = await InspectAudio(stream, ct);
else
    inspectionResult = InspectArchive(stream, ct);
```

Conserva el TryPickT0, MimeEquals y el resultado Success del final de tu método.
Si no añades esas ramas, ValidateFile intentará abrir los sonidos como comprimidos.

## 4. InspectAudio para los formatos comunes

Reutiliza el mismo MediaInfo.Analysis que ya usa InspectVideo. Los resultados
HasAudio, HasVideo, AudioStreams y General.Format existen en tu paquete 26.10.0.
Uso InvalidFile para no obligarte a crear un error nuevo; puedes añadir InvalidAudio
después si quieres un mensaje más específico.

```csharp
private async Task<OneOf<string, ServiceResultContext>> InspectAudio(
    Stream stream, CancellationToken ct = default)
{
    if (ct.IsCancellationRequested)
        return ServiceResult.RequestCancelled.Return(_logger);

    var mediaResult = await _dbHelperService.TryFunction(
        cancel => _mediaAnalyzer.AnalyzeAsync(stream, true, cancel),
        ServiceResult.InvalidFile, ct: ct);

    if (!mediaResult.TryPickT0(out var media, out var mediaError))
        return mediaError;

    if (!media.Success || !media.HasAudio || media.HasVideo ||
        !media.AudioStreams.Any())
        return ServiceResult.InvalidFile.Return(_logger);

    string? mime = media.General.Format switch
    {
        "MPEG Audio" => "audio/mpeg",
        "Wave" => "audio/wav",
        "Ogg" => "audio/ogg",
        "FLAC" => "audio/flac",
        "MPEG-4" => "audio/mp4",
        "ADTS" or "ADIF" => "audio/aac",
        "WebM" => "audio/webm",
        _ => null
    };

    return mime is null ? ServiceResult.InvalidMime.Return(_logger) : mime;
}
```

Este análisis reconoce contenedores y streams; como tu inspector de video, no
decodifica cada muestra ni garantiza que cualquier navegador soporte su códec.
El frontend muestra un error de reproducción si el navegador no puede reproducirlo.
MP4/WebM con video deben enviarse con su MIME de video, no disfrazados de audio.

## 5. InspectSnesSound para SPC y BRR

Este método lee como máximo los 5 MiB permitidos en ValidateFile, usa tu DBHelper
para los errores de lectura y comprueba la estructura sin ejecutar el archivo.
Añade `using System.Text;` al servicio.

```csharp
private async Task<OneOf<string, ServiceResultContext>> InspectSnesSound(
    Stream stream, string mime, CancellationToken ct = default)
{
    if (ct.IsCancellationRequested)
        return ServiceResult.RequestCancelled.Return(_logger);

    var readResult = await _dbHelperService.TryFunction(async cancel =>
    {
        stream.Position = 0;
        byte[] bytes = new byte[checked((int)stream.Length)];
        await stream.ReadExactlyAsync(bytes, cancel);
        return bytes;
    }, ServiceResult.InvalidFile, ct: ct);

    if (!readResult.TryPickT0(out var data, out var readError))
        return readError;

    if (mime == "audio/x-spc")
    {
        byte[] signature = Encoding.ASCII.GetBytes("SNES-SPC700 Sound File Data ");

        if (data.Length < 0x10180 ||
            !data.AsSpan(0, signature.Length).SequenceEqual(signature))
            return ServiceResult.InvalidFile.Return(_logger);

        return mime;
    }

    int offset = data.Length % 9 == 0 ? 0 : data.Length % 9 == 2 ? 2 : -1;

    if (offset < 0 || data.Length - offset < 9)
        return ServiceResult.InvalidFile.Return(_logger);

    int blocks = (data.Length - offset) / 9;

    for (int block = 0; block < blocks - 1; block++)
    {
        if (ct.IsCancellationRequested)
            return ServiceResult.RequestCancelled.Return(_logger);

        if ((data[offset + block * 9] & 1) != 0)
            return ServiceResult.InvalidFile.Return(_logger);
    }

    byte lastHeader = data[^9];

    if ((lastHeader & 1) == 0)
        return ServiceResult.InvalidFile.Return(_logger);

    bool loop = (lastHeader & 2) != 0;
    int loopOffset = offset == 2 ? data[0] | data[1] << 8 : 0;

    if (loop && offset == 2 &&
        (loopOffset % 9 != 0 || loopOffset >= blocks * 9))
        return ServiceResult.InvalidFile.Return(_logger);

    return mime;
}
```

SPC requiere la firma y el tamaño mínimo que acepta snes_spc (0x10180 bytes).
Eso valida un snapshot estructural, no si su música es buena o audible. BRR admite
bloques de 9 bytes, solos o con la cabecera de loop de 2 bytes usada por AddmusicK;
el último bloque debe tener el flag de fin y no puede haber datos después de un fin.
BRR no tiene una firma mágica: su validación es estructural.

## 6. Incluir el MIME en la respuesta de previews

En `ResourceService.GetResourceAllPreviews` ya incluyes File, pero falta FileType.
Añade la navegación siguiente:

```csharp
.Include(preview => preview.File)
    .ThenInclude(file => file!.FileType)
```

El frontend lee `preview.file.fileType.value`. Esto permite distinguir `audio/ogg`
de `video/ogg`, que pueden tener la misma extensión `.ogg`. No se necesita otra
ruta ni una migración. Para los otros formatos puede inferirse el reproductor por
extensión, pero OGG es ambiguo sin ese dato.

## Qué no necesitas cambiar

- ResourcePreview, sus relaciones ni los DTOs Previews: ya contienen IFormFile[].
- GetPreviews: ya llama PrepareFile con FileContext.Previews.
- Rutas: se mantienen los GET de metadata y `/file/{fileId}/download` protegidos.
- CacheService: cargará las filas nuevas al invalidar su caché/reiniciar.
- El formato de respuestas de error y el batch/rollback de archivos.

## Comportamiento del frontend ya preparado

- Create/Edit permiten elegir sonidos y escucharlos antes de subirlos.
- La vista individual usa los mismos controles para previews guardadas.
- El archivo se descarga únicamente al pulsar Play; SPC carga su WASM una vez.
- Hay Play/Pause, Stop, Restart, volumen y loop. Audio común y BRR permiten buscar
  una posición; SPC usa pausa/reinicio, sin seek en esta primera implementación.
- BRR incluye frecuencias de 8, 11.025, 16, 22.05 y 32 kHz. Reproduce la muestra,
  sin ADSR, afinación original ni Gaussian interpolation del DSP. El loop repite
  PCM decodificado; no emula cambios de historial BRR entre vueltas.
- Se detiene el audio al cambiar de preview, pestaña o página y al reproducir otro medio.
- El selector inspecciona audio soportado por el navegador, cabeceras SPC y bloques
  BRR antes del submit. La validación del servidor sigue siendo necesaria.
- El motor SPC, su licencia LGPL y su código fuente están en
  `wwwroot/lib/spc-player`; no se usa CDN.

La compilación del frontend es correcta. Falta aplicar estos cambios y comprobar
la subida/reproducción con tus archivos reales; todavía no se ha verificado ese flujo.

Referencias: [SMW Central SPC player](https://github.com/telinc1/smwcentral-spc-player),
[MediaInfo](https://mediaarea.net/en/MediaInfo),
[BRRtools](https://github.com/Optiroc/BRRtools).
