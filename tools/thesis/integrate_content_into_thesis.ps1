$ErrorActionPreference = "Stop"

$root = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$paperDir = Join-Path $root "论文材料"
$sourceDocx = Join-Path $paperDir "曹逸天-毕业设计说明书-提交完善版.docx"
$outDocx = Join-Path $paperDir "曹逸天-毕业设计说明书-最终整合版.docx"
$outPdf = Join-Path $paperDir "曹逸天-毕业设计说明书-最终整合版.pdf"

Copy-Item -LiteralPath $sourceDocx -Destination $outDocx -Force

function Insert-After-Find {
    param(
        [Parameter(Mandatory=$true)] $Doc,
        [Parameter(Mandatory=$true)] [string] $Needle,
        [Parameter(Mandatory=$true)] [string[]] $Lines
    )
    $range = $Doc.Content
    $find = $range.Find
    $find.ClearFormatting()
    $find.Text = $Needle
    $find.Forward = $true
    $find.Wrap = 0
    $ok = $find.Execute()
    if (-not $ok) {
        throw "Text not found: $Needle"
    }
    $range.Collapse(0)
    $payload = "`r" + (($Lines -join "`r") + "`r")
    $range.InsertAfter($payload)
}

$word = New-Object -ComObject Word.Application
$word.Visible = $false
$doc = $word.Documents.Open($outDocx)

try {
    Insert-After-Find -Doc $doc -Needle '1.4 主要工作' -Lines @(
        '从研究贡献角度看，本文并不将工作重点放在单一静态手势分类器或某一种视觉模型精度提升上，而是面向普通 RGB 摄像头下的体感游戏场景，围绕连续视觉输入如何稳定转化为游戏命令这一问题展开。与只识别某一帧手势类别的方案相比，本文更关注动态轨迹、状态转换、输入抽象和 Unity 交互闭环之间的关系。',
        '本文的主要创新点体现在三个方面。第一，构建了面向 Unity 体感游戏的多输入源统一手势命令抽象，使 Mock、Native MediaPipe 和 ExternalBridge 三类输入能够向玩法层提供一致的 GestureFrame 与 GestureCommand。第二，设计了基于时间窗、关键点轨迹、状态转换和冷却机制的动态手势识别方法，使滑动、响指、指向到握拳等动作能够被解释为稳定的游戏语义命令。第三，建立了由自动化测试、离线回放、Jester 采样挖掘、YOLO 外部桥接测试和性能记录组成的验证链路，使系统不仅能够演示，也能够通过数据说明可用性、实时性和局限性。',
        '本文重点解决的关键问题包括：普通摄像头输入不稳定时如何保持游戏流程可运行；动态手势不只是单帧分类时如何利用连续轨迹进行判断；视觉识别结果如何通过中间命令层进入菜单、移动、施法和防御逻辑；自定义动态手势在少样本条件下为什么容易出现误触发与泛化不足，以及这类问题需要怎样的样本筛选和验证机制。'
    )

    Insert-After-Find -Doc $doc -Needle '6.1 实验环境' -Lines @(
        '本章实验围绕四个问题展开：第一，系统在不同输入模式下能否保持完整交互闭环；第二，动态手势规则在离线回放和公开数据采样中是否能够产生稳定命中；第三，ExternalBridge 与 YOLO 前置检测路线是否具备接入 Unity 的工程可行性；第四，少样本自定义动态手势增强采集能否改善真实交互中的识别表现。为避免将工程演示误写成完整监督学习结论，本文将各组实验的解释范围限定在功能闭环、回放验证、性能记录和可行性分析四个层面。'
    )

    Insert-After-Find -Doc $doc -Needle '6.4 扩展自动回放实验设计与结果' -Lines @(
        '本节实验的目的不是训练一个最终可部署的深度手势分类模型，而是回答规则模板在更大规模候选视频和离线回放条件下是否比最小样例更稳定这一问题。因此，本文关注目录数、可检出 clip、进入验证 clip、模板数量、保留验证 clip、正确命中、手部检出比例和三输入模式性能指标。Jester-120 与 Jester-300 的对比用于观察样本规模和标签分布变化对规则模板稳定性的影响；YOLO 外部桥接测试用于观察目标检测前端是否具备接入价值，而不将其解释为已经完成高精度 YOLO 手势分类器。'
    )

    Insert-After-Find -Doc $doc -Needle '6.5 自定义手势增强采集实验与反馈分析' -Lines @(
        '本节实验用于分析自定义动态手势导入在真实交互中的边界。实验预期并不是简单证明样本越多越好，而是检验在线采集、质量筛选、模板自检和运行时匹配规则是否一致。若增强样本被采纳后无法被回放自检认回，则说明采集质量门槛与识别规则之间存在错位；若清理后模板自检命中提升，则说明离群样本过滤和规则一致性检查对自定义动态手势比单纯增加正样本更重要。',
        '对于响指、双指缩放、手指爬行等更依赖指尖相对运动的手势，两个静态手势的连续识别可以作为简化兜底，但并不是复杂动态手指动作的最优表达。更稳妥的做法是在关键点序列层面建模指尖距离、相对速度、接触到分离状态和多指相位关系，使手指级动态变化能够直接参与模板匹配。'
    )

    Insert-After-Find -Doc $doc -Needle '6.7 结果与局限性分析' -Lines @(
        '需要说明的是，本文的研究边界是普通摄像头条件下动态手势到 Unity 游戏命令的工程化闭环，而不是完成一个覆盖全部手势类别、全部用户和全部环境的通用手势识别模型。基于这一边界，当前结果能够说明系统具备多输入源接入、动态动作触发、离线回放验证和性能记录能力，但不能被扩展解释为大规模真实用户长期测试结论。'
    )

    Insert-After-Find -Doc $doc -Needle '7.3 不足与展望' -Lines @(
        '本文系统仍有进一步改进空间，但这些不足并不否定当前工程闭环的价值，而是说明动态手势体感交互从毕业设计原型走向长期可用系统还需要进一步积累数据、模型和用户测试证据。',
        '对于自定义动态手势导入功能，后续应重点补充负样本约束、离群样本过滤、模板聚类、阈值自动估计和采集-识别一致性检查。这样才能使响指、双指缩放、手指爬行动作等更依赖指尖相对运动的手势，不只是通过两个静态手势连续识别来近似，而是能够在关键点序列层面形成更稳定的动态模板。'
    )

    $doc.Save()
    $doc.ExportAsFixedFormat($outPdf, 17)
}
finally {
    $doc.Close($true)
    $word.Quit()
}

Write-Host "DOCX: $outDocx"
Write-Host "PDF : $outPdf"

