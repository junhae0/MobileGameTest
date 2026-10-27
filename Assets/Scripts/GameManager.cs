using System.Collections;
using UnityEngine;

public enum GameState { Menu, Countdown, Playing, Result }

[System.Serializable]
public class StageData
{
    public string name = "도시";
    public float distance = 450f;   // 목적지까지 거리
    public float timeLimit = 70f;   // 배달 제한 시간(초)
    public float baseSpeed = 8f;    // 기본 주행 속도
    public float spawnGap = 9f;     // 장애물 간격 (작을수록 어려움)
    public Color roadColor = new Color(0.22f, 0.24f, 0.29f);
    public Color sideColor = new Color(0.32f, 0.34f, 0.40f);
    public Color decoColor = new Color(0.55f, 0.6f, 0.7f);
}

/// <summary>
/// 오토바이 고수 - 게임 전체 흐름 (시작 → 플레이 → 결과) 과 저장 데이터를 관리합니다.
/// 빈 씬에 빈 오브젝트 하나 만들고 이 스크립트만 붙이면 나머지는 전부 자동 생성됩니다.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager I { get; private set; }

    [Header("한글이 네모(□)로 보이면 여기에 한글 폰트(.ttf)를 넣으세요")]
    public Font uiFont;

    [Header("스테이지 (비워두면 도시/해안/골목 3개 자동 생성)")]
    public StageData[] stages;

    [Header("직접 만든 사운드로 바꾸려면 여기에 넣으세요 (비우면 자동 생성음)")]
    public SoundClips customSounds = new SoundClips();

    [Header("밸런스")]
    public float freshnessDecayPerSec = 0.5f;
    public float hitFreshnessDamage = 20f;
    public int coinScore = 20;
    public int smashScore = 50;
    public int jumpDodgeScore = 15;

    public GameState State { get; private set; } = GameState.Menu;
    public bool IsPaused { get; private set; }
    public int StageIndex { get; private set; }
    public StageData CurrentStage => stages[StageIndex];

    public float Distance { get; private set; }
    public float TimeLeft { get; private set; }
    public float Freshness { get; private set; }
    public float Score { get; private set; }
    public float CurrentSpeed { get; private set; }
    public float CamSize { get; private set; } = 5f;

    public PlayerBike Player { get; private set; }
    public RoadScroller Road { get; private set; }
    public ObstacleSpawner Spawner { get; private set; }
    public GameUI UI { get; private set; }

    Camera cam;
    float shakeTime, shakeMag;

    // ───────── 저장 데이터 (PlayerPrefs) ─────────
    public const int MaxUpgrade = 5;
    public static readonly string[] UpgradeKeys = { "engine", "booster", "box" };

    public int Gold
    {
        get => PlayerPrefs.GetInt("gold", 0);
        set => PlayerPrefs.SetInt("gold", value);
    }
    public int UnlockedStages
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt("unlocked", 1), 1, stages.Length);
        set => PlayerPrefs.SetInt("unlocked", value);
    }
    public int BestScore(int stage) => PlayerPrefs.GetInt("best_" + stage, 0);
    public int GetUpgrade(string key) => PlayerPrefs.GetInt("up_" + key, 0);
    public int UpgradeCost(string key) => 100 * (GetUpgrade(key) + 1);

    public float EngineMult => 1f + 0.05f * GetUpgrade("engine");          // 속도 +5%/Lv
    public float ChargeRateMult => 1f + 0.15f * GetUpgrade("booster");     // 충전 속도 +15%/Lv
    public float BoostDurationMult => 1f + 0.1f * GetUpgrade("booster");   // 부스터 지속 +10%/Lv
    public float HitDamageMult => 1f - 0.12f * GetUpgrade("box");          // 충돌 시 신선도 손실 -12%/Lv

    public bool TryUpgrade(string key)
    {
        int lv = GetUpgrade(key);
        int cost = UpgradeCost(key);
        if (lv >= MaxUpgrade || Gold < cost) return false;
        Gold -= cost;
        PlayerPrefs.SetInt("up_" + key, lv + 1);
        PlayerPrefs.Save();
        return true;
    }

    // ───────── 초기화 ─────────
    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;

#if !ENABLE_LEGACY_INPUT_MANAGER
        Debug.LogError("[오토바이 고수] Project Settings > Player > Active Input Handling 을 'Both' 로 바꿔주세요.");
#endif
        Application.targetFrameRate = 60;
        Screen.orientation = ScreenOrientation.Portrait;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;

        if (stages == null || stages.Length == 0) stages = DefaultStages();

        SetupCamera();
        gameObject.AddComponent<SoundManager>().Init(customSounds);
        Road = new GameObject("Road").AddComponent<RoadScroller>();
        Spawner = new GameObject("Obstacles").AddComponent<ObstacleSpawner>();
        Player = new GameObject("Player").AddComponent<PlayerBike>();
        gameObject.AddComponent<SwipeInput>();
        UI = new GameObject("UI").AddComponent<GameUI>();
        UI.Build(this);
    }

    void Start()
    {
        GoToMenu();
    }

    static StageData[] DefaultStages()
    {
        return new[]
        {
            new StageData { name = "도시", distance = 450f, timeLimit = 70f, baseSpeed = 8f, spawnGap = 9.5f,
                roadColor = new Color(0.22f, 0.24f, 0.29f), sideColor = new Color(0.32f, 0.34f, 0.40f), decoColor = new Color(0.55f, 0.6f, 0.72f) },
            new StageData { name = "해안", distance = 650f, timeLimit = 85f, baseSpeed = 9f, spawnGap = 8.5f,
                roadColor = new Color(0.26f, 0.27f, 0.31f), sideColor = new Color(0.93f, 0.84f, 0.6f), decoColor = new Color(0.25f, 0.62f, 0.9f) },
            new StageData { name = "골목", distance = 850f, timeLimit = 100f, baseSpeed = 10f, spawnGap = 7.5f,
                roadColor = new Color(0.30f, 0.27f, 0.25f), sideColor = new Color(0.45f, 0.33f, 0.27f), decoColor = new Color(0.3f, 0.55f, 0.32f) },
        };
    }

    void SetupCamera()
    {
        cam = Camera.main;
        if (cam == null)
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            cam = go.AddComponent<Camera>();
        }
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.08f, 0.1f, 0.15f);
        cam.transform.position = new Vector3(0f, 0f, -10f);
        UpdateCameraSize();
    }

    void UpdateCameraSize()
    {
        // 도로(폭 4.5) + 길가가 화면 가로에 꽉 차도록, 기기 비율에 맞춰 자동 조정
        float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
        CamSize = Mathf.Max(5f, 2.85f / aspect);
        cam.orthographicSize = CamSize;
    }

    // 하단 버튼 영역 바로 위에 플레이어 배치
    float PlayerY => -CamSize + CamSize * 2f * 0.27f;

    // ───────── 흐름: 시작 → 플레이 → 결과 ─────────
    public void GoToMenu()
    {
        StopAllCoroutines();
        ClearPause();
        State = GameState.Menu;
        Spawner.ClearAll();
        Road.Setup(stages[0]);
        Player.ResetBike(PlayerY);
        UI.ShowMenu();
    }

    public void StartStage(int index)
    {
        if (index < 0 || index >= UnlockedStages) return;
        StopAllCoroutines();
        ClearPause();
        StageIndex = index;
        Distance = 0f;
        TimeLeft = CurrentStage.timeLimit;
        Freshness = 100f;
        Score = 0f;
        CurrentSpeed = 0f;

        UpdateCameraSize();
        Road.Setup(CurrentStage);
        Spawner.ResetSpawner();
        Player.ResetBike(PlayerY);
        UI.ShowHUD();
        UI.UpdateHUD();
        StartCoroutine(CountdownRoutine());
    }

    public void RetryStage() => StartStage(StageIndex);
    public void NextStage() => StartStage(StageIndex + 1);

    IEnumerator CountdownRoutine()
    {
        State = GameState.Countdown;
        UI.ShowMission(CurrentStage);
        yield return new WaitForSeconds(1.4f);
        for (int i = 3; i >= 1; i--)
        {
            UI.ShowCountdown(i.ToString());
            SoundManager.Play(Sfx.Countdown);
            yield return new WaitForSeconds(0.7f);
        }
        UI.ShowCountdown("출발!");
        SoundManager.Play(Sfx.Go);
        State = GameState.Playing;
        yield return new WaitForSeconds(0.6f);
        UI.HideCountdown();
    }

    void Update()
    {
        // ESC = 안드로이드 뒤로가기 버튼
        if (Input.GetKeyDown(KeyCode.Escape)) HandleBack();

        if (State != GameState.Playing || IsPaused) return;

        float dt = Time.deltaTime;
        Player.Tick(dt);

        CurrentSpeed = CurrentStage.baseSpeed * EngineMult * Player.SpeedMultiplier;
        float move = CurrentSpeed * dt;
        Distance += move;
        Score += move;
        TimeLeft -= dt;
        Freshness = Mathf.Max(0f, Freshness - freshnessDecayPerSec * dt);

        Road.Scroll(move);
        Spawner.Tick(move, dt);
        UI.UpdateHUD();

        if (Distance >= CurrentStage.distance) EndStage(true);
        else if (TimeLeft <= 0f) { TimeLeft = 0f; EndStage(false, "시간 초과!"); }
        else if (Freshness <= 0f) EndStage(false, "음식이 망가졌어요...");
    }

    void EndStage(bool clear, string failReason = "")
    {
        State = GameState.Result;
        Player.ReleaseCharge();

        int baseScore = Mathf.RoundToInt(Score);
        int timeBonus = clear ? Mathf.RoundToInt(TimeLeft * 20f) : 0;
        int freshBonus = clear ? Mathf.RoundToInt(Freshness * 10f) : 0;
        int total = baseScore + timeBonus + freshBonus;
        int gold = clear ? total / 10 : total / 30;

        Gold += gold;
        bool newBest = total > BestScore(StageIndex);
        if (newBest) PlayerPrefs.SetInt("best_" + StageIndex, total);
        if (clear && StageIndex + 2 > UnlockedStages) UnlockedStages = Mathf.Min(stages.Length, StageIndex + 2);
        PlayerPrefs.Save();

        SoundManager.Play(clear ? Sfx.Clear : Sfx.Fail);
        bool hasNext = clear && StageIndex + 1 < stages.Length;
        UI.ShowResult(clear, failReason, baseScore, timeBonus, freshBonus, total, gold, newBest, hasNext);
    }

    // ───────── 일시정지 / 뒤로가기 / 종료 ─────────
    void HandleBack()
    {
        if (UI.IsQuitDialogOpen) { UI.ShowQuitDialog(false); return; }

        switch (State)
        {
            case GameState.Menu: UI.ShowPause(!UI.IsPauseOpen); break; // 메뉴에선 설정 창 열기/닫기
            case GameState.Countdown:
            case GameState.Playing: TogglePause(); break;
            case GameState.Result: GoToMenu(); break;
        }
    }

    public void TogglePause()
    {
        if (IsPaused) Resume();
        else Pause();
    }

    public void Pause()
    {
        if (IsPaused || (State != GameState.Playing && State != GameState.Countdown)) return;
        IsPaused = true;
        Time.timeScale = 0f;
        Player.CancelCharge();
        UI.ShowPause(true);
    }

    public void Resume()
    {
        if (!IsPaused) return;
        ClearPause();
        UI.ShowPause(false);
    }

    void ClearPause()
    {
        IsPaused = false;
        Time.timeScale = 1f;
    }

    // 전화가 오거나 홈 버튼을 눌러 앱이 내려가면 자동 일시정지
    void OnApplicationPause(bool paused)
    {
        if (paused) Pause();
    }

    public void QuitGame()
    {
        PlayerPrefs.Save();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ───────── 버튼 입력 (UI / 키보드) ─────────
    public void OnJumpPressed()
    {
        if (State == GameState.Playing && !IsPaused) Player.Jump();
    }

    public void OnHoldDown()
    {
        if (State == GameState.Playing && !IsPaused) Player.BeginCharge();
    }

    public void OnHoldUp()
    {
        if (State == GameState.Playing && !IsPaused) Player.ReleaseCharge();
    }

    // ───────── 게임 이벤트 (ObstacleSpawner 가 호출) ─────────
    public void OnHit()
    {
        Freshness = Mathf.Max(0f, Freshness - hitFreshnessDamage * HitDamageMult);
        Shake(0.25f, 0.2f);
        UI.Flash(new Color(1f, 0.1f, 0.1f, 0.35f));
        UI.Popup("쾅! 신선도 감소", new Color(1f, 0.4f, 0.4f));
        SoundManager.Play(Sfx.Hit);
    }

    public void OnSmash()
    {
        Score += smashScore;
        Shake(0.12f, 0.1f);
        UI.Popup("+" + smashScore + " 파괴!", new Color(0.4f, 0.9f, 1f));
        SoundManager.Play(Sfx.Smash);
    }

    public void OnJumpDodge()
    {
        Score += jumpDodgeScore;
        UI.Popup("+" + jumpDodgeScore + " 점프!", new Color(0.5f, 1f, 0.5f));
    }

    public void OnCoin()
    {
        Score += coinScore;
        SoundManager.Play(Sfx.Coin, 0.7f);
    }

    void Shake(float time, float mag)
    {
        shakeTime = time;
        shakeMag = mag;
    }

    void LateUpdate()
    {
        if (shakeTime > 0f)
        {
            shakeTime -= Time.deltaTime;
            Vector2 r = Random.insideUnitCircle * shakeMag;
            cam.transform.position = new Vector3(r.x, r.y, -10f);
        }
        else
        {
            cam.transform.position = new Vector3(0f, 0f, -10f);
        }
    }
}
