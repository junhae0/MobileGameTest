using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 장애물 패턴 생성, 이동, 플레이어와의 충돌 판정, 결승선 표시.
/// 물리 엔진 대신 '같은 차선 + 위아래 겹침' 으로 판정해서 모바일에서도 가볍고 정확합니다.
/// </summary>
public class ObstacleSpawner : MonoBehaviour
{
    readonly List<Obstacle> active = new List<Obstacle>();
    float distSinceSpawn, nextGap;
    Transform finishLine;
    bool finishSpawned;

    public void ResetSpawner()
    {
        ClearAll();
        distSinceSpawn = 0f;
        nextGap = 6f; // 출발 직후 잠깐 여유
        finishSpawned = false;
    }

    public void ClearAll()
    {
        foreach (var o in active)
            if (o != null) Destroy(o.gameObject);
        active.Clear();
        if (finishLine != null) Destroy(finishLine.gameObject);
        finishLine = null;
    }

    public void Tick(float move, float dt)
    {
        var gm = GameManager.I;
        var player = gm.Player;
        var stage = gm.CurrentStage;
        float spawnY = gm.CamSize + 1.5f;
        float playerY = player.transform.position.y;
        float remaining = stage.distance - gm.Distance;

        // ── 생성 ──
        distSinceSpawn += move;
        if (remaining > 25f && distSinceSpawn >= nextGap)
        {
            distSinceSpawn = 0f;
            float progress = gm.Distance / stage.distance;
            SpawnPattern(spawnY, progress);
            nextGap = stage.spawnGap * Mathf.Lerp(1f, 0.7f, progress) * Random.Range(0.85f, 1.2f);
        }

        // ── 결승선 ──
        if (!finishSpawned && playerY + remaining < spawnY)
        {
            finishSpawned = true;
            finishLine = CreateFinishLine(new Vector2(0f, playerY + remaining));
        }
        if (finishLine != null) finishLine.position += Vector3.down * move;

        // ── 이동 + 충돌 ──
        Vector3 pp = player.transform.position;
        for (int i = active.Count - 1; i >= 0; i--)
        {
            var o = active[i];
            if (o == null) { active.RemoveAt(i); continue; }

            if (o.Dying)
            {
                if (o.UpdateDeath(dt)) { Destroy(o.gameObject); active.RemoveAt(i); }
                continue;
            }

            float step = Mathf.Max(move * 0.3f, move - o.ownSpeed * dt);
            o.transform.position += Vector3.down * step;
            o.Animate(Time.time);

            Vector3 op = o.transform.position;
            if (op.y < -gm.CamSize - 2f) { Destroy(o.gameObject); active.RemoveAt(i); continue; }
            if (o.consumed) continue;

            bool overlap = Mathf.Abs(op.x - pp.x) < o.halfSize.x + 0.28f &&
                           Mathf.Abs(op.y - pp.y) < o.halfSize.y + 0.5f;
            if (!overlap) continue;

            if (o.IsCoin)                               { o.Kill(); gm.OnCoin(); }
            else if (player.IsBoosting)                 { o.Kill(); gm.OnSmash(); }       // 무적 부스터로 파괴
            else if (o.IsJumpable && player.IsJumping)  { o.consumed = true; gm.OnJumpDodge(); }
            else if (player.IsInvulnerable)             { o.consumed = true; }
            else                                        { o.consumed = true; player.Hit(); gm.OnHit(); }
        }
    }

    // ───────── 패턴 ─────────
    void SpawnPattern(float y, float progress)
    {
        float[] lx = PlayerBike.LaneX;
        int r = Random.Range(0, 100);

        if (r < 22) // 한 차선 막힘
        {
            int lane = Random.Range(0, 3);
            Spawn(Random.value < 0.6f ? ObstacleType.Car : ObstacleType.Construction, lx[lane], y);
            if (Random.value < 0.5f) CoinRow((lane + Random.Range(1, 3)) % 3, y, 3);
        }
        else if (r < 38) // 두 차선 막힘 (남은 한 칸으로)
        {
            int free = Random.Range(0, 3);
            for (int l = 0; l < 3; l++)
                if (l != free) Spawn(Random.value < 0.5f ? ObstacleType.Car : ObstacleType.Construction, lx[l], y);
            CoinRow(free, y, 3);
        }
        else if (r < 52) // 대형 벽 (2차선)
        {
            bool left = Random.value < 0.5f;
            Spawn(ObstacleType.Wall, left ? (lx[0] + lx[1]) / 2f : (lx[1] + lx[2]) / 2f, y);
            CoinRow(left ? 2 : 0, y, 2);
        }
        else if (r < 66) // 전 차선 과속방지턱 → 점프 필수
        {
            Spawn(ObstacleType.SpeedBump, 0f, y);
        }
        else if (r < 84) // 물웅덩이 / 바리케이드 1~2개
        {
            int count = progress > 0.4f && Random.value < 0.5f ? 2 : 1;
            int start = Random.Range(0, 3);
            for (int k = 0; k < count; k++)
                Spawn(Random.value < 0.5f ? ObstacleType.Puddle : ObstacleType.Barricade, lx[(start + k) % 3], y);
        }
        else // 코인 줄 + 섞인 장애물
        {
            int lane = Random.Range(0, 3);
            CoinRow(lane, y, 5);
            if (progress > 0.3f) Spawn(ObstacleType.Puddle, lx[lane], y + 2.2f);
        }
    }

    void CoinRow(int lane, float y, int count)
    {
        for (int i = 0; i < count; i++)
            Spawn(ObstacleType.Coin, PlayerBike.LaneX[lane], y + i * 0.9f);
    }

    void Spawn(ObstacleType type, float x, float y)
    {
        active.Add(Obstacle.Create(type, new Vector2(x, y), transform));
    }

    Transform CreateFinishLine(Vector2 pos)
    {
        var go = new GameObject("FinishLine");
        go.transform.SetParent(transform, false);
        go.transform.position = pos;
        const int cols = 12;
        float w = 4.5f / cols;
        for (int row = 0; row < 2; row++)
        for (int c = 0; c < cols; c++)
        {
            Color col = (row + c) % 2 == 0 ? Color.white : new Color(0.1f, 0.1f, 0.1f);
            SpriteFactory.Box("Check", go.transform, col, new Vector2(-2.25f + w * (c + 0.5f), (row - 0.5f) * w), new Vector2(w, w), 9);
        }
        return go.transform;
    }
}
