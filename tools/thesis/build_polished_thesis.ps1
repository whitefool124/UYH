$ErrorActionPreference = "Stop"

$root = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$paperDir = Join-Path $root "论文材料"
$sourceDocx = Join-Path $paperDir "曹逸天-毕业设计说明书-模板格式版.docx"
$outDocx = Join-Path $paperDir "曹逸天-毕业设计说明书-提交完善版.docx"
$outPdf = Join-Path $paperDir "曹逸天-毕业设计说明书-提交完善版.pdf"
$assetRoot = Join-Path $root "unity-spell-guard\Docs\ThesisAssets"
$diagramDir = Join-Path $assetRoot "diagrams"
$screenshotDir = Join-Path $assetRoot "screenshots"
$renderDir = Join-Path $root "build-temp\thesis-assets-rendered"

New-Item -ItemType Directory -Force -Path $renderDir | Out-Null
Copy-Item -LiteralPath $sourceDocx -Destination $outDocx -Force

$svgMap = @(
    @{ Src = "system_architecture.svg"; Out = "system_architecture.png" },
    @{ Src = "input_pipeline.svg"; Out = "input_pipeline.png" },
    @{ Src = "motion_recognition_flow.svg"; Out = "motion_recognition_flow.png" },
    @{ Src = "unity_module_structure.svg"; Out = "unity_module_structure.png" },
    @{ Src = "experiment_pipeline.svg"; Out = "experiment_pipeline.png" }
)

foreach ($item in $svgMap) {
    $src = Join-Path $diagramDir $item.Src
    $dst = Join-Path $renderDir $item.Out
    if (Test-Path -LiteralPath $src) {
        & magick -background white -density 220 $src -resize 1800x $dst
        if ($LASTEXITCODE -ne 0) { throw "ImageMagick failed for $src" }
    }
}

function Add-Para {
    param(
        [Parameter(Mandatory=$true)] $Doc,
        [string] $Text = "",
        [string] $Style = "",
        [int] $Align = 0,
        [bool] $Bold = $false,
        [double] $Size = 0
    )
    $range = $Doc.Range($Doc.Content.End - 1, $Doc.Content.End - 1)
    $range.InsertAfter($Text)
    $range.InsertParagraphAfter()
    $p = $Doc.Paragraphs.Item($Doc.Paragraphs.Count)
    if ($Style -ne "") {
        try { $p.Range.Style = $Style } catch {}
    }
    $p.Alignment = $Align
    if ($Bold) { $p.Range.Font.Bold = 1 }
    if ($Size -gt 0) { $p.Range.Font.Size = $Size }
    return $p
}

function Add-PageBreak {
    param([Parameter(Mandatory=$true)] $Doc)
    $range = $Doc.Range($Doc.Content.End - 1, $Doc.Content.End - 1)
    $range.InsertBreak(7)
}

function Add-FigureAtEnd {
    param(
        [Parameter(Mandatory=$true)] $Doc,
        [Parameter(Mandatory=$true)] [string] $ImagePath,
        [Parameter(Mandatory=$true)] [string] $Caption,
        [double] $WidthPt = 420
    )
    if (!(Test-Path -LiteralPath $ImagePath)) { throw "Missing image: $ImagePath" }
    Add-Para -Doc $Doc -Text "" | Out-Null
    $range = $Doc.Range($Doc.Content.End - 1, $Doc.Content.End - 1)
    $shape = $Doc.InlineShapes.AddPicture($ImagePath, $false, $true, $range)
    if ($shape.Width -gt $WidthPt) { $shape.Width = $WidthPt }
    $Doc.Paragraphs.Item($Doc.Paragraphs.Count).Alignment = 1
    $p = Add-Para -Doc $Doc -Text $Caption -Align 1
    $p.Range.Font.Name = "宋体"
    $p.Range.Font.Size = 10.5
}

function Add-FigureAfterHeading {
    param(
        [Parameter(Mandatory=$true)] $Doc,
        [Parameter(Mandatory=$true)] [string] $HeadingText,
        [Parameter(Mandatory=$true)] [string] $ImagePath,
        [Parameter(Mandatory=$true)] [string] $Caption,
        [double] $WidthPt = 420
    )
    if (!(Test-Path -LiteralPath $ImagePath)) { throw "Missing image: $ImagePath" }
    $paras = $Doc.Paragraphs
    for ($i = 1; $i -le $paras.Count; $i++) {
        $text = $paras.Item($i).Range.Text.Trim()
        if ($text -eq $HeadingText) {
            $insert = $paras.Item($i).Range
            $insert.Collapse(0)
            $insert.InsertParagraphAfter()
            $insert.Collapse(0)
            $shape = $Doc.InlineShapes.AddPicture($ImagePath, $false, $true, $insert)
            if ($shape.Width -gt $WidthPt) { $shape.Width = $WidthPt }
            $insert.ParagraphFormat.Alignment = 1
            $insert.Collapse(0)
            $insert.InsertAfter("`r$Caption`r")
            return
        }
    }
    throw "Heading not found: $HeadingText"
}

function Add-FigureSectionAtEnd {
    param([Parameter(Mandatory=$true)] $Doc)
    Add-PageBreak -Doc $Doc
    Add-Para -Doc $Doc -Text "图表证据补充页" -Style "标题 1" -Align 1 -Bold $true -Size 16 | Out-Null
    Add-Para -Doc $Doc -Text "本节集中补充系统设计、动态识别、Unity 实现和实验流程中的关键图像证据，用于弥补模板版正文中图像未嵌入的问题。相关图像均来自项目归档目录 unity-spell-guard/Docs/ThesisAssets，与正文第 3 章至第 6 章内容对应。" | Out-Null
    $figures = @(
        @{ Img = Join-Path $renderDir "system_architecture.png"; Cap = "图 3-1 系统总体架构图" },
        @{ Img = Join-Path $renderDir "input_pipeline.png"; Cap = "图 4-1 多输入源统一链路图" },
        @{ Img = Join-Path $renderDir "motion_recognition_flow.png"; Cap = "图 4-2 动态手势轨迹识别流程图" },
        @{ Img = Join-Path $renderDir "unity_module_structure.png"; Cap = "图 5-1 Unity 工程模块结构图" },
        @{ Img = Join-Path $screenshotDir "start_menu_latest.png"; Cap = "图 5-2 游戏开始界面" },
        @{ Img = Join-Path $screenshotDir "gameplay_instruction.png"; Cap = "图 5-3 玩法说明界面" },
        @{ Img = Join-Path $screenshotDir "camera_calibration.png"; Cap = "图 5-4 摄像头校准界面" },
        @{ Img = Join-Path $screenshotDir "combat_gameplay.png"; Cap = "图 5-5 战斗运行界面" },
        @{ Img = Join-Path $screenshotDir "developer_lab.png"; Cap = "图 5-6 开发者实验室与调试入口" },
        @{ Img = Join-Path $screenshotDir "custom_gesture_validation.png"; Cap = "图 5-7 自定义动态手势验证界面" },
        @{ Img = Join-Path $renderDir "experiment_pipeline.png"; Cap = "图 6-1 扩展自动回放实验流程图" }
    )
    foreach ($fig in $figures) {
        Add-FigureAtEnd -Doc $Doc -ImagePath $fig.Img -Caption $fig.Cap -WidthPt 430
    }
}

function Add-Cover {
    param([Parameter(Mandatory=$true)] $Doc)
    $range = $Doc.Range(0,0)
    $cover = @(
        "本科毕业设计说明书（论文）",
        "Undergraduate Graduation Project Report (Thesis)",
        "",
        "",
        "面向体感游戏的动态手势轨迹跟踪方法实现",
        "IMPLEMENTATION OF DYNAMIC GESTURE TRAJECTORY TRACKING METHOD FOR MOTION-SENSING GAMES",
        "",
        "",
        "学    院： 计算机科学与技术学院、软件学院",
        "专    业： 软件工程（中外合作办学）",
        "班    级：",
        "学    号：",
        "学生姓名： 曹逸天",
        "指导老师：",
        "提交日期： 2026年6月"
    ) -join "`r"
    $range.InsertBefore($cover + "`r")
    $Doc.Range(0,0).InsertBreak(7)
}

$word = New-Object -ComObject Word.Application
$word.Visible = $false
$doc = $word.Documents.Open($outDocx)

try {
    Add-Cover -Doc $doc
    Add-FigureSectionAtEnd -Doc $doc

    Add-PageBreak -Doc $doc
    Add-Para -Doc $doc -Text "表附录" -Style "标题 1" -Align 1 -Bold $true -Size 16 | Out-Null
    Add-Para -Doc $doc -Text "表附录-1 论文实验数据与工程证据归档清单" -Align 1 | Out-Null
    $table = $doc.Tables.Add($doc.Range($doc.Content.End - 1, $doc.Content.End - 1), 5, 3)
    $table.Borders.Enable = $true
    $table.Cell(1,1).Range.Text = "证据类型"
    $table.Cell(1,2).Range.Text = "文件或目录"
    $table.Cell(1,3).Range.Text = "用途"
    $rows = @(
        @("系统截图", "unity-spell-guard/Docs/ThesisAssets/screenshots/", "支撑第 5 章系统运行效果展示"),
        @("结构图", "unity-spell-guard/Docs/ThesisAssets/diagrams/", "支撑第 3、4、5、6 章设计与实验流程说明"),
        @("性能结果", "unity-spell-guard/ExperimentResults/", "支撑 Mock、Native MediaPipe、ExternalBridge 三模式性能对比"),
        @("回放数据", "build-temp/jester_* 与 external-regression-report-*", "支撑 Jester 采样挖掘与外部回放验证")
    )
    for ($r=0; $r -lt $rows.Count; $r++) {
        for ($c=0; $c -lt 3; $c++) {
            $table.Cell($r+2,$c+1).Range.Text = $rows[$r][$c]
        }
    }
    $table.Range.Font.Name = "宋体"
    $table.Range.Font.Size = 10.5

    Add-PageBreak -Doc $doc
    Add-Para -Doc $doc -Text "致谢" -Style "标题 1" -Align 1 -Bold $true -Size 16 | Out-Null
    Add-Para -Doc $doc -Text "大学阶段的学习和本次毕业设计的完成，离不开老师、同学和家人的帮助。首先感谢指导老师在选题、系统实现、论文撰写和材料整理过程中给予的指导，使我能够把体感游戏交互、动态手势识别和 Unity 工程实现逐步收束为一套可运行、可验证的毕业设计系统。" | Out-Null
    Add-Para -Doc $doc -Text "感谢计算机科学与技术学院、软件学院各位老师在课程学习和实践训练中给予的帮助。相关课程和项目训练使我对软件工程、计算机视觉、人机交互和游戏开发有了更系统的理解，也为本课题中的输入抽象、模块划分、测试验证和论文表达提供了基础。" | Out-Null
    Add-Para -Doc $doc -Text "感谢同学和朋友在项目调试、运行截图、答辩准备和论文检查中提供的建议。毕业设计过程中遇到的许多问题并不是一次完成的，而是在不断测试、记录、修正和复盘中逐渐清晰。最后感谢家人在学习生活中的支持与包容，使我能够保持稳定的节奏完成本次毕业设计。" | Out-Null

    $doc.Save()
    $doc.ExportAsFixedFormat($outPdf, 17)
}
finally {
    $doc.Close($true)
    $word.Quit()
}

Write-Host "DOCX: $outDocx"
Write-Host "PDF : $outPdf"



