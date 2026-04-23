using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class LabPlayerController : MonoBehaviour
{
    public float speed = 5.5f;
    public float sprintSpeed = 8f;
    public float acceleration = 22f;
    public float deceleration = 28f;
    public float jumpForce = 6.5f;
    public float groundCheckDistance = 1.12f;
    public float groundCheckRadius = 0.28f;
    public float rotationSpeed = 12f;

    private Rigidbody rb;
    private Collider[] ownColliders;
    private Vector3 movement;
    private Vector3 currentVelocity;
    private bool isSprinting;
    private bool jumpQueued;
    private bool isGrounded;

    public bool InputEnabled { get; set; } = true;
    public bool IsMoving => currentVelocity.sqrMagnitude > 0.08f;
    public bool IsSprinting => isSprinting && IsMoving;
    public bool IsGrounded => isGrounded;
    public float Speed01 => Mathf.Clamp01(currentVelocity.magnitude / sprintSpeed);

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        ownColliders = GetComponentsInChildren<Collider>();
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.constraints = RigidbodyConstraints.FreezeRotation;
    }

    private void Update()
    {
        if (!InputEnabled)
        {
            movement = Vector3.zero;
            isSprinting = false;
            jumpQueued = false;
            return;
        }

        movement = ReadMovement();
        isSprinting = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        if (Input.GetKeyDown(KeyCode.Space))
        {
            jumpQueued = true;
        }

        if (movement.sqrMagnitude > 1f)
        {
            movement.Normalize();
        }
    }

    private void FixedUpdate()
    {
        UpdateGroundedState();

        if (!InputEnabled)
        {
            currentVelocity = Vector3.MoveTowards(currentVelocity, Vector3.zero, deceleration * Time.fixedDeltaTime);
            rb.linearVelocity = new Vector3(currentVelocity.x, rb.linearVelocity.y, currentVelocity.z);
            return;
        }

        float targetSpeed = isSprinting ? sprintSpeed : speed;
        Vector3 targetVelocity = movement * targetSpeed;
        float rate = movement.sqrMagnitude > 0.001f ? acceleration : deceleration;

        currentVelocity = Vector3.MoveTowards(currentVelocity, targetVelocity, rate * Time.fixedDeltaTime);
        rb.linearVelocity = new Vector3(currentVelocity.x, rb.linearVelocity.y, currentVelocity.z);

        if (jumpQueued && isGrounded)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            rb.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange);
            isGrounded = false;
        }

        jumpQueued = false;

        if (currentVelocity.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(currentVelocity.normalized, Vector3.up);
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime));
        }
    }

    public void TeleportTo(Vector3 position)
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        currentVelocity = Vector3.zero;
        jumpQueued = false;
        rb.position = position;
        transform.position = position;
    }

    private void UpdateGroundedState()
    {
        Vector3 origin = transform.position + Vector3.up * 0.15f;
        RaycastHit[] hits = Physics.SphereCastAll(
            origin,
            groundCheckRadius,
            Vector3.down,
            groundCheckDistance,
            ~0,
            QueryTriggerInteraction.Ignore);

        isGrounded = false;

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null || IsOwnCollider(hit.collider))
            {
                continue;
            }

            if (Vector3.Dot(hit.normal, Vector3.up) < 0.45f)
            {
                continue;
            }

            isGrounded = true;
            return;
        }
    }

    private bool IsOwnCollider(Collider candidate)
    {
        foreach (Collider ownCollider in ownColliders)
        {
            if (candidate == ownCollider)
            {
                return true;
            }
        }

        return false;
    }

    private static Vector3 ReadMovement()
    {
        float x = 0f;
        float z = 0f;

        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
        {
            x -= 1f;
        }

        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
        {
            x += 1f;
        }

        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
        {
            z -= 1f;
        }

        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
        {
            z += 1f;
        }

        return new Vector3(x, 0f, z);
    }
}
