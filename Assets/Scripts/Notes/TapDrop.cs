using Assets.Scripts.Notes;
using Assets.Scripts.Types;
using UnityEngine;
#nullable enable
public class TapDrop : TapBase
{
    private void Start()
    {
        PreLoad();

        spriteRenderer.sprite = tapSpr;
        exSpriteRender.sprite = exSpr;

        if (isEX && !isMine) exSpriteRender.color = exEffectTap;
        if (ForceYellowAppearance.UsesEachVisual(isEach, isForceYellow) && !isMine)
        {
            spriteRenderer.sprite = eachSpr;
            lineSpriteRender.sprite = eachLine;
            if (isEX) exSpriteRender.color = exEffectEach;
        }

        if (isBreak && !isMine)
        {
            spriteRenderer.sprite = breakSpr;
            lineSpriteRender.sprite = breakLine;
            if (isEX) exSpriteRender.color = exEffectBreak;
            spriteRenderer.material = breakMaterial;
        }

        if (isMine)
        {
            ApplyMineVisual(spriteRenderer);
            ApplyMineVisual(lineSpriteRender);
            ApplyMineVisual(exSpriteRender);
        }

        spriteRenderer.forceRenderingOff = true;
        exSpriteRender.forceRenderingOff = true;
        sensor = GameObject.Find("Sensors")
                                   .transform.GetChild(startPosition - 1)
                                   .GetComponent<Sensor>();
        manager = GameObject.Find("Sensors")
                                .GetComponent<SensorManager>();
        inputManager = GameObject.Find("Input")
                                 .GetComponent<InputManager>();
        sensorPos = (SensorType)(startPosition - 1);
        inputManager.BindArea(Check, sensorPos);
        State = NoteStatus.Initialized;
    }
}
