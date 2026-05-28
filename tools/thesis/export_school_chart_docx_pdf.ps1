$docx = (Resolve-Path "论文材料\曹逸天-毕业设计说明书-学校模板Word重排版-实验增强版-图表补充版.docx").Path
$pdf = (Join-Path (Split-Path $docx -Parent) "曹逸天-毕业设计说明书-学校模板Word重排版-实验增强版-图表补充版.pdf")
$archivePdf = (Resolve-Path "毕业设计提交归档-曹逸天").Path
$archivePdf = Join-Path $archivePdf "3毕业设计说明书-曹逸天-学校模板图表补充版.pdf"

$word = New-Object -ComObject Word.Application
$word.Visible = $false
$word.DisplayAlerts = 0
try {
    $doc = $word.Documents.Open($docx)
    $doc.ExportAsFixedFormat($pdf, 17)
    $doc.ExportAsFixedFormat($archivePdf, 17)
    $doc.Close($false)
    Write-Output $pdf
    Write-Output $archivePdf
}
finally {
    $word.Quit()
}
