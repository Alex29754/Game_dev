using UnityEngine;

public class LabCameraFollow : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0f, 6.5f, -9f);
    public float followSpeed = 8f;
    public float fovSpeed = 6f;

    private Camera followCamera;
    private float baseFov;
    private float shakeTimer;
    private float shakeIntensity;

    private void Awake()
    {
        followCamera = GetComponent<Camera>();

        if (followCamera != null)
        {
            baseFov = followCamera.fieldOfView;
        }
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        Vector3 desiredPosition = target.position + offset;

        if (shakeTimer > 0f)
        {
            shakeTimer -= Time.unscaledDeltaTime;
            desiredPosition += Random.insideUnitSphere * shakeIntensity;
        }

        transform.position = Vector3.Lerp(transform.position, desiredPosition, followSpeed * Time.deltaTime);
        transform.LookAt(target.position + Vector3.up * 0.8f);

        LabPlayerController controller = target.GetComponent<LabPlayerController>();

        if (followCamera != null && controller != null)
        {
            float targetFov = baseFov + (controller.IsSprinting ? 5f : 0f);
            followCamera.fieldOfView = Mathf.Lerp(followCamera.fieldOfView, targetFov, fovSpeed * Time.deltaTime);
        }
    }

    public void Shake(float intensity, float duration)
    {
        shakeIntensity = Mathf.Max(shakeIntensity, intensity);
        shakeTimer = Mathf.Max(shakeTimer, duration);
    }
}
