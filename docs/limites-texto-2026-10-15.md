# Límites de texto

Se modificó backend con autorización excepcional del usuario para esta tarea.
Los límites compartidos están en `SNEStorage/DTOs/TextLimits.cs`.

| Entrada | Máximo |
| --- | ---: |
| Cada tag nuevo | 20 |
| Cada autor externo | 200 |
| Username y correo | 256 |
| Contraseña nueva o confirmación | 128 |
| Contraseña al iniciar sesión | 4096 |
| Búsqueda de pickers, usuarios y autores | 256 |
| Filtro de nombre de recurso | 100 |
| Filtro de tags | 20 |
| Cada ID de usuario recibido como texto | 450 |
| Nombre de rol | 64 |
| Token de confirmación o recuperación | 4096 |
| Return URL del login | 2048 |
| Código OAuth de Discord | 4096 |
| State OAuth de Discord | 43, igual al valor generado |

El máximo de login es mayor al de nuevas contraseñas para seguir admitiendo
contraseñas antiguas superiores a 128 caracteres, hasta el límite de 4096.
Las contraseñas no se recortan ni normalizan.

`MaxItemLengthAttribute` valida cada string de Tags, ExternalAuthors y AuthorsIDs.
Los MaxLength existentes siguen limitando el número de elementos de las listas.
Los servicios comprueban también los largos de tags, autores, búsquedas y cuentas
antes de las operaciones correspondientes. Se añadieron los resultados
InvalidTag, InvalidExternalAuthor e InvalidSearch al final del enum para conservar
los valores numéricos anteriores.

Los formularios reflejan los límites. Registro y creación/edición de recursos
validan estos campos antes de solicitar captcha o enviar el formulario.
El editor conserva los tags anteriores, incluso si exceden el nuevo límite,
porque envía en Tags únicamente las adiciones. Los datos existentes no se editan.

No se modificaron modelos de EF, columnas, migraciones ni el límite del cuerpo de
noticias. No se añadieron ni ejecutaron tests. Compilan la aplicación y el
proyecto de pruebas; los cuatro módulos JS modificados pasan la revisión de
sintaxis. Queda la comprobación de los formularios en navegador.
