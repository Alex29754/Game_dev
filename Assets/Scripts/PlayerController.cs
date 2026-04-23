using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float speed = 5f;

    private Rigidbody rb;
    private Vector3 movement;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.constraints |= RigidbodyConstraints.FreezeRotation;
        }
    }

    private void Update()
    {
        if (rb == null)
        {
            movement = Vector3.zero;
            return;
        }

        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");

        movement = new Vector3(moveX, 0, moveZ);

        if (movement.sqrMagnitude > 1f)
        {
            movement.Normalize();
        }
    }

    private void FixedUpdate()
    {
        if (rb == null)
        {
            return;
        }

        rb.MovePosition(rb.position + movement * speed * Time.fixedDeltaTime);
    }
}
