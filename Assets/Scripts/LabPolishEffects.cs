using UnityEngine;

public class LabFloatingAnimation : MonoBehaviour
{
    public float bobHeight = 0.15f;
    public float bobSpeed = 2f;
    public float rotationSpeed = 45f;
    public float pulseAmount = 0.04f;

    private Vector3 startLocalPosition;
    private Vector3 startScale;
    private float seed;

    private void Start()
    {
        startLocalPosition = transform.localPosition;
        startScale = transform.localScale;
        seed = Random.Range(0f, 10f);
    }

    private void Update()
    {
        float wave = Mathf.Sin(Time.time * bobSpeed + seed);
        transform.localPosition = startLocalPosition + Vector3.up * wave * bobHeight;

        if (Mathf.Abs(rotationSpeed) > 0.01f)
        {
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
        }

        if (pulseAmount > 0f)
        {
            float pulse = 1f + Mathf.Sin(Time.time * bobSpeed * 1.3f + seed) * pulseAmount;
            transform.localScale = startScale * pulse;
        }
    }
}

public class LabCharacterAnimator : MonoBehaviour
{
    public LabPlayerController controller;
    public float bobHeight = 0.08f;
    public float squashAmount = 0.08f;

    private Vector3 startLocalPosition;
    private Vector3 startScale;
    private float stepTime;

    private void Start()
    {
        startLocalPosition = transform.localPosition;
        startScale = transform.localScale;

        if (controller == null)
        {
            controller = GetComponentInParent<LabPlayerController>();
        }
    }

    private void Update()
    {
        float speed01 = controller != null ? controller.Speed01 : 0f;
        stepTime += Time.deltaTime * Mathf.Lerp(2f, 10f, speed01);

        float bob = Mathf.Abs(Mathf.Sin(stepTime)) * bobHeight * speed01;
        float squash = Mathf.Sin(stepTime * 2f) * squashAmount * speed01;

        transform.localPosition = startLocalPosition + Vector3.up * bob;
        transform.localScale = new Vector3(
            startScale.x * (1f + squash * 0.35f),
            startScale.y * (1f - squash),
            startScale.z * (1f + squash * 0.35f));
    }
}

public class LabAutoDestroy : MonoBehaviour
{
    public float lifetime = 2f;

    private void Start()
    {
        Destroy(gameObject, lifetime);
    }
}
