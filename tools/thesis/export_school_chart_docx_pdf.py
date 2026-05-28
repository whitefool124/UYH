from pathlib import Path

import win32com.client


docx = (Path("论文材料") / "曹逸天-毕业设计说明书-学校模板Word重排版-实验增强版-图表补充版.docx").resolve()
pdf = docx.with_suffix(".pdf")
archive_pdf = (Path("毕业设计提交归档-曹逸天") / "3毕业设计说明书-曹逸天-学校模板图表补充版.pdf").resolve()

word = win32com.client.Dispatch("Word.Application")
word.Visible = False
word.DisplayAlerts = 0
try:
    doc = word.Documents.Open(str(docx))
    doc.ExportAsFixedFormat(str(pdf), 17)
    doc.ExportAsFixedFormat(str(archive_pdf), 17)
    doc.Close(False)
finally:
    word.Quit()

print(pdf)
print(archive_pdf)
