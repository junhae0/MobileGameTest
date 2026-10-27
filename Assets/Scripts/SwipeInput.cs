using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 화면 중앙(도로) 스와이프 → 차선 이동.
/// 버튼(UI) 위에서 시작한 터치는 무시하므로, 왼손으로 차징 버튼을 누른 채 오른손으로 스와이프할 수 있습니다.
/// 에디터 테스트용 키보드: ←/→ 또는 A/D = 차선, Space/↑ = 점프, 왼쪽 Shift(누르고 있기) = 차징.
/// (플레이 중 Hierarchy 의 GameManager 오브젝트를 선택하면 아래 값을 인스펙터에서 바로 조절해 볼 수 있어요)
/// </summary>
public class SwipeInput : MonoBehaviour
{
    [Tooltip("스와이프로 인정할 최소 거리 (화면 가로 길이 대비 비율). 0.12 = 화면 폭의 12%")]
    [Range(0.03f, 0.4f)] public float thresholdScreenRatio = 0.12f;

    [Tooltip("최소 거리 하한 (인치). 고해상도 폰에서 너무 민감해지는 것 방지")]
    public float minThresholdInch = 0.3f;

    [Tooltip("손을 떼지 않고 계속 밀 때 추가로 차선을 넘길지 여부. 끄면 한 번 스와이프 = 한 칸")]
    public bool allowMultiLanePerSwipe = false;

    [Tooltip("연속 이동 시 다음 이동까지 최소 간격 (초)")]
    public float laneCooldown = 0.12f;

    int trackedFinger = -1;
    bool mouseTracking;
    bool swipeConsumed;   // 이번 스와이프에서 이미 차선을 옮겼는지
    float lastLaneTime = -10f;
    Vector2 startPos;

    float Threshold
    {
        get
        {
            float byScreen = Screen.width * thresholdScreenRatio;
            float byInch = Screen.dpi > 0 ? Screen.dpi * minThresholdInch : 0f;
            return Mathf.Max(byScreen, byInch, 30f);
        }
    }

    void Update()
    {
        var gm = GameManager.I;
        if (gm == null || gm.State != GameState.Playing || gm.IsPaused)
        {
            trackedFinger = -1;
            mouseTracking = false;
            return;
        }

        // 키보드 (에디터/PC 테스트용)
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) gm.Player.ChangeLane(-1);
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) gm.Player.ChangeLane(1);
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.UpArrow)) gm.OnJumpPressed();
        if (Input.GetKeyDown(KeyCode.LeftShift)) gm.OnHoldDown();
        if (Input.GetKeyUp(KeyCode.LeftShift)) gm.OnHoldUp();

        if (Input.touchCount > 0) HandleTouches();
        else HandleMouse();
    }

    void HandleTouches()
    {
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch t = Input.GetTouch(i);
            if (t.phase == TouchPhase.Began)
            {
                if (trackedFinger == -1 && !IsOverUI(t.fingerId))
                {
                    trackedFinger = t.fingerId;
                    BeginSwipe(t.position);
                }
            }
            else if (t.fingerId == trackedFinger)
            {
                if (t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary) CheckSwipe(t.position);
                else trackedFinger = -1; // Ended / Canceled
            }
        }
    }

    void HandleMouse()
    {
        if (Input.GetMouseButtonDown(0) && !IsOverUI(-1))
        {
            mouseTracking = true;
            BeginSwipe(Input.mousePosition);
        }
        if (mouseTracking && Input.GetMouseButton(0)) CheckSwipe(Input.mousePosition);
        if (Input.GetMouseButtonUp(0)) mouseTracking = false;
    }

    void BeginSwipe(Vector2 pos)
    {
        startPos = pos;
        swipeConsumed = false;
    }

    void CheckSwipe(Vector2 pos)
    {
        if (swipeConsumed && !allowMultiLanePerSwipe) return;
        if (Time.time - lastLaneTime < laneCooldown) return;

        Vector2 delta = pos - startPos;
        // 가로로 충분히 밀었고, 세로 움직임보다 가로가 확실히 클 때만 인정
        if (Mathf.Abs(delta.x) >= Threshold && Mathf.Abs(delta.x) > Mathf.Abs(delta.y) * 1.2f)
        {
            GameManager.I.Player.ChangeLane(delta.x > 0 ? 1 : -1);
            lastLaneTime = Time.time;
            swipeConsumed = true;
            startPos = pos;
        }
    }

    static bool IsOverUI(int fingerId)
    {
        var es = EventSystem.current;
        if (es == null) return false;
        return fingerId < 0 ? es.IsPointerOverGameObject() : es.IsPointerOverGameObject(fingerId);
    }
}
