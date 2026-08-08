using Fusion;
using UnityEngine;

public class NetworkGameManager : NetworkBehaviour
{
    // ---- Match state ----
    [Networked] public NetworkBool IsGameOver { get; set; }
    [Networked] public int WinnerSide { get; set; }
    [Networked] public NetworkBool MonsterClose { get; set; }
    [Networked] private float CloseTimer { get; set; }
    [Networked] private float HitCooldown { get; set; }
    [Networked] public int HitCount { get; set; }
    [Networked] public int FinalLeftCoins { get; set; }
    [Networked] public int FinalRightCoins { get; set; }

    // ---- Lobby / match start ----
    [Networked] public NetworkBool MatchStarted { get; set; }
    [Networked] private float StartDelay { get; set; }

    // ---- Temple Run intro (monster runs with players at start) ----
    [Networked] private NetworkBool IntroStarted { get; set; }
    [Networked] private NetworkBool IntroDone { get; set; }
    [Networked] private float IntroTimer { get; set; }

    /// <summary>True when the match is actually allowed to run (after loading).</summary>
    public bool RunningAllowed => MatchStarted && StartDelay <= 0f;

    public void StartMatch()
    {
        if (HasStateAuthority && !MatchStarted)
        {
            MatchStarted = true;
            StartDelay = 1.5f;   // "LOADING..." window on both screens
        }
    }

    [Header("Intro (monster runs with players at start)")]
    [SerializeField] private float introSeconds = 3.5f;
    [SerializeField] private float introDistance = 5f;

    [Header("Monster Warning")]
    [SerializeField] private float monsterCloseTime = 10f;
    [SerializeField] private float hitCooldownTime = 0.5f;

    [Header("Monster - found at runtime by TAG")]
    [SerializeField] private string monsterTag = "Monster";
    [SerializeField] private float farDistance = 18f;
    [SerializeField] private float closeDistance = 4f;
    [SerializeField] private float attackDistance = 1.5f;
    [SerializeField] private float monsterY = 0f;
    [SerializeField] private float followSmooth = 3f;
    [SerializeField] private string playerTag = "Player";

    [Header("Monster animation (set to your Animator's trigger name)")]
    [SerializeField] private string monsterAttackTrigger = "Attack";

    [Header("Game over drama (seconds before fade starts)")]
    [SerializeField] private float dramaSeconds = 2f;

    [Header("Result wording")]
    [SerializeField] private string winText = "YOU WIN";
    [SerializeField] private string loseText = "YOU LOSE";
    [SerializeField] private string drawText = "DRAW";

    [Header("GameOverPanel object name")]
    [SerializeField] private string gameOverPanelName = "GameOverPanel";

    [Header("Hide these gameplay UI objects on game over")]
    [SerializeField]
    private string[] hideOnGameOver = new string[]
    {
        "P1_HUD", "P2_HUD", "Btn_Left", "Btn_Right", "Btn_Jump", "Btn_Slide"
    };

    private bool localHandled;
    private float currentDistance;
    private Transform monster;
    private Animator monsterAnimator;
    private bool monsterAttacked;
    private Transform[] players;
    private int prevHitCountLocal;
    private bool prevMonsterCloseLocal;

    public override void Spawned()
    {
        NetworkConnector.GameManager = this;
        currentDistance = farDistance;
        monster = FindMonster();
    }

    private Transform FindMonster()
    {
        try
        {
            GameObject m = GameObject.FindGameObjectWithTag(monsterTag);
            return (m != null) ? m.transform : null;
        }
        catch { return null; }
    }

    private Transform[] GetPlayers()
    {
        bool needRefresh = players == null || players.Length == 0;
        if (!needRefresh)
            for (int i = 0; i < players.Length; i++)
                if (players[i] == null) { needRefresh = true; break; }
        if (needRefresh)
        {
            GameObject[] gos = GameObject.FindGameObjectsWithTag(playerTag);
            players = new Transform[gos.Length];
            for (int i = 0; i < gos.Length; i++) players[i] = gos[i].transform;
        }
        return players;
    }

    public void ReportHurdleHit(HurdleType.Kind type)
    {
        // No hits count in the lobby / loading - only during the real match.
        if (!HasStateAuthority || IsGameOver || !RunningAllowed) return;
        Debug.Log("HURDLE HIT: " + type + " | HitCount will be " + (HitCount + 1) + " | time " + Time.time);

        switch (type)
        {
            case HurdleType.Kind.Jump:
                if (HitCooldown > 0f) break;
                HitCount++;
                if (MonsterClose) TriggerGameOver();
                else { MonsterClose = true; CloseTimer = monsterCloseTime; }
                HitCooldown = hitCooldownTime;
                break;

            case HurdleType.Kind.Wall:
                HitCount++;
                TriggerGameOver();
                break;

            case HurdleType.Kind.ChainKnife:
                HitCount++;
                TriggerGameOver();
                break;
        }
    }

    private void TriggerGameOver()
    {
        ComputeWinner();
        IsGameOver = true;
        MonsterClose = true;
        Debug.Log("GAME OVER. WinnerSide = " + WinnerSide);
    }

    private void ComputeWinner()
    {
        int leftCoins = 0, rightCoins = 0;
        foreach (var pc in FindObjectsOfType<NetworkPlayerController>())
        {
            if (pc == null || pc.Object == null || !pc.Object.IsValid) continue;   // despawn guard
            if (pc.PlayerSide == 0) leftCoins = pc.Coins;
            else rightCoins = pc.Coins;
        }
        FinalLeftCoins = leftCoins;
        FinalRightCoins = rightCoins;

        if (leftCoins > rightCoins) WinnerSide = 0;
        else if (rightCoins > leftCoins) WinnerSide = 1;
        else WinnerSide = 2;
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;

        if (HitCooldown > 0f) HitCooldown -= Runner.DeltaTime;
        if (MatchStarted && StartDelay > 0f) StartDelay -= Runner.DeltaTime;

        // ---- INTRO: starts the moment the match is allowed to run ----
        if (!IntroDone)
        {
            if (!IntroStarted)
            {
                if (RunningAllowed)
                {
                    IntroStarted = true;
                    IntroTimer = introSeconds;
                }
            }
            else
            {
                IntroTimer -= Runner.DeltaTime;
                if (IntroTimer <= 0f) IntroDone = true;
            }
        }

        if (IsGameOver) return;

        if (MonsterClose && CloseTimer > 0f)
        {
            CloseTimer -= Runner.DeltaTime;
            if (CloseTimer <= 0f) MonsterClose = false;
        }
    }

    public override void Render()
    {
        MoveMonster();

        // Roar ONLY during the real match: first Jump-hit warning (game-over roar
        // is played separately in HandleGameOver, so !IsGameOver avoids doubles).
        bool roarState = RunningAllowed && IntroDone && MonsterClose && !IsGameOver;
        if (roarState && !prevMonsterCloseLocal && AudioManager.I != null)
            AudioManager.I.PlayRoar();
        prevMonsterCloseLocal = roarState;

        if (IsGameOver && !localHandled)
        {
            localHandled = true;
            HandleGameOver();
        }

        // Shake on EVERY hurdle hit (networked - both screens).
        if (HitCount != prevHitCountLocal)
        {
            prevHitCountLocal = HitCount;
            CameraFollow cam = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
            if (cam != null) cam.Shake(0.35f, 0.3f);
        }
    }

    private void MoveMonster()
    {
        if (monster == null) { monster = FindMonster(); if (monster == null) return; }
        if (monsterAnimator == null) monsterAnimator = monster.GetComponentInChildren<Animator>();

        Transform[] ps = GetPlayers();
        if (ps.Length == 0) return;

        float leadZ = float.NegativeInfinity;
        for (int i = 0; i < ps.Length; i++)
            if (ps[i] != null && ps[i].position.z > leadZ) leadZ = ps[i].position.z;
        if (leadZ == float.NegativeInfinity) return;

        bool introActive = IntroStarted && !IntroDone;

        float target =
            IsGameOver ? attackDistance :
            introActive ? introDistance :
            MonsterClose ? closeDistance : farDistance;

        currentDistance = Mathf.Lerp(currentDistance, target, followSmooth * Time.deltaTime);

        // Ground-level chase - no jump. Runners' knockback already moved them
        // back toward the monster, so at attackDistance he stands right on them.
        monster.position = new Vector3(0f, monsterY, leadZ - currentDistance);

        // Attack starts the moment he has ARRIVED at the fallen runners.
        if (monsterAnimator != null && IsGameOver && !monsterAttacked
            && currentDistance <= attackDistance + 0.3f
            && !string.IsNullOrEmpty(monsterAttackTrigger))
        {
            monsterAttacked = true;
            monsterAnimator.SetTrigger(monsterAttackTrigger);   // Loop Time ON => attacks till board
        }
    }

    private void HandleGameOver()
    {
        if (AudioManager.I != null) AudioManager.I.PlayRoar();

        foreach (var pc in FindObjectsOfType<NetworkPlayerController>())
            pc.StopPlayer();

        HideGameplayUI();

        StartCoroutine(GameOverSequence());
    }

    private System.Collections.IEnumerator GameOverSequence()
    {
        yield return new WaitForSeconds(dramaSeconds);

        ScreenFader fader = FindObjectOfType<ScreenFader>(true);
        if (fader != null) fader.FadeToBlack(ShowBoard);
        else { Debug.LogWarning("[Fade] ScreenFader NOT FOUND in scene - showing board without fade."); ShowBoard(); }
    }

    private void HideGameplayUI()
    {
        if (hideOnGameOver == null) return;
        for (int i = 0; i < hideOnGameOver.Length; i++)
        {
            if (string.IsNullOrEmpty(hideOnGameOver[i])) continue;
            GameObject go = GameObject.Find(hideOnGameOver[i]);
            if (go != null) go.SetActive(false);
        }
    }

    private void ShowBoard()
    {
        GameObject board = null;
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas != null)
        {
            Transform t = canvas.transform.Find(gameOverPanelName);
            if (t != null) board = t.gameObject;
        }
        if (board == null) { Debug.LogWarning("[Board] GameOverPanel not found by name."); return; }

        board.SetActive(true);

        GameOverBoard go = board.GetComponent<GameOverBoard>();
        if (go == null)
        {
            Debug.LogWarning("[Board] Add the GameOverBoard component to GameOverPanel and drag the texts in.");
            return;
        }

        int localSide = -1;
        foreach (var pc in FindObjectsOfType<NetworkPlayerController>())
        {
            if (pc == null || pc.Object == null || !pc.Object.IsValid) continue;
            if (pc.HasInputAuthority) localSide = pc.PlayerSide;
        }

        string result;
        if (WinnerSide == 2 || localSide == -1) result = drawText;
        else result = (WinnerSide == localSide) ? winText : loseText;

        go.Show(result, FinalLeftCoins, FinalRightCoins);
    }
}