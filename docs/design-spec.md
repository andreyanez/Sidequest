# Sidequest — especificación de diseño e implementación

Versión 0.3 · 3 de octubre de 2026 · Reemplaza la UI de WeeklyReminder por Sidequest.

**Cambios en 0.2** (tras unos días de uso): se elimina el detalle del objetivo y sus notas, archivos
e imágenes pasan al detalle del Sidequest como pestaña **Notas**; los objetivos e items se pueden
**renombrar** y **reordenar arrastrando** (asa ⋮⋮ al pasar el cursor); se especifica el estado de
**añadir item**. Secciones afectadas: 1, 3, 4.4, 5.1, 5.3, 5.6, 7, 8.

**Cambios en 0.3**: la ventana compacta pasa a **alto fijo ajustable por el usuario** (igual en todas
las vistas) con scroll interno y señales de scroll; la lista de Sidequests pierde el contador "n
activos" y fija "Nuevo Sidequest" en el pie. No hay archivado de Sidequests. Secciones afectadas:
3, 4.4, 5, 5.1, 5.4, 5.5, 6, 7.

Este documento describe **qué** construir. Las convenciones del repo (cómo compilar, cómo
se organizan temas e idiomas, qué patrones de WPF se usan) viven en `README.md` y, cuando
exista, en `CLAUDE.md`. Los artboards de referencia están en `docs/design/reference/`
(HTML autocontenido: ábrelo en un navegador para ver el diseño a escala real y leer los
valores exactos de CSS).

---

## 0. Decisiones

**D1–D7 se resolvieron el 19 de septiembre de 2026** y la v1 (0.1.0) salió con ellas el 30 de
septiembre. **D7 queda revocada por D8 (0.2).** D8–D10 se resolvieron el 3 de octubre de
2026. Las marcas **[DECIDIR]** del resto del documento se leen ya como decididas.

| # | Tema | Decisión | Se descartó |
|---|------|----------|-------------|
| D1 | Recordatorios programados, snooze y auto-cierre de WeeklyReminder | **Se eliminan.** Sidequest es un panel persistente, no un popup que aparece solo. Se pierden los avisos de 09:00 / 15:00 / 23:00. | Conservar el `Scheduler` para mostrar el panel principal a horas fijas. |
| D2 | Rollover semanal | **Se elimina.** Los objetivos no expiran por fecha; el usuario los quita. | — |
| D3 | Migración de `week.json` | **Se importa** a un Sidequest llamado "Importados" (tema Grafito); los items conservan `Done` y su orden actual. | Descartar los datos antiguos. |
| D4 | Imágenes adjuntas *(en la 0.1, a un objetivo; desde la 0.2, a un Sidequest)* | **Se copian** a `%APPDATA%\Sidequest\media\` (el original puede moverse). Los archivos locales solo se **referencian** por ruta. | Referenciar también las imágenes. |
| D5 | Nombre del ejecutable, mutex y carpeta de datos | **Se renombra todo a `Sidequest`**, incluidos el repositorio de GitHub y la carpeta del proyecto. La carpeta antigua `%APPDATA%\WeeklyReminder\` solo se lee para migrar. | Mantener el nombre interno y cambiar solo la UI. |
| D6 | Whiteboard: redimensionar formas | **v1 no redimensiona**; solo crear, mover, editar texto y borrar. | Incluir asas de redimensión en v1. |
| ~~D7~~ | ~~Reordenar objetivos e items~~ | ~~No se reordena en ninguna lista; `SortIndex` y `PanelIndex` solo crecen.~~ **Revocada por D8.** Rigió la 0.1.0. | — |
| D8 | Reordenar objetivos e items *(0.2)* | **Se reordena arrastrando** desde el asa ⋮⋮ (5.1 y 5.6). El panel y cada Sidequest tienen órdenes independientes (`PanelIndex`, `SortIndex`). | Mantener D7. |
| D9 | Tamaño del botón de Sidequest en la lista *(0.3)* | **Compacto (42 px)**, ~50 % más Sidequests a la vista (5.4). | Normal (64 px). |
| D10 | Notas, archivos e imágenes guardados en objetivos por la 0.1.0 *(0.2)* | **No se migran.** No hay ninguno en uso. Al cargar un `sidequests.json` de la 0.1.0, los campos `Notes`, `Files` e `Images` de los items se ignoran y desaparecen en el siguiente guardado. | Fusionarlos en la pestaña Notas de su Sidequest. |

### Alcance

**v1 (0.1.0, publicada el 30 de septiembre de 2026).** La Bitácora (5.7) se aplazó a v1.1; el
botón "Bitácora" **no aparece** en el detalle del Sidequest — no se muestra un control muerto — y
`boards\<sidequestId>.json` no se crea todavía. Orden que se siguió: fase 0 (renombres, fuente,
resolución de color, iconos) → 1 (datos y migración) → 2 (shell y panel principal) → 3
(navegación y Sidequests) → 4 (detalle del objetivo) → 5 (ajustes y bandeja) → 6 (tema claro y
repaso de teclado).

**Cambios de la 0.2 y la 0.3.** Se construyen sobre `main` antes de la Bitácora, que sigue
siendo la fase 7 (v1.1).

### Correcciones a la 0.1

- **Cerrar el panel principal lo oculta, no lo cierra.** La 6 da `Owner = MainWindow` a la
  Bitácora y a Ajustes; en WPF una ventana propiedad de otra se cierra con ella, así que
  cerrar el panel se llevaría por delante una Bitácora abierta.
- El color efectivo de un Sidequest depende de **dos** ejes (`SidequestTheme` × tema claro /
  oscuro de la app) más los derivados con alfa de la 4.2. `ThemeService` hoy solo conoce uno.

---

## 1. Concepto y vocabulario

Sidequest es una app de escritorio (Windows, WPF, .NET 8) para atomizar tareas grandes en
pasos pequeños y tener a mano el contexto de cada paso. La filosofía viene de los RPG,
pero **la app no debe sentirse como un juego**: look serio, capaz de destacar en pantalla sin
distraer.

| Término | Significado | En el modelo |
|---------|-------------|--------------|
| **Sidequest** | Una tarea o proyecto grande, con un color de tema propio. | `Sidequest` |
| **Item** | Un paso pequeño dentro de un Sidequest. | `SidequestItem` |
| **Objetivo** | Un item que el usuario envió al panel principal. No es otra entidad: es un item con `InPanel = true`. | `SidequestItem.InPanel` |
| **Panel principal** | La ventana compacta, siempre visible, con los objetivos del usuario. | `MainWindow` (compacta) |
| **Bitácora** | El whiteboard de un Sidequest. Se abre desde el detalle del Sidequest. | `BoardWindow` |

Reglas de negocio:

- Un item se convierte en objetivo con "Enviar objetivo" desde el detalle del Sidequest. Mientras
  está en el panel, en el detalle aparece primero, con la etiqueta "Objetivo actual", y sin botón.
- Desde el panel principal un objetivo se puede **completar** (checkbox), **devolver** al Sidequest
  (`InPanel = false`, conserva `Done`) o **eliminar** (borra el item del Sidequest, previa confirmación).
- Un objetivo completado se queda en el panel, tachado, hasta que el usuario lo devuelva o elimine.
  No hay limpieza automática por día ni por semana **[D2]**.
- Los objetivos no tienen detalles propios: si un objetivo necesita notas, no es lo bastante atómico.
  El contexto (notas, enlaces, archivos, imágenes) vive en el **Sidequest**, en su pestaña Notas; los
  objetivos son el resultado destilado de ese contexto.
- Límites por Sidequest: notas ≤ 500 caracteres (las URL cuentan), archivos ≤ 5, imágenes ≤ 5. Al
  alcanzar un límite, el botón de añadir correspondiente se deshabilita y el contador lo muestra.
- Objetivos e items se **renombran** con el lápiz que aparece al pasar el cursor, editando en la misma
  tarjeta. Mismo texto en ambos sitios (es el mismo objeto).
- Objetivos (en el panel) e items pendientes (en el Sidequest) se **reordenan arrastrando** desde un
  asa ⋮⋮ que aparece al pasar el cursor. El resto de la tarjeta no inicia arrastre. Reordenar en el
  panel cambia `PanelIndex`; reordenar en el Sidequest cambia `SortIndex`. Son órdenes independientes.
- El usuario llega a un item navegando: panel principal → Sidequests → Sidequest → "Enviar objetivo".
  No hay atajo para traer items desde el panel principal; es intencional.

---

## 2. Qué se conserva de WeeklyReminder y qué cambia

Se **conserva** (con renombre a `Sidequest` **[D5]**):

- La arquitectura de host en bandeja sin ventana principal de WPF (`App.xaml.cs`): mutex de
  instancia única, `EventWaitHandle` para "mostrar", `NotifyIcon` vía WinForms, icono dibujado con GDI+.
- `Storage`: JSON legible, escritura atómica vía `.tmp` + `File.Move`, respaldo `*.corrupt-<ts>`.
- `ThemeService` (diccionario en slot 0, `DynamicResource`, `DwmSetWindowAttribute` para la barra
  nativa donde exista) y `LanguageService` (slot 1, evento `Changed` para texto construido en código).
- `AutoStart` (clave `Run` por usuario).
- Los retemplates de `TextBox`, `CheckBox`, `ComboBox`, `ScrollBar` en `App.xaml`, ajustados a los
  nuevos tokens.
- El patrón de `PopupWindow` para ventanas sin marco: `WindowStyle="None"`, `AllowsTransparency`,
  `DragMove` sobre áreas vacías, `Topmost` según ajuste, posición recordada y validada contra las
  pantallas disponibles.

Se **elimina**:

- `Scheduler`, `PopupTimes`, `ActiveDays`, `SnoozeMinutes`, `AutoCloseSeconds`, `LastFired` **[D1]**.
- `WeekPlan`, `RollForwardToCurrentWeek`, `RepeatsWeekly` **[D2]**.
- `EditorWindow` y `PopupWindow` como pantallas (sus técnicas se reutilizan; sus layouts no).
- El acento azul de la app (`AccentBrush`). Sidequest **no tiene color propio**: el único color es el
  tema de cada Sidequest.

Se **añade**: los modelos y pantallas de las secciones 3 y 5, el whiteboard, la fuente DM Sans
empaquetada, la migración de datos.

---

## 3. Modelo de datos y persistencia

Carpeta: `%APPDATA%\Sidequest\`.

| Archivo | Contenido |
|---------|-----------|
| `sidequests.json` | Lista de Sidequests con sus items (un solo archivo; el volumen esperado es pequeño). |
| `settings.json` | Ajustes de la app y posición de ventanas. |
| `boards\<sidequestId>.json` | Contenido de la Bitácora de un Sidequest. Se crea al abrirla por primera vez. |
| `media\<guid>.<ext>` | Imágenes copiadas (notas de Sidequest y bitácoras) **[D4]**. |

```csharp
public enum SidequestTheme { Amber, Teal, Indigo, Coral, Olive, Plum, Steel, Slate }

public sealed class Sidequest
{
    public Guid Id { get; set; }
    public string Name { get; set; }          // ≤ 60 caracteres
    public SidequestTheme Theme { get; set; }
    public DateTime CreatedAt { get; set; }
    public int SortIndex { get; set; }        // orden en la lista de Sidequests
    public ObservableCollection<SidequestItem> Items { get; set; }
    public string Notes { get; set; }         // ≤ 500 caracteres, texto plano; las URL se detectan al mostrar
    public List<string> Files { get; set; }   // ≤ 5 rutas absolutas (referencia, no copia)
    public List<string> Images { get; set; }  // ≤ 5 nombres de archivo dentro de media\
}

public sealed class SidequestItem : INotifyPropertyChanged
{
    public Guid Id { get; set; }
    public string Text { get; set; }          // ≤ 120 caracteres
    public bool Done { get; set; }
    public bool InPanel { get; set; }         // true = es un objetivo (aparece en el panel principal)
    public int SortIndex { get; set; }        // orden dentro del Sidequest
    public int? PanelIndex { get; set; }      // orden en el panel principal; null si InPanel = false
}

public sealed class AppSettings
{
    public AppTheme Theme { get; set; } = AppTheme.Dark;   // Dark es el tema por defecto
    public bool FollowSystemTheme { get; set; }            // "Sistema" en Apariencia
    public AppLanguage? Language { get; set; }             // null = seguir a Windows (igual que hoy)
    public bool StartWithWindows { get; set; }
    public bool KeepOnTop { get; set; } = true;
    public bool Collapsed { get; set; }                    // estado contraído del panel principal, SÍ se persiste
    public double? MainLeft { get; set; }
    public double? MainTop { get; set; }
    public double MainHeight { get; set; } = 640;          // alto de la ventana compacta, ajustable por el usuario
}
```

Notas:

- `Done` en un item que está en el panel se refleja en ambos sitios (es el mismo objeto).
- El progreso de un Sidequest es `Items.Count(i => i.Done) / Items.Count` e incluye los items en el panel.
- El contador del panel principal es `objetivos completados / objetivos en el panel`.
- `Collapsed` se persiste a propósito (a diferencia del popup antiguo): el usuario contrae el panel
  para esconderlo sin cerrarlo y espera encontrarlo así al volver.
- Whiteboard (`boards\<id>.json`): lista de elementos `{ Id, Kind, X, Y, W, H, Text?, Image?, From?, To? }`
  con `Kind ∈ { Text, Note, Image, Rect, Ellipse, Line, Arrow }`, más `Zoom` y `Pan`.

### Migración **[D3, D5]**

Al arrancar, si no existe `%APPDATA%\Sidequest\sidequests.json` y sí existe
`%APPDATA%\WeeklyReminder\week.json`:

1. Crear un Sidequest "Importados" con tema `Slate`.
2. Copiar cada `TaskItem` como `SidequestItem { Text, Done, InPanel = false }`.
3. Copiar de `settings.json` antiguo solo `Theme`, `Language`, `KeepOnTop`, `StartWithWindows`.
4. Renombrar `week.json` a `week.json.migrated`. No borrar nada.

### Datos de la 0.1.0 **[D10]**

Un `sidequests.json` escrito por la 0.1.0 se carga sin migración: los campos `Notes`, `Files` e
`Images` de los items se ignoran (no se leen ni se copian) y desaparecen en el siguiente guardado.
Lo demás (Sidequests, items, `Done`, `InPanel`, `SortIndex`, `PanelIndex`) se conserva tal cual.

---

## 4. Sistema visual

### 4.1 Tipografía

Una sola familia: **DM Sans** (Google Fonts, licencia OFL), empaquetada como recurso en
`Fonts/DMSans-*.ttf` y referenciada como `./Fonts/#DM Sans`. Pesos: 400, 500, 600, 700.
`Segoe UI` es el fallback. No hay fuente monoespaciada en la app.

| Uso | Tamaño | Peso | Extra |
|-----|--------|------|-------|
| Fecha del panel principal, título de Sidequest, "Sidequests", "Ajustes" | 22 | 700 | letter-spacing −0.02em |
| Título del objetivo (detalle) | 19 | 700 | |
| Nombre de Sidequest en lista | 15 | 600 | color del tema |
| Texto de objetivo / item | 14 | 500 | |
| Botones | 13 | 700 (primario) / 500 (secundario) | |
| Descripciones en Ajustes, contadores | 12 | 400 | color secundario |
| Etiquetas de sección ("NOTAS Y ENLACES") | 11 | 600 | mayúsculas, letter-spacing 0.06em, color secundario |
| Chip de Sidequest, píldoras pequeñas | 11 | 500–700 | |
| Nombre de la app en la barra de título | 11 | 600 | color secundario |

### 4.2 Color

Tokens neutros (reemplazan a los actuales `PageBrush`, `CardBrush`, etc.):

| Token | Oscuro | Claro | Uso |
|-------|--------|-------|-----|
| `Bg` | `#16171A` | `#F6F6F4` | fondo de ventana |
| `Surface` | `#1D1F23` | `#FFFFFF` | tarjetas, campos, chips |
| `Hover` | `#202328` | `#ECECE8` | fila/botón bajo el cursor |
| `Line` | `#2A2D33` | `#E2E2DE` | bordes suaves, separadores |
| `Line2` | `#3A3E46` | `#C4C4BE` | bordes de controles |
| `Text` | `#E8E9EC` | `#17181A` | texto principal |
| `Text2` | `#9A9EA8` | `#5B5F68` | texto secundario, iconos en reposo |
| `Inv` | `#16171A` | `#FFFFFF` | texto sobre relleno `Text` o sobre color de tema |
| `Canvas` | `#111214` | `#EFEFEC` | fondo de la Bitácora |

Temas de Sidequest (ocho, luminosidad emparejada):

| Tema | Oscuro | Claro |
|------|--------|-------|
| Amber (Ámbar) | `#D4A24A` | `#9A6B1F` |
| Teal (Turquesa) | `#4FB5A8` | `#1F6F6B` |
| Indigo (Índigo) | `#8B93E6` | `#4A4F9C` |
| Coral | `#E0795F` | `#A1452F` |
| Olive (Oliva) | `#A3B357` | `#5F6B2A` |
| Plum (Ciruela) | `#C47FB5` | `#7A3F6E` |
| Steel (Acero) | `#6AA5D8` | `#3D6285` |
| Slate (Grafito) | `#A0A4AD` | `#55554F` |

Derivados de un tema `C` (calculados, no tokens): tinte de fondo `C @ 14 %`, tinte de botón en lista
`C @ 9 %` con borde `C @ 34 %`, pista de progreso `C @ 25 %`, borde de "Enviar objetivo" `C @ 55 %`.

Dónde aparece el color de tema, y en ningún otro sitio:

- Panel principal: nombre del Sidequest en el chip; relleno del checkbox al completar.
- Lista de Sidequests: tinte del botón, nombre, barra de progreso.
- Detalle de Sidequest y de objetivo: banda de cabecera tintada, nombre, progreso, píldora "Objetivo
  actual", checks de items completados, icono de Bitácora, borde de "Enviar objetivo".
- Bitácora: línea de 2 px bajo la barra de título, nombre del Sidequest, elemento seleccionado.

Contraste: todo texto de tema sobre `Bg`/`Surface` cumple 4.5:1 con los valores de arriba. Si en el
futuro se permite color libre, hay que validar contraste al elegirlo.

### 4.3 Forma y espacio

- Radios: tarjetas y campos 10 px; bandas y botones grandes 12 px; chips, píldoras y botones de
  acción 999 px (píldora); checkbox 6 px; botones de icono 8 px.
- Bordes: 1 px `Line` en tarjetas y chips; 1 px `Line2` en botones con borde y en el marco de ventana.
- Espaciado: padding lateral 12 px en listas de tarjetas, 16 px en cabeceras; gap 6 px entre tarjetas;
  altura de fila 50 px (panel) / 48 px (detalle); altura de barra de título 36 px.
- Sin sombras dentro de las ventanas. La ventana tiene sombra suave si la plataforma lo permite.
- Iconos: trazo de 1.5 px, 16 px, `Text2` en reposo y `Text` en hover. Nunca emoji ni glifos de texto
  (los botones actuales con "▾" y "✕" se reemplazan por `Path`).

### 4.4 Componentes

| Componente | Especificación |
|------------|----------------|
| **Barra de título** | 36 px. Izquierda: "Sidequest" 11/600 `Text2` (o enlace "‹ Volver" en sub-paneles, con el nombre de la app a la derecha). Derecha: botones de icono 28×28: pin (activo = fondo `Hover`, color `Text`), contraer (chevron arriba), cerrar. Sin minimizar. Toda la barra arrastra la ventana. |
| **Tarjeta de objetivo** | 50 px, `Surface`, borde `Line`, radio 10, padding 0 8 0 4. Asa ⋮⋮ (18 px, solo en hover) · checkbox 18 px · texto 14/500 con elipsis · chip · acciones en hover: lápiz, devolver, eliminar. El texto no es clicable. |
| **Asa de arrastre** | 18 × 32, seis puntos de 1.3 px en `Text2`, cursor `grab`. Su espacio está reservado siempre (no desplaza el contenido) pero solo es visible en hover o con foco de teclado. Es el único punto que inicia el arrastre. |
| **Tarjeta arrastrada** | La tarjeta sigue al cursor (eje vertical), borde `Line2`, sombra `0 10 24 rgba(0,0,0,.45)`, cursor `grabbing`. En su posición de destino, un hueco del mismo alto con borde 1.5 px punteado en `C` y relleno `C @ 6 %`. Las demás tarjetas se desplazan para abrir el hueco (animación ≤ 120 ms). |
| **Tarjeta en edición** | Misma tarjeta, borde 1.5 px en `C`. El asa y el chip desaparecen; el texto se vuelve un campo sin borde (14/500, caret `C`, texto seleccionado al entrar); a la derecha, confirmar (check en `C`) y cancelar (x). Debajo, alineado a la derecha, "Enter guarda · Esc cancela" en 11 `Text2`. |
| **Área con scroll** | Contenido con scroll vertical entre cabecera y pie fijos. Cuando hay contenido oculto debajo: desvanecido de 36 px de `Bg @ 0 %` a `Bg` sobre el borde inferior; arriba, el mismo desvanecido invertido cuando ya se bajó. Barra de 4 px, radio 2, `ScrollThumbBrush`, a 3 px del borde derecho, **siempre visible** mientras haya overflow (no solo en hover). Sin overflow: ni desvanecido ni barra. |
| **Asa de redimensión** | Barra de 36 × 4, radio 2, `Line2`, centrada en el pie, 8 px bajo su contenido. Es solo una señal: el área real de redimensión es todo el borde inferior de la ventana (6 px, cursor `SizeNS`). |
| **Pestañas** | Contenedor `Surface`, borde `Line`, radio 12, padding 3, dos opciones de igual ancho y 32 px. Activa: relleno `Text`, texto 13/700 `Inv`. Inactiva: texto 13/500 `Text2`. Contador opcional junto a la etiqueta al 70 % de opacidad. |
| **Checkbox** | 18 px, radio 6, borde 1.5 px `Line2`. Marcado: relleno con el color del tema del Sidequest, tick `Inv` de 2.25 px. Foco: anillo 2 px `Text`. |
| **Chip de Sidequest** | Píldora 24 px, `Surface`, borde `Line`, texto 11/500 en el color del tema. Click → detalle del Sidequest. |
| **Botón primario** | Píldora 36 px, relleno `Text`, texto 13/700 `Inv`. Uno por pantalla como máximo. |
| **Botón secundario** | Píldora 34 px, `Surface`, borde `Line2`, texto 13/500. Hover: borde `Text`. |
| **"Enviar objetivo"** | Píldora 30 px, sin relleno, borde 1.5 px `C @ 55 %`, texto 12/700 en `C`, icono flecha. |
| **Píldora de estado** | "Objetivo actual": 24 px, relleno `C`, texto 11/700 `Inv`. |
| **Banda de cabecera** | Margen 12 px, radio 12, relleno `C @ 14 %`. Contiene título 22/700 en `C`, progreso 6 px y acciones. |
| **Botón de Sidequest (lista)** | Radio 12, relleno `C @ 9 %`, borde `C @ 34 %`, nombre 15/600 en `C`, contador 12 `Text2`, progreso 3 px. |
| **Campo de texto** | `Surface`, borde `Line`, radio 10, padding 12. Foco: borde `Text` (o `C` en el formulario de nuevo Sidequest). |
| **Segmentado** (Ajustes) | Contenedor `Surface` borde `Line` radio 12 con padding 3; opción activa relleno `Text` texto `Inv`. |
| **Interruptor** (Ajustes) | 40×24, pista `Surface` borde `Line2`; activo: pista `Text`, botón `Inv`. |
| **Botón punteado** | "Nuevo Sidequest", "Añadir item", añadir imagen: borde 1.5 px punteado `Line2`, texto `Text2`. |

---

## 5. Pantallas

Todas las pantallas compactas comparten una ventana de **400 px de ancho fijo y alto fijo elegido por
el usuario** (`MainWindow`, `Settings.MainHeight`, por defecto 640, mínimo 360, máximo el alto del área
de trabajo). El alto **no cambia al navegar** entre vistas: cada vista organiza su contenido en cabecera
fija, área con scroll (4.4) y pie fijo. El usuario lo ajusta arrastrando el borde inferior; se guarda al
soltar. El estado contraído (5.2) ignora este alto y lo restaura al expandir. Los paneles 1–4 son vistas
que se reemplazan dentro de ella (un `ContentControl` con la vista activa;
sin animación de transición). La Bitácora y Ajustes abren **ventanas aparte**. Referencia visual:
`docs/design/reference/<archivo>.html`.

### 5.1 Panel principal — `FnMain.html`, tema claro en `FnMainClaro.html`

- Alto: el de la ventana (5). La lista de objetivos es el área con scroll; cabecera de fecha y pie con
  "Sidequests →" quedan fijos.
- Barra de título: "Sidequest", pin, contraer, cerrar. **Cerrar** cierra la ventana, no la app (la
  app sigue en la bandeja; doble clic o "Mostrar" la reabre).
- Cabecera (padding 22/16/14): línea 1 con el día de la semana en etiqueta de sección y el contador
  `completados / total` a la derecha (12 `Text2`); línea 2 con la fecha completa 22/700,
  formateada con la cultura activa ("17 de septiembre de 2026" / "17 September 2026").
- Lista de tarjetas de objetivo en el orden `PanelIndex`. Sin secciones ni agrupación.
- El texto del objetivo no navega a ningún sitio. Click en el chip → detalle del Sidequest.
- Hover sobre una tarjeta: fondo `Hover`, aparece el asa ⋮⋮ a la izquierda y tres botones de icono a la
  derecha: **renombrar** (lápiz), **devolver al Sidequest** y **eliminar** (este último pide confirmación
  con el `ConfirmDialog` existente).
- **Renombrar** (`FnMainEdit.html`): la tarjeta pasa al estado "en edición" (4.4). Enter o perder el foco
  guarda; Esc cancela; un nombre vacío no se guarda (se restaura el anterior). F2 sobre una tarjeta con
  foco también entra en edición.
- **Reordenar** (`FnMainDrag.html`): arrastrar desde el asa. Al soltar se actualiza `PanelIndex` y se
  guarda. Esc durante el arrastre lo cancela. Teclado: con el asa enfocada, Alt+↑ / Alt+↓ mueven una
  posición. Los objetivos completados se pueden reordenar igual que los pendientes.
- Pie (60 px, borde superior `Line`): botón primario "Sidequests →" a la izquierda, icono de ajustes a
  la derecha.
- Estado vacío: texto `Text2` centrado "Sin objetivos. Entra a un Sidequest y envía uno." y debajo el
  botón "Sidequests →".

### 5.2 Panel principal contraído — `FnMainMini.html`

- 400 × 50 px, una sola fila: fecha completa 13/600 · contador en píldora `Text` · chevron abajo
  (expandir) · cerrar. Sin nombre de app ni pin.
- Se llega con el chevron arriba de la barra de título. Al contraer, el borde **inferior** de la ventana
  se mantiene fijo (el mismo comportamiento que hoy tiene el popup), para que un panel apoyado en el
  borde inferior de la pantalla se enrolle hacia arriba.
- `KeepOnTop` se mantiene mientras está contraído. El estado se persiste (`Settings.Collapsed`).

### 5.3 ~~Detalle del objetivo~~ (eliminado en 0.2)

Ya no existe. Su contenido pasa a la pestaña **Notas** del detalle del Sidequest (5.6).

### 5.4 Lista de Sidequests — `FnSidequests.html`

Referencias: `FnSidequests.html` (pocos), `FnSidequestsLargo.html` y `FnSidequestsCompacto.html` (14
Sidequests en 640 px, para comparar), `FnSidequestsLargoNuevo.html`.

- Barra: "‹ Panel principal" · "Sidequest". Cabecera: solo "Sidequests" 22/700 (sin contador; no existe
  archivado, así que todos los Sidequests están siempre en la lista).
- Área con scroll (4.4) con un botón por Sidequest, orden `SortIndex`: nombre, `completados / total` y
  barra de progreso. Clic → detalle. Tamaño del botón **[D9]**: *compacto* (42 px, radio 10, nombre
  14/600, progreso de 2 px pegado al borde inferior, gap 6), unos 9 visibles a 640 px. El *normal*
  (64 px, radio 12, progreso de 3 px bajo el nombre; unos 6 visibles) queda en `FnSidequestsLargo.html`
  solo como comparación.
- Pie fijo: botón punteado "+ Nuevo Sidequest" y asa de redimensión. Siempre visible, sin scroll.
- Estado vacío: lista vacía y el pie; sin texto extra.

### 5.5 Nuevo Sidequest — `FnSidequestNuevo.html`

- Misma pantalla que 5.4: al pulsar "Nuevo Sidequest", el botón punteado **del pie** se reemplaza por una
  tarjeta de formulario tintada con el tema seleccionado. El pie crece hacia arriba y el área con scroll
  se reduce; la lista no cambia de tamaño de botón.
- Formulario: etiqueta "NUEVO SIDEQUEST" en el color del tema; campo "Nombre" (foco automático, borde
  en el color del tema); "Tema": ocho cuadrados de 30 px, radio 10, el seleccionado con anillo de 2 px
  `Text` a 2 px de distancia; botones "Cancelar" (secundario) y "Crear" (primario).
- Crear requiere nombre no vacío; Enter crea, Escape cancela. Tras crear se navega al detalle del
  nuevo Sidequest (vacío), donde el usuario añade items.
- Tema por defecto: el primero que no esté usado por otro Sidequest; si todos están usados, Amber.

### 5.6 Detalle del Sidequest — `FnSidequest.html`, `FnSidequestAdd.html`, `FnSidequestNotas.html`

- Barra: "‹ Sidequests" · "Sidequest".
- Banda tintada: nombre 22/700 en `C` (clic prolongado o doble clic edita el nombre en línea), menú "⋯"
  con **Renombrar**, **Cambiar tema** y **Eliminar Sidequest** (confirmación; elimina también sus
  objetivos del panel y su Bitácora); progreso 6 px con `completados / total`; botón secundario
  "Bitácora" con icono de libro en `C`.
- Bajo la banda, **pestañas Items / Notas** (4.4). La pestaña activa se recuerda por Sidequest durante
  la sesión (no se persiste). Items muestra el número total de items.

**Pestaña Items**

- Lista de items en tarjetas, en este orden fijo: (1) items con `InPanel` ("Objetivo actual"), (2)
  pendientes con botón "Enviar objetivo", (3) completados (sin tarjeta: check en `C` + texto tachado
  `Text2`). Dentro de cada grupo, por `SortIndex`.
- "Enviar objetivo" marca `InPanel = true`, asigna `PanelIndex` al final del panel y mueve la tarjeta
  al grupo (1) sin salir de la pantalla.
- Hover sobre un item pendiente: asa ⋮⋮ a la izquierda y, antes de "Enviar objetivo", **renombrar**
  (lápiz) y **eliminar** (confirmación). Sobre un "Objetivo actual": asa y renombrar.
- Renombrar funciona igual que en el panel principal (5.1).
- Reordenar: se arrastra dentro de su grupo (objetivos actuales entre sí, pendientes entre sí); no se
  puede soltar un item en otro grupo. Los completados no se reordenan.
- **Añadir item** (`FnSidequestAdd.html`): el botón punteado del pie se convierte en un campo píldora de
  44 px con borde 1.5 px en `C`, icono "+" en `C` y botón de cerrar; debajo, "Enter añade y sigue · Esc
  cierra" en 11 `Text2`. Enter crea el item al final de los pendientes, lo resalta (borde `C @ 50 %`,
  relleno `C @ 8 %` durante 1.5 s), hace scroll hasta él y deja el campo vacío y con foco para el
  siguiente. Esc o perder el foco con el campo vacío cierra; con texto, perder el foco no crea nada
  (evita items accidentales).

**Pestaña Notas** (`FnSidequestNotas.html`)

Lo que en 0.1 era el detalle del objetivo, ahora a nivel de Sidequest. El contexto donde se compilan
ideas y apuntes; los objetivos son el resultado destilado.

- **Notas y enlaces** con contador `n / 500`: un solo campo de texto plano multilínea de 176 px. Las URL
  (`http://`, `https://`) se muestran subrayadas en el color del tema y se abren con **Ctrl+clic** (o
  clic simple cuando el campo no tiene el foco). Implementación sugerida: `RichTextBox` con detección de
  URL al perder el foco, o `TextBox` en edición y `TextBlock` con `Hyperlink` en reposo.
- **Archivos locales** con contador `n / 5`: filas de 42 px con icono, nombre de archivo, carpeta
  contenedora abreviada (`…\Mudanza`) y botón de quitar. Clic en la fila abre el archivo con la app
  asociada (`Process.Start` con `UseShellExecute`). "Vincular archivo" abre un `OpenFileDialog`; se
  deshabilita al llegar a 5. Si el archivo ya no existe, la fila se muestra en `Text2` con tooltip.
- **Imágenes** con contador `n / 5`: rejilla de 5 celdas de 66 px, radio 10. Celdas ocupadas muestran la
  miniatura (`Stretch=UniformToFill`); clic abre la imagen. Una celda "+" (punteada) añade con
  `OpenFileDialog` **o arrastrando** un archivo de imagen sobre la pestaña; las restantes son celdas
  vacías punteadas. Hover en una ocupada muestra "quitar".
- Los cambios se guardan al salir del campo o al cambiar de pestaña; no hay botón de guardar.
- La Bitácora se mantiene: Notas es para texto, enlaces y archivos; la Bitácora, para pensar en el
  espacio.
- Los completados se pueden desmarcar desde aquí solo si no están en el panel (si están en el panel, se
  desmarcan desde el panel). Regla simple: **el estado `Done` se edita donde se ve el checkbox**.

### 5.7 Bitácora — `FnBitacora.html`

- Ventana aparte, 1200 × 760 por defecto, redimensionable, barra de título propia (nombre de la app,
  minimizar, maximizar, cerrar) y línea de 2 px en `C` bajo ella. Debajo, fila de 48 px: "‹ Volver"
  (cierra la ventana), nombre del Sidequest en `C` + "/ Bitácora", y "Guardado" a la derecha (aparece
  2 s tras cada guardado automático).
- Barra de herramientas vertical de 52 px a la izquierda, botones de 36 px: **Seleccionar**, **Texto**,
  **Nota**, **Imagen**, separador, **Rectángulo**, **Círculo**, **Línea**, **Flecha**. La herramienta activa
  se muestra con relleno `Text` e icono `Inv`.
- Lienzo con fondo `Canvas` y rejilla de puntos de 24 px en `Line`. Zoom con Ctrl+rueda (50–200 %) y
  control en la esquina inferior derecha; desplazamiento con rueda / arrastre con la herramienta
  Seleccionar sobre espacio vacío o con el botón central.
- Elementos: Texto (sin caja, 14 px o 26/600 para títulos vía doble tamaño), Nota (tarjeta `Surface`
  borde `Line2` radio 8, texto 14 px), Imagen (copiada a `media\` **[D4]**; miniatura con borde
  `Line2`), Rectángulo, Círculo (elipse), Línea, Flecha. Formas con trazo 1.5 px `Text2`; una forma
  seleccionada se dibuja en `C` con relleno `C @ 8 %`, marco de selección 1 px `Text` y cuatro asas de
  7 px (solo indican selección en v1 **[D6]**).
- Interacción v1: crear arrastrando, mover arrastrando, editar texto con doble clic, borrar con Supr,
  deshacer/rehacer con Ctrl+Z / Ctrl+Y (mínimo 20 pasos). Guardado automático 500 ms después del último
  cambio.
- Sin capas, sin agrupación, sin conectores anclados: las flechas son elementos libres.

### 5.8 Ajustes — `FnAjustes.html`

- Ventana aparte, 720 × 520, no redimensionable, barra propia con "‹ Panel principal" y "Sidequest".
- Título "Ajustes" 22/700 y cuatro filas de 80 px separadas por `Line`, cada una con título 14/500 y
  descripción 12 `Text2`:
  1. **Apariencia** — segmentado Claro / Oscuro / Sistema. "Sistema" sigue el tema de Windows
     (`FollowSystemTheme`). Se aplica al instante a todas las ventanas.
  2. **Idioma** — segmentado Español / English. Solo cambia la interfaz. Se aplica al instante.
  3. **Abrir al iniciar el equipo** — interruptor (`AutoStart`).
  4. **Panel principal siempre visible** — interruptor (`KeepOnTop`). Se aplica al panel abierto.
- Pie: "Sidequest 0.1" en 11 `Text2` (versión del ensamblado).
- Los cambios se guardan al momento, como hoy.

### 5.9 Bandeja del sistema

Menú: **Mostrar panel**, **Sidequests…**, **Ajustes…**, **Restablecer posición**, separador, **Salir**.
Doble clic en el icono muestra el panel. El icono se sigue dibujando con GDI+ (sin binarios); su
forma puede ser un cuadrado redondeado neutro con una "S", en `Text` sobre transparente.

---

## 6. Ventanas y plataforma (WPF)

- Todas las ventanas usan `WindowStyle="None"` + `AllowsTransparency="True"` con un `Border` raíz de
  radio 12 y borde 1 px `Line2`, como hace hoy `PopupWindow`. La barra de título es contenido propio;
  `DwmSetWindowAttribute` deja de ser necesario salvo para los diálogos con marco nativo (`ConfirmDialog`
  puede mantenerlo).
- `MainWindow`: `ShowInTaskbar="False"`, `Topmost = Settings.KeepOnTop`, posición recordada
  (`MainLeft/Top`) con la misma validación de pantalla y "Restablecer posición" que hoy.
- Tamaño: `MinWidth = MaxWidth = 400`, `Height = Settings.MainHeight`, `MinHeight = 360`,
  `MaxHeight = WorkArea.Height`. **Sin** `SizeToContent`. Redimensión solo vertical por el borde
  inferior: `WindowChrome` con `ResizeBorderThickness="0,0,0,6"` o un `Thumb` propio de 6 px a lo ancho
  del borde inferior que ajusta `Height` en `DragDelta`. Guardar `MainHeight` en `DragCompleted`.
  Si la ventana está apoyada en el borde inferior de la pantalla, la redimensión mantiene fijo el borde
  superior (crece hacia abajo hasta el límite del área de trabajo).
- Cada vista es un `Grid` de tres filas: cabecera `Auto`, contenido `*` (un `ScrollViewer`), pie `Auto`.
  Las señales de scroll (4.4) se calculan con `ScrollViewer.ScrollChanged`: desvanecido inferior visible
  si `VerticalOffset < ScrollableHeight`, superior si `VerticalOffset > 0`.
- Bitácora y Ajustes: `ShowInTaskbar="True"`, se abren con `Owner = MainWindow` para que hereden el
  tema y se cierren con la app; no son `Topmost`.
- Fuente: `<Setter Property="TextElement.FontFamily" Value="./Fonts/#DM Sans"/>` en `AppWindow`.
  Comprobar que el `FontWeight` 500 y 600 resuelven a los archivos correctos (WPF los interpola si no).
- `TextOptions.TextFormattingMode="Display"` se mantiene.

---

## 7. Textos (claves para `Strings/*.xaml`)

Las claves de WeeklyReminder que no aparecen aquí se eliminan. Formato: `Clave — Español — English`.

```
L_AppName             — Sidequest — Sidequest
L_Back                — Volver — Back
L_MainPanel           — Panel principal — Main panel
L_Sidequests          — Sidequests — Sidequests
L_CounterFmt          — {0} / {1} — {0} / {1}
L_PanelEmpty          — Sin objetivos. Entra a un Sidequest y envía uno. — No objectives yet. Open a Sidequest and send one.
L_ReturnToSidequest   — Devolver al Sidequest — Return to Sidequest
L_DeleteObjective     — Eliminar objetivo — Delete objective
L_DeleteObjectiveQ    — ¿Eliminar este objetivo? También se borra de su Sidequest. — Delete this objective? It is removed from its Sidequest too.
L_Collapse            — Contraer — Collapse
L_Expand              — Expandir — Expand
L_KeepOnTop           — Mantener encima de otras apps — Keep above other apps
L_Close               — Cerrar — Close
L_Items               — Items — Items
L_Notes               — Notas — Notes
L_RenameObjective     — Renombrar objetivo — Rename objective
L_RenameItem          — Renombrar item — Rename item
L_SaveName            — Guardar nombre — Save name
L_EnterSaves          — Enter guarda — Enter saves
L_EscCancels          — Esc cancela — Esc cancels
L_DragToReorder       — Arrastrar para reordenar — Drag to reorder
L_NewItem             — Nuevo item — New item
L_EnterAddsNext       — Enter añade y sigue — Enter adds and continues
L_EscCloses           — Esc cierra — Esc closes
L_NotesAndLinks       — Notas y enlaces — Notes and links
L_LocalFiles          — Archivos locales — Local files
L_LinkFile            — Vincular archivo — Link file
L_UnlinkFile          — Desvincular archivo — Unlink file
L_FileMissing         — No se encuentra el archivo — File not found
L_Images              — Imágenes — Images
L_AddImage            — Añadir imagen — Add image
L_RemoveImage         — Quitar imagen — Remove image
L_NewSidequest        — Nuevo Sidequest — New Sidequest
L_Name                — Nombre — Name
L_Theme               — Tema — Theme
L_Cancel              — Cancelar — Cancel
L_Create              — Crear — Create
L_Theme_Amber         — Ámbar — Amber
L_Theme_Teal          — Turquesa — Teal
L_Theme_Indigo        — Índigo — Indigo
L_Theme_Coral         — Coral — Coral
L_Theme_Olive         — Oliva — Olive
L_Theme_Plum          — Ciruela — Plum
L_Theme_Steel         — Acero — Steel
L_Theme_Slate         — Grafito — Slate
L_ItemsFmt            — {0} / {1} items — {0} / {1} items
L_Journal             — Bitácora — Journal
L_CurrentObjective    — Objetivo actual — Current objective
L_SendObjective       — Enviar objetivo — Send objective
L_AddItem             — Añadir item — Add item
L_DeleteItem          — Eliminar item — Delete item
L_MoreOptions         — Más opciones — More options
L_Rename              — Renombrar — Rename
L_ChangeTheme         — Cambiar tema — Change theme
L_DeleteSidequest     — Eliminar Sidequest — Delete Sidequest
L_DeleteSidequestQ    — ¿Eliminar "{0}" con sus {1} items y su Bitácora? — Delete "{0}" with its {1} items and its Journal?
L_Saved               — Guardado — Saved
L_Tool_Select         — Seleccionar — Select
L_Tool_Text           — Texto — Text
L_Tool_Note           — Nota — Note
L_Tool_Image          — Imagen — Image
L_Tool_Rect           — Rectángulo — Rectangle
L_Tool_Ellipse        — Círculo — Circle
L_Tool_Line           — Línea — Line
L_Tool_Arrow          — Flecha — Arrow
L_ZoomIn              — Acercar — Zoom in
L_ZoomOut             — Alejar — Zoom out
L_Settings            — Ajustes — Settings
L_Appearance          — Apariencia — Appearance
L_AppearanceHelp      — Tema de todos los paneles — Theme for every panel
L_Light               — Claro — Light
L_Dark                — Oscuro — Dark
L_System              — Sistema — System
L_Language            — Idioma — Language
L_LanguageHelp        — Solo cambia la interfaz, no tu contenido — Changes the interface only, not your content
L_OpenOnStartup       — Abrir al iniciar el equipo — Open at startup
L_OpenOnStartupHelp   — Sidequest se abre cuando enciendes tu PC — Sidequest opens when your PC starts
L_AlwaysVisible       — Panel principal siempre visible — Main panel always visible
L_AlwaysVisibleHelp   — Se mantiene encima de las demás aplicaciones — Stays above other applications
L_VersionFmt          — Sidequest {0} — Sidequest {0}
L_TrayShow            — Mostrar panel — Show panel
L_TraySidequests      — Sidequests… — Sidequests…
L_TraySettings        — Ajustes… — Settings…
L_TrayResetPosition   — Restablecer posición — Reset position
L_TrayQuit            — Salir — Quit
L_ImportedSidequest   — Importados — Imported
```

Formatos de fecha: día de la semana con `dddd` en mayúscula inicial; fecha completa con `D` de la
cultura activa (`es-ES` / `en-GB`, como hoy).

---

## 8. Orden de implementación sugerido

1. Renombre del proyecto, modelos nuevos, `Storage` y migración **[D3, D5]**. Tests de serialización.
2. Tokens de tema (`Themes/*.xaml`), fuente empaquetada, retemplates en `App.xaml`.
3. `MainWindow` con navegación entre vistas: panel principal (con vacío, hover, contraído) → lista
   de Sidequests → nuevo → detalle (pestaña Items, sin renombrar ni arrastrar todavía).
4. Renombrar y reordenar (panel principal y pestaña Items), añadir item, y la pestaña Notas del
   Sidequest: notas con URL, archivos, imágenes con copia a `media\`.
5. Ajustes y bandeja.
6. Bitácora (la pieza más grande; puede entregarse después del resto).
7. Tema claro: revisar cada pantalla contra `FnMainClaro.html` y los tokens de la 4.2.

Criterio de terminado por pantalla: coincide con su HTML de referencia a escala 1:1 en ambos temas
y en ambos idiomas, y todas las interacciones de la sección 5 funcionan con teclado (Tab, Enter,
Escape, Supr) además de con ratón.
