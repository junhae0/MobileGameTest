using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 모든 UI 를 코드로 생성합니다. (기획서 480 x 854 세로 화면 기준)
///  - 상단 HUD : 제한 시간 / 점수 / 신선도 / 남은 거리
///  - 중앙     : 스와이프 영역 (UI 없음)
///  - 하단     : 왼쪽 [차징 HOLD] / 오른쪽 [점프 TAP]
/// </summary>
public class GameUI : MonoBehaviour
{
    static readonly string[] UpgradeNames = { "엔진", "부스터", "배달통" };
    static readonly string[] UpgradeDescs = { "주행 속도 +5%", "충전·지속 강화", "신선도 손실 감소" };

    static readonly Color PanelColor = new Color(0.08f, 0.1f, 0.16f, 0.94f);
    static readonly Color CardColor = new Color(0.14f, 0.17f, 0.25f, 1f);
    static readonly Color Blue = new Color(0.25f, 0.5f, 0.95f);
    static readonly Color Red = new Color(0.86f, 0.18f, 0.22f);
    static readonly Color Green = new Color(0.12f, 0.65f, 0.35f);
    static readonly Color Gray = new Color(0.3f, 0.32f, 0.38f);
    static readonly Color Gold = new Color(1f, 0.8f, 0.2f);

    GameManager gm;
    Font font;
    RectTransform safeRoot;
    Rect lastSafeArea;

    GameObject menuPanel, hudPanel, resultPanel, pausePanel, quitPanel;
    Text pauseTitle;
    Button resumeButton, restartButton, toMenuButton, pauseQuitButton;
    Text goldText, timerText, scoreText, countdownText, missionText, boostText, popupText;
    Text resultTitle, resultBody;
    Image freshFill, progressFill, chargeFill, flashImage;
    Button nextButton;
    Button[] stageButtons;
    Text[] stageLabels, upgradeLabels, upgradeButtonLabels;
    Button[] upgradeButtons;
    PressButton holdButton, jumpButton;
    Coroutine flashRoutine, popupRoutine;

    // ───────── 생성 ─────────
    public void Build(GameManager manager)
    {
        gm = manager;
        font = gm.uiFont != null ? gm.uiFont : LoadDefaultFont();
        EnsureEventSystem();

        var canvasGo = new GameObject("Canvas", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(480, 854);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        safeRoot = MakeRect("SafeArea", canvasGo.transform, Vector2.zero, Vector2.one);
        ApplySafeArea();

        BuildHUD();
        BuildMenu();
        BuildResult();

        countdownText = MakeText("Countdown", safeRoot, "", 110, Color.white, TextAnchor.MiddleCenter, new Vector2(0, 0.45f), new Vector2(1, 0.65f));
        countdownText.fontStyle = FontStyle.Bold;
        AddOutline(countdownText);
        missionText = MakeText("Mission", safeRoot, "", 26, Color.white, TextAnchor.MiddleCenter, new Vector2(0.05f, 0.66f), new Vector2(0.95f, 0.78f));
        AddOutline(missionText);

        BuildPause();
        BuildQuitDialog();

        flashImage = MakeImage("Flash", canvasGo.transform, new Color(1, 0, 0, 0), Vector2.zero, Vector2.one);
        flashImage.raycastTarget = false;
    }

    static Font LoadDefaultFont()
    {
        Font f = null;
        try { f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }   // Unity 2022.2 이상
        if (f == null) try { f = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { } // 그 이전 버전
        return f;
    }

    static void EnsureEventSystem()
    {
#if UNITY_2023_1_OR_NEWER
        bool exists = Object.FindAnyObjectByType<EventSystem>() != null;
#else
        bool exists = Object.FindObjectOfType<EventSystem>() != null;
#endif
        if (exists) return;
        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<StandaloneInputModule>();
    }

    void BuildHUD()
    {
        hudPanel = MakeRect("HUD", safeRoot, Vector2.zero, Vector2.one).gameObject;
        Transform hud = hudPanel.transform;

        // ① 상단 정보 바
        var top = MakeImage("TopBar", hud, new Color(0.06f, 0.08f, 0.13f, 0.88f), new Vector2(0, 0.885f), new Vector2(1, 1));
        timerText = MakeText("Timer", top.transform, "01:10", 32, Color.white, TextAnchor.MiddleLeft, new Vector2(0.04f, 0.5f), new Vector2(0.4f, 1f));
        timerText.fontStyle = FontStyle.Bold;
        scoreText = MakeText("Score", top.transform, "0 Pt", 28, Gold, TextAnchor.MiddleRight, new Vector2(0.6f, 0.5f), new Vector2(0.96f, 1f));
        scoreText.fontStyle = FontStyle.Bold;
        MakeButton("PauseButton", top.transform, "II", new Color(1f, 1f, 1f, 0.18f), new Vector2(0.44f, 0.52f), new Vector2(0.56f, 0.95f), () => gm.TogglePause(), 22);

        MakeText("FreshLabel", top.transform, "신선도", 17, new Color(0.8f, 0.85f, 0.9f), TextAnchor.MiddleLeft, new Vector2(0.04f, 0.1f), new Vector2(0.2f, 0.45f));
        freshFill = MakeBar(top.transform, new Vector2(0.18f, 0.18f), new Vector2(0.5f, 0.38f), Green);
        MakeText("DistLabel", top.transform, "거리", 17, new Color(0.8f, 0.85f, 0.9f), TextAnchor.MiddleLeft, new Vector2(0.55f, 0.1f), new Vector2(0.66f, 0.45f));
        progressFill = MakeBar(top.transform, new Vector2(0.65f, 0.18f), new Vector2(0.96f, 0.38f), Blue);

        boostText = MakeText("Boost", hud, "BOOST!", 40, new Color(0.4f, 0.9f, 1f), TextAnchor.MiddleCenter, new Vector2(0, 0.78f), new Vector2(1, 0.86f));
        boostText.fontStyle = FontStyle.BoldAndItalic;
        AddOutline(boostText);
        popupText = MakeText("Popup", hud, "", 26, Color.white, TextAnchor.MiddleCenter, new Vector2(0, 0.7f), new Vector2(1, 0.77f));
        AddOutline(popupText);

        // ② 중앙 스와이프 안내
        MakeText("SwipeHint", hud, "<   화면 스와이프 (차선 이동)   >", 17, new Color(1, 1, 1, 0.6f), TextAnchor.MiddleCenter, new Vector2(0, 0.155f), new Vector2(1, 0.19f));

        // ③ 하단 액션 버튼 (왼손 차징 / 오른손 점프)
        holdButton = MakePressButton("HoldButton", hud, "차징 (HOLD)", "길게 눌렀다 떼기", Red, new Vector2(0.03f, 0.015f), new Vector2(0.485f, 0.15f));
        holdButton.onDown = gm.OnHoldDown;
        holdButton.onUp = gm.OnHoldUp;
        chargeFill = MakeImage("ChargeFill", holdButton.transform, new Color(1f, 0.85f, 0.2f, 0.55f), new Vector2(0.03f, 0.06f), new Vector2(0.97f, 0.94f));
        chargeFill.type = Image.Type.Filled;
        chargeFill.fillMethod = Image.FillMethod.Horizontal;
        chargeFill.fillAmount = 0f;
        chargeFill.transform.SetSiblingIndex(0); // 글자 아래로

        jumpButton = MakePressButton("JumpButton", hud, "점프 (TAP)", "낮은 장애물 넘기", Green, new Vector2(0.515f, 0.015f), new Vector2(0.97f, 0.15f));
        jumpButton.onDown = gm.OnJumpPressed;
    }

    void BuildMenu()
    {
        var bg = MakeImage("Menu", safeRoot, PanelColor, Vector2.zero, Vector2.one);
        bg.raycastTarget = true;
        menuPanel = bg.gameObject;
        Transform m = menuPanel.transform;

        // 상단 작은 버튼: 종료 / 음악 / 효과음
        MakeButton("QuitButton", m, "종료", Gray, new Vector2(0.03f, 0.955f), new Vector2(0.2f, 0.995f), () => ShowQuitDialog(true), 16);
        MakeButton("SettingsButton", m, "설정", Gray, new Vector2(0.76f, 0.955f), new Vector2(0.97f, 0.995f), () => ShowPause(true), 16);

        var title = MakeText("Title", m, "오토바이 고수", 54, Color.white, TextAnchor.MiddleCenter, new Vector2(0, 0.86f), new Vector2(1, 0.95f));
        title.fontStyle = FontStyle.Bold;
        AddOutline(title);
        MakeText("Sub", m, "스와이프 · 점프 · 부스터로 제시간에 배달하라!", 19, new Color(0.75f, 0.8f, 0.9f), TextAnchor.MiddleCenter, new Vector2(0, 0.815f), new Vector2(1, 0.86f));
        goldText = MakeText("Gold", m, "", 26, Gold, TextAnchor.MiddleCenter, new Vector2(0, 0.76f), new Vector2(1, 0.805f));

        MakeText("StageHeader", m, "스테이지 선택", 22, Color.white, TextAnchor.MiddleLeft, new Vector2(0.08f, 0.705f), new Vector2(0.9f, 0.75f));
        int n = gm.stages.Length;
        stageButtons = new Button[n];
        stageLabels = new Text[n];
        float top = 0.70f, h = 0.075f, gap = 0.012f;
        for (int i = 0; i < n; i++)
        {
            int idx = i;
            float y1 = top - i * (h + gap);
            stageButtons[i] = MakeButton("Stage" + i, m, "", Blue, new Vector2(0.08f, y1 - h), new Vector2(0.92f, y1), () => gm.StartStage(idx), 19);
            stageLabels[i] = stageButtons[i].GetComponentInChildren<Text>();
        }

        float upTop = top - n * (h + gap) - 0.02f;
        MakeText("UpgradeHeader", m, "장비 업그레이드", 22, Color.white, TextAnchor.MiddleLeft, new Vector2(0.08f, upTop - 0.045f), new Vector2(0.9f, upTop));
        int u = GameManager.UpgradeKeys.Length;
        upgradeLabels = new Text[u];
        upgradeButtons = new Button[u];
        upgradeButtonLabels = new Text[u];
        float rowTop = upTop - 0.05f, rowH = 0.07f;
        for (int i = 0; i < u; i++)
        {
            string key = GameManager.UpgradeKeys[i];
            float y1 = rowTop - i * (rowH + gap);
            var card = MakeImage("UpRow" + i, m, CardColor, new Vector2(0.08f, y1 - rowH), new Vector2(0.92f, y1), false, true);
            upgradeLabels[i] = MakeText("Label", card.transform, "", 19, Color.white, TextAnchor.MiddleLeft, new Vector2(0.05f, 0), new Vector2(0.62f, 1));
            upgradeButtons[i] = MakeButton("UpBtn", card.transform, "", Green, new Vector2(0.64f, 0.12f), new Vector2(0.97f, 0.88f), () =>
            {
                if (gm.TryUpgrade(key)) RefreshMenu();
            }, 18);
            upgradeButtonLabels[i] = upgradeButtons[i].GetComponentInChildren<Text>();
        }

        MakeText("Help", m, "스와이프: 차선 이동   |   오른쪽 버튼: 점프\n왼쪽 버튼 길게 누르고 떼기: 무적 부스터", 16,
            new Color(0.65f, 0.7f, 0.8f), TextAnchor.MiddleCenter, new Vector2(0.03f, 0.02f), new Vector2(0.97f, 0.09f));
    }

    void BuildResult()
    {
        var bg = MakeImage("Result", safeRoot, new Color(0, 0, 0, 0.75f), Vector2.zero, Vector2.one);
        bg.raycastTarget = true;
        resultPanel = bg.gameObject;
        var card = MakeImage("Card", resultPanel.transform, CardColor, new Vector2(0.07f, 0.2f), new Vector2(0.93f, 0.82f), false, true);
        Transform c = card.transform;

        resultTitle = MakeText("Title", c, "", 44, Color.white, TextAnchor.MiddleCenter, new Vector2(0, 0.82f), new Vector2(1, 0.97f));
        resultTitle.fontStyle = FontStyle.Bold;
        resultBody = MakeText("Body", c, "", 22, Color.white, TextAnchor.UpperCenter, new Vector2(0.06f, 0.33f), new Vector2(0.94f, 0.8f));
        resultBody.lineSpacing = 1.25f;
        resultBody.supportRichText = true;

        nextButton = MakeButton("Next", c, "다음 스테이지", Green, new Vector2(0.08f, 0.17f), new Vector2(0.92f, 0.3f), () => gm.NextStage(), 24);
        MakeButton("Retry", c, "다시 하기", Blue, new Vector2(0.08f, 0.03f), new Vector2(0.49f, 0.15f), () => gm.RetryStage(), 22);
        MakeButton("Menu", c, "메뉴", Gray, new Vector2(0.51f, 0.03f), new Vector2(0.92f, 0.15f), () => gm.GoToMenu(), 22);
    }

    void BuildPause()
    {
        var bg = MakeImage("Pause", safeRoot, new Color(0, 0, 0, 0.7f), Vector2.zero, Vector2.one, true);
        pausePanel = bg.gameObject;
        var card = MakeImage("Card", bg.transform, CardColor, new Vector2(0.08f, 0.17f), new Vector2(0.92f, 0.83f), false, true);
        Transform c = card.transform;

        pauseTitle = MakeText("Title", c, "일시정지", 38, Color.white, TextAnchor.MiddleCenter, new Vector2(0, 0.87f), new Vector2(1, 0.98f));
        pauseTitle.fontStyle = FontStyle.Bold;

        // 사운드 설정
        MakeText("SoundHeader", c, "사운드", 20, new Color(0.75f, 0.8f, 0.9f), TextAnchor.MiddleLeft, new Vector2(0.07f, 0.81f), new Vector2(0.9f, 0.87f));
        var sm = SoundManager.I;
        MakeVolumeRow(c, "음악", 0.80f, sm.MusicVolume, v => sm.MusicVolume = v);   // 0.70 ~ 0.80
        MakeVolumeRow(c, "효과음", 0.685f, sm.SfxVolume, v => sm.SfxVolume = v);  // 0.585 ~ 0.685

        resumeButton = MakeButton("Resume", c, "계속하기", Green, new Vector2(0.08f, 0.43f), new Vector2(0.92f, 0.53f), OnPausePrimary, 24);
        restartButton = MakeButton("Restart", c, "처음부터 다시", Blue, new Vector2(0.08f, 0.305f), new Vector2(0.92f, 0.405f), () => gm.RetryStage(), 22);
        toMenuButton = MakeButton("ToMenu", c, "메뉴로", Gray, new Vector2(0.08f, 0.18f), new Vector2(0.92f, 0.28f), () => gm.GoToMenu(), 22);
        pauseQuitButton = MakeButton("Quit", c, "게임 종료", Red, new Vector2(0.08f, 0.055f), new Vector2(0.92f, 0.155f), () => ShowQuitDialog(true), 22);
        pausePanel.SetActive(false);
    }

    // 플레이 중이면 '계속하기', 메뉴에서 열었으면 '닫기'
    void OnPausePrimary()
    {
        SoundManager.I.SaveSettings();
        if (gm.State == GameState.Menu) ShowPause(false);
        else gm.Resume();
    }

    void MakeVolumeRow(Transform parent, string label, float yTop, float value, System.Action<float> onChange)
    {
        float yBottom = yTop - 0.1f;
        var row = MakeImage("Row_" + label, parent, new Color(1, 1, 1, 0.06f), new Vector2(0.06f, yBottom), new Vector2(0.94f, yTop), false, true);
        MakeText("Label", row.transform, label, 20, Color.white, TextAnchor.MiddleLeft, new Vector2(0.05f, 0), new Vector2(0.27f, 1));
        var valueText = MakeText("Value", row.transform, "", 19, Gold, TextAnchor.MiddleRight, new Vector2(0.8f, 0), new Vector2(0.96f, 1));
        valueText.text = Mathf.RoundToInt(value * 100) + "%";
        MakeSlider(row.transform, new Vector2(0.27f, 0f), new Vector2(0.79f, 1f), value, v =>
        {
            onChange(v);
            valueText.text = Mathf.RoundToInt(v * 100) + "%";
        });
    }

    Slider MakeSlider(Transform parent, Vector2 aMin, Vector2 aMax, float value, UnityAction<float> onChange)
    {
        var root = MakeRect("Slider", parent, aMin, aMax);

        var track = MakeImage("Track", root, new Color(1, 1, 1, 0.18f), new Vector2(0, 0.38f), new Vector2(1, 0.62f), true, true);
        track.pixelsPerUnitMultiplier = 5f;

        var fillArea = MakeRect("FillArea", root, new Vector2(0, 0.38f), new Vector2(1, 0.62f));
        var fill = MakeImage("Fill", fillArea, Blue, Vector2.zero, new Vector2(0, 1), false, true);
        fill.pixelsPerUnitMultiplier = 5f;

        var handleArea = MakeRect("HandleArea", root, new Vector2(0, 0.15f), new Vector2(1, 0.85f));
        handleArea.offsetMin = new Vector2(12, 0);
        handleArea.offsetMax = new Vector2(-12, 0);
        var handle = MakeImage("Handle", handleArea, Color.white, Vector2.zero, new Vector2(0, 1), true);
        handle.sprite = SpriteFactory.Circle;
        handle.preserveAspect = true;
        handle.rectTransform.sizeDelta = new Vector2(30, 0);

        var slider = root.gameObject.AddComponent<Slider>();
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.value = value;
        slider.onValueChanged.AddListener(onChange);
        return slider;
    }

    void BuildQuitDialog()
    {
        var bg = MakeImage("QuitDialog", safeRoot, new Color(0, 0, 0, 0.75f), Vector2.zero, Vector2.one, true);
        quitPanel = bg.gameObject;
        var card = MakeImage("Card", bg.transform, CardColor, new Vector2(0.1f, 0.38f), new Vector2(0.9f, 0.62f), false, true);
        Transform c = card.transform;

        var title = MakeText("Title", c, "게임을 종료할까요?", 28, Color.white, TextAnchor.MiddleCenter, new Vector2(0, 0.6f), new Vector2(1, 0.92f));
        title.fontStyle = FontStyle.Bold;
        MakeText("Sub", c, "골드와 업그레이드는 자동 저장돼요.", 17, new Color(0.75f, 0.8f, 0.9f), TextAnchor.MiddleCenter, new Vector2(0, 0.42f), new Vector2(1, 0.6f));
        MakeButton("Cancel", c, "취소", Gray, new Vector2(0.06f, 0.08f), new Vector2(0.48f, 0.36f), () => ShowQuitDialog(false), 22);
        MakeButton("Confirm", c, "종료", Red, new Vector2(0.52f, 0.08f), new Vector2(0.94f, 0.36f), () => gm.QuitGame(), 22);
        quitPanel.SetActive(false);
    }

    public bool IsQuitDialogOpen => quitPanel != null && quitPanel.activeSelf;
    public void ShowQuitDialog(bool on) => quitPanel.SetActive(on);
    /// <summary>플레이 중엔 일시정지 메뉴, 메인 메뉴에선 설정 창으로 보여줌</summary>
    public void ShowPause(bool on)
    {
        if (on)
        {
            bool inGame = gm.State != GameState.Menu;
            pauseTitle.text = inGame ? "일시정지" : "설정";
            resumeButton.GetComponentInChildren<Text>().text = inGame ? "계속하기" : "닫기";
            restartButton.gameObject.SetActive(inGame);
            toMenuButton.gameObject.SetActive(inGame);
            var qr = (RectTransform)pauseQuitButton.transform;
            qr.anchorMin = new Vector2(0.08f, inGame ? 0.055f : 0.305f);
            qr.anchorMax = new Vector2(0.92f, inGame ? 0.155f : 0.405f);
        }
        else if (SoundManager.I != null) SoundManager.I.SaveSettings();
        pausePanel.SetActive(on);
    }

    public bool IsPauseOpen => pausePanel != null && pausePanel.activeSelf;

    // ───────── 화면 전환 ─────────
    public void ShowMenu()
    {
        pausePanel.SetActive(false);
        quitPanel.SetActive(false);
        menuPanel.SetActive(true);
        hudPanel.SetActive(false);
        resultPanel.SetActive(false);
        HideCountdown();
        RefreshMenu();
    }

    public void ShowHUD()
    {
        pausePanel.SetActive(false);
        quitPanel.SetActive(false);
        menuPanel.SetActive(false);
        resultPanel.SetActive(false);
        hudPanel.SetActive(true);
        boostText.gameObject.SetActive(false);
        popupText.text = "";
    }

    public void ShowMission(StageData s)
    {
        missionText.text = $"<b>{s.name}</b> 배달 출발!\n목적지 {s.distance:0}m · 제한시간 {s.timeLimit:0}초";
        countdownText.text = "";
    }

    public void ShowCountdown(string s)
    {
        countdownText.text = s;
        countdownText.transform.localScale = Vector3.one * 1.3f;
    }

    public void HideCountdown()
    {
        countdownText.text = "";
        missionText.text = "";
    }

    void RefreshMenu()
    {
        goldText.text = $"보유 골드  {gm.Gold:N0} G";
        int unlocked = gm.UnlockedStages;
        for (int i = 0; i < stageButtons.Length; i++)
        {
            var s = gm.stages[i];
            bool open = i < unlocked;
            stageButtons[i].interactable = open;
            stageLabels[i].text = open
                ? $"{i + 1}. {s.name}   ({s.distance:0}m / {s.timeLimit:0}초)   최고 {gm.BestScore(i):N0}"
                : $"{i + 1}. {s.name}   (이전 스테이지 클리어 시 해금)";
        }
        for (int i = 0; i < GameManager.UpgradeKeys.Length; i++)
        {
            string key = GameManager.UpgradeKeys[i];
            int lv = gm.GetUpgrade(key);
            bool max = lv >= GameManager.MaxUpgrade;
            upgradeLabels[i].text = $"{UpgradeNames[i]} Lv.{lv}\n<size=14>{UpgradeDescs[i]}</size>";
            upgradeLabels[i].supportRichText = true;
            upgradeButtonLabels[i].text = max ? "MAX" : $"강화 {gm.UpgradeCost(key)}G";
            upgradeButtons[i].interactable = !max && gm.Gold >= gm.UpgradeCost(key);
        }
    }

    public void ShowResult(bool clear, string reason, int baseScore, int timeBonus, int freshBonus, int total, int gold, bool newBest, bool hasNext)
    {
        hudPanel.SetActive(false);
        pausePanel.SetActive(false);
        resultPanel.SetActive(true);
        HideCountdown();

        resultTitle.text = clear ? "배달 완료!" : "배달 실패";
        resultTitle.color = clear ? new Color(0.4f, 1f, 0.55f) : new Color(1f, 0.45f, 0.45f);

        string body = clear ? "" : $"<color=#FF9A9A>{reason}</color>\n";
        body += $"주행 점수   {baseScore:N0}\n";
        if (clear)
        {
            body += $"남은 시간 보너스   +{timeBonus:N0}\n";
            body += $"신선도 {gm.Freshness:0}% 보너스   +{freshBonus:N0}\n";
        }
        body += $"\n<size=30><b>총점 {total:N0}</b></size>";
        if (newBest) body += "  <color=#FFD54A>NEW!</color>";
        body += $"\n<color=#FFD54A>골드 +{gold:N0} G</color>";
        resultBody.text = body;

        nextButton.gameObject.SetActive(hasNext);
    }

    // ───────── 매 프레임 갱신 ─────────
    public void UpdateHUD()
    {
        float t = Mathf.Max(0f, gm.TimeLeft);
        int sec = Mathf.CeilToInt(t);
        timerText.text = $"{sec / 60:00}:{sec % 60:00}";
        timerText.color = t < 10f ? new Color(1f, 0.4f, 0.4f) : Color.white;
        scoreText.text = $"{Mathf.RoundToInt(gm.Score):N0} Pt";

        float fresh = gm.Freshness / 100f;
        freshFill.fillAmount = fresh;
        freshFill.color = Color.Lerp(Red, Green, fresh);
        progressFill.fillAmount = Mathf.Clamp01(gm.Distance / gm.CurrentStage.distance);

        var p = gm.Player;
        if (p.IsBoosting)
        {
            chargeFill.fillAmount = p.BoostRatio;
            chargeFill.color = new Color(0.3f, 0.85f, 1f, 0.6f);
        }
        else
        {
            chargeFill.fillAmount = p.Charge;
            chargeFill.color = p.Charge >= p.minChargeToBoost
                ? new Color(1f, 0.85f, 0.2f, 0.6f)
                : new Color(1f, 1f, 1f, 0.3f);
        }
        boostText.gameObject.SetActive(p.IsBoosting);
    }

    void Update()
    {
        ApplySafeArea();
        if (countdownText != null && countdownText.transform.localScale.x > 1f)
            countdownText.transform.localScale = Vector3.MoveTowards(countdownText.transform.localScale, Vector3.one, Time.deltaTime * 1.5f);
    }

    public void Flash(Color c)
    {
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(FlashRoutine(c));
    }

    IEnumerator FlashRoutine(Color c)
    {
        for (float t = 0; t < 0.3f; t += Time.deltaTime)
        {
            flashImage.color = new Color(c.r, c.g, c.b, c.a * (1f - t / 0.3f));
            yield return null;
        }
        flashImage.color = Color.clear;
    }

    public void Popup(string msg, Color c)
    {
        if (popupRoutine != null) StopCoroutine(popupRoutine);
        popupRoutine = StartCoroutine(PopupRoutine(msg, c));
    }

    IEnumerator PopupRoutine(string msg, Color c)
    {
        popupText.text = msg;
        for (float t = 0; t < 0.8f; t += Time.deltaTime)
        {
            popupText.color = new Color(c.r, c.g, c.b, 1f - t / 0.8f);
            yield return null;
        }
        popupText.text = "";
    }

    void ApplySafeArea()
    {
        Rect sa = Screen.safeArea;
        if (sa == lastSafeArea || Screen.width <= 0 || Screen.height <= 0) return;
        lastSafeArea = sa;
        safeRoot.anchorMin = new Vector2(sa.xMin / Screen.width, sa.yMin / Screen.height);
        safeRoot.anchorMax = new Vector2(sa.xMax / Screen.width, sa.yMax / Screen.height);
        safeRoot.offsetMin = safeRoot.offsetMax = Vector2.zero;
    }

    // ───────── UI 생성 도우미 ─────────
    static RectTransform MakeRect(string name, Transform parent, Vector2 aMin, Vector2 aMax)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return rt;
    }

    static Image MakeImage(string name, Transform parent, Color color, Vector2 aMin, Vector2 aMax, bool raycast = false, bool rounded = false)
    {
        var img = MakeRect(name, parent, aMin, aMax).gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = raycast;
        if (rounded)
        {
            img.sprite = SpriteFactory.RoundRect;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 2.5f;
        }
        else
        {
            img.sprite = SpriteFactory.Square;
        }
        return img;
    }

    Image MakeBar(Transform parent, Vector2 aMin, Vector2 aMax, Color fillColor)
    {
        MakeImage("BarBG", parent, new Color(1, 1, 1, 0.15f), aMin, aMax);
        var fill = MakeImage("BarFill", parent, fillColor, aMin, aMax);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillAmount = 1f;
        return fill;
    }

    Text MakeText(string name, Transform parent, string text, int size, Color color, TextAnchor anchor, Vector2 aMin, Vector2 aMax)
    {
        var t = MakeRect(name, parent, aMin, aMax).gameObject.AddComponent<Text>();
        t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = anchor;
        t.raycastTarget = false;
        t.supportRichText = true;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    static void AddOutline(Text t)
    {
        var o = t.gameObject.AddComponent<Outline>();
        o.effectColor = new Color(0, 0, 0, 0.7f);
        o.effectDistance = new Vector2(2, -2);
    }

    Button MakeButton(string name, Transform parent, string label, Color color, Vector2 aMin, Vector2 aMax, UnityAction onClick, int fontSize)
    {
        var img = MakeImage(name, parent, color, aMin, aMax, true, true);
        var b = img.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        var colors = b.colors;
        colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.6f);
        b.colors = colors;
        b.onClick.AddListener(() => SoundManager.Play(Sfx.Click));
        b.onClick.AddListener(onClick);
        var t = MakeText("Label", img.transform, label, fontSize, Color.white, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one);
        t.fontStyle = FontStyle.Bold;
        return b;
    }

    PressButton MakePressButton(string name, Transform parent, string label, string sub, Color color, Vector2 aMin, Vector2 aMax)
    {
        var img = MakeImage(name, parent, color, aMin, aMax, true, true);
        var t = MakeText("Label", img.transform, label, 26, Color.white, TextAnchor.MiddleCenter, new Vector2(0, 0.38f), new Vector2(1, 0.85f));
        t.fontStyle = FontStyle.Bold;
        MakeText("Sub", img.transform, sub, 15, new Color(1, 1, 1, 0.8f), TextAnchor.MiddleCenter, new Vector2(0, 0.1f), new Vector2(1, 0.4f));
        return img.gameObject.AddComponent<PressButton>();
    }
}
