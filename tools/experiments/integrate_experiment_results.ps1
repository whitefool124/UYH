$ErrorActionPreference = "Stop"

$Root = "E:\bishe\gesture-game"
$SourceDocx = Join-Path $Root "论文材料\曹逸天-毕业设计说明书-学校模板Word重排版.docx"
$OutDocx = Join-Path $Root "论文材料\曹逸天-毕业设计说明书-学校模板Word重排版-实验增强版.docx"
$OutPdf = Join-Path $Root "论文材料\曹逸天-毕业设计说明书-学校模板Word重排版-实验增强版.pdf"
$DataDir = Join-Path $Root "论文材料\实验补充数据"

Copy-Item -LiteralPath $SourceDocx -Destination $OutDocx -Force
Remove-Item -LiteralPath $OutPdf -ErrorAction SilentlyContinue

function Read-CsvUtf8($path) {
    Import-Csv -LiteralPath $path -Encoding UTF8
}

function Set-FindReplace($doc, $findText, $replaceText) {
    $range = $doc.Content
    $find = $range.Find
    $find.ClearFormatting()
    $find.Replacement.ClearFormatting()
    $find.Text = $findText
    $find.Replacement.Text = $replaceText
    $find.Forward = $true
    $find.Wrap = 1
    $find.Format = $false
    $find.MatchCase = $false
    $find.MatchWholeWord = $false
    $find.MatchWildcards = $false
    [void]$find.Execute($findText, $false, $false, $false, $false, $false, $true, 1, $false, $replaceText, 2)
}

function Add-Paragraph($doc, [ref]$range, $text, $styleName = "正文", $alignment = 0) {
    $start = $range.Value.Start
    $range.Value.InsertBefore($text + "`r")
    $end = $start + ($text + "`r").Length
    $pRange = $doc.Range($start, $end)
    $p = $pRange.Paragraphs.Item(1)
    try { $p.Range.Style = $doc.Styles.Item($styleName) } catch {}
    $range.Value = $doc.Range($end, $end)
}

function Add-WordTable($doc, [ref]$range, [string[]]$headers, [object[]]$rows, [scriptblock]$rowMapper) {
    $table = $doc.Tables.Add($range.Value, [Math]::Max(1, $rows.Count + 1), $headers.Count)
    $table.Borders.Enable = 1
    $table.Rows.Item(1).Range.Bold = $true
    for ($c = 0; $c -lt $headers.Count; $c++) {
        $table.Cell(1, $c + 1).Range.Text = $headers[$c]
    }
    for ($r = 0; $r -lt $rows.Count; $r++) {
        $values = & $rowMapper $rows[$r]
        for ($c = 0; $c -lt $headers.Count; $c++) {
            $table.Cell($r + 2, $c + 1).Range.Text = [string]$values[$c]
        }
    }
    $table.AutoFitBehavior(2)
    $range.Value = $doc.Range($table.Range.End, $table.Range.End)
    $range.Value.InsertAfter("`r")
    $range.Value = $doc.Range($range.Value.End, $range.Value.End)
}

$overview = @{}
Read-CsvUtf8 (Join-Path $DataDir "experiment_overview.csv") | ForEach-Object { $overview[$_.metric] = $_.value }
$gestureMetrics = @(Read-CsvUtf8 (Join-Path $DataDir "gesture_precision_recall_f1.csv"))
$extendedMetrics = @(Read-CsvUtf8 (Join-Path $DataDir "extended_recognition_metrics.csv"))
$performance = @(Read-CsvUtf8 (Join-Path $DataDir "input_mode_performance_summary.csv"))
$yolo = @(Read-CsvUtf8 (Join-Path $DataDir "yolo_bridge_summary.csv"))
$finger = @(Read-CsvUtf8 (Join-Path $DataDir "finger_level_feature_by_gesture.csv"))
$comparison = @(Read-CsvUtf8 (Join-Path $DataDir "static_vs_dynamic_comparison.csv"))

$word = New-Object -ComObject Word.Application
$word.Visible = $false
$word.DisplayAlerts = 0
try {
    $doc = $word.Documents.Open($OutDocx, $false, $false)
    $insert = $doc.Content.Duplicate
    $insert.Find.Text = "结果与局限性分析"
    $insert.Find.Forward = $true
    $insert.Find.Wrap = 0
    if (-not $insert.Find.Execute()) {
        throw "Cannot find insertion point: 结果与局限性分析"
    }
    $insert.Paragraphs.Item(1).Range.Text = "6.8 结果与局限性分析`r"
    $range = $doc.Range($insert.Start, $insert.Start)

    Add-Paragraph $doc ([ref]$range) "6.7 实验补充统计与缺口验证" "标题 2"
    Add-Paragraph $doc ([ref]$range) "为补充前文中动态手势识别准确性、数据集构成、手指级动态特征和 YOLO 外部桥接边界等证据，本文基于现有 Jester 挖掘结果、ExternalBridge 回放验证、自定义手势模板库和 YOLO 参考视频处理结果进行了补充统计。该部分实验不将 YOLO 路线表述为已经完成的高精度手势分类模型，而是将其限定为外部视觉桥接与前置定位可行性验证。" "正文"

    Add-Paragraph $doc ([ref]$range) "6.7.1 数据集构成与样本筛选" "标题 3"
    Add-Paragraph $doc ([ref]$range) "Jester-300 挖掘实验共处理 300 条记录，接受 215 条，接受率为 $($overview['accepted_rate'])。外部模板回放子集包含 $($overview['subset_clips']) 个 clip，其中训练 $($overview['train_clips']) 个、测试 $($overview['test_clips']) 个，覆盖 8 类方向性动态手势。AVI-200 扩展回放集生成 $($overview['dataset_clips']) 个 clip，总回放帧数为 $($overview['total_replay_frames'])。在 AVI-200 采样中，共采样 $($overview['sampled_frames']) 帧，其中 MediaPipe 有效检测帧为 $($overview['detected_frames']) 帧，有效帧比例为 $($overview['detected_frame_rate'])。" "正文"
    $datasetRows = @(
        [pscustomobject]@{Item="Jester-300 挖掘记录";Value=$overview['mined_rows'];Note="用于动态位移候选样本挖掘"},
        [pscustomobject]@{Item="接受样本";Value=$overview['accepted_rows'];Note="满足手部检测和位移规则"},
        [pscustomobject]@{Item="接受率";Value=$overview['accepted_rate'];Note="accepted / mined"},
        [pscustomobject]@{Item="外部模板回放子集";Value=$overview['subset_clips'];Note="8 类方向性动态手势"},
        [pscustomobject]@{Item="AVI-200 扩展回放 clips";Value=$overview['dataset_clips'];Note="端到端回放和性能统计"},
        [pscustomobject]@{Item="有效检测帧比例";Value=$overview['detected_frame_rate'];Note="7359 / 9600"}
    )
    Add-WordTable $doc ([ref]$range) @("数据项","数值","说明") $datasetRows { param($r) @($r.Item,$r.Value,$r.Note) }

    Add-Paragraph $doc ([ref]$range) "6.7.2 动态手势识别准确性" "标题 3"
    Add-Paragraph $doc ([ref]$range) "外部模板回放验证共包含 $($overview['validation_clips']) 个测试 clip，正确识别 $($overview['validation_correct']) 个，Micro Precision、Micro Recall 和 Micro F1 均为 $($overview['micro_f1'])。需要说明的是，该组结果对应模板化离线回放子集，样本量较小，主要用于验证动态模板匹配链路的正确性；系统级扩展回放结果则进一步统计了不同手势在端到端回放中的成功率与误匹配情况。" "正文"
    Add-WordTable $doc ([ref]$range) @("手势","样本数","Precision","Recall","F1","漏检率") $gestureMetrics { param($r) @($r.gesture,$r.support,$r.precision,$r.recall,$r.f1,$r.miss_rate) }
    Add-Paragraph $doc ([ref]$range) "在 AVI-200 扩展回放结果中，body_shift_left、body_shift_right、swipe_lr 和 swipe_rl 的成功率分别为 0.75、0.80、0.80 和 1.00；严格通过子集的成功率为 1.00。该结果表明规则模板在筛选后的回放数据上具有可用性，但在样本较少和边界动作较多时仍存在 false_match，需要通过负样本、阈值自适应和更多真实用户样本继续改进。" "正文"
    Add-WordTable $doc ([ref]$range) @("手势","尝试次数","正确","误匹配","Precision","Recall","F1") $extendedMetrics { param($r) @($r.gesture,$r.attempts,$r.correct,$r.false_match,$r.precision,$r.recall,$r.f1) }

    Add-Paragraph $doc ([ref]$range) "6.7.3 手指级动态特征与静态连续识别对照" "标题 3"
    Add-Paragraph $doc ([ref]$range) "为避免将动态手势简化为手掌位置移动，本文进一步统计了自定义手势模板中的手指级动态特征。系统中的 CustomGestureSequenceFeatures 已包含拇指-食指距离、拇指-中指距离、选定指尖距离变化、指尖峰值速度、振荡次数和手掌路径长度等指标。以双指外滑放大手势为例，手指距离路径与手掌路径之比达到 6.2521，说明该类手势主要由指尖相对运动决定，而不是由手掌整体位移决定。" "正文"
    $fingerSelected = @($finger | Sort-Object {[double]$_.mean_finger_to_palm_path_ratio} -Descending | Select-Object -First 6)
    Add-WordTable $doc ([ref]$range) @("手势","样本数","指距变化均值","指距路径","手掌路径","指/掌路径比") $fingerSelected { param($r) @($r.gesture_id,$r.samples,$r.mean_abs_selected_distance_delta,$r.mean_selected_distance_path,$r.mean_palm_path_length,$r.mean_finger_to_palm_path_ratio) }
    Add-Paragraph $doc ([ref]$range) "从方法选择上看，滑动类手势适合使用手掌轨迹、时间窗和冷却机制；指向到握拳适合使用姿态状态转移；响指、双指缩放和模拟夹动则应使用 FingerDistanceChange、FeatureSequence 或 FingerOscillation 等手指级动态特征。两静态手势连续识别可以作为状态切换明显动作的轻量方案，但不足以表达连续距离变化和重复开合等细粒度动作。" "正文"
    Add-WordTable $doc ([ref]$range) @("手势","单帧静态","两静态连续","手掌轨迹","手指级特征","推荐方案") $comparison { param($r) @($r.gesture,$r.single_static_frame,$r.two_static_sequence,$r.dynamic_trajectory,$r.finger_level_features,$r.recommended_method) }

    Add-Paragraph $doc ([ref]$range) "6.7.4 YOLO 外部桥接可行性" "标题 3"
    Add-Paragraph $doc ([ref]$range) "YOLO 外部桥接实验共处理 18 个参考视频，桥接程序均成功完成，平均处理速度为 $($overview['yolo_avg_fps']) FPS，平均 hand_ratio 为 $($overview['yolo_avg_hand_ratio'])。其中检测到手部关键点的视频为 $($overview['yolo_hand_positive_videos']) 个，未检测到手部关键点的视频为 $($overview['yolo_zero_hand_videos']) 个。该结果说明 YOLO + MediaPipe 路线可以作为外部视觉桥接和前置定位增强的探索方向，但当前结果不足以支撑“已经完成高精度 YOLO 动态手势分类器”的结论。" "正文"
    Add-WordTable $doc ([ref]$range) @("指标","数值") $yolo { param($r) @($r.metric,$r.value) }

    Add-Paragraph $doc ([ref]$range) "6.7.5 三种输入模式性能对比" "标题 3"
    Add-Paragraph $doc ([ref]$range) "性能统计覆盖 Mock、Native MediaPipe 和 ExternalBridge 三种输入模式，每种模式重复 9 次。Mock 模式平均 FPS 接近 60，适合作为稳定演示和自动化测试基线；Native MediaPipe 模式平均 FPS 为 29.650，反映 Unity 内部视觉处理带来的额外开销；ExternalBridge 模式平均 FPS 为 49.909，平均估计链路延迟为 17.593 ms，说明外部视觉链路在当前工程中具备接入游戏逻辑的实时性基础。" "正文"
    Add-WordTable $doc ([ref]$range) @("模式","运行次数","平均 FPS","P95 帧时(ms)","最小 FPS","链路延迟(ms)") $performance { param($r) @($r.mode,$r.runs,$r.average_fps_mean,$r.p95_frame_ms_mean,$r.min_fps_mean,$r.avg_estimated_latency_ms_mean) }
    Add-Paragraph $doc ([ref]$range) "综上，补充实验表明，本文系统已形成从数据筛选、动态模板验证、手指级特征建模、外部视觉桥接到 Unity 性能监控的实验闭环。其局限性同样明确：当前准确率结果主要来自筛选后的离线回放和小规模验证集，尚不能替代大规模真实用户实验；YOLO 路线目前应作为外部桥接可行性和后续增强方向，而不是最终手势分类模型。" "正文"

    $doc.Fields.Update() | Out-Null
    foreach ($toc in $doc.TablesOfContents) { $toc.Update() | Out-Null }
    $doc.Repaginate()
    $doc.Save()
    $doc.ExportAsFixedFormat($OutPdf, 17)
    [pscustomobject]@{
        Docx = $OutDocx
        Pdf = $OutPdf
        Pages = $doc.ComputeStatistics(2)
        Words = $doc.ComputeStatistics(0)
        Tables = $doc.Tables.Count
        InlineShapes = $doc.InlineShapes.Count
        PdfExists = Test-Path -LiteralPath $OutPdf
        PdfLength = (Get-Item -LiteralPath $OutPdf -ErrorAction SilentlyContinue).Length
    } | Format-List
    $doc.Close($true)
}
finally {
    $word.Quit()
}





