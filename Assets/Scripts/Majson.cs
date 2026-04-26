using MajSimai;
using System.Collections.Generic;

internal class Majson
{
    public string artist = "default";
    public string designer = "default";
    public string difficulty = "EZ";
    public int diffNum = 0;
    public string level = "1";
    public List<SimaiTimingPoint> timingList = new();
    public string title = "default";
}

internal class EditRequestjson
{
    public float audioSpeed;
    public float backgroundCover;
    public EditorComboIndicator comboStatusType;
    public EditorPlayMethod editorPlayMethod;
    public EditorControlMethod control;
    public string jsonPath;
    public float noteSpeed;
    public long startAt;
    public float startTime;
    public float touchSpeed;
    public bool smoothSlideAnime;
}

public enum EditorComboIndicator
{
    None,

    // List of viable indicators that won't be a static content.
    // ScoreBorder, AchievementMaxDown, ScoreDownDeluxe are static.
    Combo,
    ScoreClassic,
    AchievementClassic,
    AchievementDownClassic,
    AchievementDeluxe = 11,
    AchievementDownDeluxe,
    ScoreDeluxe,

    // Please prefix custom indicator with C
    CScoreDedeluxe = 101,
    CScoreDownDedeluxe,
    MAX
}

internal enum EditorControlMethod
{
    Start,
    Stop,
    OpStart,
    Pause,
    Continue,
    Record,
    SeekTo
}

public enum EditorPlayMethod
{
    Classic, DJAuto, Random, Disabled
}