using Fusion;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Networked player controller (Host Mode + input) + ANIMATION driving.
/// RESPONSIVENESS UPDATE:
///  - Jump/Slide buttons now fire on PointerDown (press) instead of onClick (release)
///  - Local animation prediction: input-authority player animates INSTANTLY,
///    without waiting for the network round-trip.
/// PUT THIS ON: the player prefab (same as before).
/// </summary>
public class NetworkPlayerController : NetworkBehaviour
{
    [Header("Forward Movement")]
    [SerializeField] private float baseForwardSpeed = 8f;

    [Header("Lateral Steering")]
    [SerializeField] private float steerSpeed = 4f;
    [SerializeField] private float laneHalfWidth = 2.5f;

    [Header("Jump")]
    [SerializeField] private float jumpHeight = 2.2f;
    [SerializeField] private float gravity = -20f;

    [Header("Slide")]
    [SerializeField] private float slideDuration = 0.8f;
    [SerializeField] private CapsuleCollider playerCollider;

    [Header("Synced Start")]
    [SerializeField] private int playersNeededToStart = 2;
    [SerializeField] private string playerTag = "Player";

    [Header("Animation (set names to match your Animator)")]
    [SerializeField] private Animator animator;
    [Tooltip("Optional bool, true while running. Leave empty if your default state is already Run.")]
    [SerializeField] private string runBool = "";
    [SerializeField] private string jumpTrigger = "Jump";
    [SerializeField] private string slideBool = "Slide";
    [SerializeField] private string deathTrigger = "Die";
    [Header("Audio")]
    [SerializeField] private bool isFemale;  
    private int prevCoinsLocal;

    [Networked] private float PlayerX { get; set; }
    [Networked] private float PlayerY { get; set; }
    [Networked] private float PlayerZ { get; set; }
    [Networked] private float VerticalVelocity { get; set; }
    [Networked] private NetworkBool IsGrounded { get; set; }
    [Networked] private NetworkBool IsSliding { get; set; }
    [Networked] private float SlideTimer { get; set; }
    [Networked] private NetworkBool Stopped { get; set; }

    [Networked] public int Coins { get; set; }
    [Networked] public int PlayerSide { get; set; }   // 0 = left, 1 = right

    public void StopPlayer()
    {
        Debug.Log("STOP PLAYER CALLED! HasStateAuthority=" + HasStateAuthority);
        if (HasStateAuthority)
        {
            Stopped = true;
            PlayerZ -= 1.4f;   // knockback
        }
    }

    public void AddCoin()
    {
        if (HasStateAuthority) Coins++;
    }

    private float baseGroundY;
    private float normalColliderHeight;
    private CameraFollow cachedCamera;

    // animation transition tracking
    private bool prevGrounded = true;
    private bool prevStopped = false;

    // LOCAL PREDICTION: instant animation for the local player
    private float localSlideAnimTimer;

    public override void Spawned()
    {
        baseGroundY = transform.position.y;
        if (playerCollider == null) playerCollider = GetComponent<CapsuleCollider>();
        if (playerCollider != null) normalColliderHeight = playerCollider.height;
        if (animator == null) animator = GetComponentInChildren<Animator>();

        if (HasStateAuthority)
        {
            PlayerX = transform.position.x;
            PlayerY = transform.position.y;
            PlayerZ = transform.position.z;
            IsGrounded = true;
            PlayerSide = (transform.position.x < 0f) ? 0 : 1;
        }

        if (HasInputAuthority)
        {
            ConnectButtons();

            if (Camera.main != null)
            {
                cachedCamera = Camera.main.GetComponent<CameraFollow>();
                if (cachedCamera != null) cachedCamera.SetTarget(transform);
            }

            TrackSpawner track = FindObjectOfType<TrackSpawner>();
            if (track != null) track.SetPlayer(transform);

            CoinSpawner coins = FindObjectOfType<CoinSpawner>();
            if (coins != null) coins.SetPlayer(transform);

            ObstacleSpawner obstacles = FindObjectOfType<ObstacleSpawner>();
            if (obstacles != null) obstacles.SetPlayer(transform);
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;

        if (NetworkConnector.GameManager == null || !NetworkConnector.GameManager.RunningAllowed) return;

        if (Stopped) return;

        float dt = Runner.DeltaTime;

        PlayerZ += baseForwardSpeed * dt;

        if (GetInput(out NetworkInputData input))
        {
            float dir = 0f;
            if (input.SteerLeft) dir -= 1f;
            if (input.SteerRight) dir += 1f;
            if (dir != 0f)
                PlayerX = Mathf.Clamp(PlayerX + dir * steerSpeed * dt, -laneHalfWidth, laneHalfWidth);

            if (input.Jump && IsGrounded && !IsSliding)
            {
                VerticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                IsGrounded = false;
            }

            if (input.Slide && IsGrounded && !IsSliding)
            {
                IsSliding = true;
                SlideTimer = slideDuration;
            }
        }

        if (!IsGrounded)
        {
            VerticalVelocity += gravity * dt;
            PlayerY += VerticalVelocity * dt;
            if (PlayerY <= baseGroundY)
            {
                PlayerY = baseGroundY;
                VerticalVelocity = 0f;
                IsGrounded = true;
            }
        }

        if (IsSliding)
        {
            SlideTimer -= dt;
            if (SlideTimer <= 0f) IsSliding = false;
        }

        transform.position = new Vector3(PlayerX, PlayerY, PlayerZ);
    }

    public override void Render()
    {
        if (playerCollider != null)
        {
            float h = IsSliding ? normalColliderHeight * 0.5f : normalColliderHeight;
            playerCollider.height = h;
        }
        if (HasInputAuthority && Coins != prevCoinsLocal)
        {
            prevCoinsLocal = Coins;
            if (AudioManager.I != null) AudioManager.I.PlayCoin();
        }
        DriveAnimation();

        if (HasInputAuthority && cachedCamera != null)
            cachedCamera.DriveFromRender();
    }

    private void DriveAnimation()
    {
        if (animator == null) return;

        
        if (!string.IsNullOrEmpty(runBool)) animator.SetBool(runBool, !Stopped);


        if (!HasInputAuthority && !string.IsNullOrEmpty(jumpTrigger) && prevGrounded && !IsGrounded)
        {
            animator.SetTrigger(jumpTrigger);
            if (AudioManager.I != null
                && NetworkConnector.GameManager != null
                && NetworkConnector.GameManager.RunningAllowed)
                AudioManager.I.PlayJump(isFemale);
        }
        prevGrounded = IsGrounded;


        if (localSlideAnimTimer > 0f) localSlideAnimTimer -= Time.deltaTime;
        bool slidingAnim = IsSliding || (HasInputAuthority && localSlideAnimTimer > 0f);
        if (!string.IsNullOrEmpty(slideBool)) animator.SetBool(slideBool, slidingAnim);

       
        if (!string.IsNullOrEmpty(deathTrigger) && !prevStopped && Stopped)
        {
            animator.SetTrigger(deathTrigger);
            Debug.Log("DIE TRIGGER FIRED on " + gameObject.name);
            if (AudioManager.I != null) AudioManager.I.PlayDeath(isFemale);
        }
        prevStopped = Stopped;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!HasStateAuthority) return;

        HurdleType ht = other.GetComponentInParent<HurdleType>();
        if (ht != null && NetworkConnector.GameManager != null)
            NetworkConnector.GameManager.ReportHurdleHit(ht.Type);
    }

    private void ConnectButtons()
    {
        WireHold("Btn_Left",
            () => NetworkConnector.L_SteerLeft = true,
            () => NetworkConnector.L_SteerLeft = false);
        WireHold("Btn_Right",
            () => NetworkConnector.L_SteerRight = true,
            () => NetworkConnector.L_SteerRight = false);

        // RESPONSIVE: fire on PRESS (PointerDown), not release + instant local animation
        WirePress("Btn_Jump", () =>
        {
            NetworkConnector.L_Jump = true;
            if (animator != null && IsGrounded && !IsSliding && !Stopped
                && !string.IsNullOrEmpty(jumpTrigger))
                animator.SetTrigger(jumpTrigger);
            if (AudioManager.I != null) AudioManager.I.PlayJump(isFemale);

        });
        WirePress("Btn_Slide", () =>
        {
            NetworkConnector.L_Slide = true;
            if (IsGrounded && !IsSliding && !Stopped)
                localSlideAnimTimer = slideDuration;
            if (AudioManager.I != null) AudioManager.I.PlaySlide();
        });
    }

    /// <summary>Fires the action the moment the finger touches the button (PointerDown).</summary>
    private void WirePress(string buttonName, System.Action onDown)
    {
        GameObject go = GameObject.Find(buttonName);
        if (go == null) { Debug.LogWarning("Button not found: " + buttonName); return; }

        EventTrigger trigger = go.GetComponent<EventTrigger>();
        if (trigger == null) trigger = go.AddComponent<EventTrigger>();

        var down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        down.callback.AddListener((_) => onDown());
        trigger.triggers.Add(down);
    }

    private void WireHold(string buttonName, System.Action onDown, System.Action onUp)
    {
        GameObject go = GameObject.Find(buttonName);
        if (go == null) { Debug.LogWarning("Button not found: " + buttonName); return; }

        EventTrigger trigger = go.GetComponent<EventTrigger>();
        if (trigger == null) trigger = go.AddComponent<EventTrigger>();

        var down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        down.callback.AddListener((_) => onDown());
        trigger.triggers.Add(down);

        var up = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
        up.callback.AddListener((_) => onUp());
        trigger.triggers.Add(up);
    }
}