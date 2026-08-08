using UnityEngine;
using PlayFab;
using PlayFab.ClientModels;
using System.Collections.Generic;
using System;

/// <summary>
/// Solo leaderboard (single mode). Ranking is by COINS.
/// Distance is stored as PUBLIC UserData (so it can be shown for every player
/// in the Top 10, not just the local one) and fetched per-entry.
/// </summary>
public class LeaderboardManager : MonoBehaviour
{
    public static LeaderboardManager Instance { get; private set; }

    // NOTE: keeps the same PlayFab statistic name that already works, but the
    // VALUE it stores is now COINS (used to rank the leaderboard).
    const string SOLO_LEADERBOARD = "TopScores";

    public event Action<List<LeaderboardEntry>> OnLeaderboardLoaded;
    public event Action<string> OnLeaderboardError;
    public event Action<LeaderboardEntry> OnMyRankLoaded;
    public event Action<string> OnMyRankError;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // ═══════════════════════════════════════════════════════
    //  SUBMIT (call on single-mode game over)
    // ═══════════════════════════════════════════════════════

    public void SubmitSoloScore(int distance, int coins)
    {
        if (!PlayFabClientAPI.IsClientLoggedIn())
        {
            Debug.LogWarning("LeaderboardManager: not logged in, score not submitted.");
            return;
        }

        // 1) COINS -> leaderboard statistic (this decides the RANK)
        var statReq = new UpdatePlayerStatisticsRequest
        {
            Statistics = new List<StatisticUpdate>
            {
                new StatisticUpdate { StatisticName = SOLO_LEADERBOARD, Value = coins }
            }
        };
        PlayFabClientAPI.UpdatePlayerStatistics(statReq,
            r => Debug.Log($"Solo coins submitted: {coins}"),
            e => Debug.LogError("Coins submit error: " + e.ErrorMessage));

        // 2) DISTANCE -> player data, marked PUBLIC so other players' rows can read it
        var dataReq = new UpdateUserDataRequest
        {
            Data = new Dictionary<string, string> { { "Distance", distance.ToString() } },
            Permission = UserDataPermission.Public
        };
        PlayFabClientAPI.UpdateUserData(dataReq,
            r => Debug.Log($"Distance saved: {distance}"),
            e => Debug.LogError("Distance save error: " + e.ErrorMessage));
    }

    // Back-compat overload
    public void SubmitSoloScore(int distance)
    {
        int coins = PlayerPrefs.GetInt("Coins", 0);
        SubmitSoloScore(distance, coins);
    }

    // ═══════════════════════════════════════════════════════
    //  FETCH TOP 10 (coins = rank, distance = real per-player fetch)
    // ═══════════════════════════════════════════════════════

    public void FetchSoloTop10()
    {
        if (!PlayFabClientAPI.IsClientLoggedIn())
        {
            OnLeaderboardError?.Invoke("Not logged in");
            return;
        }

        var request = new GetLeaderboardRequest
        {
            StatisticName = SOLO_LEADERBOARD,
            StartPosition = 0,
            MaxResultsCount = 10,
            ProfileConstraints = new PlayerProfileViewConstraints { ShowDisplayName = true }
        };

        PlayFabClientAPI.GetLeaderboard(request, OnFetchSuccess, OnFetchError);
    }

    void OnFetchSuccess(GetLeaderboardResult result)
    {
        var entries = new List<LeaderboardEntry>();

        foreach (var item in result.Leaderboard)
        {
            entries.Add(new LeaderboardEntry
            {
                Rank = item.Position + 1,
                PlayerName = string.IsNullOrEmpty(item.DisplayName)
                             ? $"Runner{item.Position + 1}"
                             : item.DisplayName,
                Coins = item.StatValue,   // real ranking value
                Score = 0,                // distance filled in below, per-player
                PlayFabId = item.PlayFabId
            });
        }

        if (entries.Count == 0)
        {
            OnLeaderboardLoaded?.Invoke(entries);
            return;
        }

        // Fetch each player's real Distance (public UserData). No estimates/fakes.
        int remaining = entries.Count;
        foreach (var e in entries)
        {
            var dataReq = new GetUserDataRequest
            {
                PlayFabId = e.PlayFabId,
                Keys = new List<string> { "Distance" }
            };

            PlayFabClientAPI.GetUserData(dataReq,
                r =>
                {
                    if (r.Data != null && r.Data.ContainsKey("Distance"))
                        int.TryParse(r.Data["Distance"].Value, out e.Score);

                    remaining--;
                    if (remaining == 0) OnLeaderboardLoaded?.Invoke(entries);
                },
                err =>
                {
                    Debug.LogWarning("Distance fetch failed for " + e.PlayerName + ": " + err.ErrorMessage);
                    remaining--;
                    if (remaining == 0) OnLeaderboardLoaded?.Invoke(entries);
                });
        }
    }

    void OnFetchError(PlayFabError error)
    {
        Debug.LogError("Leaderboard fetch error: " + error.ErrorMessage);
        OnLeaderboardError?.Invoke(error.ErrorMessage);
    }
    // ═══════════════════════════════════════════════════════
    //  MY RANK (used by the Search feature when not in Top 10)
    // ═══════════════════════════════════════════════════════

    public void FetchMyRank()
    {
        if (!PlayFabClientAPI.IsClientLoggedIn())
        {
            OnMyRankError?.Invoke("Not logged in");
            return;
        }

        var request = new GetLeaderboardAroundPlayerRequest
        {
            StatisticName = SOLO_LEADERBOARD,
            MaxResultsCount = 1,
            ProfileConstraints = new PlayerProfileViewConstraints { ShowDisplayName = true }
        };

        PlayFabClientAPI.GetLeaderboardAroundPlayer(request, OnMyRankSuccess, OnMyRankFail);
    }

    void OnMyRankSuccess(GetLeaderboardAroundPlayerResult result)
    {
        if (result.Leaderboard == null || result.Leaderboard.Count == 0)
        {
            OnMyRankError?.Invoke("No score submitted yet.");
            return;
        }

        var item = result.Leaderboard[0];
        var entry = new LeaderboardEntry
        {
            Rank = item.Position + 1,
            PlayerName = string.IsNullOrEmpty(item.DisplayName) ? "You" : item.DisplayName,
            Coins = item.StatValue,
            Score = 0,
            PlayFabId = item.PlayFabId
        };

        var dataReq = new GetUserDataRequest
        {
            PlayFabId = entry.PlayFabId,
            Keys = new List<string> { "Distance" }
        };
        PlayFabClientAPI.GetUserData(dataReq,
            r =>
            {
                if (r.Data != null && r.Data.ContainsKey("Distance"))
                    int.TryParse(r.Data["Distance"].Value, out entry.Score);
                OnMyRankLoaded?.Invoke(entry);
            },
            err => OnMyRankLoaded?.Invoke(entry));
    }

    void OnMyRankFail(PlayFabError error)
    {
        OnMyRankError?.Invoke(error.ErrorMessage);
    }
}

// ── Data Model ────────────────────────────────────────────────
[Serializable]
public class LeaderboardEntry
{
    public int Rank;
    public string PlayerName;
    public int Score;       // distance in meters (real, fetched per player)
    public int Coins;       // coins earned (real ranking stat)
    public string PlayFabId;
}