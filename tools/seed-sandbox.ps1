param([Parameter(Mandatory = $true)][string]$Sandbox)

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
        Notes         = ''
        Files         = @()
        Images        = @()
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
    }
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

$dir = Join-Path $Sandbox 'Sidequest'
New-Item -ItemType Directory -Force -Path $dir | Out-Null

# FnObjetivo's objective: its notes (129 characters, as the artboard counts them), one
# linked file and three images. The file is real, in the sandbox, so it does not show as
# missing; the images are drawn here and put straight into media\, as an import would.
$cotizar = $mudanza.Items[0]
$cotizar.Notes = "Pedir cotización a 3 empresas. Confirmar embalaje y seguro.`nhttps://transportes.example/cotizar`nhttps://hojas.example/comparativa"

$linked = Join-Path $Sandbox 'files\Mudanza\inventario-cajas.xlsx'
New-Item -ItemType Directory -Force -Path (Split-Path $linked) | Out-Null
Set-Content -LiteralPath $linked -Value '' -Encoding UTF8
$cotizar.Files = @($linked)

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
$cotizar.Images = $names

$json = ConvertTo-Json @($portafolio, $mudanza, $curso, $impuestos) -Depth 8
Set-Content -LiteralPath (Join-Path $dir 'sidequests.json') -Value $json -Encoding UTF8

$all = @($portafolio, $mudanza, $curso, $impuestos)
foreach ($p in $all) {
    $done = @($p.Items | Where-Object { $_.Done }).Count
    "{0,-22} {1,-7} {2} / {3}" -f $p.Name, $p.Theme, $done, $p.Items.Count
}
$panel = $all.Items | Where-Object { $_.InPanel } | Sort-Object PanelIndex
"panel objectives: {0}, of which {1} done" -f $panel.Count, (@($panel | Where-Object { $_.Done }).Count)
