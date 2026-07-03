# MajdataView 内存与 GC 卡顿诊断报告

- 诊断日期: 2026-07-04
- 诊断环境: Unity Editor Play Mode,通过 MCP Profiler 工具(profiler-get-script-stats 时间序列、script-execute 运行时反射、强制 GC 校准、原生对象清单、全程序集静态字段扫描)对运行中的编辑器实测
- 工作目录: F:\MajdataView

## 一、结论速览

症状"gameplay 过程中固定一段时间出现严重 GC 暂停"的成因是**两条叠加**:

1. **主因 —— 每帧托管堆分配抖动(~17 MB/s)**:soflan 更新路径对每个音符每帧都调用一次 `SoflanManager.ConvertAudioTimeToY_PreviewMode`,内部存在闭包 + `TGrid`(class)堆分配。1058 个 Tap × 64fps ≈ 13.5 万次/秒分配,产生 ~17 MB/s 垃圾,堆积到阈值触发 Full GC。
2. **放大因 —— 托管存活堆异常偏大(~762 MB)**:每次 Full GC 都要扫描这块巨堆,导致暂停严重。"固定间隔"= 攒够一次 GC 所需的时间。

原生侧(纹理 116MB / 显卡 155MB / 4923 GameObject)正常,问题全在托管堆。

## 二、实测数据

| 指标 | 实测值 | 说明 |
|---|---|---|
| Mono 堆分配速率 | ~17 MB/s(780→933MB / 9s) | 每帧产生大量垃圾 |
| 强制 GC 后存活堆 | ~762–800 MB | 校准证明为真实存活对象 |
| MonoHeap(预留) | 1094 MB | 堆已扩张且不回收 |
| 原生纹理 | 116 MB / 925 张 | 正常 |
| 显卡内存 | 155 MB | 正常 |
| GameObject 总数 | 4923(其中 Notes 子物体 2114) | 无孤儿/重复 |
| Soflan/BPM 缓存 | 总共 ~5867 个点,几百 KB | 不是 762MB 来源 |
| 全程序集静态字段 | 无大型累积器 | 不是单一集合泄漏 |
| AudioTimeProvider.isStart | False(诊断时) | 歌曲未播放,但音符 Update 仍每帧跑 |

### 校准实验(证明存活堆真实)

```
baseline(GC 后)       = 800,763,904
分配 200MB 数组后      = 1,010,491,392  (delta +209,727,488 ≈ 200MB)
释放并强制 GC 后       = 800,759,808    (delta -4096,回到基线)
```

`GC.GetTotalMemory(true)` 精确追踪存活对象,因此 ~762MB 是真实存活对象,非堆膨胀。

## 三、根因 1:soflan 更新路径的每帧堆分配(主因)

### 调用链

`TapBase.Update`(F:\MajdataView\Assets\Scripts\Notes\TapBase.cs:95)在 `SoflanManager.containsSoflans()` 为真时,对**每个音符每帧**走 `Update_soflan`:

```
TapBase.Update_soflan (TapBase.cs:157)
  -> NoteDrop.GetSoflanTiming (NoteDrop.cs:47)
    -> NoteDrop.GetSoflanValue (NoteDrop.cs:46)
      -> SoflanManager.ConvertAudioTimeToY_PreviewMode (SoflanManager.cs:148)
        -> TGridCalculator.ConvertAudioTimeToY_PreviewMode (TGridCalculator.cs)
          -> TGridCalculator.ConvertAudioTimeToTGrid (TGridCalculator.cs:24)
```

### 每次调用的确定堆分配

在 `TGridCalculator.ConvertAudioTimeToTGrid` 内:

1. `positionBpmList.LastOrDefault(x => x.AudioTime <= audioTime)` —— 捕获 `audioTime` 的**闭包对象 + 委托**,每次 new(`LastOrDefault` 接受 `Func<T,bool>`,lambda 闭包逃逸到堆)。
2. `pickBpm.TGrid + relativeBpmLenOffset` —— `TGrid` 是 **class**(`public class TGrid : GridBase`),`operator+` 每次 `new TGrid()`。
3. `bpmList.GetCachedAllBpmUniformPositionList()` 每次都 `foreach` 重算 hash(无谓 CPU)。

`GridOffset` 是 struct、`TimeSpan.FromMilliseconds` 返回 struct,二者不产生堆分配。

### 量级核算

1058 个 Tap × 64fps × (闭包 + TGrid) ≈ 13.5 万次/秒,约 10–17 MB/s,与实测吻合。这是"固定间隔 GC 卡顿"的直接来源。

注:仅 TapBase 走 soflan 路径(`containsSoflans` 判断只在 TapBase.Update 中);Hold/Slide/Touch 等未接入 soflan 更新。

## 四、根因 2:~762MB 存活托管堆(放大因)

校准证明该堆为真实存活对象。排查过程与排除项:

- 音符组件实例数正常(TapDrop=1058,其余各 1),管理器各 1 个 —— 无孤儿/重复 GameObject 泄漏
- 用户侧集合都很小(NoteManager.notes=0、noteOrder=1079、triggerSensors/Sensor.tasks=0)
- 全程序集静态字段扫描无大型累积器(最大的是 mscorlib/UnityEditor/Roslyn 的框架缓存,与游戏无关)
- Soflan/BPM 缓存只有几百 KB
- Sensor 事件订阅 1057 个 ≈ 当前存活 Tap 数,无事件泄漏

补充:部分存活堆包含 `script-execute` 使用的 Roslyn 程序集(`Microsoft.CodeAnalysis` 缓存),为排查工具自身引入;但首次读数(尚未跑脚本前)Mono 已达 805MB,游戏自身堆仍异常偏大。

**精确到类型需要 Memory Profiler 堆快照对比**:包程序的 `UnityEditor.MemoryProfiler.MemoryProfiler.TakeSnapshot` 在本版本从 MCP 脚本宿主访问不到,实验性 `UnityEngine.Profiling.Memory.Experimental.MemoryProfiler` 也无 `TakeSnapshot` 方法。建议:打开 Window > Analysis > Memory Profiler,正常播放时拍一张快照,播放约 1 分钟后再拍一张,用 Compare 看增量。

## 五、其他确认的真实缺陷(次要)

1. **事件订阅泄漏(切谱时)**:TapBase.OnDestroy(TapBase.cs:271)等 `if (HttpHandler.IsReloding) return;` 会跳过 `inputManager.UnbindArea`,切谱/SeekTo 时旧音符委托残留在 Sensor 上。正常游玩不触发,反复切谱会累积。
2. **每音符 `new Guid()`**:NoteDrop.cs:22 `protected Guid guid = Guid.NewGuid();`(疑似死代码,InputManager 用自己的 guid)。
3. **每次判定的 `GameObject.Find` + `GetComponent` 链**:NoteEffectManager.PlayFastLate(NoteEffectManager.cs:154)等 3 处每次判定都 `GameObject.Find("Outline").GetComponent<CustomSkin>()`。
4. **每次判定的 `string.Format`**:ObjectCounter(ObjectCounter.cs:531)在 `outputDirty` 时多次 `string.Format` 重建文本。
5. **原生纹理泄漏(独立问题)**:ScreenRecorder.CaptureScreen(ScreenRecorder.cs:87)每帧 `texture = ScreenCapture.CaptureScreenshotAsTexture()` 但从不 `Object.Destroy(texture)` 旧纹理,录屏时持续漏原生内存。

## 六、根因 1 优化方案(本次实施)

核心思路:同一帧内所有音符读取的 `timeProvider.AudioTime` 完全相同,而 `ConvertAudioTimeToY_PreviewMode` 仅依赖 `(soflanGroup, msec)`(`speed` 参数在 SoflanManager 中被忽略,固定 scale=1)。因此按 `soflanGroup` 做帧内记忆化,把每帧 1058 次计算降到 9 次(每个 soflanGroup 一次),命中时零分配。

### 实施

在 `SoflanManager` 中:

- 新增字段 `Dictionary<int, (float msec, float result)> _yPreviewCache`
- `ConvertAudioTimeToY_PreviewMode` 命中缓存(`msec` 精确相等)则直接返回,否则计算一次并写入
- `clearAll()` 中清空缓存(切谱失效;`JsonDataLoader.LoadJson` 每次先调 `clearAll`)

### 预期效果

- 每帧分配次数:~13.5 万/秒 → ~1150/秒(仅 9 次 miss 各分配 1 闭包 + 1 TGrid),降幅约 99%
- GC 触发频率大幅下降,卡顿基本消除
- 定位数学逻辑不变(同一 (group, msec) 结果完全一致),零行为风险

### 可选的进一步优化(未实施,风险较高)

- 改写 `TGridCalculator.ConvertAudioTimeToTGrid`:用项目自带的 `LastOrDefaultByBinarySearch`(非捕获 lambda 不分配)替换 `LastOrDefault(闭包)`,并新增返回 `TotalUnit` 的重载避免 `new TGrid`。涉及第三方框架文件,定位数学敏感,故本次仅做 SoflanManager 层记忆化。

## 七、验证方法

实施后用 MCP `profiler-get-script-stats` 重复采样 `GCMemoryUsageMB` 时间序列,确认分配速率从 ~17 MB/s 降至可忽略;并观察 gameplay 卡顿是否消失。