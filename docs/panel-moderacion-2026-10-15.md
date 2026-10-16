# Panel de moderación — 2026-10-15

## Implementado en frontend

- `/Moderation`, protegida con `RequireAccount(StaffOnly = true)` y el guard existente de sesión verificada.
- Botón a la izquierda del menú de usuario, visible solo para moderadores y administradores con sesión verificada.
- Recursos activo; Comentarios y Seal of Quality requests quedan visibles como secciones futuras deshabilitadas.
- Consulta `GET /api/flags?Type=Resource`, con estado, prioridad mínima/máxima, razón, rango de creación (UTC), ordenación y páginas de 20.
- Razones cargadas desde `GET /api/flags/options`, agrupadas por categoría de la base de datos y colapsadas al inicio. Selección única o «All reasons».
- Valida los rangos de fechas y prioridades antes de enviar. La fecha final incluye el día completo según el filtro del backend. Aplicar o limpiar filtros vuelve a la primera página; paginar y resolver conservan los filtros aplicados.
- Cada reporte muestra recurso y enlace, razón, reportante con avatar/color, prioridad, fechas y estado.
- Resolver/reabrir usa `PUT /api/flags/{flagId}/edit` con un booleano JSON y antiforgery. Después se vuelve a consultar el listado.
- Delete resource usa `DELETE /api/resource/{resourceId}`, pide confirmación explícita y captcha, y vuelve a consultar la lista después del borrado. Se avisa al staff que se quitarán las referencias de recursos dependientes.
- Errores de red no disparan reintentos automáticos de escritura. Si no se confirma el resultado, se pide refrescar antes de otra edición.
- Los recursos `AlreadyModerated` conservan el menú y la opción de reportar deshabilitada, con explicación visible y tooltip. Los comentarios mantienen el comportamiento existente.

## Backend revisado y aplicado por el usuario

- FlagFilterDTO usa propiedades y acepta Type.
- El GET exige JWT y FullGet comprueba Moderator/Admin.
- FlagReadDTO proyecta Resource con ID y nombre.
- Edit conserva la fecha original al repetir el mismo estado, fija ResolvedAt al resolver y lo limpia al reabrir.
- FindEdit devuelve FlagNotFound.
- ValidateCreate rechaza reportes de recursos AlreadyModerated.

## Pendientes de backend

### Acción para marcar el recurso como moderado

Resolver un reporte solo cambia Flag.Resolved. No cambia Resource.AlreadyModerated.
No existe todavía una acción para marcar el recurso como conforme con las reglas; no se implementó un botón que simule esa operación.

### Detalles menores

1. `Services/Definitions/FlagService/CreateResourceDefinition.cs`, consulta de Reason en ValidateCreate: aún falta pasar `ct: ct` a `_getReasonById.Execute(...)`.
2. `Services/Definitions/FlagService/FullGetDefinition.cs`, GetFilter: `to.Date.AddDays(1)` lanza una excepción con CreatedAtTo=9999-12-31. Se debe validar ese límite antes de construir el filtro, o evitar sumar un día cuando la fecha ya es DateTime.MaxValue.Date. El panel actual no envía filtros de fecha.
3. `FlagReadDTO.CommentId` se añadió pero GetSelect no lo rellena. No afecta al panel de recursos; al implementar comentarios se debe añadir `CommentId = f.CommentFlags == null ? null : (long?)f.CommentFlags.CommentId`.

El backend de flags fue modificado por el usuario. Para conectar el borrado, el usuario autorizó añadir CanDelete y DeleteBlockReason a ResourceReadDTO y a ResourceService.Get, y corregir el InvalidSearch duplicado, dejando los dos errores nuevos al final del enum para conservar los valores anteriores.
