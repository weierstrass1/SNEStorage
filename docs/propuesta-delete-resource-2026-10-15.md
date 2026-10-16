# Borrado de recursos mediante IDeleteSingle

Propuesta para aplicar manualmente. No se modificaron servicios, controladores ni modelos del proyecto.

## Política acordada

| Situación | Usuario normal | Moderador / administrador |
| --- | --- | --- |
| Sin sesión | No | No |
| Autor, submitter del recurso o de alguna versión | Sí | No necesita ser contributor |
| Recurso sensible sin acceso por edad | No | Puede eliminarlo como parte de la moderación |
| Alguna versión tiene Seal of Quality | No, debe contactar al staff | Sí |
| Otro recurso depende de él | No, debe contactar al staff | Sí, quitando los enlaces entrantes |
| AlreadyModerated | No añade restricciones al borrado | No añade restricciones |
| SealOfQualityRequested sin sello concedido | No añade restricciones al borrado | No añade restricciones |

La regla de conservar una versión protege el borrado individual de versiones de un recurso que sigue existiendo. No impide eliminar un recurso completo.

El staff decide sobre las excepciones, incluso cuando hay autores o submitters baneados: las reglas impiden que estos exijan el borrado de un recurso sellado, no prohíben que el staff tome una decisión de moderación.

## 1. Nuevos resultados

Al FINAL de ServiceResultTypes, reemplaza `InvalidSearch` por:

```csharp
InvalidSearch,
ResourceHasSealOfQuality,
ResourceHasDependents
```

No los insertes entre resultados anteriores: conservar los números evita cambiar respuestas existentes.

Copia las dos propiedades de `tmp/IA/propuesta-delete-resource/ServiceResult.DeleteResource.cs` a ServiceResultStatics.cs, o conserva ese archivo como otra parte de ServiceResult dentro de DTOs.

Ambos devuelven HTTP 409. No requieren cambios en GetActionResult, que ya usa ActionType.

## 2. Servicio

Código completo en:

`tmp/IA/propuesta-delete-resource/ResourceService.DeleteSingleDefinition.cs`

Puedes copiarlo a `Services/Definitions/ResourceService/DeleteSingleDefinition.cs`.
La declaración parcial añade IDeleteSingle<long, Resource>; no hace falta repetirlo en ResourceService.cs. También puedes mover esa interfaz a la lista principal si prefieres mantenerla allí.

Conserva las cuatro operaciones:

1. Delete: llama a DeleteSingle de la interfaz.
2. FindDelete: exige una identidad y busca el Resource con seguimiento, sin cargar todas sus relaciones.
3. ValidateDelete: reutiliza GetAccess, comprueba permisos y reglas. El staff queda exento de las restricciones del borrado normal.
4. DeleteInternal: abre una transacción, repite la validación dentro de ella, prepara la limpieza, guarda, confirma y luego borra archivos físicos.

La segunda validación protege el intervalo entre la validación inicial de IDeleteSingle y el borrado. La transacción Serializable impide que se añada una dependencia o se conceda un sello durante ese intervalo a un borrado normal. Si hay un conflicto de concurrencia o un deadlock, se devuelve el error del helper y se revierte la transacción.

## 3. Acción del controlador

Añade a ResourceController:

```csharp
[EnableRateLimiting("writes")]
[HttpDelete("{resourceId:long}")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[RequireCaptcha("resource_delete", CaptchaPolicy.Always)]
public async Task<ActionResult> DeleteResource(
    [FromRoute] long resourceId, CancellationToken ct = default)
{
    var result = await _resourceService.Delete(resourceId, ct);
    return this.GetActionResult(result);
}
```

Contrato: `DELETE /api/resource/{resourceId}`. Success devuelve el resultado de servicio habitual. Se conserva la protección antiforgery de la aplicación; el frontend deberá usar captchaFetch con la acción resource_delete.

## Qué elimina

- El recurso y sus versiones/previews, tipos, tags, autores y otras relaciones que ya tienen cascada en la DB.
- Comentarios y respuestas del recurso. El AddReply actual asigna ResourceComment también a las respuestas.
- Relaciones ResourceComments y CommentComments necesarias para los ClientCascade del modelo.
- Flags de recurso y de sus comentarios, junto con los enlaces ResourceFlags/CommentFlags. Eliminar solo el recurso dejaría Flag sin su objetivo.
- Dependencias entrantes cuando el staff interviene; no elimina los otros recursos. Actualiza UpdatedAt de esos recursos afectados. Las dependencias salientes se eliminan por la cascada existente.
- Filas File correspondientes a versiones/previews y archivos físicos, conservando un File si otro recurso o avatar todavía lo referencia. También contempla File.Resources, que actualmente genera una relación adicional en el modelo.
- Los triggers existentes de Resource y Comment eliminan sus Score, y la cascada de Score elimina votos y likes. Esos triggers deben estar aplicados en la base de datos de destino.

No se eliminan usuarios, videojuegos, categorías, tipos ni los Tag compartidos.

## Archivos y transacción

- PrepareDeletion prepara las filas File para borrar; todavía no elimina archivos físicos.
- SaveChangesAsync guarda el conjunto; CommitAsync confirma la transacción.
- FinishDeletion se ejecuta después del commit y sin depender del token HTTP para la limpieza física.
- Si falla antes de confirmar, la transacción sin commit se revierte al salir de await using y los archivos físicos se conservan.
- Si falla la limpieza física después de confirmar, no se responde como si el recurso siguiera existiendo: FinishDeletion registra el fallo y el mantenimiento periódico debe limpiar el archivo sobrante.
- No se necesita BeginBatch: no se están creando archivos que haya que revertir.
- Si se pierde la conexión durante el commit, su resultado puede ser incierto. El frontend debe consultar si el recurso todavía existe antes de ofrecer repetir el borrado.

## Frontend conectado

- Botón Delete resource en la vista individual para autores/submitters y staff, y en los reportes del panel de moderación para staff.
- La vista individual usa CanDelete y DeleteBlockReason añadidos a ResourceReadDTO y a ResourceService.Get con autorización puntual del usuario. El resto del backend de borrado fue aplicado por el usuario.
- Un contributor bloqueado por sello o dependientes conserva el botón deshabilitado; el tooltip explica el motivo y recomienda contactar a un moderador o administrador. Visitantes y usuarios ajenos al recurso no ven el botón.
- Confirmación explícita antes del captcha y el DELETE; el staff recibe el aviso de eliminación de referencias entrantes.
- Después de borrar, la vista individual navega a Resources y moderación refresca sus reportes.
- Si se pierde la respuesta, no se repite automáticamente el DELETE: se ofrece comprobar el estado mediante GET antes de otro intento.

## Alcance de verificación

### Borrado de versiones conectado — 2026-10-16

- El historial ofrece Delete version a autores, submitters y staff verificados.
- `DELETE /api/resource/{resourceId}/version/{versionId}`, con captcha `resource_deleteVersion` y confirmación explícita.
- La última versión queda protegida para todos, incluido el staff, tanto en el frontend como en ValidateDelete del backend.
- Las versiones con sello quedan protegidas para usuarios normales; el staff puede borrarlas si sigue existiendo otra versión.
- El usuario autorizó el ajuste puntual de FindDelete y ValidateDelete para que el staff use GetViewAccess y los demás usuarios GetEditAccess. El chequeo de cantidad mínima de versiones se conserva.
- Después de borrar se actualizan historial, última versión, descargas totales y permisos del recurso; se conserva el borrador de comentarios. Si el usuario pierde acceso al recurso al borrar su única contribución, se muestra el error de acceso del backend.
- Una respuesta de red incierta requiere comprobar la lista de versiones antes de permitir otro intento; no se repite el DELETE automáticamente.

La propuesta compiló con los tipos actuales del proyecto, incorporándola temporalmente mediante MSBuild y una copia del enum con los dos resultados nuevos. No se ejecutaron borrados en ninguna base de datos ni se añadieron migraciones. Los archivos `ServiceResult.CompileOnly.cs` y `compile-proposal.targets` son auxiliares de esa comprobación; no se deben copiar al proyecto.
