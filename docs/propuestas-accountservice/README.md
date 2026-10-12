# AccountService completo: integración

`AccountService.cs` contiene la versión completa de la propuesta del 2026-10-11.
Está fuera del proyecto de la aplicación; no reemplaza el servicio actual.

## Dependencia obligatoria

Añadir esta sobrecarga dentro de la clase existente DBHelperService. Conservar sus otros métodos:

```csharp
public async Task<T> TryFunction<T>(
    Func<CancellationToken, Task<T>> func,
    Func<Exception, T> errorFactory,
    CancellationToken ct = default)
{
    try
    {
        return await func(ct);
    }
    catch (Exception exception)
    {
        return errorFactory(exception);
    }
}
```

## Otros cambios necesarios

- Adaptar AccountController, RegisterController y LoginController con los ejemplos del documento principal.
- Adaptar ProfileController porque EditUserInfo devuelve ahora OneOf<AccountServiceResult, FileServiceResult>.
- Registrar e inyectar el servicio de la misma forma que antes: ILogger<AccountService> se resuelve mediante el logging de ASP.NET Core.
- Actualizar construcciones manuales del servicio para incluir el logger.
- Mantener las firmas de FileService.PrepareFile y EmailService.Confirmation que usa actualmente el proyecto.
- GetUser continúa como adaptador nullable para los consumidores existentes; GetUserResult ofrece el contrato nuevo.
- No agregar nuevamente AccountServiceResult en otro archivo: esta propuesta ya incluye el enum completo y conserva el orden de sus valores antiguos.
- No colocar esta carpeta de propuestas dentro de la aplicación junto con el AccountService original: tendrías clases duplicadas.

## Notas

- Login valida la contraseña con CheckPasswordSignInAsync y devuelve JWT; LoginController sigue guardando el JWT en Session. No crea la cookie de Identity que generaba PasswordSignInAsync.
- Las operaciones de Identity empleadas no aceptan CancellationToken; las queries EF lo reciben cuando su API lo permite.
- Registro mantiene su transacción y envía el email después del commit.
- Las operaciones de archivos conservan los FileServiceResult específicos.
- Los fallos al limpiar el avatar anterior se registran, pero no convierten el perfil guardado en un fracaso.
- GetAvatarUrl comprueba la existencia con la ruta física absoluta y devuelve una ruta relativa al ContentRoot.
- La propuesta no añade un flujo 2FA, revocación de JWT ni nuevas validaciones de formularios/archivos.
- Los fragmentos se revisaron por lectura; no se compiló ni se probó el flujo en ejecución.

Para los controladores y el orden de aplicación, consultar [el documento completo](../propuesta-accountservice-resultados-2026-10-11.md).