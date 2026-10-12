# Reglas de SNEStorage: edición y propuesta de moderación

## Página pública

Ruta: `/Rules`. Es pública, no requiere sesión ni consulta la base de datos.

El contenido está en `SNEStorage/wwwroot/content/site-rules.json`. El navegador
lee este archivo estático; no hay un servicio, controlador ni caché de servidor
nuevo. La página usa el tema de SNEStorage, incluye navegación por secciones y
permite enlazar directamente una regla, por ejemplo `/Rules#rom-uploads`.

La vista es `Components/Pages/Rules.razor`; `Rules.razor.cs` selecciona el JSON
y numera las secciones. La carga y limpieza se comparten mediante el componente
base `Components/Shared/StaticDocumentPage.cs`; la apariencia está en
`Rules.razor.css`. Las clases de
`Components/Rules/SiteRulesDocument.cs` describen exclusivamente este documento
de presentación: no son entidades de EF ni DTOs de la API.

### Cómo editar el JSON

- `title`, `introduction`: título y texto introductorio.
- `updatedOn`: fecha visible de la revisión, en formato `YYYY-MM-DD`; actualízala
  cuando cambies la política. No se modifica automáticamente.
- `contextTitle`, `contextDescription`: aviso sobre revisión contextual.
- `sections`: lista de secciones; puedes cambiar su orden, título y descripción.
- `groups`: subsecciones opcionales, cada una con `id`, `title`, `description` y
  `rules`. Sus reglas continúan la numeración dentro de la misma sección.
- Cada sección tiene un `id` estable, por ejemplo `resources`. Usa letras
  minúsculas, números y guiones, sin espacios ni duplicados: se usa como ancla.
- `rules`: reglas de esa sección. Cada regla tiene `id`, `title` y `text`.
  El `id` es estable y único, por ejemplo `rom-uploads`. La página numera las
  secciones automáticamente con letras (A, B, C…) según su orden en el JSON.
  Las reglas se numeran dentro de cada sección: A.1, A.2, B.1, B.2… No necesitas
  mantener letras ni números manualmente. Los IDs y enlaces permanecen estables.
- Mantén todo el texto de la aplicación en inglés. Los textos se muestran como
  texto normal, no como HTML: no necesitas etiquetas ni puedes insertar scripts.

Ejemplo de una sección nueva, para añadir dentro del arreglo `sections`:

```json
{
  "id": "new-section",
  "title": "New section",
  "description": "What this section covers.",
  "rules": [
    {
      "id": "new-rule",
      "title": "A short rule title",
      "text": "Explain what users should or should not do."
    }
  ]
}
```

Recarga la página después de guardar el archivo. La petición revalida el JSON
para evitar reutilizar una copia desactualizada del navegador. En producción,
actualiza también el archivo publicado; no basta con cambiar tu copia local.
El archivo es público: no pongas información interna de moderación en él.

Las pautas resumidas del formulario de subida conservan sus instrucciones
prácticas y enlazan a estas reglas completas. La página no implementa permisos,
baneos ni restricciones nuevas en el backend. Las políticas de acceso y
dependencias deben ser verificadas por las acciones correspondientes.

### Página independiente de Seal of Quality

Ruta pública: `/SealOfQuality`. Su contenido está separado en
`SNEStorage/wwwroot/content/seal-of-quality.json`, y ya no aparece dentro del
JSON o índice de las reglas generales. Conserva el título dorado y el logo con
su brillo. Sus cinco apartados son solicitud, beneficios, compromisos, retirada
y requisitos, con letras y números propios.

Edita `title`, `kicker`, `introduction`, `updatedLabel`, `updatedOn`,
`navigationTitle`, `relatedLinkLabel` y `sections` en ese archivo. Los IDs de los
apartados y entradas permiten enlaces como `/SealOfQuality#quality-benefits`.
Las vistas, código y estilos están en `SealOfQuality.razor`, `.razor.cs` y
`.razor.css`. Ambas páginas reutilizan la carga estática y la lista de entradas.

La guía describe las condiciones del sello; impedir borrados, cambios de
visibilidad y ediciones, y verificar usuarios relacionados baneados, corresponde
al backend.

## Propuesta de severidades — pendiente de tu decisión

### Orden de lectura por severidad o prioridad

Los puntos se ordenaron dentro de cada sección de mayor a menor gravedad o
importancia. El orden está en los arreglos `rules` de cada JSON: puedes mover
entradas y la numeración se recalcula automáticamente, conservando los enlaces
basados en IDs. Las secciones mantienen su orden temático.

| Sección de reglas | Orden de prioridad |
| --- | --- |
| Comunidad y perfiles | Acoso; contenido del perfil; contexto del lenguaje. |
| Recursos y previews | Engaño en los archivos; avisos de contenido sensible; pertinencia a SNES; previews; clasificación; precisión descriptiva. |
| Seguridad y autoría | Material ilegal; enlaces maliciosos; ROMs y parches; permiso de distribución; créditos completos. |
| Comentarios | Spam; discusiones sensibles fuera de contexto; comentarios vagos. |
| Visibilidad y permisos | Acceso privado; permisos de gestión; protección de dependencias; conservar una versión. |
| Moderación | Reincidencia; es el único punto de la sección. |

| Sección del Seal of Quality | Orden de prioridad |
| --- | --- |
| Solicitud | Revisión optativa por versión; es el único punto. |
| Beneficios | Confianza en el estándar revisado; descubrimiento mediante listado/filtro; distintivo visual; nombre dorado. |
| Compromisos | Proteger el recurso; proteger versiones selladas; permiso para editar; visibilidad pública; procedimiento de borrado excepcional. |
| Retirada | Ausencia de autores o submitters relacionados baneados; es el único punto. |
| Requisitos | Cumplir las normas; funcionar correctamente; información completa; README adecuado; calidad artística; previews cuando sean necesarios. |

En conducta, prioricé el daño potencial, el abuso deliberado y el impacto sobre
otros usuarios. En permisos y compromisos, prioricé seguridad, integridad y
protección del trabajo revisado. En beneficios, la confianza y la utilidad
preceden a la apariencia. Esto **no convierte cada punto en una sanción fija**:
por ejemplo, un enlace irrelevante por error no equivale a phishing, ni olvidar
un crédito equivale a apropiarse deliberadamente del trabajo ajeno.

### Escala orientativa

Esta parte es una propuesta interna. Los plazos siguientes **no se publicaron
como sanciones obligatorias ni se implementaron como automatismos**. Tú y el
staff decidís la respuesta según el caso. No propongo un sistema de tres faltas
ni que todas las infracciones tengan un ban.

| Nivel | Situación típica | Respuesta orientativa |
| --- | --- | --- |
| Leve | Error de clasificación, información imprecisa, comentario poco útil sin intención de molestar | Solicitar corrección o retirar el contenido; aviso si hace falta. Normalmente sin suspensión. |
| Moderada | Spam, provocación, contenido inapropiado, incumplimiento repetido tras una explicación | Retirar/corregir contenido y advertir. Si continúa, restricción de la función afectada o suspensión de 24 horas a 7 días. |
| Grave | Acoso deliberado, engaño, distribución sin permiso consciente, ocultación intencional de contenido sensible | Retirar contenido y considerar suspensión de 7 a 30 días. Reincidencia o daño elevado pueden justificar un plazo mayor. |
| Crítica | Distribución deliberada de malware, phishing, amenazas graves, difusión de material ilegal especialmente dañino | Retirar o bloquear el acceso al contenido y considerar suspensión inmediata de 30 a 90 días o ban permanente, incluso en la primera infracción. |
| Política de acceso | Quién ve, edita o elimina un recurso; restricciones de dependencias | Aplicar las reglas de permisos en el backend. No asignar un castigo por describir una política. |

Las restricciones de una función son una opción de diseño futuro, no una
capacidad que esta página añada. Si el sistema solo admite suspensiones, usa
los mecanismos que implementes y comunica claramente su alcance.

### Clasificación de tus 21 reglas

Los números de esta tabla identifican los puntos de tu lista original.
La página pública usa referencias por sección (A.1, B.1…) asignadas automáticamente.

| N.º original | Regla | Sección pública | Severidad orientativa / respuesta |
| --- | --- | --- | --- |
| 1 | No molestar o acosar a otros usuarios | Comunidad | Moderada a grave; amenazas graves o acoso persistente con daño elevado pueden ser críticos. |
| 2 | El lenguaje se evalúa por contexto e intención | Comunidad | Ninguna sanción por una palabra aislada. Provocación deliberada: moderada; acoso: grave. |
| 3 | No subir material ilegal | Seguridad y autoría | Grave a crítica según el material, intención y daño; retirar el contenido. |
| 4 | Perfil y avatar apropiados | Comunidad | Normalmente moderada: retirar/cambiar el contenido y avisar. Amenazas o contenido peligroso requieren evaluar mayor severidad. |
| 5 | No introducir enlaces maliciosos, dudosos o irrelevantes | Seguridad y autoría | Enlace irrelevante por error: leve. Promoción o abuso: moderada. Phishing o malware deliberados: crítica. |
| 6 | No spammear comentarios | Comentarios | Moderada; repetición deliberada o campañas masivas pueden ser graves. |
| 7 | No engañar sobre los archivos subidos | Recursos | Grave cuando el engaño es intencional; crítica si distribuye malware. Un archivo equivocado por error requiere corrección, no el mismo castigo. |
| 8 | Solo material pertinente a SNES | Recursos | Leve si es un error comprensible; moderada si continúa; grave si usa la web deliberadamente para abuso o spam. |
| 9 | Previews pertinentes y no usadas para polémicas | Recursos | Leve a moderada según contenido e intención; revisar otras reglas si el contenido es peligroso o ilegal. |
| 10 | Flags correctos para contenido sensible | Recursos | Leve si fue una omisión accidental corregida. Moderada a grave si oculta el contenido deliberadamente o reincide. |
| 11 | Clasificación correcta | Recursos | Leve: corregir. Moderada si clasifica mal de forma deliberada y repetida. |
| 12 | Información precisa, no vaga | Recursos | Leve: solicitar mejores datos o retirar una publicación insuficiente. |
| 13 | Permiso para redistribuir recursos ajenos | Seguridad y autoría | Grave si la distribución sin permiso es consciente; valorar error, licencia y rectificación antes de decidir la suspensión. |
| 14 | Quién puede ver recursos privados | Visibilidad y permisos | Política de acceso; no es una infracción por sí misma. |
| 15 | Dependencias no privadas ni convertibles a privadas si están referenciadas; consultar al staff antes de borrar un recurso con dependientes | Visibilidad y permisos | Política de acceso/integridad; rechazar la operación cuando corresponda. Abusar de un fallo requiere evaluar la conducta aparte. |
| 16 | Incluir todos los autores | Seguridad y autoría | Leve si omitió un crédito por error; grave si busca apropiarse del trabajo ajeno. |
| 17 | Quién puede subir versiones, editar o borrar | Visibilidad y permisos | Política de acceso; el permiso no justifica usar esas acciones para acosar o engañar. |
| 18 | Temas sensibles solo con contexto pertinente | Comentarios | Leve a moderada si está fuera de contexto; grave si deriva en acoso. Una conversación pertinente no es infracción. |
| 19 | Comentarios vagos o sin sentido pueden borrarse | Comentarios | Retirar el comentario si corresponde. No hace falta castigar la cuenta salvo abuso o spam. |
| 20 | Reincidencia agrava las sanciones | Moderación | Criterio contextual, no una falta adicional ni un contador automático de bans. |
| 21 | No ROMs salvo homebrew sin infracción de derechos; compartir modificaciones mediante parches BPS o IPS | Seguridad y autoría | Grave si incumple conscientemente; distribución reiterada puede justificar un ban largo o permanente. Verificar la excepción de homebrew antes de decidir. |

Regla adicional de integridad: mientras un recurso exista, debe conservar al
menos una versión. No se permite borrar la última versión restante. Esta
condición y la consulta previa al staff para borrar recursos con dependientes
deben aplicarse también en el backend; el JSON solo publica las reglas.

## Cómo elegir una sanción

1. **Verificar el hecho:** regla concreta, contenido afectado y evidencia.
2. **Distinguir intención:** error, negligencia, provocación o acción maliciosa.
3. **Valorar el daño:** a otros usuarios, a los autores o a la seguridad del sitio.
4. **Revisar antecedentes relacionados:** no tratar igual una primera confusión
   y la repetición de una conducta que ya fue explicada.
5. **Considerar la respuesta del usuario:** corregir y colaborar atenúa; ocultar,
   insistir o evadir agrava. No exige tolerar riesgos graves mientras se revisa.
6. **Elegir una respuesta proporcionada:** corregir o retirar contenido puede
   bastar; un ban permanente debe tener una razón clara y documentada.

Un ban largo o permanente inmediato tiene sentido especialmente en acciones
deliberadas que ponen en peligro a otros: malware, phishing, amenazas graves o
material ilegal especialmente dañino. No recomiendo equiparar esas situaciones
con una etiqueta incorrecta, un comentario torpe o un crédito omitido por error.
También conviene comprobar si una cuenta fue comprometida antes de atribuirle
intención al titular.

Cuando añadas el sistema de moderación, recomiendo registrar y comunicar la
regla infringida, el motivo, el alcance y la duración de la sanción. Conviene
definir un canal de revisión de errores; no se anuncia aquí uno que todavía
no existe.

Como referencia de diseño, Discord también distingue la gravedad, el contexto
y los antecedentes de un número fijo de faltas, y contempla suspensión
permanente inmediata para daños especialmente graves:
[Discord Warning System](https://support.discord.com/hc/en-us/articles/18210965981847-Discord-Warning-System).
Los niveles y plazos propuestos aquí son decisiones sugeridas para SNEStorage,
no una copia de sus sanciones.
