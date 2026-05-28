$ErrorActionPreference = "Stop"

$root = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$paperDir = Join-Path $root "论文材料"
$sourceDocx = Join-Path $paperDir "曹逸天-毕业设计说明书-最终整合版.docx"
$outDocx = Join-Path $paperDir "曹逸天-毕业设计说明书-学校格式轻修版.docx"
$outPdf = Join-Path $paperDir "曹逸天-毕业设计说明书-学校格式轻修版.pdf"

Copy-Item -LiteralPath $sourceDocx -Destination $outDocx -Force

function Is-ChapterHeading([string]$Text) {
    return ($Text -match '^第\s*[0-9一二三四五六七八九十]+\s*章') -or
           ($Text -eq '摘要') -or ($Text -eq 'Abstract') -or
           ($Text -eq '参考文献') -or ($Text -eq '图表证据补充页') -or
           ($Text -eq '表附录') -or ($Text -eq '致谢')
}
function Is-SectionHeading([string]$Text) { return ($Text -match '^[0-9]+\.[0-9]+\s+') }
function Is-SubSectionHeading([string]$Text) { return ($Text -match '^[0-9]+\.[0-9]+\.[0-9]+\s+') }

$word = New-Object -ComObject Word.Application
$word.Visible = $false
$doc = $word.Documents.Open($outDocx)

try {
    $ps = $doc.PageSetup
    $ps.TopMargin = 72
    $ps.BottomMargin = 72
    $ps.LeftMargin = 90
    $ps.RightMargin = 90
    $ps.HeaderDistance = 42.55
    $ps.FooterDistance = 49.6

    # Fix cover text based on task book.
    $cover = @(
        '本科毕业设计说明书（论文）',
        'Undergraduate International Students Graduation Project Report (Thesis)',
        '',
        '',
        '面向体感游戏的动态手势轨迹跟踪方法实现',
        'IMPLEMENTATION OF DYNAMIC GESTURE TRAJECTORY TRACKING METHOD FOR MOTION-SENSING GAMES',
        '',
        '',
        '学    院： 计算机科学与技术学院、软件学院',
        '专    业： 软件工程（中外合作办学）',
        '班    级： 2022软件工程（中外合作办学）',
        '学    号： 202203340102',
        '学生姓名： 曹逸天',
        '指导老师：',
        '提交日期： 2026年6月'
    )
    for ($i=1; $i -le $cover.Count; $i++) {
        $p = $doc.Paragraphs.Item($i)
        $p.Range.Text = $cover[$i-1] + "`r"
        $p.Range.Style = '正文'
        $p.Alignment = 1
        $p.FirstLineIndent = 0
        $p.Range.Font.NameFarEast = '宋体'
        $p.Range.Font.Name = 'Times New Roman'
        $p.Range.Font.Size = 14
        $p.Range.Font.Bold = 0
    }
    foreach($i in 1,2,5,6) {
        $p = $doc.Paragraphs.Item($i)
        $p.Range.Font.Bold = 1
        $p.Range.Font.Size = 16
    }

    for ($i=16; $i -le $doc.Paragraphs.Count; $i++) {
        $p = $doc.Paragraphs.Item($i)
        $text = $p.Range.Text.Trim()
        if ($text.Length -eq 0) { continue }

        if (Is-ChapterHeading $text) {
            $p.Range.Style = '标题 1'
            $p.Alignment = 1
            $p.FirstLineIndent = 0
            $p.Range.Font.NameFarEast = '黑体'
            $p.Range.Font.Name = 'Times New Roman'
            $p.Range.Font.Size = 16
            $p.Range.Font.Bold = 1
        } elseif (Is-SubSectionHeading $text) {
            $p.Range.Style = '标题 3'
            $p.Alignment = 0
            $p.FirstLineIndent = 0
            $p.Range.Font.NameFarEast = '黑体'
            $p.Range.Font.Name = 'Times New Roman'
            $p.Range.Font.Size = 12
            $p.Range.Font.Bold = 1
        } elseif (Is-SectionHeading $text) {
            $p.Range.Style = '标题 2'
            $p.Alignment = 0
            $p.FirstLineIndent = 0
            $p.Range.Font.NameFarEast = '黑体'
            $p.Range.Font.Name = 'Times New Roman'
            $p.Range.Font.Size = 14
            $p.Range.Font.Bold = 1
        } elseif ($p.Range.Style.NameLocal -notmatch '^TOC') {
            $p.Range.Style = '正文'
            $p.Alignment = 0
            $p.FirstLineIndent = 21
            $p.Range.Font.NameFarEast = '宋体'
            $p.Range.Font.Name = 'Times New Roman'
            $p.Range.Font.Size = 10.5
            $p.Range.Font.Bold = 0
            $p.LineSpacingRule = 4
            $p.LineSpacing = 20
            $p.SpaceBefore = 0
            $p.SpaceAfter = 0
        }
    }

    foreach ($section in $doc.Sections) {
        $footer = $section.Footers.Item(1)
        if ($footer.PageNumbers.Count -eq 0) {
            $footer.PageNumbers.Add(1, $true) | Out-Null
        }
    }

    $doc.Save()
    $doc.ExportAsFixedFormat($outPdf, 17)
}
finally {
    $doc.Close($true)
    $word.Quit()
}

Write-Host "DOCX: $outDocx"
Write-Host "PDF : $outPdf"

