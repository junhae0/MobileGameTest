using UnityEngine;

/// <summary>
/// 플레이어 오토바이. 3차선 이동(스와이프), 점프(탭), 차징 부스터(홀드)를 처리합니다.
/// 시간 흐름은 GameManager 가 Tick() 을 호출할 때만 진행됩니다(일시정지/메뉴에서 멈춤).
/// </summary>
public class PlayerBike : MonoBehaviour
{
    public static readonly float[] LaneX = { -1.5f, 0f, 1.5f };

    [Header("조작 감도")]
    public float laneChangeSpeed = 14f;
    public float jumpDuration = 0.6f;
    public float chargePerSecond = 0.55f;   // 1.0 이 가득 찬 게이지
    public float minChargeToBoost = 0.25f;

    public int Lane { get; private set; } = 1;
    public bool IsJumping => jumpTimer > 0f;
    public bool IsBoosting => boostTimer > 0f;
    public bool IsCharging { get; private set; }
    public bool IsInvulnerable => invulnTimer > 0f;
    public float Charge { get; private set; }
    public float BoostRatio => boostDuration > 0f ? Mathf.Clamp01(boostTimer / boostDuration) : 0f;

    public float SpeedMultiplier
    {
        get
        {
            float m = 1f;
            if (IsBoosting) m = 1.8f;
            else if (IsCharging) m = 0.85f;   // 차징 중에는 살짝 느려짐 → 타이밍 고민 요소
            if (slowTimer > 0f) m *= 0.55f;   // 충돌 직후 감속
            return m;
        }
    }

    float jumpTimer, boostTimer, boostDuration, invulnTimer, slowTimer;
    Transform body;
    SpriteRenderer shadow, aura, flame;
    static readonly Vector3 ShadowScale = new Vector3(0.8f, 1.3f, 1f);

    void Awake()
    {
        BuildVisual();
    }

    void BuildVisual()
    {
        shadow = SpriteFactory.Dot("Shadow", transform, new Color(0f, 0f, 0f, 0.35f), Vector2.zero, ShadowScale, 19);
        aura = SpriteFactory.Dot("Aura", transform, new Color(1f, 0.85f, 0.2f, 0f), Vector2.zero, new Vector2(1.6f, 1.6f), 20);

        body = new GameObject("Body").transform;
        body.SetParent(transform, false);

        Color dark = new Color(0.12f, 0.12f, 0.14f);
        flame = SpriteFactory.Dot("Flame", body, new Color(1f, 0.55f, 0.1f, 0.9f), new Vector2(0f, -0.95f), new Vector2(0.45f, 0.9f), 21);
        SpriteFactory.Box("RearWheel", body, dark, new Vector2(0f, -0.5f), new Vector2(0.22f, 0.42f), 22);
        SpriteFactory.Box("FrontWheel", body, dark, new Vector2(0f, 0.58f), new Vector2(0.2f, 0.38f), 22);
        SpriteFactory.RoundBox("Frame", body, new Color(0.9f, 0.15f, 0.2f), new Vector2(0f, 0.05f), new Vector2(0.5f, 1.05f), 23);
        SpriteFactory.Box("Handle", body, new Color(0.25f, 0.25f, 0.3f), new Vector2(0f, 0.38f), new Vector2(0.8f, 0.09f), 24);
        SpriteFactory.Box("DeliveryBox", body, new Color(1f, 0.6f, 0.15f), new Vector2(0f, -0.38f), new Vector2(0.52f, 0.36f), 25);
        SpriteFactory.Box("BoxLine", body, new Color(1f, 0.85f, 0.5f), new Vector2(0f, -0.38f), new Vector2(0.52f, 0.06f), 26);
        SpriteFactory.Dot("Rider", body, new Color(0.15f, 0.25f, 0.55f), new Vector2(0f, 0f), new Vector2(0.48f, 0.42f), 26);
        SpriteFactory.Dot("Helmet", body, Color.white, new Vector2(0f, 0.12f), new Vector2(0.34f, 0.34f), 27);
        SpriteFactory.Box("HelmetStripe", body, new Color(0.9f, 0.15f, 0.2f), new Vector2(0f, 0.12f), new Vector2(0.08f, 0.32f), 28);

        flame.enabled = false;
    }

    public void ResetBike(float y)
    {
        transform.position = new Vector3(LaneX[1], y, 0f);
        Lane = 1;
        jumpTimer = boostTimer = boostDuration = invulnTimer = slowTimer = 0f;
        Charge = 0f;
        IsCharging = false;
        body.gameObject.SetActive(true);
        body.localScale = Vector3.one;
        body.localPosition = Vector3.zero;
        body.localRotation = Quaternion.identity;
        shadow.transform.localScale = ShadowScale;
        flame.enabled = false;
        SetAuraAlpha(0f);
    }

    // ───────── 입력 ─────────
    public void ChangeLane(int dir)
    {
        int prev = Lane;
        Lane = Mathf.Clamp(Lane + dir, 0, LaneX.Length - 1);
        if (Lane != prev) SoundManager.Play(Sfx.Lane);
    }

    public void Jump()
    {
        if (IsJumping) return;
        jumpTimer = jumpDuration;
        SoundManager.Play(Sfx.Jump);
    }

    public void BeginCharge()
    {
        if (IsBoosting) return;
        IsCharging = true;
    }

    public void ReleaseCharge()
    {
        if (!IsCharging) return;
        IsCharging = false;
        if (Charge >= minChargeToBoost)
        {
            boostDuration = (0.8f + Charge * 2.2f) * GameManager.I.BoostDurationMult;
            boostTimer = boostDuration;
            SoundManager.Play(Sfx.Boost);
        }
        Charge = 0f;
    }

    /// <summary>일시정지 등으로 차징을 부스터 없이 취소</summary>
    public void CancelCharge()
    {
        IsCharging = false;
        Charge = 0f;
    }

    public void Hit()
    {
        invulnTimer = 1.2f;
        slowTimer = 0.8f;
        Charge = 0f; // 맞으면 모으던 게이지 초기화
    }

    // ───────── 매 프레임 (Playing 상태일 때만) ─────────
    public void Tick(float dt)
    {
        // 차선 이동 + 기울기 연출
        Vector3 p = transform.position;
        p.x = Mathf.MoveTowards(p.x, LaneX[Lane], laneChangeSpeed * dt);
        transform.position = p;
        float dx = LaneX[Lane] - p.x;
        body.localRotation = Quaternion.Euler(0f, 0f, -dx * 14f);

        // 점프: 위에서 내려다보는 시점이라 크기를 키워서 떠오르는 느낌을 줌
        if (jumpTimer > 0f)
        {
            jumpTimer -= dt;
            float t = 1f - Mathf.Clamp01(jumpTimer / jumpDuration);
            float h = Mathf.Sin(t * Mathf.PI);
            body.localScale = Vector3.one * (1f + 0.4f * h);
            body.localPosition = new Vector3(0f, 0.25f * h, 0f);
            shadow.transform.localScale = ShadowScale * (1f - 0.35f * h);
        }
        else
        {
            body.localScale = Vector3.one;
            body.localPosition = Vector3.zero;
            shadow.transform.localScale = ShadowScale;
        }

        // 차징
        if (IsCharging)
        {
            Charge = Mathf.Min(1f, Charge + dt * chargePerSecond * GameManager.I.ChargeRateMult);
            float pulse = 1f + 0.08f * Mathf.Sin(Time.time * 20f);
            aura.transform.localScale = Vector3.one * (1.2f + Charge * 0.8f) * pulse;
            aura.color = new Color(1f, 0.85f, 0.2f, 0.15f + 0.35f * Charge);
        }

        // 부스터
        if (boostTimer > 0f)
        {
            boostTimer -= dt;
            flame.enabled = true;
            flame.transform.localScale = new Vector3(0.45f, 0.9f + Random.Range(0f, 0.6f), 1f);
            aura.transform.localScale = Vector3.one * 1.8f;
            aura.color = new Color(0.3f, 0.85f, 1f, 0.45f);
            if (boostTimer <= 0f)
            {
                flame.enabled = false;
                SetAuraAlpha(0f);
                invulnTimer = Mathf.Max(invulnTimer, 0.4f); // 부스터 끝난 직후 짧은 보호
            }
        }
        else if (!IsCharging)
        {
            SetAuraAlpha(0f);
        }

        // 무적 깜빡임
        if (invulnTimer > 0f)
        {
            invulnTimer -= dt;
            body.gameObject.SetActive(invulnTimer <= 0f || Mathf.Repeat(invulnTimer, 0.16f) > 0.08f);
        }
        else if (!body.gameObject.activeSelf)
        {
            body.gameObject.SetActive(true);
        }

        if (slowTimer > 0f) slowTimer -= dt;
    }

    void SetAuraAlpha(float a)
    {
        Color c = aura.color;
        c.a = a;
        aura.color = c;
    }
}
