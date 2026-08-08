using UnityEngine;

/// <summary>
/// Attach to: a GameObject named "ScoreManager".
/// Keeps track of both players' coins and the distance.
/// </summary>
public class ScoreManager : MonoBehaviour
{
    public static int LeftCoins { get; private set; }
    public static int RightCoins { get; private set; }
    public static float Distance { get; private set; }

    [SerializeField] private Transform player;  // assign Player_Parent (for distance)
    private float startZ;

    private void Awake()
    {
        // Reset on scene start.
        LeftCoins = 0;
        RightCoins = 0;
        Distance = 0f;
    }

    private void Start()
    {
        if (player != null) startZ = player.position.z;
    }

    private void Update()
    {
        if (player != null)
            Distance = player.position.z - startZ;
    }

    public static void AddCoinLeft() => LeftCoins++;
    public static void AddCoinRight() => RightCoins++;
}
