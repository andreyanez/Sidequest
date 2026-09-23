# Sidequest — especificación de diseño e implementación

Versión 0.1 · 18 de septiembre de 2026 · Reemplaza la UI de WeeklyReminder por Sidequest.

Este documento describe **qué** construir. Las convenciones del repo (cómo compilar, cómo
se organizan temas e idiomas, qué patrones de WPF se usan) viven en `README.md` y, cuando
exista, en `CLAUDE.md`. Los artboards de referencia están en `docs/design/reference/`
(HTML autocontenido: ábrelo en un navegador para ver el diseño a escala real y leer los
valores exactos de CSS).

---

## 0. Decisiones pendientes

Estas decisiones cambian el alcance. Están marcadas con **[DECIDIR]** en el texto; hasta que
se resuelvan, la opción por defecto es la que aparece primero.

| # | Tema | Opción por defecto | Alternativa |
|---|------|--------------------|-------------|
| D1 | Recordatorios programados, snooze y auto-cierre de WeeklyReminder | **Se eliminan.** Sidequest es un panel persistente, no un popup que aparece solo. | Conservar el `Scheduler` para mostrar el panel principal a horas fijas. |
| D2 | Rollover semanal | **Se elimina.** Los objetivos no expiran por fecha; el usuario los quita. | — |
| D3 | Migración de `week.json` | **Se importa** a un Sidequest llamado "Importados" (tema Grafito); los items conservan `Done`. | Descartar los datos antiguos. |
| D4 | Imágenes adjuntas a un objetivo | **Se copian** a `%APPDATA%\Sidequest\media\` (el original puede moverse). Los archivos locales solo se **referencian** por ruta. | Referenciar también las imágenes. |
| D5 | Nombre del ejecutable, mutex y carpeta de datos | **Se renombra todo a `Sidequest`.** La carpeta antigua `%APPDATA%\WeeklyReminder\` solo se lee para migrar. | Mantener el nombre interno y cambiar solo la UI. |
| D6 | Whiteboard: redimensionar formas | **v1 no redimensiona**; solo crear, mover, editar texto y borrar. | Incluir asas de redimensión en v1. |

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
- Los detalles de un objetivo (notas, archivos, imágenes) se editan solo desde el panel principal
  (click en el texto del objetivo). Un item que no está en el panel no tiene pantalla de detalle.
- Límites por item: notas ≤ 500 caracteres (las URL cuentan), archivos ≤ 5, imágenes ≤ 5. Al
  alcanzar un límite, el botón de añadir correspondiente se deshabilita y el contador lo muestra.
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
| `media\<guid>.<ext>` | Imágenes copiadas (objetivos y bitácoras) **[D4]**. |

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
}

public sealed class SidequestItem : INotifyPropertyChanged
{
    public Guid Id { get; set; }
    public string Text { get; set; }          // ≤ 120 caracteres
    public bool Done { get; set; }
    public bool InPanel { get; set; }         // true = es un objetivo (aparece en el panel principal)
    public int SortIndex { get; set; }        // orden dentro del Sidequest
    public int? PanelIndex { get; set; }      // orden en el panel principal; null si InPanel = false
    public string Notes { get; set; }         // ≤ 500 caracteres, texto plano; las URL se detectan al mostrar
    public List<string> Files { get; set; }   // ≤ 5 rutas absolutas (referencia, no copia)
    public List<string> Images { get; set; }  // ≤ 5 nombres de archivo dentro de media\
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
| **Tarjeta de objetivo** | 50 px, `Surface`, borde `Line`, radio 10. Checkbox 18 px · texto 14/500 con elipsis · chip · acciones (aparecen en hover: devolver, eliminar). |
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

Todas las pantallas compactas comparten una ventana de **400 px de ancho** (`MainWindow`). Los
paneles 1–4 son vistas que se reemplazan dentro de ella (un `ContentControl` con la vista activa;
sin animación de transición). La Bitácora y Ajustes abren **ventanas aparte**. Referencia visual:
`docs/design/reference/<archivo>.html`.

### 5.1 Panel principal — `FnMain.html`, tema claro en `FnMainClaro.html`

- Alto: crece con el contenido hasta `WorkArea.Height − 48`; luego scroll (mismo enfoque que hoy).
  Alto mínimo 360 px para que el estado vacío no quede aplastado.
- Barra de título: "Sidequest", pin, contraer, cerrar. **Cerrar** cierra la ventana, no la app (la
  app sigue en la bandeja; doble clic o "Mostrar" la reabre).
- Cabecera (padding 22/16/14): línea 1 con el día de la semana en etiqueta de sección y el contador
  `completados / total` a la derecha (12 `Text2`); línea 2 con la fecha completa 22/700,
  formateada con la cultura activa ("17 de septiembre de 2026" / "17 September 2026").
- Lista de tarjetas de objetivo en el orden `PanelIndex`. Sin secciones ni agrupación.
- Click en el texto → detalle del objetivo. Click en el chip → detalle del Sidequest.
- Hover sobre una tarjeta: fondo `Hover` y aparecen dos botones de icono a la derecha: **devolver al
  Sidequest** y **eliminar** (este último pide confirmación con el `ConfirmDialog` existente).
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

### 5.3 Detalle del objetivo — `FnObjetivo.html`

- Barra: "‹ Panel principal" · "Sidequest".
- Banda tintada con el tema del Sidequest: checkbox (mismo comportamiento que en el panel), título del
  objetivo 19/700, chip del Sidequest debajo (click → detalle del Sidequest).
- Sección **Notas y enlaces** con contador `n / 500`: un solo campo de texto plano multilínea. Las URL
  (`http://`, `https://`) se muestran subrayadas en el color del tema y se abren con **Ctrl+clic** (o clic
  simple cuando el campo no tiene el foco). Implementación sugerida: `RichTextBox` con detección de URL
  al perder el foco, o `TextBox` en edición y `TextBlock` con `Hyperlink` en reposo.
- Sección **Archivos locales** con contador `n / 5`: filas de 42 px con icono, nombre de archivo, carpeta
  contenedora abreviada (`…\Mudanza`) y botón de quitar. Clic en la fila abre el archivo con la app
  asociada (`Process.Start` con `UseShellExecute`). Botón "Vincular archivo" abre un `OpenFileDialog`;
  se deshabilita al llegar a 5. Si el archivo ya no existe, la fila se muestra en `Text2` con tooltip.
- Sección **Imágenes** con contador `n / 5`: rejilla de 5 celdas de 66 px, radio 10. Celdas ocupadas
  muestran la miniatura (`Stretch=UniformToFill`); clic abre la imagen. Una celda "+" (punteada) añade
  con `OpenFileDialog` **o arrastrando** un archivo de imagen sobre el panel; las restantes son celdas
  vacías punteadas. Hover en una ocupada muestra "quitar".
- Los cambios se guardan al salir del campo o cerrar la vista; no hay botón de guardar.

### 5.4 Lista de Sidequests — `FnSidequests.html`

- Barra: "‹ Panel principal" · "Sidequest". Cabecera: "Sidequests" 22/700 y "n activos" a la derecha.
- Un botón por Sidequest (spec en 4.4), orden `SortIndex`, con nombre, `completados / total` y barra de
  progreso. Clic → detalle.
- Al final, botón punteado "+ Nuevo Sidequest".
- Estado vacío: solo el botón punteado, sin texto extra.

### 5.5 Nuevo Sidequest — `FnSidequestNuevo.html`

- Misma pantalla que 5.4: al pulsar "Nuevo Sidequest", el botón punteado se reemplaza por una tarjeta
  de formulario tintada con el tema seleccionado, y los botones de la lista pasan a su versión compacta
  (44 px, sin barra de progreso) para dejar sitio.
- Formulario: etiqueta "NUEVO SIDEQUEST" en el color del tema; campo "Nombre" (foco automático, borde
  en el color del tema); "Tema": ocho cuadrados de 30 px, radio 10, el seleccionado con anillo de 2 px
  `Text` a 2 px de distancia; botones "Cancelar" (secundario) y "Crear" (primario).
- Crear requiere nombre no vacío; Enter crea, Escape cancela. Tras crear se navega al detalle del
  nuevo Sidequest (vacío), donde el usuario añade items.
- Tema por defecto: el primero que no esté usado por otro Sidequest; si todos están usados, Amber.

### 5.6 Detalle del Sidequest — `FnSidequest.html`

- Barra: "‹ Sidequests" · "Sidequest".
- Banda tintada: nombre 22/700 en `C` (clic prolongado o doble clic edita el nombre en línea), menú "⋯"
  con **Renombrar**, **Cambiar tema** y **Eliminar Sidequest** (confirmación; elimina también sus
  objetivos del panel y su Bitácora); progreso 6 px con `completados / total`; botón secundario
  "Bitácora" con icono de libro en `C`.
- Lista de items en tarjetas, en este orden fijo: (1) items con `InPanel` ("Objetivo actual"), (2)
  pendientes con botón "Enviar objetivo", (3) completados (sin tarjeta: check en `C` + texto tachado
  `Text2`). Dentro de cada grupo, por `SortIndex`.
- "Enviar objetivo" marca `InPanel = true`, asigna `PanelIndex` al final del panel y mueve la tarjeta
  al grupo (1) sin salir de la pantalla.
- Hover sobre un item pendiente muestra a la izquierda del botón un icono de eliminar (confirmación).
- Pie: botón punteado "+ Añadir item" que se convierte en un campo de texto en línea; Enter añade y
  deja el campo abierto para el siguiente; Escape cierra.
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
  (`MainLeft/Top`) con la misma validación de pantalla y "Restablecer posición" que hoy. Ancho fijo 400.
- El cambio de vista dentro de `MainWindow` no cambia el ancho; el alto se recalcula
  (`SizeToContent="Height"` con el mismo `MaxHeight` que hoy).
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
L_ActiveFmt           — {0} activos — {0} active
L_CounterFmt          — {0} / {1} — {0} / {1}
L_PanelEmpty          — Sin objetivos. Entra a un Sidequest y envía uno. — No objectives yet. Open a Sidequest and send one.
L_ReturnToSidequest   — Devolver al Sidequest — Return to Sidequest
L_DeleteObjective     — Eliminar objetivo — Delete objective
L_DeleteObjectiveQ    — ¿Eliminar este objetivo? También se borra de su Sidequest. — Delete this objective? It is removed from its Sidequest too.
L_Collapse            — Contraer — Collapse
L_Expand              — Expandir — Expand
L_KeepOnTop           — Mantener encima de otras apps — Keep above other apps
L_Close               — Cerrar — Close
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
   de Sidequests → nuevo → detalle. Sin detalle de objetivo todavía.
4. Detalle del objetivo: notas con URL, archivos, imágenes con copia a `media\`.
5. Ajustes y bandeja.
6. Bitácora (la pieza más grande; puede entregarse después del resto).
7. Tema claro: revisar cada pantalla contra `FnMainClaro.html` y los tokens de la 4.2.

Criterio de terminado por pantalla: coincide con su HTML de referencia a escala 1:1 en ambos temas
y en ambos idiomas, y todas las interacciones de la sección 5 funcionan con teclado (Tab, Enter,
Escape, Supr) además de con ratón.
