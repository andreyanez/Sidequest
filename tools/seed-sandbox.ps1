param(
    [Parameter(Mandatory = $true)][string]$Sandbox,
    # FnSidequestsCompacto's fourteen Sidequests instead, for the long list (5.4).
    [switch]$Many
)

# Seeds the sandbox with the sample data from docs/design/reference, so the screens can be
# compared against the artboards 1:1. Never point this at %APPDATA%.

$i = 0
function Item($text, $done, $panelIndex) {
    $script:i++
    [ordered]@{
        Id            = [guid]::NewGuid().ToString()
        Text          = $text
        Done          = [bool]$done
        InPanel       = ($null -ne $panelIndex)
        SortIndex     = $script:i
        PanelIndex    = $panelIndex
    }
}

function Project($name, $theme, $sort, $items) {
    [ordered]@{
        Id        = [guid]::NewGuid().ToString()
        Name      = $name
        Theme     = $theme
        CreatedAt = (Get-Date).ToString('o')
        SortIndex = $sort
        Items     = $items
        Notes     = ''
        Files     = @()
        Images    = @()
    }
}

$dir = Join-Path $Sandbox 'Sidequest'
New-Item -ItemType Directory -Force -Path $dir | Out-Null

if ($Many) {
    # Name, theme and item count of each, in FnSidequestsCompacto's order. Nothing is
    # done and nothing is on the panel, as the artboard shows.
    $rows = @(
        @('Job Pivoting', 'Slate', 1), @('Misc', 'Teal', 3), @('Mantenimiento de Auto', 'Steel', 1),
        @('Meditación Preparación', 'Amber', 2), @('Salud y Bienestar', 'Olive', 5),
        @('Audio Rescue', 'Indigo', 2), @('To Solve Loopitis', 'Coral', 4), @('Money Investment', 'Amber', 3),
        @('A Voice to Train', 'Steel', 2), @('Knot-ledge', 'Indigo', 2), @('Smile for Me', 'Teal', 1),
        @('Tesismo Moderno', 'Indigo', 1), @('How To Untie A Ballon', 'Coral', 1), @('Alternative Revenue', 'Teal', 6)
    )
    $sort = 0
    $list = foreach ($row in $rows) {
        Project $row[0] $row[1] ($sort++) @(1..$row[2] | ForEach-Object { Item "Paso $_" $false $null })
    }
    Set-Content -LiteralPath (Join-Path $dir 'sidequests.json') -Value (ConvertTo-Json @($list) -Depth 8) -Encoding UTF8
    "{0} Sidequests, as FnSidequestsCompacto" -f @($list).Count
    return
}

# Panel order and the two ticked objectives come straight from FnMain.html.
$portafolio = Project 'Portafolio web' 'Amber' 0 @(
    (Item 'Elegir 3 proyectos para mostrar' $false 0),
    (Item 'Escribir textos de cada caso'    $false $null),
    (Item 'Elegir paleta y tipografía'      $false $null),
    (Item 'Montar la home'                  $false $null),
    (Item 'Comprar dominio'                 $true  4),
    (Item 'Publicar y revisar en móvil'     $false $null)
)

$mudanza = Project 'Mudanza' 'Teal' 1 @(
    (Item 'Cotizar camión de mudanza'        $false 1),
    (Item 'Cambiar dirección de servicios'   $false $null),
    (Item 'Reservar día libre en el trabajo' $false $null),
    (Item 'Limpiar depósito'                 $false $null),
    (Item 'Donar muebles que no van'         $false $null),
    (Item 'Inventario de cajas'              $true  5),
    (Item 'Avisar al arrendador'             $true  $null),
    (Item 'Comprar cinta y cajas'            $true  $null)
)

$curso = Project 'Curso de TypeScript' 'Indigo' 2 @(
    (Item 'Terminar módulo de genéricos' $false 2),
    (Item 'Tipos básicos'                $true  $null),
    (Item 'Interfaces'                   $true  $null),
    (Item 'Uniones y guardas'            $true  $null),
    (Item 'Clases'                       $true  $null),
    (Item 'Módulos'                      $true  $null),
    (Item 'Utilidades de tipos'          $true  $null),
    (Item 'Configurar tsconfig'          $true  $null),
    (Item 'Decoradores'                  $false $null),
    (Item 'Tipos condicionales'          $false $null),
    (Item 'Proyecto final'               $false $null),
    (Item 'Repasar apuntes'              $false $null)
)

$impuestos = Project 'Impuestos 2026' 'Coral' 3 @(
    (Item 'Reunir recibos de honorarios' $false 3),
    (Item 'Descargar constancias'        $false $null),
    (Item 'Revisar deducciones'          $false $null),
    (Item 'Presentar declaración'        $false $null)
)

# FnSidequestNotas's Notas tab, on Mudanza: its notes (207 characters, as the artboard
# counts them), one linked file and three images. The file is real, in the sandbox, so it
# does not show as missing; the images are drawn here and put straight into media\, as an
# import would.
$mudanza.Notes = "Empresas a cotizar: Transportes Ica y Mudanzas Lima Sur. Mudarse antes del 15 de octubre. Preguntar si el edificio pide carta de mudanza.`nhttps://transportes.example/cotizar`nhttps://hojas.example/comparativa"

$linked = Join-Path $Sandbox 'files\Mudanza\inventario-cajas.xlsx'
New-Item -ItemType Directory -Force -Path (Split-Path $linked) | Out-Null
Set-Content -LiteralPath $linked -Value '' -Encoding UTF8
$mudanza.Files = @($linked)

Add-Type -AssemblyName System.Drawing
$media = Join-Path $dir 'media'
New-Item -ItemType Directory -Force -Path $media | Out-Null
$names = @()
foreach ($pair in @(@('#4FB5A8', '#1D2D2E'), @('#D4A24A', '#2B2419'), @('#8B93E6', '#1F2033'))) {
    $bmp = New-Object System.Drawing.Bitmap 480, 360
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $rect = New-Object System.Drawing.Rectangle 0, 0, 480, 360
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect,
        ([System.Drawing.ColorTranslator]::FromHtml($pair[0])), ([System.Drawing.ColorTranslator]::FromHtml($pair[1])), 45
    $g.FillRectangle($brush, $rect)
    $name = ([guid]::NewGuid().ToString('N')) + '.png'
    $bmp.Save((Join-Path $media $name), [System.Drawing.Imaging.ImageFormat]::Png)
    $brush.Dispose(); $g.Dispose(); $bmp.Dispose()
    $names += $name
}
$mudanza.Images = $names

$json = ConvertTo-Json @($portafolio, $mudanza, $curso, $impuestos) -Depth 8
Set-Content -LiteralPath (Join-Path $dir 'sidequests.json') -Value $json -Encoding UTF8

$all = @($portafolio, $mudanza, $curso, $impuestos)
foreach ($p in $all) {
    $done = @($p.Items | Where-Object { $_.Done }).Count
    "{0,-22} {1,-7} {2} / {3}" -f $p.Name, $p.Theme, $done, $p.Items.Count
}
$panel = $all.Items | Where-Object { $_.InPanel } | Sort-Object PanelIndex
"panel objectives: {0}, of which {1} done" -f $panel.Count, (@($panel | Where-Object { $_.Done }).Count)
"Mudanza's notes: {0} / 500" -f $mudanza.Notes.Length
