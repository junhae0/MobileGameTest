using UnityEngine;

public enum ObstacleType
{
    Car,          // 차선 변경으로 피하기
    Construction, // 차선 변경으로 피하기
    Wall,         // 2차선을 막는 대형 벽 → 차선 변경
    SpeedBump,    // 3차선 전체 과속방지턱 → 점프
    Puddle,       // 물웅덩이 → 점프
    Barricade,    // 낮은 바리케이드 → 점프
    Coin          // 획득 아이템
}

/// <summary>
/// 도로 위 장애물/아이템 하나. 이동과 충돌 판정은 ObstacleSpawner 가 담당합니다.
/// </summary>
public class Obstacle : MonoBehaviour
{
    public ObstacleType type;
    public Vector2 halfSize;     // 충돌 판정용 반 크기
    public float ownSpeed;       // 자체 전진 속도 (자동차)
    public bool consumed;        // 이미 처리됨(충돌/회피/획득)

    public bool Dying => dieTimer >= 0f;
    public bool IsCoin => type == ObstacleType.Coin;
    public bool IsJumpable =>
        type == ObstacleType.SpeedBump || type == ObstacleType.Puddle || type == ObstacleType.Barricade;

    const float DieDuration = 0.25f;
    float dieTimer = -1f;
    SpriteRenderer[] renderers;
    Color[] baseColors;
    Vector3 baseScale;

    /// <summary>부스터로 부수거나 코인을 먹었을 때: 커지면서 사라지는 연출</summary>
    public void Kill()
    {
        consumed = true;
        dieTimer = 0f;
        baseScale = transform.localScale;
        renderers = GetComponentsInChildren<SpriteRenderer>();
        baseColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++) baseColors[i] = renderers[i].color;
    }

    /// <summary>true 를 반환하면 삭제해도 됨</summary>
    public bool UpdateDeath(float dt)
    {
        dieTimer += dt;
        float t = Mathf.Clamp01(dieTimer / DieDuration);
        transform.localScale = baseScale * (1f + 0.6f * t);
        for (int i = 0; i < renderers.Length; i++)
        {
            Color c = baseColors[i];
            c.a *= 1f - t;
            renderers[i].color = c;
        }
        return t >= 1f;
    }

    public void Animate(float time)
    {
        if (IsCoin && !Dying)
            transform.localScale = new Vector3(0.25f + 0.75f * Mathf.Abs(Mathf.Cos(time * 4f)), 1f, 1f);
    }

    // ───────── 생성 ─────────
    static readonly Color[] CarColors =
    {
        new Color(1f, 0.8f, 0.1f), new Color(0.25f, 0.55f, 1f), new Color(0.92f, 0.92f, 0.95f),
        new Color(0.2f, 0.75f, 0.4f), new Color(0.6f, 0.35f, 0.9f)
    };

    public static Obstacle Create(ObstacleType type, Vector2 pos, Transform parent)
    {
        var go = new GameObject(type.ToString());
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        var o = go.AddComponent<Obstacle>();
        o.type = type;
        Transform t = go.transform;
        Color dark = new Color(0.1f, 0.1f, 0.12f);

        switch (type)
        {
            case ObstacleType.Car:
            {
                Color c = CarColors[Random.Range(0, CarColors.Length)];
                for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                    SpriteFactory.Box("Wheel", t, dark, new Vector2(sx * 0.4f, sy * 0.42f), new Vector2(0.14f, 0.3f), 11);
                SpriteFactory.RoundBox("Body", t, c, Vector2.zero, new Vector2(0.82f, 1.4f), 12);
                SpriteFactory.RoundBox("Roof", t, c * 0.85f + new Color(0, 0, 0, 0.15f), new Vector2(0f, -0.05f), new Vector2(0.62f, 0.6f), 13);
                SpriteFactory.Box("Windshield", t, new Color(0.2f, 0.25f, 0.35f), new Vector2(0f, 0.36f), new Vector2(0.62f, 0.22f), 14);
                SpriteFactory.Box("RearWindow", t, new Color(0.2f, 0.25f, 0.35f), new Vector2(0f, -0.45f), new Vector2(0.6f, 0.16f), 14);
                SpriteFactory.Box("TailL", t, new Color(1f, 0.2f, 0.2f), new Vector2(-0.28f, -0.66f), new Vector2(0.16f, 0.07f), 14);
                SpriteFactory.Box("TailR", t, new Color(1f, 0.2f, 0.2f), new Vector2(0.28f, -0.66f), new Vector2(0.16f, 0.07f), 14);
                o.halfSize = new Vector2(0.4f, 0.7f);
                o.ownSpeed = 2f;
                break;
            }
            case ObstacleType.Construction:
            {
                SpriteFactory.Box("LegL", t, dark, new Vector2(-0.45f, -0.5f), new Vector2(0.1f, 0.2f), 11);
                SpriteFactory.Box("LegR", t, dark, new Vector2(0.45f, -0.5f), new Vector2(0.1f, 0.2f), 11);
                SpriteFactory.RoundBox("Board", t, new Color(1f, 0.5f, 0.1f), Vector2.zero, new Vector2(1.2f, 0.9f), 12);
                SpriteFactory.Box("Stripe1", t, Color.white, new Vector2(0f, 0.2f), new Vector2(1.05f, 0.13f), 13);
                SpriteFactory.Box("Stripe2", t, Color.white, new Vector2(0f, -0.15f), new Vector2(1.05f, 0.13f), 13);
                SpriteFactory.Dot("Lamp", t, new Color(1f, 0.95f, 0.3f), new Vector2(0f, 0.38f), new Vector2(0.18f, 0.18f), 14);
                o.halfSize = new Vector2(0.55f, 0.45f);
                break;
            }
            case ObstacleType.Wall:
            {
                SpriteFactory.RoundBox("Wall", t, new Color(0.55f, 0.55f, 0.6f), Vector2.zero, new Vector2(2.9f, 0.75f), 12);
                SpriteFactory.Box("Top", t, new Color(0.7f, 0.7f, 0.76f), new Vector2(0f, 0.27f), new Vector2(2.8f, 0.12f), 13);
                SpriteFactory.Box("Mortar", t, new Color(0.4f, 0.4f, 0.45f), new Vector2(0f, -0.05f), new Vector2(2.8f, 0.04f), 13);
                for (int i = -3; i <= 3; i++)
                    SpriteFactory.Box("Brick", t, new Color(0.4f, 0.4f, 0.45f), new Vector2(i * 0.4f + (i % 2 == 0 ? 0.2f : 0f), -0.2f), new Vector2(0.04f, 0.26f), 13);
                SpriteFactory.Box("Hazard", t, new Color(1f, 0.8f, 0.1f), new Vector2(0f, -0.33f), new Vector2(2.8f, 0.07f), 13);
                o.halfSize = new Vector2(1.45f, 0.37f);
                break;
            }
            case ObstacleType.SpeedBump:
            {
                SpriteFactory.Box("Bump", t, new Color(1f, 0.82f, 0.1f), Vector2.zero, new Vector2(4.4f, 0.35f), 10);
                for (int i = 0; i < 9; i++)
                    SpriteFactory.Box("Stripe", t, dark, new Vector2(-2f + i * 0.5f, 0f), new Vector2(0.22f, 0.35f), 11);
                o.halfSize = new Vector2(2.2f, 0.18f);
                break;
            }
            case ObstacleType.Puddle:
            {
                SpriteFactory.Dot("Water", t, new Color(0.25f, 0.55f, 0.95f, 0.85f), Vector2.zero, new Vector2(1.15f, 0.75f), 10);
                SpriteFactory.Dot("Shine", t, new Color(1f, 1f, 1f, 0.45f), new Vector2(-0.22f, 0.12f), new Vector2(0.4f, 0.18f), 11);
                o.halfSize = new Vector2(0.5f, 0.33f);
                break;
            }
            case ObstacleType.Barricade:
            {
                SpriteFactory.Box("Base", t, Color.white, Vector2.zero, new Vector2(1.2f, 0.32f), 10);
                for (int i = 0; i < 3; i++)
                    SpriteFactory.Box("Red", t, new Color(0.9f, 0.15f, 0.15f), new Vector2(-0.4f + i * 0.4f, 0f), new Vector2(0.2f, 0.32f), 11);
                o.halfSize = new Vector2(0.6f, 0.16f);
                break;
            }
            case ObstacleType.Coin:
            {
                SpriteFactory.Dot("Coin", t, new Color(1f, 0.78f, 0.1f), Vector2.zero, new Vector2(0.5f, 0.5f), 15);
                SpriteFactory.Dot("Inner", t, new Color(1f, 0.92f, 0.45f), Vector2.zero, new Vector2(0.3f, 0.3f), 16);
                o.halfSize = new Vector2(0.3f, 0.3f);
                break;
            }
        }
        return o;
    }
}
