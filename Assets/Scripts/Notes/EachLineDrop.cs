using UnityEngine;
using Assets.Scripts.Notes;
#nullable enable
public class EachLineDrop : MonoBehaviour
{
    public float time;
    public int startPosition = 1;
    public int curvLength = 1;
    public float noteSpeedValue = 600f;
    public int soflanGroup;
    public float soflanTime;

    public GameObject obj1;
    public GameObject obj2;

    public Sprite[] curvSprites;
    private SpriteRenderer sr;

    private AudioTimeProvider timeProvider;

    // Start is called before the first frame update
    private void Start()
    {
        timeProvider = GameObject.Find("AudioTimeProvider").GetComponent<AudioTimeProvider>();

        sr = gameObject.GetComponent<SpriteRenderer>();
        sr.sprite = curvSprites[curvLength - 1];
        sr.forceRenderingOff = true;
    }

    // Update is called once per frame
    private void Update()
    {
        var judgeTiming = timeProvider.AudioTime - time;
        var timing = judgeTiming;
        if (SoflanManager.Instance.containsSoflans())
        {
            var soflanValue = SoflanManager.Instance.ConvertAudioTimeToY_PreviewMode(
                timeProvider.AudioTime * 1000f,
                soflanGroup);
            timing = (soflanValue - soflanTime) / 1000f;
        }

        var distance = TapBase.GetTapDistance(timing, noteSpeedValue);
        var destScale = TapBase.GetTapScale(timing, noteSpeedValue);
        if (judgeTiming > 0) Destroy(gameObject);

        sr.forceRenderingOff = destScale <= 0.3f;
        if (destScale < 1f)
        {
            distance = 1.225f;
        }

        var lineScale = Mathf.Abs(distance / 4.8f);
        transform.localScale = new Vector3(lineScale, lineScale, 1f);
        transform.rotation = Quaternion.Euler(0, 0, -45f * (startPosition - 1));
    }
}
