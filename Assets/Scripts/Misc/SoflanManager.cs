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

    private struct VisibleMsecRange
    {
        public VisibleMsecRange(float minMSec, TGrid minTGrid, float maxMSec, TGrid maxTGrid)
        {
            MinMSec = minMSec;
            MinTGrid = minTGrid;
            MaxMSec = maxMSec;
            MaxTGrid = maxTGrid;
        }

        public float MinMSec { get; }
        public TGrid MinTGrid { get; }
        public float MaxMSec { get; }
        public TGrid MaxTGrid { get; }

        public bool Contain(float msec)
        {
            return MinMSec <= msec && msec <= MaxMSec;
        }
    }

    public float ConvertAudioTimeToY_PreviewMode(float msec, int soflanGroup, float speed = 1)
    {
        return (float)TGridCalculator.ConvertAudioTimeToY_PreviewMode(TimeSpan.FromMilliseconds(msec), getSoflanList(soflanGroup), bpmList, 1);
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
