$ErrorActionPreference = "Stop"

$docx = (Resolve-Path "毕业设计提交归档-曹逸天\3毕业设计说明书-曹逸天-学校模板图表补充精修版.docx").Path
$outDir = (Resolve-Path "build-temp").Path
$pdf = Join-Path $outDir "thesis_final_word_export.pdf"

$word = New-Object -ComObject Word.Application
$word.Visible = $false
$word.DisplayAlerts = 0
try {
    $doc = $word.Documents.Open($docx, $false, $true)
    # Refresh fields such as TOC/page numbers before export when Word can resolve them.
    foreach ($field in $doc.Fields) {
        try { [void]$field.Update() } catch {}
    }
    foreach ($toc in $doc.TablesOfContents) {
        try { [void]$toc.Update() } catch {}
    }
    $doc.ExportAsFixedFormat($pdf, 17)
    $doc.Close($false)
}
finally {
    $word.Quit()
}

Write-Output $pdf
