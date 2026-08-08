using UnityEngine;
using TMPro;

/// <summary>
/// MULTIPLAYER HUD - COINS ONLY (distance removed; distance stays in single mode).
/// Shows each player's live coin count. Assign only the two coin number texts.
///
/// PUT THIS ON: the Canvas (replaces the previous NetworkScoreUI).
/// </summary>
public class NetworkScoreUI : MonoBehaviour
{
    [Header("Coin number texts (the coin bars)")]
    [SerializeField] private TMP_Text p1Coins;   // left player (host)
    [SerializeField] private TMP_Text p2Coins;   // right player (client)

    private void Update()
    {
        NetworkPlayerController[] players = FindObjectsOfType<NetworkPlayerController>();
        for (int i = 0; i < players.Length; i++)
        {
            NetworkPlayerController p = players[i];
            if (p.PlayerSide == 0)
            {
                if (p1Coins != null) p1Coins.text = NumberFormat.Coins(p.Coins);
            }
            else
            {
                if (p2Coins != null) p2Coins.text = NumberFormat.Coins(p.Coins);
            }
        }
    }
}