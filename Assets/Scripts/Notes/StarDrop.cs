using Assets.Scripts.Notes;
using Assets.Scripts.Types;
using UnityEngine;
#nullable enable
public class StarDrop : TapBase
{
    public float rotateSpeed = 1f;

    public bool isDouble;
    public bool isNoHead;
    public bool isFakeStar = false;
    public bool isFakeStarRotate = false;

    public Sprite tapSpr_Double;
    public Sprite eachSpr_Double;
    public Sprite breakSpr_Double;
    public Sprite exSpr_Double;

    public GameObject slide;
    private bool TryActivateSlide(bool shouldActivate)
    {
        if (!shouldActivate || isFakeStar || slide.activeSelf)
            return false;

        slide.SetActive(true);
        if (isNoHead)
        {
            Destroy(tapLine);
            Destroy(gameObject);
            return true;
        }

        return false;
    }
    private void Start()
    {
        PreLoad();

        if (isDouble)
        {
            exSpriteRender.sprite = exSpr_Double;
            spriteRenderer.sprite = tapSpr_Double;
            if (isEX && !isMine) exSpriteRender.color = exEffectTap;
            if (ForceYellowAppearance.UsesEachVisual(isEach, isForceYellow) && !isMine)
            {
                lineSpriteRender.sprite = eachLine;
                spriteRenderer.sprite = eachSpr_Double;
                if (isEX) exSpriteRender.color = exEffectEach;
            }

            if (isBreak && !isMine)
            {
                lineSpriteRender.sprite = breakLine;
                spriteRenderer.sprite = breakSpr_Double;
                if (isEX) exSpriteRender.color = exEffectBreak;
                spriteRenderer.material = breakMaterial;
            }
        }
        else
        {
            exSpriteRender.sprite = exSpr;
            spriteRenderer.sprite = tapSpr;
            if (isEX && !isMine) exSpriteRender.color = exEffectTap;
            if (ForceYellowAppearance.UsesEachVisual(isEach, isForceYellow) && !isMine)
            {
                lineSpriteRender.sprite = eachLine;
                spriteRenderer.sprite = eachSpr;
                if (isEX) exSpriteRender.color = exEffectEach;
            }

            if (isBreak && !isMine)
            {
                lineSpriteRender.sprite = breakLine;
                spriteRenderer.sprite = breakSpr;
                if (isEX) exSpriteRender.color = exEffectBreak;
                spriteRenderer.material = breakMaterial;
            }
        }

        if (isMine)
        {
            ApplyMineVisual(spriteRenderer);
            ApplyMineVisual(lineSpriteRender);
            ApplyMineVisual(exSpriteRender);
        }

        spriteRenderer.forceRenderingOff = true;
        exSpriteRender.forceRenderingOff = true;

        if(!isNoHead)
        {
            sensor = GameObject.Find("Sensors")
                                   .transform.GetChild(startPosition - 1)
                                   .GetComponent<Sensor>();
            manager = GameObject.Find("Sensors")
                                    .GetComponent<SensorManager>();
            inputManager = GameObject.Find("Input")
                                 .GetComponent<InputManager>();
            sensorPos = (SensorType)(startPosition - 1);
            inputManager.BindArea(Check, sensorPos);
        }
        State = NoteStatus.Initialized;
    }
    // Update is called once per frame
    protected override void Update()
    {
        if (SoflanManager.Instance.containsSoflans())
        {
            Update_soflan();
            return;
        }

        var songSpeed = timeProvider.CurrentSpeed;
        var judgeTiming = GetJudgeTiming();
        var distance = GetTapDistance(judgeTiming);
        var destScale = GetTapScale(judgeTiming);

        switch (State)
        {
            case NoteStatus.Initialized:
                if (destScale >= 0f)
                {

                    if(!isNoHead)
                        tapLine.transform.rotation = Quaternion.Euler(0, 0, -22.5f + -45f * (startPosition - 1));
                    State = NoteStatus.Pending;
                    goto case NoteStatus.Pending;
                }
                else
                    transform.localScale = new Vector3(0, 0);
                return;
            case NoteStatus.Pending:
                {
                    if (destScale > 0.3f && !isNoHead)
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
                        if (TryActivateSlide(true))
                            return;

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

        if (isNoHead)
        {
            spriteRenderer.forceRenderingOff = true;
            if (isEX && !isMine) exSpriteRender.forceRenderingOff = true;
        }
        else
        {
            spriteRenderer.forceRenderingOff = false;
            if (isEX && !isMine) exSpriteRender.forceRenderingOff = false;
        }

        if (timeProvider.isStart && !isFakeStar)
            transform.Rotate(0f, 0f, -180f * Time.deltaTime * songSpeed / rotateSpeed);
        else if (isFakeStarRotate)
            transform.Rotate(0f, 0f, 400f * Time.deltaTime);  
    }
    // soflan(变速)谱面下的定位:镜像 TapBase.Update_soflan,每帧按 GetSoflanTiming() 重算,
    // 不走 State 状态机(变速可能反向)。保留 Star 特有逻辑:isNoHead 守卫、slide 激活与自销毁、旋转。
    private void Update_soflan()
    {
        var songSpeed = timeProvider.CurrentSpeed;
        var timing = GetSoflanTiming();
        var distance = GetSoflanTapDistance(timing);
        var destScale = GetSoflanTapScale(timing);
        var shouldActivateSlide = GetTapScale(GetJudgeTiming()) >= 1f;

        if (destScale >= 0f)
        {
            if (!isNoHead)
                tapLine.transform.rotation = Quaternion.Euler(0, 0, -22.5f + -45f * (startPosition - 1));
        }
        else
            transform.localScale = new Vector3(0, 0);

        if (destScale > 0.3f)
        {
            if (!isNoHead)
                tapLine.SetActive(true);
        }
        else
            tapLine.SetActive(false);

        if (TryActivateSlide(shouldActivateSlide))
            return;

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

        if (isNoHead)
        {
            spriteRenderer.forceRenderingOff = true;
            if (isEX && !isMine) exSpriteRender.forceRenderingOff = true;
        }
        else
        {
            spriteRenderer.forceRenderingOff = false;
            if (isEX && !isMine) exSpriteRender.forceRenderingOff = false;
        }

        if (timeProvider.isStart && !isFakeStar)
            transform.Rotate(0f, 0f, -180f * Time.deltaTime * songSpeed / rotateSpeed);
        else if (isFakeStarRotate)
            transform.Rotate(0f, 0f, 400f * Time.deltaTime);
    }

    protected override void OnDestroy()
    {
        if(!isNoHead || isFakeStar)
            base.OnDestroy();
    }
}
