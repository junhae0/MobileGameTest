using UnityEngine;

/// <summary>
/// 외부 이미지 없이 코드로 기본 스프라이트(사각형, 원, 둥근 사각형)를 만들어 주는 도우미.
/// 나중에 직접 그린 이미지로 교체하고 싶으면 SpriteRenderer.sprite 만 바꾸면 됩니다.
/// </summary>
public static class SpriteFactory
{
    static Sprite square, circle, roundRect;

    public static Sprite Square => square != null ? square : (square = Build(64, 0, false));
    public static Sprite Circle => circle != null ? circle : (circle = Build(64, 0, true));
    public static Sprite RoundRect => roundRect != null ? roundRect : (roundRect = Build(64, 16, false));

    static Sprite Build(int size, int radius, bool isCircle)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        var px = new Color32[size * size];
        float half = size / 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float cx = x + 0.5f, cy = y + 0.5f;
                float a = 1f;
                if (isCircle)
                {
                    float d = Vector2.Distance(new Vector2(cx, cy), new Vector2(half, half));
                    a = Mathf.Clamp01(half - d + 0.5f);
                }
                else if (radius > 0)
                {
                    float qx = Mathf.Max(radius - cx, cx - (size - radius), 0f);
                    float qy = Mathf.Max(radius - cy, cy - (size - radius), 0f);
                    float d = Mathf.Sqrt(qx * qx + qy * qy);
                    a = Mathf.Clamp01(radius - d + 0.5f);
                }
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        }
        tex.SetPixels32(px);
        tex.Apply();

        Vector4 border = radius > 0 ? new Vector4(radius, radius, radius, radius) : Vector4.zero;
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size, 0,
            SpriteMeshType.FullRect, border);
    }

    /// <summary>각진 사각형</summary>
    public static SpriteRenderer Box(string name, Transform parent, Color color, Vector2 pos, Vector2 size, int order)
    {
        var sr = Make(name, parent, Square, color, pos, order);
        sr.transform.localScale = new Vector3(size.x, size.y, 1f);
        return sr;
    }

    /// <summary>모서리가 둥근 사각형 (가로/세로 0.5 이상일 때 사용)</summary>
    public static SpriteRenderer RoundBox(string name, Transform parent, Color color, Vector2 pos, Vector2 size, int order)
    {
        var sr = Make(name, parent, RoundRect, color, pos, order);
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.size = size;
        return sr;
    }

    /// <summary>원/타원</summary>
    public static SpriteRenderer Dot(string name, Transform parent, Color color, Vector2 pos, Vector2 size, int order)
    {
        var sr = Make(name, parent, Circle, color, pos, order);
        sr.transform.localScale = new Vector3(size.x, size.y, 1f);
        return sr;
    }

    static SpriteRenderer Make(string name, Transform parent, Sprite sprite, Color color, Vector2 pos, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order;
        return sr;
    }
}
