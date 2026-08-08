using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class LeaderboardUI : MonoBehaviour
{
    [Header("List References")]
    [SerializeField] private Transform rowsContainer;
    [SerializeField] private GameObject rowTemplate;
    [SerializeField] private TextMeshProUGUI statusText;

    [Header("Search References")]
    [SerializeField] private TMP_InputField searchInput;
    [SerializeField] private Button searchButton;
    [SerializeField] private GameObject myRankRow;
    [SerializeField] private TextMeshProUGUI myRankRankText;
    [SerializeField] private TextMeshProUGUI myRankNameText;
    [SerializeField] private TextMeshProUGUI myRankDistanceText;
    [SerializeField] private TextMeshProUGUI myRankCoinsText;

    [Header("Highlight Colors")]
    [SerializeField] private Color highlightColor = new Color(1f, 0.82f, 0f, 0.35f);
    [SerializeField] private Color normalColor = new Color(0f, 0f, 0f, 0f);

    private readonly List<GameObject> spawnedRows = new List<GameObject>();
    private readonly List<LeaderboardEntry> currentEntries = new List<LeaderboardEntry>();
    private GameObject lastHighlightedRow;

    private void Start()
    {
        if (AudioManager.I != null) AudioManager.I.PlayLeaderboardMusic();
        if (myRankRow != null) myRankRow.SetActive(false);

        if (statusText != null)
        {
            statusText.text = "Loading...";
            statusText.gameObject.SetActive(true);
        }

        if (searchButton != null) searchButton.onClick.AddListener(OnSearchClicked);

        if (LeaderboardManager.Instance != null)
        {
            LeaderboardManager.Instance.OnLeaderboardLoaded += HandleLoaded;
            LeaderboardManager.Instance.OnLeaderboardError += HandleError;
            LeaderboardManager.Instance.OnMyRankLoaded += HandleMyRankLoaded;
            LeaderboardManager.Instance.OnMyRankError += HandleMyRankError;
            LeaderboardManager.Instance.FetchSoloTop10();
        }
        else
        {
            if (statusText != null) statusText.text = "Leaderboard unavailable.";
        }
    }

    private void OnDestroy()
    {
        if (LeaderboardManager.Instance != null)
        {
            LeaderboardManager.Instance.OnLeaderboardLoaded -= HandleLoaded;
            LeaderboardManager.Instance.OnLeaderboardError -= HandleError;
            LeaderboardManager.Instance.OnMyRankLoaded -= HandleMyRankLoaded;
            LeaderboardManager.Instance.OnMyRankError -= HandleMyRankError;
        }
    }

    

    private void HandleLoaded(List<LeaderboardEntry> entries)
    {
        ClearRows();
        currentEntries.Clear();
        lastHighlightedRow = null;
        if (myRankRow != null) myRankRow.SetActive(false);

        if (entries.Count == 0)
        {
            if (statusText != null)
            {
                statusText.text = "No scores yet. Be the first!";
                statusText.gameObject.SetActive(true);
            }
            return;
        }

        if (statusText != null) statusText.gameObject.SetActive(false);

        foreach (var entry in entries)
        {
            GameObject row = Instantiate(rowTemplate, rowsContainer);
            row.SetActive(true);
            spawnedRows.Add(row);
            currentEntries.Add(entry);

            var rank = row.transform.Find("RankText")?.GetComponent<TextMeshProUGUI>();
            var name = row.transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
            var dist = row.transform.Find("DistanceText")?.GetComponent<TextMeshProUGUI>();
            var coins = row.transform.Find("CoinsText")?.GetComponent<TextMeshProUGUI>();

            if (rank != null) rank.text = entry.Rank.ToString();
            if (name != null) name.text = entry.PlayerName;
            if (dist != null) dist.text = NumberFormat.Distance(entry.Score);
            if (coins != null) coins.text = NumberFormat.Coins(entry.Coins);

            var img = row.GetComponent<Image>();
            if (img != null) img.color = normalColor;
        }
    }

    private void HandleError(string message)
    {
        ClearRows();
        currentEntries.Clear();
        if (statusText != null)
        {
            statusText.text = "Could not load leaderboard.";
            statusText.gameObject.SetActive(true);
        }
        Debug.LogWarning("Leaderboard error: " + message);
    }

    private void ClearRows()
    {
        foreach (var row in spawnedRows) Destroy(row);
        spawnedRows.Clear();
    }

    

    private void OnSearchClicked()
    {
        string typed = searchInput != null ? searchInput.text.Trim() : "";
        if (string.IsNullOrEmpty(typed)) return;

        if (lastHighlightedRow != null)
        {
            var prevImg = lastHighlightedRow.GetComponent<Image>();
            if (prevImg != null) prevImg.color = normalColor;
            lastHighlightedRow = null;
        }
        if (myRankRow != null) myRankRow.SetActive(false);

        for (int i = 0; i < currentEntries.Count; i++)
        {
            if (string.Equals(currentEntries[i].PlayerName, typed, System.StringComparison.OrdinalIgnoreCase))
            {
                var row = spawnedRows[i];
                var img = row.GetComponent<Image>();
                if (img != null) img.color = highlightColor;
                lastHighlightedRow = row;
                return;
            }
        }

        if (statusText != null)
        {
            statusText.text = "Searching...";
        }
        if (LeaderboardManager.Instance != null)
            LeaderboardManager.Instance.FetchMyRank();
    }

    private void HandleMyRankLoaded(LeaderboardEntry entry)
    {
        if (statusText != null) statusText.gameObject.SetActive(false);
        if (myRankRow == null) return;

        if (myRankRankText != null) myRankRankText.text = "#" + entry.Rank;
        if (myRankNameText != null) myRankNameText.text = entry.PlayerName;
        if (myRankDistanceText != null) myRankDistanceText.text = NumberFormat.Distance(entry.Score);
        if (myRankCoinsText != null) myRankCoinsText.text = NumberFormat.Coins(entry.Coins);

        myRankRow.SetActive(true);
    }

    private void HandleMyRankError(string message)
    {
        if (statusText != null)
        {
            statusText.text = "Rank not found.";
            statusText.gameObject.SetActive(true);
        }
        Debug.LogWarning("My rank error: " + message);
    }
}