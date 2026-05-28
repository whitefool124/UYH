$ErrorActionPreference = "Stop"

$root = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$paperDir = Join-Path $root "论文材料"
$sourceDocx = Join-Path $paperDir "曹逸天-毕业设计说明书-最终整合版.docx"
$outDocx = Join-Path $paperDir "曹逸天-毕业设计说明书-学校格式规范版.docx"
$outPdf = Join-Path $paperDir "曹逸天-毕业设计说明书-学校格式规范版.pdf"

Copy-Item -LiteralPath $sourceDocx -Destination $outDocx -Force

function Set-Text {
    param($Para, [string]$Text)
    $Para.Range.Text = $Text + "`r"
}

function Is-ChapterHeading {
    param([string]$Text)
    return ($Text -match '^第\s*[0-9一二三四五六七八九十]+\s*章') -or
           ($Text -eq '摘要') -or
           ($Text -eq '摘  要') -or
           ($Text -eq 'Abstract') -or
           ($Text -eq 'ABSTRACT') -or
           ($Text -eq '参考文献') -or
           ($Text -eq '图表证据补充页') -or
           ($Text -eq '图附录') -or
           ($Text -eq '表附录') -or
           ($Text -eq '致谢')
}

function Is-SectionHeading {
    param([string]$Text)
    return ($Text -match '^[0-9]+\.[0-9]+\s+')
}

function Is-SubSectionHeading {
    param([string]$Text)
    return ($Text -match '^[0-9]+\.[0-9]+\.[0-9]+\s+')
}

$word = New-Object -ComObject Word.Application
$word.Visible = $false
$doc = $word.Documents.Open($outDocx)

try {
    # Page setup copied from the school template.
    $ps = $doc.PageSetup
    $ps.TopMargin = 72
    $ps.BottomMargin = 72
    $ps.LeftMargin = 90
    $ps.RightMargin = 90
    $ps.HeaderDistance = 42.55
    $ps.FooterDistance = 49.6

    # Baseline styles: keep names from Word's Chinese UI.
    $normal = $doc.Styles.Item('正文')
    $normal.Font.NameFarEast = '宋体'
    $normal.Font.Name = 'Times New Roman'
    $normal.Font.Size = 10.5
    $normal.ParagraphFormat.Alignment = 0
    $normal.ParagraphFormat.FirstLineIndent = 21
    $normal.ParagraphFormat.LineSpacingRule = 4
    $normal.ParagraphFormat.LineSpacing = 20
    $normal.ParagraphFormat.SpaceBefore = 0
    $normal.ParagraphFormat.SpaceAfter = 0

    $h1 = $doc.Styles.Item('标题 1')
    $h1.Font.NameFarEast = '黑体'
    $h1.Font.Name = 'Times New Roman'
    $h1.Font.Size = 16
    $h1.Font.Bold = 1
    $h1.ParagraphFormat.Alignment = 1
    $h1.ParagraphFormat.FirstLineIndent = 0
    $h1.ParagraphFormat.LineSpacingRule = 4
    $h1.ParagraphFormat.LineSpacing = 20
    $h1.ParagraphFormat.SpaceBefore = 12
    $h1.ParagraphFormat.SpaceAfter = 12

    $h2 = $doc.Styles.Item('标题 2')
    $h2.Font.NameFarEast = '黑体'
    $h2.Font.Name = 'Times New Roman'
    $h2.Font.Size = 14
    $h2.Font.Bold = 1
    $h2.ParagraphFormat.Alignment = 0
    $h2.ParagraphFormat.FirstLineIndent = 0
    $h2.ParagraphFormat.LineSpacingRule = 4
    $h2.ParagraphFormat.LineSpacing = 20
    $h2.ParagraphFormat.SpaceBefore = 8
    $h2.ParagraphFormat.SpaceAfter = 6

    $h3 = $doc.Styles.Item('标题 3')
    $h3.Font.NameFarEast = '黑体'
    $h3.Font.Name = 'Times New Roman'
    $h3.Font.Size = 12
    $h3.Font.Bold = 1
    $h3.ParagraphFormat.Alignment = 0
    $h3.ParagraphFormat.FirstLineIndent = 0
    $h3.ParagraphFormat.LineSpacingRule = 4
    $h3.ParagraphFormat.LineSpacing = 20
    $h3.ParagraphFormat.SpaceBefore = 6
    $h3.ParagraphFormat.SpaceAfter = 3

    # Fix cover fields and make cover paragraphs normal, not headings.
    $coverText = @(
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
    for ($i = 1; $i -le [Math]::Min($coverText.Count, $doc.Paragraphs.Count); $i++) {
        Set-Text -Para $doc.Paragraphs.Item($i) -Text $coverText[$i-1]
        $p = $doc.Paragraphs.Item($i)
        $p.Range.Style = '正文'
        $p.Alignment = 1
        $p.Range.Font.NameFarEast = '宋体'
        $p.Range.Font.Name = 'Times New Roman'
        $p.Range.Font.Size = 14
        $p.Range.Font.Bold = 0
        $p.FirstLineIndent = 0
    }
    foreach ($i in 1,2,5,6) {
        if ($i -le $doc.Paragraphs.Count) {
            $p = $doc.Paragraphs.Item($i)
            $p.Range.Font.Bold = 1
            $p.Range.Font.Size = 16
        }
    }

    # Apply heading hierarchy and body style.
    for ($i = 1; $i -le $doc.Paragraphs.Count; $i++) {
        $p = $doc.Paragraphs.Item($i)
        $text = $p.Range.Text.Trim()
        if ($text.Length -eq 0) { continue }
        if ($i -le 15) { continue }
        if (Is-ChapterHeading $text) {
            $p.Range.Style = '标题 1'
        } elseif (Is-SubSectionHeading $text) {
            $p.Range.Style = '标题 3'
        } elseif (Is-SectionHeading $text) {
            $p.Range.Style = '标题 2'
        } else {
            # Avoid changing TOC entries if script is rerun.
            if (($p.Range.Style.NameLocal -notmatch '^TOC') -and ($text -ne '目  录') -and ($text -ne 'CONTENT')) {
                $p.Range.Style = '正文'
            }
        }
    }

    # Remove a previous generated TOC if this script is rerun.
    $content = $doc.Content
    $find = $content.Find
    $find.ClearFormatting()
    $find.Text = '目  录'
    $find.Forward = $true
    $find.Wrap = 0
    if ($find.Execute()) {
        $start = $content.Start
        $next = $doc.Range($content.End, $doc.Content.End)
        $nf = $next.Find
        $nf.ClearFormatting()
        $nf.Text = '第 1 章 绪论'
        $nf.Forward = $true
        $nf.Wrap = 0
        if ($nf.Execute()) {
            $doc.Range($start, $next.Start).Delete() | Out-Null
        }
    }

    # Insert an automatic TOC before Chapter 1.
    $r = $doc.Content
    $f = $r.Find
    $f.ClearFormatting()
    $f.Text = '第 1 章 绪论'
    $f.Forward = $true
    $f.Wrap = 0
    if (-not $f.Execute()) { throw 'Cannot find Chapter 1 heading.' }
    $r.Collapse(1)
    $r.InsertBreak(7)
    $r.InsertBefore("目  录`r")
    $tocTitle = $doc.Paragraphs.Item($doc.Paragraphs.Count - 1)
    $tocTitle.Range.Style = '标题 1'
    $tocTitle.Range.Text = "目  录`r"
    $tocRange = $tocTitle.Range
    $tocRange.Collapse(0)
    $doc.TablesOfContents.Add($tocRange, $true, 1, 3) | Out-Null
    $afterToc = $doc.Range($tocRange.End, $tocRange.End)
    $afterToc.InsertBreak(7)

    # Page numbers.
    foreach ($section in $doc.Sections) {
        $footer = $section.Footers.Item(1)
        $footer.PageNumbers.RestartNumberingAtSection = $false
        if ($footer.PageNumbers.Count -eq 0) {
            $footer.PageNumbers.Add(1, $true) | Out-Null
        }
    }

    $doc.TablesOfContents.Item(1).Update()
    $doc.Save()
    $doc.ExportAsFixedFormat($outPdf, 17)
}
finally {
    $doc.Close($true)
    $word.Quit()
}

Write-Host "DOCX: $outDocx"
Write-Host "PDF : $outPdf"



