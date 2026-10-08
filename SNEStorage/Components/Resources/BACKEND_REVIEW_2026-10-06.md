# Revisión de backend — 6 de octubre de 2026

Este informe conserva la revisión inicial, anterior a los cambios posteriores de
Resource y ResourceVersion. No es una lista actualizada de errores: posteriormente
el Seal of Quality pasó a ResourceVersion y el frontend se adaptó a ese contrato.
La documentación vigente de las vistas está en README.md.

Revisión de la copia actual de Resource, ResourceService, AccountService y FileService,
incluyendo sus DTOs, mapeos y controladores. No se modificó backend ni se ejecutaron
migraciones o pruebas contra la base de datos. Los cambios realizados son de frontend.

## 1. Bloqueos de compilación

- [FileService.cs](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/FileService.cs:45):
  las llamadas de las líneas 45, 53 y 65 son `BatchRollback(batchPath)`, pero la firma
  exige también `CancellationToken`. Debes ajustar esas llamadas o la firma.
- [AccountController.cs](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Controllers/AccountController.cs:69):
  llama `ConfirmUser(userId, token, ct)`, mientras
  [AccountService.cs](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/AccountService.cs:274)
  declara `ConfirmUser(string userId, string token64)`. Debes alinear ambas firmas.

Después de adaptar el formulario, la compilación sigue fallando solamente por estos
cuatro errores de backend. Se corrigieron los errores de frontend causados por los
nuevos miembros `required` y por el cambio de VideogameId a `long`.

## 2. RepositoryURL comparte la columna de Name

[ResourceBuild.cs](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Models/Builds/ResourceBuild.cs:62)
mapea RepositoryURL a `name`, que ya corresponde a Name. Además, las configuraciones
usan longitudes diferentes. Así no representa dos datos independientes y puede
fallar la validación del modelo de EF. Usa una columna propia, por ejemplo
`repository_url`, y revisa la migración generada para conservar la información existente.

## 3. Edit y EditVersion modifican entidades sin seguimiento

[Edit](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/ResourceService.cs:76)
obtiene el recurso mediante Get, cuya consulta usa AsNoTracking.
[EditVersion](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/ResourceService.cs:264)
también usa AsNoTracking. Modificar esos objetos y llamar SaveChanges no registra
automáticamente sus cambios. EditVersion puede devolver Success sin guardar nada;
Edit puede guardar archivos preparados sin guardar las nuevas relaciones del recurso.

Para escrituras, carga el recurso y las relaciones editables con seguimiento en el
mismo contexto; puedes conservar Get como consulta de lectura. Si el contexto tiene
NoTracking como configuración global, utiliza AsTracking en la consulta de edición.
Las relaciones retiradas deben quedar marcadas para eliminación, y las nuevas para
inserción. No soluciona esto hacer Update indiscriminadamente sobre todo el grafo.

Referencia: [seguimiento de entidades en EF Core](https://learn.microsoft.com/en-us/ef/core/change-tracking/explicit-tracking).

## 4. Las acciones de versiones no cargan autores

Las consultas de [AddVersion](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/ResourceService.cs:207)
y EditVersion incluyen versiones, pero no ResourcesAuthors. CheckResourceIsEditable
comprueba esa colección: estará vacía y rechazará a los autores reales.

Carga ResourcesAuthors en ambas consultas y valida los permisos antes de escribir.
Además, AddVersion asigna `Resource = resource` a una versión nueva y luego llama
`ResourceVersions.Add(version)` con el recurso desconectado. Si se supera el chequeo
de permisos, Add puede marcar ese recurso y sus versiones existentes para insertar
otra vez. Usa el recurso ya seguido o una asociación por ResourceId que no añada un
grafo existente como nuevo.

Referencia: [entidades desconectadas en EF Core](https://learn.microsoft.com/en-us/ef/core/saving/disconnected-entities).

## 5. Los filtros Remove conservan lo que debería eliminarse

[ResourceService.cs](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/ResourceService.cs:140)
usa `.Where(d => dto.RemovedTypeIDs.Contains(d.Id))` y el mismo patrón para previews,
dependencias, autores, autores externos y tags. Eso conserva exclusivamente las
relaciones que se pidieron eliminar. Si la lista Remove está vacía, descarta todas
las relaciones anteriores.

Para conservar las demás, el filtro debe ser `!dto.RemovedTypeIDs.Contains(d.Id)`
y sus equivalentes. Define también si esos arrays reciben el ID de la relación o
el ID de la entidad referenciada: ahora comparas el ID de la relación.

## 6. Edit cambia la relación inversa de dependencias

[ResourceService.cs](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/ResourceService.cs:165)
asigna las dependencias del DTO a `resource.Dependents`. Esa colección representa
los otros recursos que dependen de este. Debe editarse `resource.ResourceDependencies`,
que representa los recursos que este necesita, igual que hace Create.

## 7. DistinctBy con IDs todavía en cero pierde elementos nuevos

En Edit, DistinctBy(FileId) combina previews cuyos archivos nuevos aún tienen Id 0.
DistinctBy(Id) combina autores externos y dependencias externas nuevos con Id 0.
DistinctBy(TagId) tampoco diferencia correctamente relaciones nuevas cuyos IDs
todavía no se han propagado desde la navegación Tag.

El resultado puede contener solo uno de los elementos nuevos. Deduplica por una
clave disponible antes de guardar: el nombre normalizado del tag/autor, la identidad
de la dependencia externa que decidas admitir, o conserva todos los archivos nuevos
y deduplica únicamente los existentes con ID real. No guardes anticipadamente solo
para obtener IDs, porque rompería la operación conjunta que estabas preparando.

## 8. La limpieza asíncrona se ejecuta sin esperar

[DBHelperService.cs](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/DBHelperService.cs:37)
recibe `Action? ErrorCallback`. Las lambdas `async () => await BatchRollback(...)`
de Create/Edit se convierten en async void. TryFunction puede devolver el error
mientras el rollback sigue trabajando con el contexto o los archivos.

Si el callback hace trabajo asíncrono, usa `Func<Task>?` y espera su ejecución.
TrySyncContext ya espera BatchRollback; el problema restante está en TryFunction.

Además, los retornos ResourceTypeNotFound, UserNotFound y ResourceNotFound de
Create/Edit, después de preparar archivos y detectar IDs inexistentes, no llaman
rollback. Debes completar la limpieza antes de devolver esos errores.

## 9. No cancelar la limpieza con la petición que falló

BatchRollback lee el manifiesto con el mismo ct de la petición. Si está cancelado,
la lectura puede abortar y el catch termina dejando los archivos pendientes.
Usa un token independiente para rollback (por ejemplo CancellationToken.None o un
timeout propio); la operación de subida y consultas sí pueden usar el token de la
petición. Esto también aplica a RollbackAsync de la transacción de registro.

Mantener el manifiesto cuando la limpieza física falla sigue siendo compatible con
tu estrategia de mantenimiento manual. Esto es distinto de omitir el rollback o
devolver antes de que termine.

## 10. GetAuthors falla antes de devolver UserNotFound

[ResourceService.cs](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/ResourceService.cs:674)
usa DistinctBy(author => author.UserId), aunque la lista puede contener null cuando
no existe un usuario. Produce NullReferenceException y TryFunction lo transforma
en DBError; la comprobación posterior `authors.Any(t => t == null)` no llega a
ejecutarse. Conserva o devuelve explícitamente la señal de usuario inexistente
antes de deduplicar los autores válidos.

## 11. GetMainResourceFolder omite la carpeta del recurso

[ResourceService.cs](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/ResourceService.cs:703)
toma los dos primeros segmentos de la URL. Para
`Storage/Resources/{carpeta-del-recurso}/{carpeta-de-version}/archivo.zip`, devuelve
solo `Storage/Resources`. Las versiones y previews nuevas quedan fuera de la
carpeta propia del recurso. Conserva también el tercer segmento, o calcula la raíz
según la estructura de almacenamiento que defines. Comprueba la ausencia de una
versión o de su File antes de usar First y File!.

## 12. DTOs de edición: campos públicos y defaults

- [ResourceEditDTO.cs](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/DTOs/ResourceEditDTO.cs:19):
  RemoveTagIDs y RemovedTypeIDs son campos públicos. Para el model binding normal
  de formularios deben ser propiedades públicas con get/set, como los demás arrays.
- [ResourceVersionEditDTO.cs](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/DTOs/ResourceVersionEditDTO.cs:6):
  Version y ReleaseNotes también son campos, no propiedades. Si null significa
  "sin cambios", deberían ser propiedades `string?`; mantén
  DisplayFormat(ConvertEmptyStringToNull = false) si quieres distinguir el vacío.
- Los flags `bool?` de ResourceEditDTO tienen default false. Si se omite un flag,
  se recibe false y Edit lo aplica, en vez de conservar el valor. Para el contrato
  de modificación parcial, el default debe ser null.

Referencia: [model binding en ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/mvc/models/model-binding).

## Conexión con el frontend

- Create sigue enviando multipart a POST /api/resource/create/submit. Ahora incluye
  RepositoryURL y SealOfQualityRequested; todos los booleans se envían explícitamente.
  El campo Id viaja como 0 y el servicio asigna el ID real.
- Name: 2–50 caracteres; Description: mínimo 10; Version: obligatorio, máximo 20;
  ChangeLog: obligatorio. Las restricciones de interfaz no sustituyen las del backend.
- Se mantienen archivo de 250 MB, hasta 20 previews de 5 MB y los pickers existentes.
- ResourceDetails muestra UpdatedAt, revisión de normas, solicitud de revisión de
  calidad y SealOfQuality como estados independientes. El repositorio es un enlace
  HTTP/HTTPS; no se expone una ruta física de archivos.
- Los nuevos errores de ResourceServiceResult tienen mensajes específicos en inglés.
- ProfileController devuelve los mismos estados HTTP que consume la vista y ahora
  usa correctamente el bool de EditUserInfo; no requiere cambios de frontend por eso.
- Los DTOs y métodos de edición/versiones existen, pero ResourceController todavía
  no publica esas acciones. Las pantallas de edición siguen siendo conceptuales y
  no simulan un guardado. Faltan rutas, verbos HTTP y respuestas para conectarlas.
- Debes decidir si editar contenido o publicar una versión nueva revoca temporalmente
  SealOfQuality y reinicia la revisión. Edit solo pone AlreadyModerated en false;
  AddVersion/EditVersion no actualizan UpdatedAt ni los estados de moderación.

Los avisos anteriores sobre búsquedas públicas sin filtro de visibilidad y sobre
serializar ApplicationUser completo siguen documentados en README.md y pendientes
del backend. Consumir únicamente UserName en una vista no limita el JSON de la API.
