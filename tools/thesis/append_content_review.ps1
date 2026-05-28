$ErrorActionPreference = "Stop"

$root = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$paperDir = Join-Path $root "论文材料"
$sourceDocx = Join-Path $paperDir "曹逸天-毕业设计说明书-提交完善版.docx"
$outDocx = Join-Path $paperDir "曹逸天-毕业设计说明书-内容强化版.docx"
$outPdf = Join-Path $paperDir "曹逸天-毕业设计说明书-内容强化版.pdf"

Copy-Item -LiteralPath $sourceDocx -Destination $outDocx -Force

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
}

function Add-PageBreak {
    param([Parameter(Mandatory=$true)] $Doc)
    $range = $Doc.Range($Doc.Content.End - 1, $Doc.Content.End - 1)
    $range.InsertBreak(7)
}

$word = New-Object -ComObject Word.Application
$word.Visible = $false
$doc = $word.Documents.Open($outDocx)

try {
    Add-PageBreak -Doc $doc
    Add-Para -Doc $doc -Text "内容强化补充说明" -Style "标题 1" -Align 1 -Bold $true -Size 16
    Add-Para -Doc $doc -Text "为进一步增强论文的研究性表达，本补充页对正文中需要重点突出的贡献、实验解释和研究边界进行归纳。后续若继续精修，可将本页内容分散吸收到第 1 章、第 6 章和第 7 章中。"

    Add-Para -Doc $doc -Text "一、研究贡献凝练" -Style "标题 2" -Bold $true
    Add-Para -Doc $doc -Text "本文的贡献不应只表述为完成了 Unity 游戏原型和若干功能模块，而应凝练为三个层面：第一，构建了面向 Unity 体感游戏的多输入源统一手势命令抽象，使 Mock、Native MediaPipe 和 ExternalBridge 能够向玩法层提供一致的数据帧和命令；第二，设计了基于时间窗、关键点轨迹、状态转换和冷却机制的动态手势识别方法，使滑动、响指、指向到握拳等连续动作能够转化为稳定的游戏语义命令；第三，建立了自动化测试、离线回放、Jester 采样挖掘、YOLO 外部桥接测试和性能记录组成的验证链路，使系统不仅能够演示，也能够通过数据说明可用性、实时性和局限性。"
    Add-Para -Doc $doc -Text "论文应主动强调：本文不是单纯训练一个静态手势分类器，而是解决普通摄像头条件下连续视觉输入如何稳定进入游戏交互逻辑的问题。这个定位能够解释为什么系统同时包含输入抽象、动态规则、命令历史、Unity 玩法映射和实验回放。"

    Add-Para -Doc $doc -Text "二、实验解释边界" -Style "标题 2" -Bold $true
    Add-Para -Doc $doc -Text "第 6 章的数据需要按照问题、指标、结论和边界来解释。Mock 模式主要验证游戏逻辑闭环和答辩兜底能力；Native MediaPipe 模式验证真实摄像头链路在 Unity 内部运行时的可用性；ExternalBridge 模式验证外部 Python、离线视频和 YOLO + MediaPipe 链路接入 Unity 的工程可行性；Jester 采样回放用于观察规则模板在公开动态手势视频分布下的稳定性变化。"
    Add-Para -Doc $doc -Text "YOLO 外部桥接实验只能说明目标检测前端和外部视觉链路具备接入价值，不能表述为已经完成高精度 YOLO 手势分类器。Jester-120 与 Jester-300 的结果可以说明扩大采样规模和改善标签分布后，规则模板的离线回放表现更稳定，但不能等同于真实用户长期实验。"

    Add-Para -Doc $doc -Text "三、自定义动态手势的研究发现" -Style "标题 2" -Bold $true
    Add-Para -Doc $doc -Text "自定义动态手势实验中，增强采集后 13 个样本只有少量能被规则认回，表面上看是不理想结果，但论文中应将其解释为有价值的研究发现：少样本动态手势导入不能只靠增加人工确认的正样本，还需要负样本约束、离群样本过滤、模板一致性检查和阈值自动估计。采集阶段的质量门槛与识别阶段的匹配规则如果不一致，就会出现样本被采纳但无法被模板自检认回的问题。"
    Add-Para -Doc $doc -Text "对于响指、双指缩放、手指爬行等更依赖指尖相对运动的手势，后续方案不应只依赖两个静态手势的连续识别，而应在关键点序列层面建模指尖距离、相对速度、接触-分离状态和多指相位关系。两个静态手势连续识别可以作为简化兜底，但不是复杂动态手指动作的最优表达。"

    Add-Para -Doc $doc -Text "四、总结与展望表达建议" -Style "标题 2" -Bold $true
    Add-Para -Doc $doc -Text "论文结尾应避免把局限写成单纯自我否定，而应写成研究边界：本文已经完成普通 RGB 摄像头下动态手势到 Unity 游戏命令的工程化闭环，但尚未完成大规模真实用户实验、深度时序模型训练和完整商业化游戏内容。后续工作可以沿三条路线展开：其一，引入 YOLO 前置检测并与纯 MediaPipe 链路做定量比较；其二，引入 LSTM、GRU 或 Transformer 学习复杂动态手势，减少手工阈值调参；其三，完善自定义动态手势导入中的负样本、聚类、离群过滤和阈值自动估计机制。"

    $doc.Save()
    $doc.ExportAsFixedFormat($outPdf, 17)
}
finally {
    $doc.Close($true)
    $word.Quit()
}

Write-Host "DOCX: $outDocx"
Write-Host "PDF : $outPdf"

