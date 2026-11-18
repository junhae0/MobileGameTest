using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class MenuUI : MonoBehaviour
{
    public TMP_Text uidText;
    void Start() { uidText.text = AuthManager.Name; }
    public void Logout() { AuthManager.Logout(); } // 로그아웃 버튼
    public void StartGame() { SceneManager.LoadScene("Game"); } // 게임 시작 버튼 (오토바이 고수 게임 씬으로)
}
