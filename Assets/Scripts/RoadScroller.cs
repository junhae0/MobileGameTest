using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 3차선 도로 배경. 차선 점선과 길가 장식을 아래로 흘려서 달리는 느낌을 만듭니다.
/// </summary>
public class RoadScroller : MonoBehaviour
{
    const float LoopLength = 26f; // 이 길이마다 반복
    SpriteRenderer side, road;
    readonly List<Transform> dashes = new List<Transform>();
    readonly List<SpriteRenderer> decos = new List<SpriteRenderer>();

    void Awake()
    {
        side = SpriteFactory.Box("Side", transform, Color.gray, Vector2.zero, new Vector2(30f, 40f), 0);
        road = SpriteFactory.Box("Road", transform, Color.black, Vector2.zero, new Vector2(4.5f, 40f), 1);
        SpriteFactory.Box("CurbL", transform, Color.white, new Vector2(-2.25f, 0f), new Vector2(0.08f, 40f), 2);
        SpriteFactory.Box("CurbR", transform, Color.white, new Vector2(2.25f, 0f), new Vector2(0.08f, 40f), 2);

        float[] lineX = { -0.75f, 0.75f };
        foreach (float x in lineX)
            for (float y = -LoopLength / 2f; y < LoopLength / 2f; y += 2f)
                dashes.Add(SpriteFactory.Box("Dash", transform, new Color(1f, 1f, 1f, 0.55f), new Vector2(x, y), new Vector2(0.08f, 0.9f), 2).transform);

        for (float y = -LoopLength / 2f; y < LoopLength / 2f; y += 1.3f)
        {
            for (int s = -1; s <= 1; s += 2)
            {
                float size = Random.Range(0.35f, 0.7f);
                float x = s * (2.25f + 0.25f + size / 2f + Random.Range(0f, 0.3f));
                decos.Add(SpriteFactory.Box("Deco", transform, Color.white, new Vector2(x, y), new Vector2(size, size), 1));
            }
        }
    }

    public void Setup(StageData stage)
    {
        side.color = stage.sideColor;
        road.color = stage.roadColor;
        foreach (var d in decos)
        {
            Color c = stage.decoColor * Random.Range(0.8f, 1.1f);
            c.a = 1f;
            d.color = c;
        }
    }

    public void Scroll(float move)
    {
        foreach (var d in dashes) Wrap(d, move);
        foreach (var d in decos) Wrap(d.transform, move);
    }

    static void Wrap(Transform t, float move)
    {
        Vector3 p = t.localPosition;
        p.y -= move;
        if (p.y < -LoopLength / 2f) p.y += LoopLength;
        t.localPosition = p;
    }
}
