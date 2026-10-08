# Pendientes acordados

## Validación de archivos por operación

- Estado: pospuesta por el usuario durante el desarrollo, el 6 de octubre de 2026.
- Retomar únicamente cuando el usuario pregunte por los pendientes; no programar recordatorios.
- Distinguir avatar, preview y archivo principal; comprobar archivos vacíos, formatos y límites por uso en backend.
- Antes de publicar, priorizar el avatar: AccountService conserva la extensión proporcionada por el cliente y guarda el archivo dentro de wwwroot. FileService solo comprueba el MIME declarado contra la tabla, sin comprobar la extensión ni el contenido.
- No implementar ni modificar backend sin autorización del usuario.

## Contrato de búsqueda de dependencias

- La búsqueda actual puede incluir recursos no listados para seleccionarlos como dependencias.
- La futura lista general de recursos debe excluir los no listados para usuarios normales y aplicar sus propias reglas de visibilidad.
