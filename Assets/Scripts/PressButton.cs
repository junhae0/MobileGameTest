using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 누르는 순간(onDown)과 떼는 순간(onUp)을 모두 알려주는 버튼.
/// 일반 Button 은 손을 뗄 때 반응해서 점프가 늦게 느껴지므로, 점프·차징 모두 이걸 씁니다.
/// </summary>
public class PressButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public Action onDown;
    public Action onUp;
    public bool IsPressed { get; private set; }

    public void OnPointerDown(PointerEventData e)
    {
        IsPressed = true;
        transform.localScale = Vector3.one * 0.95f;
        onDown?.Invoke();
    }

    public void OnPointerUp(PointerEventData e)
    {
        Release();
    }

    void OnDisable()
    {
        Release();
    }

    void Release()
    {
        if (!IsPressed) return;
        IsPressed = false;
        transform.localScale = Vector3.one;
        onUp?.Invoke();
    }
}
