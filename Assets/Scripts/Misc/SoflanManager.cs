using MajSimai;
using OngekiFumenEditor.Core.Base;
using OngekiFumenEditor.Core.Base.Collections;
using OngekiFumenEditor.Core.Base.EditorObjects;
using OngekiFumenEditor.Core.Base.OngekiObjects;
using OngekiFumenEditor.Core.Modules.FumenVisualEditor;
using OngekiFumenEditor.Core.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

internal class SoflanManager
{
    public static SoflanManager Instance { get; } = new SoflanManager();

    private SoflanListMap soflanListMap = new();
    private BpmList bpmList = new BpmList();
    private bool containSoflans = false;
    private Dictionary<int, int> registerNoteIndexToSoflanGroupMap = new();

    // 帧内记忆化:同一帧内所有音符读取的 AudioTime 相同,且 ConvertAudioTimeToY_PreviewMode
    // 仅依赖 (soflanGroup, msec)(speed 参数被忽略,固定 scale=1)。按 soflanGroup 缓存最近
    // 一次 (msec, result),避免每个音符每帧重复分配闭包/TGrid。详见 docs/内存与GC卡顿诊断.md。
    private struct YPreviewCacheEntry
    {
        public float msec;
        public float result;
    }
    private readonly Dictionary<int, YPreviewCacheEntry> _yPreviewCache = new();

    private readonly struct VisibleRangeCacheKey : IEquatable<VisibleRangeCacheKey>
    {
        public VisibleRangeCacheKey(int soflanGroup, float visibleMsec, float visualAudioOffsetMsec)
        {
            SoflanGroup = soflanGroup;
            VisibleMsec = visibleMsec;
            VisualAudioOffsetMsec = visualAudioOffsetMsec;
        }

        private int SoflanGroup { get; }
        private float VisibleMsec { get; }
        private float VisualAudioOffsetMsec { get; }

        public bool Equals(VisibleRangeCacheKey other)
        {
            return SoflanGroup == other.SoflanGroup
                && VisibleMsec == other.VisibleMsec
                && VisualAudioOffsetMsec == other.VisualAudioOffsetMsec;
        }

        public override bool Equals(object obj)
        {
            return obj is VisibleRangeCacheKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = SoflanGroup;
                hashCode = (hashCode * 397) ^ VisibleMsec.GetHashCode();
                hashCode = (hashCode * 397) ^ VisualAudioOffsetMsec.GetHashCode();
                return hashCode;
            }
        }
    }

    private sealed class VisibleRangeCache
    {
        public int Version;
        public float CurrentSoflanY = float.NaN;
        public readonly List<SoflanList.VisibleMsecRange> Ranges = new();
        public readonly SoflanList.VisibleRangeQueryScratch Scratch = new();
    }

    private readonly Dictionary<VisibleRangeCacheKey, VisibleRangeCache> _visibleRangeCache = new();
    private float _visibleRangeFrameMsec = float.NaN;
    private int _visibleRangeCacheVersion = 1;

    private void log(string message)
    {
        //todo
    }

    /// <summary>
    /// clear all
    /// </summary>
    public void clearAll()
    {
        soflanListMap = new();
        bpmList = new();
        containSoflans = false;

        registerNoteIndexToSoflanGroupMap.Clear();
        _yPreviewCache.Clear();
        _visibleRangeCache.Clear();
        _visibleRangeFrameMsec = float.NaN;
        _visibleRangeCacheVersion = 1;

        log("SoflanManager cleared");
    }

    private int GetNoteId(SimaiNote note)
    {
        return note.GetHashCode();
    }

    public void loadChart(IEnumerable<SimaiTimingPoint> timingPoints)
    {
        float lastBpm = float.NaN;

        var lastHSpeedMap = new Dictionary<int, float>();
        float getLastHSpeed(int soflanGroup) => lastHSpeedMap.GetValueOrDefault(soflanGroup, 1);
        void setLastHSpeed(int soflanGroup, float lastHSpeed) => lastHSpeedMap[soflanGroup] = lastHSpeed;

        foreach (var tp in timingPoints)
        {
            //BPM 变化
            if (tp.Bpm != lastBpm)
            {
                if (float.IsNaN(lastBpm))
                {
                    //init firstBPM
                    bpmList.FirstBpm = tp.Bpm;
                }
                else
                {
                    //add new
                    var tGrid = TGridCalculator.ConvertAudioTimeToTGrid(TimeSpan.FromSeconds(tp.Timing), bpmList);
                    var bpmChange = new BPMChange()
                    {
                        TGrid = tGrid,
                        BPM = tp.Bpm
                    };
                    bpmList.Add(bpmChange);
                }

                lastBpm = tp.Bpm;
            }

            //HSpeed 变化
            var lastHSpeed = getLastHSpeed(tp.SoflanGroup);
            if (tp.HSpeed != lastHSpeed)
            {
                var tGrid = TGridCalculator.ConvertAudioTimeToTGrid(TimeSpan.FromSeconds(tp.Timing), bpmList);
                var soflan = new KeyframeSoflan()
                {
                    TGrid = tGrid,
                    Speed = tp.HSpeed,
                    SoflanGroup = tp.SoflanGroup,
                };
                soflanListMap[tp.SoflanGroup].Add(soflan);
                setLastHSpeed(tp.SoflanGroup, tp.HSpeed);

                containSoflans = true;
            }

            for (var k = 0; k < tp.Notes.Length; k++)
            {
                var note = tp.Notes[k];

                var noteId = GetNoteId(note);
                registerNoteIndexToSoflanGroupMap[noteId] = note.SoflanGroup;
                log($"register noteId:{noteId}, soflanGroup:{note.SoflanGroup}");
            }
        }
    }

    public bool containsSoflans()
    {
        return containSoflans;
    }

    public SoflanList getSoflanList(int soflanGroup)
    {
        return soflanListMap[soflanGroup];
    }

    public SoflanListMap getSoflanListMap()
    {
        return soflanListMap;
    }

    //-------------------------------------------

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private void BeginVisibleRangeFrame(float currentAudioMsec)
    {
        if (_visibleRangeFrameMsec == currentAudioMsec)
            return;

        _visibleRangeFrameMsec = currentAudioMsec;
        if (_visibleRangeCacheVersion == int.MaxValue)
        {
            _visibleRangeCache.Clear();
            _visibleRangeCacheVersion = 1;
        }
        else
        {
            _visibleRangeCacheVersion++;
        }
    }

    public float GetCurrentSoflanY(float currentAudioMsec, int soflanGroup, float visualAudioOffsetMsec)
    {
        var adjustedAudioMsec = IsFinite(currentAudioMsec) && IsFinite(visualAudioOffsetMsec)
            ? currentAudioMsec + visualAudioOffsetMsec
            : 0f;
        if (!IsFinite(adjustedAudioMsec) || adjustedAudioMsec < 0f)
            adjustedAudioMsec = 0f;

        return ConvertAudioTimeToY_PreviewMode(adjustedAudioMsec, soflanGroup);
    }

    public bool IsNoteVisible(
        float currentAudioMsec,
        float noteAudioMsec,
        int soflanGroup,
        float visibleMsec,
        float visualAudioOffsetMsec)
    {
        if (!containSoflans)
            return true;
        if (!IsFinite(currentAudioMsec)
            || !IsFinite(noteAudioMsec)
            || !IsFinite(visibleMsec)
            || !IsFinite(visualAudioOffsetMsec)
            || visibleMsec <= 0f)
            return false;

        BeginVisibleRangeFrame(currentAudioMsec);

        var key = new VisibleRangeCacheKey(soflanGroup, visibleMsec, visualAudioOffsetMsec);
        if (!_visibleRangeCache.TryGetValue(key, out var cache))
        {
            cache = new VisibleRangeCache();
            _visibleRangeCache[key] = cache;
        }

        var currentSoflanY = GetCurrentSoflanY(
            currentAudioMsec,
            soflanGroup,
            visualAudioOffsetMsec);
        if (cache.Version != _visibleRangeCacheVersion || cache.CurrentSoflanY != currentSoflanY)
        {
            getSoflanList(soflanGroup).FillVisibleMsecRangesForGamePreview(
                currentSoflanY,
                visibleMsec,
                bpmList,
                cache.Ranges,
                cache.Scratch);
            cache.Version = _visibleRangeCacheVersion;
            cache.CurrentSoflanY = currentSoflanY;
        }

        for (var i = 0; i < cache.Ranges.Count; i++)
        {
            if (cache.Ranges[i].Contain(noteAudioMsec))
                return true;
        }

        return false;
    }

    public float ConvertAudioTimeToY_PreviewMode(float msec, int soflanGroup, float speed = 1)
    {
        // speed 参数在本实现中被忽略(下方固定 scale=1),因此缓存键只需 (soflanGroup, msec)。
        // 同一帧内所有音符的 msec 相同(同一 AudioTime),故每帧每 soflanGroup 仅计算一次。
        if (_yPreviewCache.TryGetValue(soflanGroup, out var cached) && cached.msec == msec)
            return cached.result;

        var result = (float)TGridCalculator.ConvertAudioTimeToY_PreviewMode(TimeSpan.FromMilliseconds(msec), getSoflanList(soflanGroup), bpmList, 1);
        _yPreviewCache[soflanGroup] = new YPreviewCacheEntry { msec = msec, result = result };
        return result;
    }

    public SoflanList.SoflanPoint GetSoflanSpeedPoint_PreviewMode(float msec, int soflanGroup, float speed = 1)
    {
        var cachedSoflanPositionList_PreviewMode = getSoflanList(soflanGroup).GetCachedSoflanPositionList_PreviewMode(bpmList);
        var soflanPoint = cachedSoflanPositionList_PreviewMode.LastOrDefaultByBinarySearch(TGridCalculator.ConvertAudioTimeToTGrid(TimeSpan.FromMilliseconds(msec), bpmList).TotalUnit, (SoflanList.SoflanPoint x) => x.TGrid.TotalUnit);
        return soflanPoint;
    }

    public void DumpCurrent(int currentTime = -1)
    {
        log($"-------DUMP SOFLAN TIMING POINTS-------");
        foreach (KeyValuePair<int, SoflanList> pair in soflanListMap)
        {
            var soflanGroup = pair.Key;
            var soflanList = pair.Value;

            log($"");
            log($"SoflanGroup: {soflanGroup}");
            foreach (var timingPoint in soflanList.GetCachedSoflanPositionList_PreviewMode(bpmList))
                log($"\t\t * AudioTime:{TGridCalculator.ConvertTGridToAudioTime(timingPoint.TGrid, bpmList).TotalMilliseconds}ms {timingPoint}");
        }
        log($"---------------------------------------");

        log($"containSoflans: {containSoflans}");
        log($"cachedVisibleRangeListMap:");
    }
}
