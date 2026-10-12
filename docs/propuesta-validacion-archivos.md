# Propuesta para ValidateFile en FileService

Código para que lo incorpores tú al backend. No se han modificado servicios, controladores ni paquetes.

## 1. Contrato

Mantener `bool ValidateFile(IFormFile form, out string mime)` y añadir un `CancellationToken` opcional. Devuelve `true` y el MIME detectado cuando supera los controles; en caso contrario devuelve `false` y `string.Empty`.

El nombre del archivo selecciona qué formatos aceptamos, pero el lector comprueba el formato del contenido. No se utiliza `form.ContentType` como prueba de validez. Por ejemplo, un ejecutable llamado `avatar.png` no pasa el decodificador de imágenes. También rechazamos un JPEG llamado `.png`, aunque ambos formatos estén permitidos.

Después, `PrepareFile` busca ese MIME en `CacheService.GetAllFileTypesByMime` y obtiene el `FileType` real de tu tabla. Esta separación permite que la parte de inspección siga siendo estática y no consulte la base de datos.

## 2. Bibliotecas

Ya tienes SkiaSharp 4.153.1 y MediaInfo.Wrapper.Core 26.10.0. Para los comprimidos, la propuesta usa SharpCompress 0.50.5:

```powershell
dotnet add SNEStorage/SNEStorage.csproj package SharpCompress --version 0.50.5
```

SharpCompress es una biblioteca de C# que cubre ZIP, RAR, 7z, TAR y GZIP. No necesita lanzar un ejecutable externo. [Paquete oficial](https://www.nuget.org/packages/SharpCompress/0.50.5).

SkiaSharp y MediaInfo usan bibliotecas nativas, aunque no lancemos procesos externos. Estas dependencias deben publicarse para la arquitectura del hosting. Un fallo al cargar esas bibliotecas es un problema de despliegue, no una prueba de que el archivo del usuario sea inválido.

## 3. Código para incorporar dentro de FileService

Añade estos `using` al archivo:

```csharp
using MediaInfo;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;
using SkiaSharp;
```

Los límites de 250 MiB para archivos y 5 MiB para imágenes/videos corresponden a los actuales del proyecto. Los límites de dimensiones, frames y entradas son una propuesta ajustable; no estaban definidos por ti.

```csharp
private const long MaxUploadBytes = 250L * 1024 * 1024;
private const long MaxMediaBytes = 5L * 1024 * 1024;
private const int MaxImageSide = 4096;
private const long MaxImagePixels = 4_194_304;
private const int MaxImageFrames = 300;
private const long MaxImageFramePixels = 100_000_000;
private const int MaxVideoSide = 4096;
private const int MaxArchiveEntries = 10_000;

public static bool ValidateFile(
    IFormFile form,
    out string mime,
    CancellationToken ct = default)
{
    mime = string.Empty;
    ct.ThrowIfCancellationRequested();

    if (form.Length <= 0 || form.Length > MaxUploadBytes)
        return false;

    string? expectedMime = GetExpectedMime(form.FileName);
    if (expectedMime is null)
        return false;

    bool isImage = expectedMime.StartsWith("image/", StringComparison.Ordinal);
    bool isVideo = expectedMime.StartsWith("video/", StringComparison.Ordinal);

    if ((isImage || isVideo) && form.Length > MaxMediaBytes)
        return false;

    // IFormFile recibido mediante multipart usa un stream con buffering.
    // No copiar el archivo completo a un MemoryStream adicional.
    using var stream = form.OpenReadStream();
    if (!stream.CanRead || !stream.CanSeek || stream.Length != form.Length)
        return false;

    string? detectedMime = isImage
        ? ValidateImage(stream, ct)
        : isVideo
            ? InspectVideo(stream, ct)
            : InspectArchive(stream, ct);

    ct.ThrowIfCancellationRequested();
    if (detectedMime is null || detectedMime != expectedMime)
        return false;

    mime = detectedMime;
    return true;
}

private static string? GetExpectedMime(string fileName)
{
    return Path.GetExtension(fileName).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        ".avif" => "image/avif",
        ".mp4" => "video/mp4",
        ".webm" => "video/webm",
        ".ogg" or ".ogv" => "video/ogg",
        ".zip" => "application/zip",
        ".rar" => "application/vnd.rar",
        ".7z" => "application/x-7z-compressed",
        ".tar" => "application/x-tar",
        // .tar.gz también termina en .gz; el contenedor exterior es GZIP.
        ".gz" => "application/gzip",
        _ => null
    };
}

private static string? ValidateImage(Stream stream, CancellationToken ct)
{
    using var codec = SKCodec.Create(stream);
    if (codec is null)
        return null;

    string? mime = codec.EncodedFormat switch
    {
        SKEncodedImageFormat.Png => "image/png",
        SKEncodedImageFormat.Jpeg => "image/jpeg",
        SKEncodedImageFormat.Gif => "image/gif",
        SKEncodedImageFormat.Webp => "image/webp",
        SKEncodedImageFormat.Avif => "image/avif",
        _ => null
    };

    if (mime is null)
        return null;

    var source = codec.Info;
    if (source.Width <= 0 || source.Height <= 0 ||
        source.Width > MaxImageSide || source.Height > MaxImageSide)
        return null;

    long pixels = (long)source.Width * source.Height;
    if (pixels > MaxImagePixels)
        return null;

    // Algunos codecs reportan 0 para una imagen estática.
    int frames = Math.Max(1, codec.FrameCount);
    if (frames > MaxImageFrames || pixels * frames > MaxImageFramePixels)
        return null;

    var info = new SKImageInfo(
        source.Width, source.Height,
        SKColorType.Rgba8888, SKAlphaType.Premul);

    using var bitmap = new SKBitmap(info);
    if (bitmap.GetPixels() == IntPtr.Zero)
        throw new InvalidOperationException("Could not allocate the image buffer.");

    for (int frame = 0; frame < frames; frame++)
    {
        ct.ThrowIfCancellationRequested();
        bitmap.Erase(SKColors.Transparent);

        // Permitir que el codec reconstruya los frames previos necesarios.
        var options = new SKCodecOptions(frame) { PriorFrame = -1 };
        var result = codec.GetPixels(
            info, bitmap.GetPixels(), bitmap.RowBytes, options);

        // IncompleteInput no cuenta como imagen válida.
        if (result != SKCodecResult.Success)
            return null;
    }

    return mime;
}

private static string? InspectVideo(Stream stream, CancellationToken ct)
{
    ct.ThrowIfCancellationRequested();

    // API síncrona disponible en tu versión. MediaInfoWrapper no implementa
    // IDisposable; el stream sí se cierra en ValidateFile.
    var media = new MediaInfoWrapper(stream);
    ct.ThrowIfCancellationRequested();

    if (!media.Success || !media.HasVideo || media.VideoStreams.Count == 0)
        return null;

    // Revisar todas las pistas, no solo la principal.
    if (media.VideoStreams.Any(video =>
        video.Width <= 0 || video.Height <= 0 ||
        video.Width > MaxVideoSide || video.Height > MaxVideoSide))
        return null;

    return media.Format switch
    {
        "MPEG-4" => "video/mp4",
        "WebM" => "video/webm",
        "Ogg" => "video/ogg",
        _ => null
    };
}

private static string? InspectArchive(Stream stream, CancellationToken ct)
{
    ct.ThrowIfCancellationRequested();

    // IsArchive inspecciona el contenido; no recibe la extensión como prueba.
    if (!ArchiveFactory.IsArchive(stream, out var archiveType))
        return null;

    string? mime = archiveType switch
    {
        ArchiveType.Zip => "application/zip",
        ArchiveType.Rar => "application/vnd.rar",
        ArchiveType.SevenZip => "application/x-7z-compressed",
        ArchiveType.Tar => "application/x-tar",
        ArchiveType.GZip => "application/gzip",
        _ => null
    };

    if (mime is null)
        return null;

    stream.Position = 0;
    using var archive = ArchiveFactory.OpenArchive(stream, new ReaderOptions
    {
        LeaveStreamOpen = true,
        LookForHeader = false
    });

    if (archive.Type != archiveType || archive.IsEncrypted)
        return null;

    int entries = 0;
    bool hasFile = false;
    foreach (var entry in archive.Entries)
    {
        ct.ThrowIfCancellationRequested();

        if (++entries > MaxArchiveEntries || entry.IsEncrypted ||
            !entry.IsComplete || entry.IsSplitAfter)
            return null;

        hasFile |= !entry.IsDirectory;
    }

    return hasFile ? mime : null;
}
```

La función devuelve `false` para los rechazos normales. Una biblioteca puede lanzar una excepción al leer un archivo corrupto; el límite de manejo de errores de `PrepareFile`, explicado debajo, también debe cubrir la validación. La cancelación debe conservarse como cancelación.

## 4. Conectar PrepareFile con tu caché y con cada uso

Reemplaza la consulta de `_context.FileTypes` al comienzo de `PrepareFile` por la validación y la búsqueda en caché. Incluye esta parte dentro del manejo de errores del método; llamar a una biblioteca también puede fallar.

Para evitar que un ZIP válido se acepte como avatar, añade `IReadOnlySet<string> allowedMimes` a los argumentos de `PrepareFile`, antes de `CancellationToken ct`. Cada servicio pasa el conjunto que corresponde a su operación.

```csharp
FileType? type;
try
{
    string? expectedMime = GetExpectedMime(fileRequest.FileName);
    if (expectedMime is null || !allowedMimes.Contains(expectedMime) ||
        !ValidateFile(fileRequest, out var mime, ct) ||
        !allowedMimes.Contains(mime))
    {
        await BatchRollback(batchPath, CancellationToken.None);
        return null;
    }

    var types = await _cacheService.GetAllFileTypesByMime(ct);
    if (types is null || !types.TryGetValue(mime, out type))
    {
        await BatchRollback(batchPath, CancellationToken.None);
        return null;
    }
}
catch (OperationCanceledException) when (ct.IsCancellationRequested)
{
    await BatchRollback(batchPath, CancellationToken.None);
    throw;
}
catch (Exception exception) when (exception is not OutOfMemoryException)
{
    // Registrar exception con ILogger<FileService> para distinguir corrupción,
    // formatos no soportados y problemas de carga de bibliotecas nativas.
    await BatchRollback(batchPath, CancellationToken.None);
    return null;
}

// Continúa el código de escritura y creación del modelo que ya tienes.
// FileTypeId = type.Id;
```

Pasa un token independiente a la limpieza: si `ct` ya está cancelado, usarlo para leer el manifiesto puede impedir el rollback. Este cambio también corresponde a tus otros caminos de limpieza; aquí se muestra solamente la conexión del validador.

Para implementar el log del ejemplo, añade `ILogger<FileService> logger` al constructor y guarda `_logger`; en el último catch:

```csharp
_logger.LogWarning(exception, "File validation failed.");
```

Ejemplos de conjuntos para declarar en FileService:

```csharp
public static readonly IReadOnlySet<string> AvatarMimes =
    new[] { "image/png", "image/jpeg" }.ToFrozenSet(StringComparer.Ordinal);

public static readonly IReadOnlySet<string> PreviewMimes = new[]
{
    "image/png", "image/jpeg", "image/gif", "image/webp", "image/avif",
    "video/mp4", "video/webm", "video/ogg"
}.ToFrozenSet(StringComparer.Ordinal);

public static readonly IReadOnlySet<string> ResourceFileMimes = new[]
{
    "application/zip", "application/vnd.rar", "application/x-7z-compressed",
    "application/x-tar", "application/gzip"
}.ToFrozenSet(StringComparer.Ordinal);
```

Esos conjuntos requieren `using System.Collections.Frozen;`.

```csharp
// AccountService: avatar
await _fileService.PrepareFile(
    avatar, path, batchPath, FileService.AvatarMimes, ct);

// ResourceService: archivo principal
await _fileService.PrepareFile(
    dto.File, path, batchPath, FileService.ResourceFileMimes, ct);

// ResourceService: cada preview, dentro del flujo que ya tienes
await _fileService.PrepareFile(
    preview, path, batchPath, FileService.PreviewMimes, ct);
```

La implementación retorna los MIME canónicos `application/zip` y `application/gzip`. Tu tabla ya contiene esos valores. No necesita escoger los alias enviados por Opera/Windows como `application/x-zip-compressed`.

## 5. Qué garantiza y qué no

| Tipo | Controles implementados | Límite de la comprobación |
| --- | --- | --- |
| Imagen | Formato detectado, tamaño, dimensiones y decodificación de todos los frames reportados por el codec | No elimina metadatos ni bytes adicionales; no es un análisis antivirus |
| Video | Contenedor reconocido, presencia de pistas de video y dimensiones | MediaInfo no decodifica todos los frames: un video parcialmente corrupto puede pasar; tampoco garantiza un codec compatible con todos los navegadores |
| Comprimido | Contenedor reconocido, catálogo legible, entradas completas según la biblioteca, límite de entradas y rechazo de cifrado/volúmenes incompletos | No descomprime los archivos interiores ni comprueba todos sus CRC; corrupción dentro de una entrada puede pasar |

**El `true` significa que cumple estos controles, no que todo el archivo está certificado como íntegro o libre de malware.** Para una inspección profunda de comprimidos habría que leer los datos descomprimidos con límites de salida y trabajo; hacerlo sin límites abriría la posibilidad de bombas de descompresión y alargaría las subidas. Para video completo hace falta un decodificador, no solo MediaInfo.

El caso `.tar.gz` identifica y comprueba el contenedor GZIP; no verifica que su carga interior sea realmente un TAR. Si quieres exigir también esa correspondencia, es una segunda validación sobre el contenido descomprimido con límites.

El rechazo de archivos cifrados, comprimidos vacíos y volúmenes incompletos es una política de esta propuesta; puedes decidir admitirlos, pero no inspeccionaríamos su contenido con este código.

AVIF requiere que el codec esté disponible en los binarios nativos publicados. El enum existe en tu SkiaSharp 4.153.1; si `SKCodec.Create` no puede abrirlo, se rechaza y no se sustituye la comprobación por una revisión de la extensión.

Los chequeos de cancelación se hacen entre operaciones. No pueden interrumpir una llamada nativa síncrona ya en curso. Los límites de frames/píxeles reducen el trabajo, pero algunos frames animados reconstruyen frames anteriores: no representan un límite exacto de CPU. La API síncrona de MediaInfo está marcada como obsoleta en el README de tu paquete; se conserva aquí para respetar la firma bool/out. Si migras a la API async, el resultado deberá ser una tupla, un DTO o `FileType?`, ya que los métodos async no admiten parámetros `out`.

## 6. Fuentes y estado

APIs contrastadas con la documentación XML instalada de SkiaSharp 4.153.1 y MediaInfo.Wrapper.Core 26.10.0, y con las interfaces de SharpCompress 0.50.5. Es una propuesta pendiente de compilación y pruebas por tu parte; no se ejecutaron tests ni se instalaron paquetes.

- [SKCodec: formato, frames y decodificación](https://github.com/mono/SkiaSharp/blob/main/binding/SkiaSharp/SKCodec.cs).
- [MediaInfoWrapper: lectura desde Stream y metadatos](https://github.com/yartat/MP-MediaInfo/blob/master/MediaInfo.Wrapper/MediaInfoWrapper.cs).
- [SharpCompress 0.50.5: detección y apertura de archivos](https://github.com/adamhathcock/sharpcompress/blob/0.50.5/src/SharpCompress/Archives/ArchiveFactory.cs).
- [SharpCompress 0.50.5: entradas de archivos](https://github.com/adamhathcock/sharpcompress/blob/0.50.5/src/SharpCompress/Archives/IArchiveEntry.cs).
