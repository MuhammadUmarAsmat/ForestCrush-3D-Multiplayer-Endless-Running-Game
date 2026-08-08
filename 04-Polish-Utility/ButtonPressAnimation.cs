using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Juicy press animation for UI buttons: squeezes DOWN instantly on press,
/// springs back with a tiny overshoot on release. No Animator needed.
/// PUT ON: any UI Button (works alongside Button component + EventTriggers).
/// </summary>
public class ButtonPressAnimation : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [Tooltip("Dabane par kitna sikude (0.9 = 90% size)")]
    [SerializeField] private float pressedScale = 0.9f;
    [Tooltip("Wapas aate waqt kitna uchhle (1.05 = halka bounce)")]
    [SerializeField] private float overshootScale = 1.05f;
    [Tooltip("Wapas aane ki raftar")]
    [SerializeField] private float speed = 12f;

    private Vector3 baseScale;
    private bool held;

    private void Awake()
    {
        baseScale = transform.localScale;
    }

    private void OnEnable()
    {
        transform.localScale = baseScale;
        held = false;
    }

    public void OnPointerDown(PointerEventData e)
    {
        held = true;
        transform.localScale = baseScale * pressedScale;   // instant squeeze
    }

    public void OnPointerUp(PointerEventData e)
    {
        held = false;
        transform.localScale = baseScale * overshootScale; // spring start
    }

    private void Update()
    {
        if (held) return;   // dabaye rakha hai to sikuda rahe
        // Smoothly settle back to normal size.
        transform.localScale = Vector3.Lerp(
            transform.localScale, baseScale, speed * Time.unscaledDeltaTime);
    }
}