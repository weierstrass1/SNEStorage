# Búsquedas en pickers y preferencias de contenido

## Pickers

ResourceAuthorPicker (usuarios y autores externos) y ResourceDependencyPicker
usan `@bind`, `@bind:event="oninput"` y `@bind:after` como el picker de videojuegos.
El enlace informa al renderer de Blazor del valor actual del navegador antes de
iniciar la búsqueda asíncrona, evitando que un render restaure texto anterior.
Se conservan el debounce y el descarte de respuestas de búsquedas anteriores.
El picker de videojuegos no se modificó.

## Contenido sensible en la lista

Resources usa la sesión compartida del navegador (`account-session.js?v=2`),
sin una petición independiente de perfil por cada filtro. Lee únicamente la
fecha de nacimiento de `/api/profile/me`, nunca la de perfiles públicos.

- La sección Content preferences permanece oculta hasta verificar la sesión.
- Con una cuenta menor de 18 años, o con fecha ausente/inválida, no se muestra.
- Solo una cuenta autenticada con 18 años cumplidos puede activar esas opciones.
- Para visitantes sin sesión también permanecen ocultas y desactivadas.
- Un estado de sesión desconocido no habilita esas opciones.
- Al perder el permiso se limpian los cinco flags del borrador y de los filtros
  aplicados. Si había flags activos, se recarga la primera página sin ellos.
- También se restablecen antes de aplicar filtros o solicitar otro bloque.
- La edad se calcula por fecha UTC, incluyendo mes y día del cumpleaños.

## Backend actualizado por el usuario

- `ResourceService.GetFilter` añade un predicado de acceso por edad antes de
  aplicar las preferencias: los cinco flags no pueden habilitar material sensible
  para visitantes ni cuentas menores. La edad se comprueba mediante EXISTS en SQL.
- `GetResourceLike` reutiliza ese filtro para el selector de dependencias.
- `ResourceAccessRow.ContentAllowed` incluye la comprobación de edad en la
  proyección. `CheckViewAccess` y `CheckEditAccess` la exigen, también al staff.
- Las lecturas del recurso, previews y archivos, incluyendo descargas, utilizan
  `GetViewAccess`. `AddOrEditScore` comprueba explícitamente ContentAllowed.
- `CommentService.CheckResourcePermission` delega en GetViewAccess, cubriendo
  listados, respuestas, lectura del voto propio y likes de comentarios de recursos.
- `GetResourceAllDependencies` conserva los IDs y devuelve Name = null para una
  dependencia no accesible por edad o visibilidad. La UI muestra “Dependency
  unavailable” sin enlace; el editor mantiene la selección para no borrarla.

`CommentService.EditComment` y `DeleteComment` ya comprueban
CheckResourcePermission después de autoría/moderación y antes de modificar datos,
cuando existe ResourceComment. Ambos cargan esa relación. Los comentarios
independientes conservan su comportamiento actual.
El constructor manual de CommentService en `SNEStorageTesting/CommentServiceTests.cs`
ya recibe ResourceAccessService. El caso de acceso anónimo privado espera ahora
UserNotFoundUnauthorized, como GetViewAccess.
La revisión del código no sustituye probar las consultas con SQL Server y las
distintas sesiones. No se ejecutaron tests automatizados durante esta revisión.
