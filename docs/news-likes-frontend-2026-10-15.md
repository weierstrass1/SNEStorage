# Likes de noticias

## Backend revisado

El usuario añadió a `NewsReadDTO` `Likes`, `Dislikes` y `MyVote` (por defecto `LikeValues.Remove`). `NewsService.GetSelect` los proyecta desde `Score.LikesUsers`, filtrando el voto propio por el usuario conectado. Se usan tanto en el GET individual como en la lista. No requiere Include ni migración.

Se conserva `POST /api/news/{newsId}/like`, con `value` como campo de formulario: `Like`, `Dislike` o `Remove`. La respuesta exitosa es el `LikeUsers` guardado o retirado. El backend actual permite votar noticias propias.

## Frontend

- `NewsReactions` se integra en `NewsCard`: landing, lista de noticias y noticia individual.
- Los botones aparecen tras verificar sesión con el mecanismo compartido del navegador.
- El botón activo queda marcado. Pulsarlo otra vez envía Remove; pulsar el contrario cambia el voto.
- Se muestran likes, dislikes y porcentaje de aprobación cuando hay votos.
- Las lecturas de noticias usan credentials same-origin para recibir MyVote.
- Los contadores iniciales llegan con la lista; no se hace un GET por noticia al cargarla.
- Se usa el captcha condicional existente y antiforgery; los botones se deshabilitan durante el envío.
- Tras votar se consulta la noticia individual para sincronizar los contadores. Si se pierde la respuesta del POST, se consulta el estado sin repetir la escritura.
- Si no se puede confirmar el estado, se ofrece Refresh reactions antes de permitir otro voto.
- Cambiar de cuenta limpia el botón activo y vuelve a leer el estado. Navegar fuera detiene los observadores y cancela las solicitudes de ese componente.
- Se actualizaron las referencias de news.js a v7 y la versión del CSS aislado.

## Comprobación

Compilación del proyecto y comprobación sintáctica de news.js. No se ejecutaron pruebas automatizadas ni votos reales en la base de datos.
