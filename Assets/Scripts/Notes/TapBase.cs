using Assets.Scripts.Types;
using System;
using System.ComponentModel;
using UnityEngine;
#nullable enable
namespace Assets.Scripts.Notes
{
    public class TapBase : NoteDrop
    {
        public bool isBreak;
        public bool isEX;
        bool isTriggered = false;

        private float getJudgeTimingDisplay;
        private float distanceDisplay;
        public float getSoflanTimingDisplay;
        public float noteSpeedValue = 600f;
        public bool isFixedSoflan;
        public float fixedSoflanSpeed = 600f;

        public Sprite tapSpr;
        public Sprite eachSpr;
        public Sprite breakSpr;
        public Sprite exSpr;

        public Sprite eachLine;
        public Sprite breakLine;

        public RuntimeAnimatorController BreakShine;

        public GameObject tapLine;

        public Color exEffectTap;
        public Color exEffectEach;
        public Color exEffectBreak;

        public Material breakMaterial;

        protected SpriteRenderer exSpriteRender;
        protected SpriteRenderer lineSpriteRender;

        protected SpriteRenderer spriteRenderer;

        protected void PreLoad()
        {
            var notes = GameObject.Find("Notes").transform;
            noteManager = notes.GetComponent<NoteManager>();
            tapLine = Instantiate(tapLine, notes);
            tapLine.SetActive(false);
            lineSpriteRender = tapLine.GetComponent<SpriteRenderer>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            exSpriteRender = transform.GetChild(0).GetComponent<SpriteRenderer>();
            timeProvider = GameObject.Find("AudioTimeProvider").GetComponent<AudioTimeProvider>();
            objectCounter = GameObject.Find("ObjectCounter").GetComponent<ObjectCounter>();

            spriteRenderer.sortingOrder += noteSortOrder;
            exSpriteRender.sortingOrder += noteSortOrder;


        }
        protected bool IsFixedSoflanEnabled()
        {
            return isFixedSoflan && fixedSoflanSpeed > 0f;
        }
        protected float GetSoflanNoteSpeedValue()
        {
            return IsFixedSoflanEnabled() ? fixedSoflanSpeed : noteSpeedValue;
        }
        protected float GetMaiBugAdjustMSec()
        {
            return GetMaiBugAdjustMSec(noteSpeedValue);
        }
        protected float GetMaiBugAdjustMSec(float speedValue)
        {
            var speedRatio = speedValue / 150f;
            return (speedRatio - 1f) * (-0.5f / speedRatio) * 1.6f * 1000f / 60f;
        }
        protected float GetDefaultMsec()
        {
            return GetDefaultMsec(noteSpeedValue);
        }
        protected float GetDefaultMsec(float speedValue)
        {
            return 240000f / speedValue;
        }
        protected float GetMoveStartTime()
        {
            return GetMoveStartTime(noteSpeedValue);
        }
        protected float GetMoveStartTime(float speedValue)
        {
            return GetDefaultMsec(speedValue) - GetMaiBugAdjustMSec(speedValue);
        }
        protected float GetScaleStartTime()
        {
            return GetScaleStartTime(noteSpeedValue);
        }
        protected float GetScaleStartTime(float speedValue)
        {
            return 2f * GetDefaultMsec(speedValue) - GetMaiBugAdjustMSec(speedValue);
        }
        protected float GetTapDistance(float timing)
        {
            return GetTapDistance(timing, noteSpeedValue);
        }
        protected float GetTapDistance(float timing, float speedValue)
        {
            var moveStartTime = GetMoveStartTime(speedValue);
            var progress = (moveStartTime + timing * 1000f) / (2f * moveStartTime);
            var outsideDistance = 4.8f + (4.8f - 1.225f);
            return Mathf.Lerp(1.225f, outsideDistance, progress);
        }
        protected float GetTapScale(float timing)
        {
            return GetTapScale(timing, noteSpeedValue);
        }
        protected float GetTapScale(float timing, float speedValue)
        {
            return (GetScaleStartTime(speedValue) - MathF.Abs(timing * 1000f)) / GetDefaultMsec(speedValue);
        }
        protected float GetSoflanTapDistance(float timing)
        {
            return GetTapDistance(timing, GetSoflanNoteSpeedValue());
        }
        protected float GetSoflanTapScale(float timing)
        {
            return GetTapScale(timing, GetSoflanNoteSpeedValue());
        }
        protected void FixedUpdate()
        {
            var timing = GetJudgeTiming();
            if (!isJudged && timing > 0.15f)
            {
                judgeResult = JudgeType.Miss;
                isJudged = true;
                Destroy(tapLine);
                Destroy(gameObject);
            }
            else if (isJudged)
            {
                Destroy(tapLine);
                Destroy(gameObject);
            }
            else if (timing >= -0.01f)
            {
                switch (InputManager.Mode)
                {
                    case AutoPlayMode.Enable:
                        judgeResult = JudgeType.Perfect;
                        isJudged = true;
                        break;
                    case AutoPlayMode.Random:
                        judgeResult = (JudgeType)UnityEngine.Random.Range(1, 14);
                        isJudged = true;
                        break;
                    case AutoPlayMode.DJAuto:
                        if (isTriggered)
                            return;
                        inputManager.ClickSensor(sensorPos);
                        isTriggered = true;
                        break;
                }
            }

        }
        // Update is called once per frame
        protected virtual void Update()
        {
            if (SoflanManager.Instance.containsSoflans())
            {
                Update_soflan();
                return;
            }

            var timing = getJudgeTimingDisplay = GetJudgeTiming();
            var distance = distanceDisplay = GetTapDistance(timing);
            var destScale = GetTapScale(timing);

            switch (State)
            {
                case NoteStatus.Initialized:
                    if (destScale >= 0f)
                    {
                        tapLine.transform.rotation = Quaternion.Euler(0, 0, -22.5f + -45f * (startPosition - 1));
                        State = NoteStatus.Pending;
                        goto case NoteStatus.Pending;
                    }
                    else
                        transform.localScale = new Vector3(0, 0);
                    return;
                case NoteStatus.Pending:
                    {
                        if (destScale > 0.3f)
                            tapLine.SetActive(true);
                        if (destScale < 1f)
                        {
                            transform.localScale = new Vector3(destScale, destScale);
                            transform.position = getPositionFromDistance(1.225f);
                            var lineScale = Mathf.Abs(1.225f / 4.8f);
                            tapLine.transform.localScale = new Vector3(lineScale, lineScale, 1f);
                        }
                        else
                        {
                            State = NoteStatus.Running;
                            goto case NoteStatus.Running;
                        }
                    }
                    break;
                case NoteStatus.Running:
                    {
                        transform.position = getPositionFromDistance(distance);
                        transform.localScale = new Vector3(1f, 1f);
                        var lineScale = Mathf.Abs(distance / 4.8f);
                        tapLine.transform.localScale = new Vector3(lineScale, lineScale, 1f);
                    }
                    break;
            }

            spriteRenderer.forceRenderingOff = false;
            if (isEX && !isMine) exSpriteRender.forceRenderingOff = false;
            if (isBreak && !isMine)
            {
                var extra = Math.Max(Mathf.Sin(timeProvider.GetFrame() * 0.17f) * 0.5f, 0);
                spriteRenderer.material.SetFloat("_Brightness", 0.95f + extra);
            }
        }

        private void Update_soflan()
        {
            getJudgeTimingDisplay = GetJudgeTiming();

            var timing = getSoflanTimingDisplay = GetSoflanTiming();
            var distance = distanceDisplay = GetSoflanTapDistance(timing);
            var destScale = GetSoflanTapScale(timing);

            if (destScale >= 0f)
                tapLine.transform.rotation = Quaternion.Euler(0, 0, -22.5f + -45f * (startPosition - 1));
            else
                transform.localScale = new Vector3(0, 0);

            if (destScale > 0.3f)
                tapLine.SetActive(true);
            else
                tapLine.SetActive(false);

            if (destScale < 1f)
            {
                var limitedDestScale = Mathf.Max(0, destScale);
                transform.localScale = new Vector3(limitedDestScale, limitedDestScale);
                transform.position = getPositionFromDistance(1.225f);
                var lineScale = Mathf.Abs(1.225f / 4.8f);
                tapLine.transform.localScale = new Vector3(lineScale, lineScale, 1f);
            }
            else
            {
                transform.position = getPositionFromDistance(distance);
                transform.localScale = new Vector3(1f, 1f);
                var lineScale = Mathf.Abs(distance / 4.8f);
                tapLine.transform.localScale = new Vector3(lineScale, lineScale, 1f);
            }

            spriteRenderer.forceRenderingOff = false;
            if (isEX && !isMine) exSpriteRender.forceRenderingOff = false;
            if (isBreak && !isMine)
            {
                var extra = Math.Max(Mathf.Sin(timeProvider.GetFrame() * 0.17f) * 0.5f, 0);
                spriteRenderer.material.SetFloat("_Brightness", 0.95f + extra);
            }
        }

        protected void Check(object sender, InputEventArgs arg)
        {
            if (arg.Type != sensor.Type)
                return;
            else if (isJudged || !noteManager.CanJudge(gameObject, startPosition))
                return;
            else if (InputManager.Mode is AutoPlayMode.Enable or AutoPlayMode.Random)
                return;

            if (arg.IsClick)
            {
                if (!inputManager.IsIdle(arg))
                    return;
                else
                    inputManager.SetBusy(arg);

                Judge();
                if (isJudged)
                {
                    Destroy(tapLine);
                    Destroy(gameObject);
                }
            }
        }
        protected void Judge()
        {

            const int JUDGE_GOOD_AREA = 150;
            const int JUDGE_GREAT_AREA = 100;
            const int JUDGE_PERFECT_AREA = 50;

            const float JUDGE_SEG_PERFECT1 = 16.66667f;
            const float JUDGE_SEG_PERFECT2 = 33.33334f;
            const float JUDGE_SEG_GREAT1 = 66.66667f;
            const float JUDGE_SEG_GREAT2 = 83.33334f;

            if (isJudged)
                return;

            var timing = timeProvider.AudioTime - time;
            var isFast = timing < 0;
            var diff = MathF.Abs(timing * 1000);
            JudgeType result;
            if (diff > JUDGE_GOOD_AREA && isFast)
                return;
            else if (diff < JUDGE_SEG_PERFECT1)
                result = JudgeType.Perfect;
            else if (diff < JUDGE_SEG_PERFECT2)
                result = JudgeType.LatePerfect1;
            else if (diff < JUDGE_PERFECT_AREA)
                result = JudgeType.LatePerfect2;
            else if (diff < JUDGE_SEG_GREAT1)
                result = JudgeType.LateGreat;
            else if (diff < JUDGE_SEG_GREAT2)
                result = JudgeType.LateGreat1;
            else if (diff < JUDGE_GREAT_AREA)
                result = JudgeType.LateGreat;
            else if (diff < JUDGE_GOOD_AREA)
                result = JudgeType.LateGood;
            else
                result = JudgeType.Miss;

            if (result != JudgeType.Miss && isFast)
                result = 14 - result;
            if (result != JudgeType.Miss && isEX)
                result = JudgeType.Perfect;

            judgeResult = result;
            isJudged = true;
        }
        protected virtual void OnDestroy()
        {
            if (HttpHandler.IsReloding)
                return;
            var effectManager = GameObject.Find("NoteEffects").GetComponent<NoteEffectManager>();
            effectManager.PlayEffect(startPosition, isBreak && !isMine, judgeResult);
            effectManager.PlayFastLate(startPosition, judgeResult);
            objectCounter.NextNote(startPosition);
            objectCounter.ReportResult(this, judgeResult, isBreak && !isMine);
            inputManager.UnbindArea(Check, sensorPos);
        }
    }
}
