# Cerrar los dos pendientes del release

**Estado: aplicado y probado con autorización puntual del propietario.**
El código activo está en `SNEStorage/Services/Definitions/ResourceService/SaveChanges.cs`.
CreateInternal y EditInternal ya lo utilizan; no quedan sustituciones por aplicar.
La transacción usa `await using var transactionScope = transaction;`, sin un bloque
que envuelva el cuerpo del método. Los apartados de integración siguientes conservan
los reemplazos realizados como referencia.

## 1. Frontend del staff: aplicado

El frontend permite al staff entrar a Edit resource y Edit version después de
verificar la sesión, el ID de usuario y los claims Admin/Moderator.

- El botón Edit resource y los enlaces Edit version aparecen para el staff.
- PageAccessGuard admite esa excepción únicamente en las dos páginas de edición.
- ResourceEditor acepta esa excepción al cargar y reintentar el formulario.
- CanEdit del DTO conserva su significado actual de colaborador. No lo cambies
  a true para todo el staff: también se utiliza para calificaciones y nuevas versiones.
- Add version sigue usando el permiso de colaborador, de acuerdo con el servicio actual.
- Un colaborador no ve Edit version para una versión sellada.
- La API conserva la autorización definitiva; no se concede acceso por un query string
  ni por datos guardados por el navegador.

## 2. Backend: integración aplicada

El archivo `SNEStorage/Services/Definitions/ResourceService/SaveChanges.cs` contiene
los dos métodos de guardado y validación. No es necesario copiar la propuesta temporal.

### A. Añade el archivo al proyecto

Archivo añadido:

`SNEStorage/Services/Definitions/ResourceService/SaveChanges.cs`

Es otra parte de la clase parcial ResourceService; no necesita DI ni un servicio nuevo.
Solo tiene dos métodos: uno coordina el guardado y otro comprueba las reglas.

### B. Cambia una llamada en CreateInternal

Archivo: `SNEStorage/Services/Definitions/ResourceService/CreateDefinition.cs`.
Línea 158 del estado leído. Reemplaza:

```csharp
        var result = await _fileService.TrySyncContextWithFiles(batchfile, resource, ct);
```

por:

```csharp
        var result = await SaveResourceChanges(resource, batchfile, ct);
```

Conserva `_context.Resources.Add(resource)` antes de esta llamada y el código de
notificación a Discord después del resultado exitoso.

### C. Cambia el bloque de guardado en EditInternal

Archivo: `SNEStorage/Services/Definitions/ResourceService/EditSingleDefinition.cs`.
Líneas 257–259 del estado leído. Reemplaza:

```csharp
        var saveResult = await _fileService.TrySyncContextWithFiles(batchfile, ct);
        if (saveResult.Result != ServiceResult.Success)
            return saveResult;
```

por:

```csharp
        var saveResult = await SaveResourceChanges(resource, batchfile, ct);
        if (!saveResult.TryPickT0(out _, out var saveError))
            return saveError;
```

Conserva `FinishDeletion(files)` después de ese bloque: los archivos antiguos se
eliminan físicamente solo tras confirmar el commit.

### D. Conserva los chequeos que ya añadiste

Los chequeos tempranos del sello, dependientes y GetDependencies siguen siendo útiles:
rechazan pronto una petición que ya era inválida. La validación del método nuevo
es adicional y usa el estado de la DB dentro de la transacción.

## Cómo cierra la carrera entre peticiones

1. Se preparan los archivos y el grafo de entidades como ahora.
2. SaveResourceChanges abre una transacción SERIALIZABLE.
3. Vuelve a leer el sello, la visibilidad efectiva y los dependientes del recurso existente.
4. Comprueba que todos los destinos de las dependencias sigan existiendo y no sean privados.
5. Guarda las entidades y confirma la transacción.
6. Solo entonces elimina el manifiesto con BatchComplete.

La relectura usa consultas SQL, no las entidades cargadas al inicio. La visibilidad
efectiva tiene en cuenta si EF va a actualizar esa columna: si no la modifica, se
usa la que actualmente está en la DB.

SERIALIZABLE conserva los bloqueos de las lecturas y sus rangos hasta terminar la
transacción. Una petición que vuelve privado el destino y otra que añade una
dependencia a él ya no pueden confirmar las dos basándose en comprobaciones viejas.
Ambos caminos deben usar el método nuevo. Añadirlo únicamente a Edit deja abierto Create.
[Documentación de SQL Server](https://learn.microsoft.com/en-us/sql/t-sql/statements/set-transaction-isolation-level-transact-sql?view=sql-server-ver17).

Si dos operaciones entran en conflicto, una puede esperar o fallar con DBError
(por ejemplo, si SQL Server resuelve un deadlock). No se reintenta automáticamente
una subida. El frontend conserva el formulario y muestra el error.

No hay una transacción abierta mientras se copian o validan archivos. Tampoco
necesita migración, un semáforo global ni cambios en FileService.

## Archivos si el commit falla

Antes de intentar el commit, un fallo provoca rollback y limpieza del batch usando
CancellationToken.None. El token cancelado del cliente no impide esa limpieza.

Si falla la respuesta del commit, se conservan el manifiesto y los archivos: una
caída de conexión en ese instante puede dejar incierto si SQL Server confirmó.
Borrarlos podría eliminar archivos de un recurso que sí llegó a guardarse.
Ese manifiesto queda para el procedimiento de mantenimiento, contrastando la DB;
no debe eliminarse ciegamente todo lo que aparezca en él.
[Documentación de EF Core](https://learn.microsoft.com/en-us/ef/core/miscellaneous/connection-resiliency#transaction-commit-failure-and-the-idempotency-issue).

## Pruebas realizadas

- Compilación y suite completa: **320 correctas, 0 fallos, 0 omitidas**.
- Dentro de ellas, **15 casos con SQL Server LocalDB real**, en una base temporal
  independiente de la aplicación y de producción, y 9 casos nuevos para los permisos
  de presentación del staff.
- Dependencias públicas y no listadas aceptadas; privadas rechazadas en Create/Edit.
- Rechazo del cambio a privado cuando existe un dependiente.
- Relectura del sello concedido después de cargar el recurso; excepciones del staff
  y obligación de visibilidad pública.
- Conservación de un cambio concurrente de visibilidad cuando EF no modifica esa columna.
- Rollback de escrituras SQL ya ejecutadas ante un fallo previo al commit, limpieza
  de archivos nuevos y ChangeTracker vacío.
- Fallo inyectado después del commit real: la DB conserva el cambio y el servicio
  conserva los archivos y el manifiesto.
- Limpieza con el token del cliente ya cancelado.
- Dos escenarios de concurrencia deterministas, con dos contextos y transacciones
  simultáneas detenidas después de validar: pasar el destino a privado frente a
  añadirlo como dependencia de un recurso nuevo o de uno existente. Se verificó que
  exactamente una operación confirme y que nunca quede una dependencia a un destino privado.
- Los tests verifican que el aislamiento utilizado sea Serializable.
- Las bases temporales y sus archivos se eliminaron al terminar las pruebas.

Tests: `SNEStorageTesting/ResourcePersistenceSqlServerTests.cs` y
`SNEStorageTesting/ResourceStaffPresentationTests.cs`.

Para ejecutar los tests SQL se debe establecer `SNESTORAGE_TEST_LOCALDB` a una
instancia `(localdb)\...`; rechazan destinos que no sean LocalDB. Sin esa variable,
se omiten explícitamente esos 15 casos. Los tests no leen los secretos ni la
configuración de conexión de la aplicación.

La fixture crea las tablas relevantes con el DDL generado por los mappings de
producción, incluidas las relaciones y los índices. No utiliza EnsureCreated sobre
todo el modelo: al probar ese camino se detectaron dos claves primarias llamadas
`id` en tablas ajenas a este guardado (CommentComments y ResourceCategory). Ese
problema de creación completa de un esquema nuevo es anterior y no se modificó
como parte de esta autorización. Las pruebas finales usan las tablas del guardado.

No se aplicaron migraciones ni cambios en producción. Estas pruebas comprueban los
casos enumerados; no certifican todas las operaciones o configuraciones del sitio.

## Alcance

La propuesta protege Create/Edit realizados por estos servicios. No sustituye una
restricción global para modificaciones manuales en SQL ni otros caminos futuros
que cambien visibilidad, dependencias o concedan sellos: esos caminos también
deberán respetar las reglas y coordinar su propio guardado.

No se necesitan targets adicionales de compilación ni cambios en el csproj.
