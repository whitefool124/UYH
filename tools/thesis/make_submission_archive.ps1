$ErrorActionPreference = "Stop"

$root = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$paperDir = Join-Path $root "论文材料"
$archiveRoot = Join-Path $root "毕业设计提交归档-曹逸天"
$codeZip = Join-Path $archiveRoot "18源代码与工程.zip"

if (Test-Path -LiteralPath $archiveRoot) {
    Remove-Item -LiteralPath $archiveRoot -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $archiveRoot | Out-Null

function Copy-IfExists {
    param(
        [Parameter(Mandatory=$true)] [string] $Source,
        [Parameter(Mandatory=$true)] [string] $DestName
    )
    if (Test-Path -LiteralPath $Source) {
        Copy-Item -LiteralPath $Source -Destination (Join-Path $archiveRoot $DestName) -Force
        return $true
    }
    return $false
}

$copied = New-Object System.Collections.Generic.List[string]
$missing = New-Object System.Collections.Generic.List[string]

$items = @(
    @{ Source = Join-Path $paperDir "曹逸天-毕业设计说明书-最终整合版.docx"; Name = "3毕业设计说明书-曹逸天.docx"; Required = $true },
    @{ Source = Join-Path $paperDir "曹逸天-毕业设计说明书-最终整合版.pdf"; Name = "3毕业设计说明书-曹逸天.pdf"; Required = $true },
    @{ Source = Join-Path $paperDir "任务书\毕业设计任务书-曹逸天.doc"; Name = "2任务书-曹逸天.doc"; Required = $true },
    @{ Source = Join-Path $paperDir "任务书\毕业设计任务书-曹逸天.pdf"; Name = "2任务书-曹逸天.pdf"; Required = $false },
    @{ Source = Join-Path $paperDir "文献综述\曹逸天-文献综述-英文提交版.docx"; Name = "4文献综述-曹逸天.docx"; Required = $true },
    @{ Source = Join-Path $paperDir "开题报告\曹逸天-开题报告-英文提交版.docx"; Name = "5开题报告-曹逸天.docx"; Required = $true },
    @{ Source = Join-Path $paperDir "外文翻译\曹逸天-利用MediaPipe和YOLO-Pose提升手势识别效率-v4.docx"; Name = "7外文翻译译文-曹逸天.docx"; Required = $true },
    @{ Source = Join-Path $paperDir "外文翻译\曹逸天-面向体感游戏的动态手势轨迹跟踪方法实现.pdf"; Name = "6外文翻译原文-曹逸天.pdf"; Required = $false }
)

foreach ($item in $items) {
    if (Copy-IfExists -Source $item.Source -DestName $item.Name) {
        $copied.Add($item.Name) | Out-Null
    } elseif ($item.Required) {
        $missing.Add($item.Name) | Out-Null
    }
}

$evidenceDir = Join-Path $archiveRoot "论文图表与实验数据"
New-Item -ItemType Directory -Force -Path $evidenceDir | Out-Null
Copy-Item -LiteralPath (Join-Path $root "unity-spell-guard\Docs\ThesisAssets") -Destination (Join-Path $evidenceDir "ThesisAssets") -Recurse -Force
Copy-Item -LiteralPath (Join-Path $root "unity-spell-guard\ExperimentResults") -Destination (Join-Path $evidenceDir "ExperimentResults") -Recurse -Force -ErrorAction SilentlyContinue

$codeTemp = Join-Path $root "build-temp\source-archive-staging"
if (Test-Path -LiteralPath $codeTemp) {
    Remove-Item -LiteralPath $codeTemp -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $codeTemp | Out-Null

$codeTarget = Join-Path $codeTemp "gesture-game"
New-Item -ItemType Directory -Force -Path $codeTarget | Out-Null

$topFiles = @("README.md","GAME_PLAN.md","GAME_PLAN_V2.md","package.json","package-lock.json","index.html","yolo11n.pt")
foreach ($file in $topFiles) {
    $src = Join-Path $root $file
    if (Test-Path -LiteralPath $src) {
        Copy-Item -LiteralPath $src -Destination (Join-Path $codeTarget $file) -Force
    }
}

$dirs = @("src","tools","unity-spell-guard\Assets","unity-spell-guard\Packages","unity-spell-guard\ProjectSettings","unity-spell-guard\Docs","unity-spell-guard\bridge","unity-spell-guard\ExperimentResults")
foreach ($dir in $dirs) {
    $src = Join-Path $root $dir
    if (Test-Path -LiteralPath $src) {
        $dest = Join-Path $codeTarget $dir
        New-Item -ItemType Directory -Force -Path (Split-Path $dest -Parent) | Out-Null
        Copy-Item -LiteralPath $src -Destination $dest -Recurse -Force
    }
}

if (Test-Path -LiteralPath $codeZip) {
    Remove-Item -LiteralPath $codeZip -Force
}
Compress-Archive -Path (Join-Path $codeTemp "gesture-game") -DestinationPath $codeZip -Force

$manifest = @()
$manifest += "# 毕业设计提交归档清单 - 曹逸天"
$manifest += ""
$manifest += "生成时间：$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
$manifest += ""
$manifest += "## 已归档文件"
foreach ($name in ($copied | Sort-Object)) {
    $manifest += "- $name"
}
$manifest += "- 18源代码与工程.zip"
$manifest += "- 论文图表与实验数据/"
$manifest += ""
$manifest += "## 参考优秀论文范例但当前未提供的材料"
$manifest += "- 0诚信承诺书"
$manifest += "- 1毕业论文（设计）申报表"
$manifest += "- 8文献综述评分表"
$manifest += "- 9开题报告评分表"
$manifest += "- 10外文翻译评分表"
$manifest += "- 11中期总结及检查表"
$manifest += "- 12进程安排与考核表"
$manifest += "- 13指导教师评语表"
$manifest += "- 14评阅人评语表"
$manifest += "- 15指导答疑记录表"
$manifest += "- 16检测报告"
$manifest += "- 17毕业设计成绩单总表"
$manifest += ""
$manifest += "## 缺失的必需文件"
if ($missing.Count -eq 0) {
    $manifest += "- 无"
} else {
    foreach ($name in $missing) {
        $manifest += "- $name"
    }
}
$manifest += ""
$manifest += "## 源码归档说明"
$manifest += "源码压缩包包含前端原型、Unity 工程 Assets/Packages/ProjectSettings、技术文档、桥接脚本和实验结果；未包含 Unity Library、Temp、Logs 等可再生成缓存目录。"

Set-Content -LiteralPath (Join-Path $archiveRoot "归档清单.md") -Value ($manifest -join "`r`n") -Encoding UTF8

Get-ChildItem -LiteralPath $archiveRoot | Select-Object Name,Length,LastWriteTime | Format-Table -AutoSize

