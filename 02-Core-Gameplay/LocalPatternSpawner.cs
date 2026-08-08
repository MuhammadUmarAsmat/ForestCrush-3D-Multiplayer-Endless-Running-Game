using System.Collections.Generic;
using UnityEngine;

/// <summary>SINGLE MODE pattern spawner - same rhythm as NetworkPatternSpawner, no Fusion.</summary>
public class LocalPatternSpawner : MonoBehaviour
{
    [Header("Coin (LOCAL prefab)")]
    [SerializeField] private GameObject coinPrefab;
    [SerializeField] private int coinsPerLine = 8;
    [SerializeField] private float coinSpacing = 3f;
    [SerializeField] private float coinY = 1f;

    [Header("Jump-Arc coin lines")]
    [Range(0f, 1f)][SerializeField] private float arcLineChance = 0.35f;
    [SerializeField] private float arcHeight = 1.6f;

    [Header("Hurdles - REGULAR (LOCAL prefabs)")]
    [SerializeField] private GameObject[] hurdlePrefabs;

    [Header("Hurdles - CHAIN/RARE")]
    [SerializeField] private GameObject[] chainHurdles;
    [SerializeField] private int chainEvery = 4;

    [Header("Rhythm (SECONDS of running)")]
    [SerializeField] private float runSpeed = 8f;
    [SerializeField] private float startDelaySeconds = 3.5f;
    [SerializeField] private float gapAfterCoinsSeconds = 3.5f;
    [SerializeField] private float gapAfterHurdleSeconds = 3.5f;
    [Range(0f, 1f)][SerializeField] private float doubleCoinLineChance = 0.25f;
    [Range(0f, 1f)][SerializeField] private float doubleHurdleChance = 0.15f;

    [Header("Lanes")]
    [SerializeField] private float leftLaneX = -1.5f;
    [SerializeField] private float centerLaneX = 0f;
    [SerializeField] private float rightLaneX = 1.5f;

    [Header("Pacing")]
    [SerializeField] private float leadDistance = 60f;
    [SerializeField] private float despawnBehind = 25f;
    [SerializeField] private string playerTag = "Player";

    private enum Phase { CoinLine, Hurdle }
    private Phase phase = Phase.CoinLine;
    private float nextZ;
    private bool started;
    private int coinsPlaced, lineLane, hurdleCounter, lastHurdleIndex = -1;
    private bool lineIsArc;
    private readonly List<GameObject> active = new List<GameObject>();

    private void Update()
    {
        if (LocalGameManager.I == null || !LocalGameManager.I.RunningAllowed) return;
        if (LocalGameManager.I.IsGameOver) return;

        float refZ = GetPlayerZ(out bool has);
        if (!has) return;

        if (!started)
        {
            nextZ = refZ + startDelaySeconds * runSpeed;
            BeginCoinLine();
            started = true;
        }

        while (refZ + leadDistance >= nextZ)
        {
            if (phase == Phase.CoinLine) StepCoinLine();
            else PlaceHurdleAndAdvance();
        }

        for (int i = active.Count - 1; i >= 0; i--)
        {
            if (active[i] == null) { active.RemoveAt(i); continue; }
            if (active[i].transform.position.z < refZ - despawnBehind)
            {
                Destroy(active[i]);
                active.RemoveAt(i);
            }
        }
    }

    private void BeginCoinLine()
    {
        phase = Phase.CoinLine;
        coinsPlaced = 0;
        lineLane = Random.Range(0, 3);
        lineIsArc = Random.value < arcLineChance;
    }

    private void StepCoinLine()
    {
        if (coinsPlaced < coinsPerLine)
        {
            float y = CoinYForIndex(coinsPlaced, coinsPerLine);
            GameObject c = Instantiate(coinPrefab,
                new Vector3(LaneToX(lineLane), y, nextZ), Quaternion.identity);
            active.Add(c);
            coinsPlaced++;
            nextZ += coinSpacing;
            return;
        }

        if (Random.value < doubleCoinLineChance)
        {
            nextZ += (gapAfterCoinsSeconds * 0.5f) * runSpeed;
            BeginCoinLine();
        }
        else
        {
            nextZ += gapAfterCoinsSeconds * runSpeed;
            phase = Phase.Hurdle;
        }
    }

    private float CoinYForIndex(int i, int n)
    {
        if (!lineIsArc) return coinY;
        int mid = n / 2;
        int d = Mathf.Abs(i - mid);
        if (d > 2) return coinY;
        float t = 1f - (d / 3f);
        return coinY + arcHeight * t;
    }

    private void PlaceHurdleAndAdvance()
    {
        GameObject prefab = PickHurdle();
        if (prefab == null) { BeginCoinLine(); return; }

        float x = centerLaneX;
        HurdlePlacement hp = prefab.GetComponent<HurdlePlacement>();
        if (hp != null && hp.mode == HurdlePlacement.Mode.RandomLeftRight)
            x = (Random.value < 0.5f) ? -hp.sideX : hp.sideX;

        float y = prefab.transform.position.y;
        GameObject obj = Instantiate(prefab, new Vector3(x, y, nextZ), prefab.transform.rotation);
        active.Add(obj);

        hurdleCounter++;

        if (Random.value < doubleHurdleChance)
            nextZ += (gapAfterHurdleSeconds * 0.6f) * runSpeed;
        else
        {
            nextZ += gapAfterHurdleSeconds * runSpeed;
            BeginCoinLine();
        }
    }

    private GameObject PickHurdle()
    {
        bool chainTurn = chainHurdles != null && chainHurdles.Length > 0
                         && chainEvery > 0 && (hurdleCounter % chainEvery) == (chainEvery - 1);
        if (chainTurn) return chainHurdles[Random.Range(0, chainHurdles.Length)];

        if (hurdlePrefabs == null || hurdlePrefabs.Length == 0) return null;
        if (hurdlePrefabs.Length == 1) return hurdlePrefabs[0];

        int idx = Random.Range(0, hurdlePrefabs.Length);
        if (idx == lastHurdleIndex) idx = (idx + 1) % hurdlePrefabs.Length;
        lastHurdleIndex = idx;
        return hurdlePrefabs[idx];
    }

    private float LaneToX(int lane)
    {
        if (lane == 0) return leftLaneX;
        if (lane == 2) return rightLaneX;
        return centerLaneX;
    }

    private float GetPlayerZ(out bool found)
    {
        GameObject p = GameObject.FindGameObjectWithTag(playerTag);
        found = p != null;
        return found ? p.transform.position.z : 0f;
    }
}