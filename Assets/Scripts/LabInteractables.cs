using UnityEngine;

public abstract class LabInteractable : MonoBehaviour
{
    [TextArea]
    public string prompt;

    protected LabGameManager manager;

    public void Init(LabGameManager gameManager, string interactionPrompt)
    {
        manager = gameManager;
        prompt = interactionPrompt;
    }

    public abstract void Interact();

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<LabPlayerController>() != null)
        {
            manager.SetActiveInteractable(this);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponentInParent<LabPlayerController>() != null)
        {
            manager.ClearActiveInteractable(this);
        }
    }
}

public class LabDoorButton : LabInteractable
{
    public override void Interact()
    {
        manager.TryOpenDoor();
    }
}

public class LabTeleporter : LabInteractable
{
    public Vector3 targetPosition;

    public override void Interact()
    {
        manager.TeleportPlayer(targetPosition, "Телепорт сработал: игрок перенесен на боковую платформу.");
    }
}

public class LabCollectible : MonoBehaviour
{
    private LabGameManager manager;
    private bool collected;

    public void Init(LabGameManager gameManager)
    {
        manager = gameManager;
    }

    private void Update()
    {
        transform.Rotate(0f, 120f * Time.deltaTime, 0f, Space.World);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (collected || other.GetComponentInParent<LabPlayerController>() == null)
        {
            return;
        }

        collected = true;
        manager.CollectItem(gameObject);
    }
}

public class LabHazard : MonoBehaviour
{
    public int damage = 1;

    private LabGameManager manager;

    public void Init(LabGameManager gameManager, int hazardDamage)
    {
        manager = gameManager;
        damage = hazardDamage;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<LabPlayerController>() != null)
        {
            manager.TakeDamage(damage, "Опасная зона: здоровье уменьшилось.");
        }
    }
}

public class LabMovingHazard : MonoBehaviour
{
    public Vector3 axis = Vector3.right;
    public float amplitude = 2f;
    public float speed = 1.6f;

    private Vector3 startPosition;

    private void Start()
    {
        startPosition = transform.position;
    }

    private void Update()
    {
        transform.position = startPosition + axis.normalized * Mathf.Sin(Time.time * speed) * amplitude;
        transform.Rotate(0f, 90f * Time.deltaTime, 0f, Space.World);
    }
}

public class LabCannonShooter : MonoBehaviour
{
    public float fireInterval = 2.4f;
    public float projectileSpeed = 8.5f;
    public float fireRange = 18f;
    public int damage = 1;

    private LabGameManager manager;
    private Transform target;
    private Material projectileMaterial;
    private float fireTimer;

    public void Init(LabGameManager gameManager, Transform playerTarget, Material material, float interval, float speed, float range)
    {
        manager = gameManager;
        target = playerTarget;
        projectileMaterial = material;
        fireInterval = interval;
        projectileSpeed = speed;
        fireRange = range;
        fireTimer = Random.Range(0.4f, fireInterval);
    }

    private void Update()
    {
        if (manager == null || target == null)
        {
            return;
        }

        fireTimer -= Time.deltaTime;

        if (fireTimer > 0f)
        {
            return;
        }

        Vector3 muzzlePosition = transform.position + Vector3.up * 0.65f;
        Vector3 aimPosition = target.position + Vector3.up * 0.6f;
        Vector3 toTarget = aimPosition - muzzlePosition;

        if (toTarget.magnitude <= fireRange)
        {
            Fire(muzzlePosition, toTarget.normalized);
        }

        fireTimer = fireInterval;
    }

    private void Fire(Vector3 muzzlePosition, Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.001f)
        {
            direction = transform.forward;
        }

        Vector3 flatDirection = new Vector3(direction.x, 0f, direction.z);

        if (flatDirection.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(flatDirection, Vector3.up);
        }

        GameObject projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        projectile.name = "Cannonball";
        projectile.transform.position = muzzlePosition + direction * 1.1f;
        projectile.transform.localScale = Vector3.one * 0.38f;

        Renderer renderer = projectile.GetComponent<Renderer>();

        if (renderer != null && projectileMaterial != null)
        {
            renderer.material = projectileMaterial;
        }

        Collider collider = projectile.GetComponent<Collider>();
        collider.isTrigger = true;

        Rigidbody rb = projectile.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;

        LabCannonProjectile cannonProjectile = projectile.AddComponent<LabCannonProjectile>();
        cannonProjectile.Init(manager, direction, projectileSpeed, damage);
        manager.CannonShot(muzzlePosition);
    }
}

public class LabCannonProjectile : MonoBehaviour
{
    private LabGameManager manager;
    private Vector3 direction;
    private float speed;
    private int damage;
    private float lifetime = 4f;
    private bool consumed;

    public void Init(LabGameManager gameManager, Vector3 flyDirection, float flySpeed, int projectileDamage)
    {
        manager = gameManager;
        direction = flyDirection.normalized;
        speed = flySpeed;
        damage = projectileDamage;
    }

    private void Update()
    {
        transform.position += direction * speed * Time.deltaTime;
        transform.Rotate(220f * Time.deltaTime, 120f * Time.deltaTime, 0f, Space.Self);

        lifetime -= Time.deltaTime;

        if (lifetime <= 0f)
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (consumed)
        {
            return;
        }

        if (other.GetComponentInParent<LabPlayerController>() != null)
        {
            consumed = true;
            manager.TakeDamage(damage, "Попадание ядра: здоровье уменьшилось.");
            Destroy(gameObject);
            return;
        }

        if (!other.isTrigger && other.GetComponentInParent<LabCannonShooter>() == null)
        {
            consumed = true;
            Destroy(gameObject);
        }
    }
}

public class LabFinishZone : MonoBehaviour
{
    private LabGameManager manager;

    public void Init(LabGameManager gameManager)
    {
        manager = gameManager;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<LabPlayerController>() != null)
        {
            manager.TryFinish();
        }
    }
}

public class LabFallZone : MonoBehaviour
{
    private LabGameManager manager;

    public void Init(LabGameManager gameManager)
    {
        manager = gameManager;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<LabPlayerController>() != null)
        {
            manager.PlayerFell();
        }
    }
}

public class LabBillboardLabel : MonoBehaviour
{
    private void LateUpdate()
    {
        Camera camera = Camera.main;

        if (camera == null)
        {
            return;
        }

        transform.rotation = Quaternion.LookRotation(transform.position - camera.transform.position, Vector3.up);
    }
}
