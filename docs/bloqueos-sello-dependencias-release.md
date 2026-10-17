# Release: edición con sello y visibilidad de dependencias

Los cuatro cambios de backend de esta guía ya están aplicados por el propietario
y fueron revisados. El frontend reconoce los tres errores existentes con mensajes
aplicables a edición y borrado.

La continuación está en `docs/guardado-transaccional-dependencias-release.md`.
El acceso del staff en frontend y el guardado transaccional de Create/Edit ya están
aplicados. La suite completa pasó con 320 pruebas, incluidas 15 pruebas en SQL
Server LocalDB para el guardado, los conflictos simultáneos y los fallos de SQL/commit.

No hacen falta nuevas propiedades, enums ni migraciones para estos chequeos.
Las líneas indicadas corresponden al código leído antes de aplicar esta guía.

## Reglas

- Si alguna versión tiene sello, un colaborador no puede editar los datos del recurso.
- Si la versión que se está editando tiene sello, un colaborador no puede editarla.
- El staff puede efectuar esas ediciones; no hay todavía un permiso temporal que
  permita delegarlas a un colaborador.
- Un recurso con sello debe permanecer público (`VisibilityId == 1`).
- Un recurso utilizado como dependencia no puede pasar a privado (`VisibilityId == 3`).
  Esta comprobación se aplica también al staff: para una excepción debe resolver
  primero las dependencias. El permiso excepcional de borrado del staff no cambia.
- Al crear o editar no se pueden añadir dependencias privadas. Las no listadas
  siguen siendo válidas.
- Crear una versión nueva sigue permitido. El sello de una versión anterior no
  se copia a la nueva.

## 1. ResourceService: permitir la intervención del staff

Archivo: `SNEStorage/Services/Definitions/ResourceService/EditSingleDefinition.cs`.
En `FindEdit`, línea 50, reemplaza:

```csharp
        var accessResult = await _accessService.GetEditAccess(resourceId, ct);
```

por:

```csharp
        var accessResult = await (_accountService.IsModeratorOrAdmin() ?
            _accessService.GetViewAccess(resourceId, ct) :
            _accessService.GetEditAccess(resourceId, ct));
```

Se conserva el control de acceso al contenido sensible. Un usuario corriente sigue
necesitando ser colaborador; esta alternativa solo permite al staff intervenir.

## 2. ResourceService: bloquear las operaciones antes de preparar archivos

En el mismo archivo, dentro de `EditInternal`, inmediatamente antes de
`string mainFolder = ...` (línea 79), inserta:

```csharp
        bool hasSeal = resource.ResourceVersions.Any(version => version.SealOfQuality);
        bool isStaff = _accountService.IsModeratorOrAdmin();
        long visibilityId = dto.VisibilityId ?? resource.VisibilityId;

        if (hasSeal && (!isStaff || visibilityId != 1))
            return ServiceResult.ResourceHasSealOfQuality.Return(_logger);

        if (visibilityId == 3)
        {
            var dependentsResult = await _dbHelperService.TryFunction(
                cancel => _context.ResourceDependencies.AsNoTracking()
                    .AnyAsync(dependency => dependency.DependencyResourceId == resourceId &&
                        dependency.ResourceId != resourceId, cancel), ct: ct);

            if (!dependentsResult.TryPickT0(out bool hasDependents, out var dependentsError))
                return dependentsError;
            if (hasDependents)
                return ServiceResult.ResourceHasDependents.Return(_logger);
        }
```

`ResourceVersions` ya viene cargada por `FindEdit`; no añadas un Include.
La consulta de dependientes usa `AnyAsync`: no carga los recursos dependientes.
Estos retornos ocurren antes de `BeginBatch`, de preparar previews o de cambiar
las relaciones del recurso, así que no requieren rollback de archivos.

La asignación existente `resource.VisibilityId = dto.VisibilityId ?? resource.VisibilityId`
puede quedarse tal como está.

## 3. ResourceVersionService: permitir staff y bloquear versiones selladas

Archivo: `SNEStorage/Services/Definitions/ResourceVersionService/EditSingleDefinition.cs`.
En `FindEdit`, línea 34, reemplaza:

```csharp
        var accessResult = await _accessService.GetEditAccess(key.ResourceId, ct);
```

por:

```csharp
        var accessResult = await (_accountService.IsModeratorOrAdmin() ?
            _accessService.GetViewAccess(key.ResourceId, ct) :
            _accessService.GetEditAccess(key.ResourceId, ct));
```

Al inicio de **EditInternal** (antes del `if (dto.Version is not null)` de la
línea 58, no del que aparece en `ValidateEdit`), inserta:

```csharp
        if (item.SealOfQuality && !_accountService.IsModeratorOrAdmin())
            return ServiceResult.VersionHasSealOfQuality.Return(_logger);
```

Esto conserva la validación del formato y orden de versiones y no modifica
`AddVersion` ni los permisos de borrado.

## 4. Cerrar el otro camino: añadir una dependencia privada

Archivo: `SNEStorage/Services/ResourceService.cs`.
En `GetDependencies`, línea 272, reemplaza:

```csharp
                .Where(resource => ids.Contains(resource.Id))
```

por:

```csharp
                .Where(resource => ids.Contains(resource.Id) && resource.VisibilityId != 3)
```

Este método ya lo utilizan Create y Edit. Un ID privado quedará como no disponible;
las comprobaciones existentes devolverán `ResourceNotFound` y harán rollback.
No se devolverán los datos del recurso privado para explicar el rechazo.

## Frontend aplicado

`SNEStorage/Components/ApiErrorPresentation.cs` presenta ahora:

- `ResourceHasSealOfQuality`: protección del recurso y obligación de mantenerlo público.
- `VersionHasSealOfQuality`: permiso del staff para editar o borrar esa versión.
- `ResourceHasDependents`: no se puede borrar ni pasar a privado mientras tenga dependientes.

Los controladores ya convierten estos resultados en HTTP 409. El editor existente
muestra el mensaje y conserva el formulario. No necesitas cambiar rutas o DTOs.

## Guardado transaccional aplicado

Los chequeos tempranos descritos arriba se mantienen. Además, Create y Edit llaman
ahora a SaveResourceChanges, que revalida y guarda dentro de una transacción
Serializable. Las subidas de archivos siguen fuera de la transacción. Los detalles,
el alcance y las pruebas están en `docs/guardado-transaccional-dependencias-release.md`.
