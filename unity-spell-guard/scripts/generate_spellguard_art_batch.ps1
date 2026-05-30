param(
  [string]$LocalApi = "http://127.0.0.1:3000",
  [string]$Model = "gpt-image-2"
)

$ErrorActionPreference = "Stop"

$root = "E:\bishe\gesture-game\unity-spell-guard"
$outputRoot = Join-Path $root "Assets\Art"
$databaseDirName = -join ([char[]](0x6570, 0x636E, 0x5E93))
$imageGeneratorDirName = -join ([char[]](0x56FE, 0x7247, 0x751F, 0x6210))
$relayOutputRoot = Join-Path (Join-Path "E:\" $databaseDirName) (Join-Path $imageGeneratorDirName "outputs")

function Ensure-Dir {
  param([string]$Path)
  if (-not (Test-Path -LiteralPath $Path)) {
    New-Item -ItemType Directory -Path $Path -Force | Out-Null
  }
}

function Copy-LatestGenerated {
  param(
    [Parameter(Mandatory = $true)][string]$FileName,
    [Parameter(Mandatory = $true)][string]$DestinationDir,
    [Parameter(Mandatory = $true)][object]$Response
  )

  if (-not $Response.saved -or $Response.saved.Count -lt 1) {
    throw "Image relay response did not include saved files for $FileName."
  }

  Ensure-Dir -Path $DestinationDir
  $savedName = $Response.saved[0].fileName
  $source = Join-Path $relayOutputRoot $savedName
  if (-not (Test-Path -LiteralPath $source)) {
    throw "Relay saved file not found: $source"
  }

  $target = Join-Path $DestinationDir $FileName
  Copy-Item -LiteralPath $source -Destination $target -Force
  return $target
}

function Invoke-ArtGeneration {
  param(
    [Parameter(Mandatory = $true)][hashtable]$Asset
  )

  $body = [ordered]@{
    prompt = $Asset.prompt
    model = $Model
    size = $Asset.genSize
    n = 1
  }

  $json = $body | ConvertTo-Json -Depth 8
  $response = Invoke-RestMethod -Method Post -Uri "$LocalApi/api/generate-json" -ContentType "application/json" -Body $json -TimeoutSec 1800
  $fileName = "$($Asset.name).png"
  $path = Copy-LatestGenerated -FileName $fileName -DestinationDir $Asset.dir -Response $response

  [ordered]@{
    name = $Asset.name
    target = $path
    source = $response.saved[0].fileName
    expectedWidth = $Asset.width
    expectedHeight = $Asset.height
    import = $Asset.import
    border = $Asset.border
  }
}

$style = "Near-future sci-fi ritual rune magic for a Unity game called Spell Guard. Deep space blue black #0A0E1A, electric blue #3D8BFF, cyan #4DC9F6, gold orange #F5A623, white #E8ECF2. Holographic UI, Tron-like light strips, thin glowing linework, subtle energy halo, clean game asset, no readable text, no watermark."

$uiDir = Join-Path $outputRoot "UI\SpellGuard\GeneratedCore"
$screenDir = Join-Path $outputRoot "UI\SpellGuard\Screens"
$vfxDir = Join-Path $outputRoot "VFX\SpellGuard\GeneratedCore"
$envDir = Join-Path $outputRoot "Environment\SpellGuard\GeneratedCore"
$handDir = Join-Path $outputRoot "UI\SpellGuard\Hands"

$assets = @(
  @{
    name = "ui_panel_main"; width = 512; height = 512; genSize = "1024x1024"; dir = $uiDir; import = "Sprite (2D and UI), Clamp, Bilinear"; border = "24";
    prompt = "$style Asset: ui_panel_main. Square 9-slice panel background, rounded rectangle centered with transparent-looking dark fill #0D111A alpha impression, 2 px electric-blue glowing border #3D8BFF, tiny L-shaped technology line decorations at all four corners. Keep entire panel inside canvas with padding, orthographic flat UI sprite, no text."
  },
  @{
    name = "ui_btn_primary_normal"; width = 256; height = 64; genSize = "1024x1024"; dir = $uiDir; import = "Sprite (2D and UI), Clamp, Bilinear"; border = "16";
    prompt = "$style Asset: ui_btn_primary_normal. Wide primary button sprite, dark base #131B2A, crisp 2 px electric-blue border #3D8BFF with subtle glow, short horizontal decorative light ticks at left and right ends, very slight inner blue luminance, no label text, centered with padding."
  },
  @{
    name = "ui_btn_primary_hover"; width = 256; height = 64; genSize = "1024x1024"; dir = $uiDir; import = "Sprite (2D and UI), Clamp, Bilinear"; border = "16";
    prompt = "$style Asset: ui_btn_primary_hover. Wide primary button hover/selected state, same shape as normal, brighter border #5DAFFF, stronger blue glow, a left-to-right inner highlight light band, no label text, centered with padding."
  },
  @{
    name = "ui_btn_primary_active"; width = 256; height = 64; genSize = "1024x1024"; dir = $uiDir; import = "Sprite (2D and UI), Clamp, Bilinear"; border = "16";
    prompt = "$style Asset: ui_btn_primary_active. Wide primary button pressed state, dark base, gold-orange border #F5A623, short intense inner highlight, small sci-fi line ticks at both ends, no label text, centered with padding."
  },
  @{
    name = "ui_btn_secondary_normal"; width = 200; height = 48; genSize = "1024x1024"; dir = $uiDir; import = "Sprite (2D and UI), Clamp, Bilinear"; border = "12";
    prompt = "$style Asset: ui_btn_secondary_normal. Compact secondary button sprite, dark base #0D111A, thin 1 px blue border with low alpha impression, restrained glow, minimal sci-fi corner ticks, no label text, centered with padding."
  },
  @{
    name = "ui_btn_secondary_hover"; width = 200; height = 48; genSize = "1024x1024"; dir = $uiDir; import = "Sprite (2D and UI), Clamp, Bilinear"; border = "12";
    prompt = "$style Asset: ui_btn_secondary_hover. Compact secondary button hover state, same shape as normal, brighter blue border alpha impression, clean inner glow, no label text, centered with padding."
  },
  @{
    name = "ui_screen_bg_main_menu"; width = 1920; height = 1080; genSize = "1920x1088"; dir = $screenDir; import = "Sprite (2D and UI), Clamp, Bilinear"; border = "";
    prompt = "$style Asset: ui_screen_bg_main_menu. Fullscreen main menu background, deep space blue-black base, large glowing rune array in lower right blurred/defocused, faint diagonal light-band motion from upper left to lower right, subtle ritual core silhouette slightly right of center, cinematic but not busy, leave clean dark space for menu UI, no text."
  },
  @{
    name = "ui_icon_fire"; width = 64; height = 64; genSize = "1024x1024"; dir = $uiDir; import = "Sprite (2D and UI), Clamp, Bilinear"; border = "";
    prompt = "$style Asset: ui_icon_fire. Minimal monochrome line icon, simplified flame outline with a small diamond at center, white and cyan line art only, flat icon on dark clean background, no text."
  },
  @{
    name = "ui_icon_ice"; width = 64; height = 64; genSize = "1024x1024"; dir = $uiDir; import = "Sprite (2D and UI), Clamp, Bilinear"; border = "";
    prompt = "$style Asset: ui_icon_ice. Minimal monochrome line icon, six-sided snowflake / ice crystal symbol, white and cyan line art only, geometric sci-fi style, flat icon on dark clean background, no text."
  },
  @{
    name = "ui_icon_shield"; width = 64; height = 64; genSize = "1024x1024"; dir = $uiDir; import = "Sprite (2D and UI), Clamp, Bilinear"; border = "";
    prompt = "$style Asset: ui_icon_shield. Minimal monochrome line icon, hexagonal shield border with inner shield contour and tiny warning mark, white and cyan line art only, flat icon on dark clean background, no text."
  },
  @{
    name = "ui_icon_health"; width = 48; height = 48; genSize = "1024x1024"; dir = $uiDir; import = "Sprite (2D and UI), Clamp, Bilinear"; border = "";
    prompt = "$style Asset: ui_icon_health. Minimal monochrome line icon, medical cross plus outer circular ring, futuristic segmented linework, white and cyan only, flat icon on dark clean background, no text."
  },
  @{
    name = "ui_progress_bar_bg"; width = 256; height = 24; genSize = "1024x1024"; dir = $uiDir; import = "Sprite (2D and UI), Clamp, Bilinear"; border = "4";
    prompt = "$style Asset: ui_progress_bar_bg. Wide thin progress bar background, rounded dark translucent strip #0D111A, subtle low-alpha blue rim, empty interior, no fill, no text, centered with padding."
  },
  @{
    name = "ui_progress_bar_fill"; width = 256; height = 24; genSize = "1024x1024"; dir = $uiDir; import = "Sprite (2D and UI), Clamp, Bilinear"; border = "4";
    prompt = "$style Asset: ui_progress_bar_fill. Wide thin progress bar fill, rounded capsule strip, cyan-blue gradient from #3D8BFF to #4DC9F6, horizontal flowing light streak, no frame, no text, centered with padding."
  },
  @{
    name = "vfx_fire_projectile"; width = 64; height = 64; genSize = "1024x1024"; dir = $vfxDir; import = "Sprite (2D and UI), Clamp, Bilinear"; border = "";
    prompt = "$style Asset: vfx_fire_projectile. Isolated circular fire projectile VFX sprite, bright white core, orange to red radial glow, soft blurred fading edge, alpha-friendly black background, no text."
  },
  @{
    name = "vfx_fire_impact"; width = 128; height = 128; genSize = "1024x1024"; dir = $vfxDir; import = "Sprite (2D and UI), Clamp, Bilinear"; border = "";
    prompt = "$style Asset: vfx_fire_impact. Isolated fire impact explosion VFX sprite, white central flash, orange-red sparks scattering outward, irregular burst silhouette, soft glow fade, alpha-friendly black background, no text."
  },
  @{
    name = "vfx_shield_hex"; width = 256; height = 256; genSize = "1024x1024"; dir = $vfxDir; import = "Sprite (2D and UI), Clamp, Bilinear"; border = "";
    prompt = "$style Asset: vfx_shield_hex. Hexagonal energy shield sprite, electric-blue hex border #3D8BFF, internal honeycomb grid lines, translucent energy pane impression, subtle top and bottom energy flow, centered, alpha-friendly black background, no text."
  },
  @{
    name = "env_floor_grid"; width = 1024; height = 1024; genSize = "1024x1024"; dir = $envDir; import = "Default, Repeat, Bilinear"; border = "";
    prompt = "$style Asset: env_floor_grid. Tileable top-down floor texture, deep #0A0E1A base, fine cyan grid lines #3D8BFF low opacity, center area slightly brighter and denser, subtle radial gradient to darker edges, seamless game texture, no text."
  },
  @{
    name = "hand_sprite_point"; width = 256; height = 256; genSize = "1024x1024"; dir = $handDir; import = "Sprite (2D and UI), Clamp, Bilinear"; border = "";
    prompt = "$style Asset: hand_sprite_point. FPS-view right hand sprite, wrist to fingertips, index finger extended pointing toward screen center, sci-fi dark gray glove/skin material with cyan glowing knuckle lines, isolated on dark alpha-friendly background, no text."
  },
  @{
    name = "hand_sprite_fist"; width = 256; height = 256; genSize = "1024x1024"; dir = $handDir; import = "Sprite (2D and UI), Clamp, Bilinear"; border = "";
    prompt = "$style Asset: hand_sprite_fist. FPS-view right hand sprite, clenched fist, wrist to knuckles, sci-fi dark gray material with cyan glowing joint lines, isolated on dark alpha-friendly background, no text."
  },
  @{
    name = "hand_sprite_vsign"; width = 256; height = 256; genSize = "1024x1024"; dir = $handDir; import = "Sprite (2D and UI), Clamp, Bilinear"; border = "";
    prompt = "$style Asset: hand_sprite_vsign. FPS-view right hand sprite making V sign with index and middle finger extended, sci-fi dark gray material with cyan glowing joint lines, isolated on dark alpha-friendly background, no text."
  },
  @{
    name = "hand_sprite_openpalm"; width = 256; height = 256; genSize = "1024x1024"; dir = $handDir; import = "Sprite (2D and UI), Clamp, Bilinear"; border = "";
    prompt = "$style Asset: hand_sprite_openpalm. FPS-view right hand sprite with open palm and five fingers spread, sci-fi dark gray material with cyan glowing joint lines, isolated on dark alpha-friendly background, no text."
  }
)

Ensure-Dir -Path $uiDir
Ensure-Dir -Path $screenDir
Ensure-Dir -Path $vfxDir
Ensure-Dir -Path $envDir
Ensure-Dir -Path $handDir

$manifest = @()
foreach ($asset in $assets) {
  Write-Host "Generating $($asset.name) ..."
  $manifest += Invoke-ArtGeneration -Asset $asset
}

$manifestPath = Join-Path $outputRoot "SpellGuard_GeneratedArtManifest.json"
$manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
Write-Host "Manifest: $manifestPath"
