using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LabGameManager : MonoBehaviour
{
    private enum GameState
    {
        Playing,
        Paused,
        Victory,
        Defeat
    }

    private const int MaxHealth = 3;
    private const int TotalItems = 3;

    private readonly Vector3 playerStartPosition = new Vector3(-6f, 1.15f, -2f);

    private GameState state = GameState.Playing;
    private Transform levelRoot;
    private LabPlayerController player;
    private GameObject door;
    private Renderer doorRenderer;
    private Renderer buttonRenderer;
    private LabInteractable activeInteractable;

    private Material groundMaterial;
    private Material bridgeMaterial;
    private Material wallMaterial;
    private Material playerMaterial;
    private Material itemMaterial;
    private Material closedDoorMaterial;
    private Material openDoorMaterial;
    private Material buttonIdleMaterial;
    private Material buttonActiveMaterial;
    private Material hazardMaterial;
    private Material finishMaterial;
    private Material teleporterMaterial;
    private Material waterMaterial;
    private Material propWoodMaterial;
    private Material propStoneMaterial;
    private Material propShipMaterial;
    private Material particleMaterial;

    private Text scoreText;
    private Text healthText;
    private Text stateText;
    private Text hintText;
    private Text progressText;
    private Image progressFill;
    private GameObject overlayPanel;
    private Text overlayText;

    private LabCameraFollow cameraFollow;
    private AudioSource sfxSource;
    private AudioSource musicSource;
    private AudioClip pickupClip;
    private AudioClip buttonClip;
    private AudioClip teleportClip;
    private AudioClip damageClip;
    private AudioClip victoryClip;
    private AudioClip defeatClip;
    private AudioClip stepClip;
    private AudioClip cannonClip;

    private int collectedItems;
    private int health = MaxHealth;
    private bool doorOpened;
    private bool isRespawning;
    private bool musicEnabled = true;
    private float damageCooldown;
    private float messageTimer;
    private float footstepTimer;
    private string temporaryMessage;
    private string zoneHint;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateManager()
    {
        if (FindFirstObjectByType<LabGameManager>() != null)
        {
            return;
        }

        new GameObject("Lab5_8_Manager").AddComponent<LabGameManager>();
    }

    private void Awake()
    {
        Time.timeScale = 1f;
        BuildLabScene();
    }

    private void Update()
    {
        if (damageCooldown > 0f)
        {
            damageCooldown -= Time.unscaledDeltaTime;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            TogglePause();
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            RestartScene();
        }

        if (Input.GetKeyDown(KeyCode.M))
        {
            ToggleMusic();
        }

        if (state == GameState.Playing && activeInteractable != null && Input.GetKeyDown(KeyCode.E))
        {
            activeInteractable.Interact();
        }

        if (messageTimer > 0f)
        {
            messageTimer -= Time.unscaledDeltaTime;

            if (messageTimer <= 0f)
            {
                temporaryMessage = string.Empty;
                RefreshHint();
            }
        }

        HandleFootsteps();
    }

    public void CollectItem(GameObject item)
    {
        Vector3 itemPosition = item.transform.position;
        collectedItems++;
        Destroy(item);
        PlaySfx(pickupClip, 0.75f);
        SpawnBurstEffect(itemPosition, itemMaterial.color, 28, 2.6f);

        if (collectedItems >= TotalItems)
        {
            ShowHint("Все ключи собраны. Теперь нажми E на кнопке, чтобы открыть дверь.", 4f);
        }
        else
        {
            ShowHint("Ключ подобран. Собери остальные ключи для открытия двери.", 3f);
        }

        UpdateUi();
    }

    public void TryOpenDoor()
    {
        if (doorOpened)
        {
            ShowHint("Дверь уже открыта. Иди к зеленой зоне финиша.", 3f);
            return;
        }

        if (collectedItems < TotalItems)
        {
            ShowHint("Кнопка заблокирована: сначала собери все ключи.", 3f);
            return;
        }

        doorOpened = true;

        if (buttonRenderer != null)
        {
            buttonRenderer.material = buttonActiveMaterial;
        }

        if (doorRenderer != null)
        {
            doorRenderer.material = openDoorMaterial;
        }

        StartCoroutine(OpenDoorRoutine());
        PlaySfx(buttonClip, 0.85f);
        SpawnBurstEffect(door.transform.position + Vector3.up, openDoorMaterial.color, 40, 2.8f);
        ShakeCamera(0.12f, 0.25f);
        ShowHint("Кнопка активирована: дверь открывается.", 3f);
        UpdateUi();
    }

    public void TryFinish()
    {
        if (!doorOpened || collectedItems < TotalItems)
        {
            ShowHint("Финиш закрыт: нужны все ключи и открытая дверь.", 3f);
            return;
        }

        state = GameState.Victory;
        Time.timeScale = 0f;
        player.InputEnabled = false;
        overlayPanel.SetActive(true);
        overlayText.text = "Победа!\nФинальная демонстрация готова: взаимодействия, UI, анимации, звук и эффекты работают.\nНажми R для рестарта.";
        PlaySfx(victoryClip, 0.9f);
        SpawnBurstEffect(player.transform.position + Vector3.up, finishMaterial.color, 90, 4.5f, true);
        ShakeCamera(0.18f, 0.45f);
        UpdateUi();
    }

    public void TakeDamage(int damage, string reason)
    {
        if (state != GameState.Playing || damageCooldown > 0f)
        {
            return;
        }

        damageCooldown = 1.2f;
        health -= damage;
        PlaySfx(damageClip, 0.8f);
        SpawnBurstEffect(player.transform.position + Vector3.up * 0.6f, hazardMaterial.color, 32, 3f);
        ShakeCamera(0.18f, 0.25f);

        if (health <= 0)
        {
            health = 0;
            state = GameState.Defeat;
            Time.timeScale = 0f;
            player.InputEnabled = false;
            overlayPanel.SetActive(true);
            overlayText.text = "Поражение\nЗдоровье закончилось.\nНажми R для рестарта.";
            PlaySfx(defeatClip, 0.85f);
        }
        else
        {
            ShowHint(reason, 3f);
        }

        UpdateUi();
    }

    public void PlayerFell()
    {
        if (state != GameState.Playing || isRespawning)
        {
            return;
        }

        isRespawning = true;
        health--;
        PlaySfx(damageClip, 0.8f);
        ShakeCamera(0.2f, 0.3f);

        if (health <= 0)
        {
            health = 0;
            state = GameState.Defeat;
            Time.timeScale = 0f;
            player.InputEnabled = false;
            overlayPanel.SetActive(true);
            overlayText.text = "Поражение\nИгрок упал за пределы уровня слишком много раз.\nНажми R для рестарта.";
            PlaySfx(defeatClip, 0.85f);
            UpdateUi();
            return;
        }

        SpawnBurstEffect(player.transform.position, waterMaterial.color, 34, 2.8f);
        RespawnPlayer();
        SpawnBurstEffect(playerStartPosition, teleporterMaterial.color, 28, 2.5f);
        ShowHint("Падение за границу уровня: игрок возвращен на старт, здоровье уменьшилось.", 4f);
        UpdateUi();
        StartCoroutine(ResetRespawnFlag());
    }

    public void TeleportPlayer(Vector3 targetPosition, string message)
    {
        if (state != GameState.Playing)
        {
            return;
        }

        Vector3 oldPosition = player.transform.position;
        SpawnBurstEffect(oldPosition, teleporterMaterial.color, 32, 3f);
        player.TeleportTo(targetPosition);
        SpawnBurstEffect(targetPosition, teleporterMaterial.color, 32, 3f);
        PlaySfx(teleportClip, 0.85f);
        ShakeCamera(0.1f, 0.22f);
        ShowHint(message, 3f);
    }

    public void CannonShot(Vector3 position)
    {
        PlaySfx(cannonClip, 0.7f);
        SpawnBurstEffect(position, hazardMaterial.color, 14, 2.2f);
        ShakeCamera(0.04f, 0.08f);
    }

    public void SetActiveInteractable(LabInteractable interactable)
    {
        activeInteractable = interactable;
        zoneHint = interactable.prompt;
        RefreshHint();
    }

    public void ClearActiveInteractable(LabInteractable interactable)
    {
        if (activeInteractable != interactable)
        {
            return;
        }

        activeInteractable = null;
        zoneHint = string.Empty;
        RefreshHint();
    }

    private void BuildLabScene()
    {
        levelRoot = new GameObject("Lab5_8_RuntimeLevel").transform;

        CleanupSampleObjects();
        CreateMaterials();
        CreateLighting();
        CreateLevelGeometry();
        CreateSceneProps();
        CreatePlayer();
        CreateInteractions();
        CreateCamera();
        CreateAudio();
        CreateUi();
        UpdateUi();
        ShowHint("ЛР7/ЛР8: WASD - движение, Space - прыжок, Shift - ускорение, E - действие, M - музыка, Esc - пауза.", 7f);
    }

    private void CleanupSampleObjects()
    {
        PlayerController[] oldControllers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);

        foreach (PlayerController controller in oldControllers)
        {
            controller.enabled = false;
        }

        GameObject[] sceneRoots = SceneManager.GetActiveScene().GetRootGameObjects();

        foreach (GameObject root in sceneRoots)
        {
            if (root == gameObject ||
                root == levelRoot.gameObject ||
                root.GetComponent<Light>() != null ||
                IsLabPropName(root.name))
            {
                continue;
            }

            root.SetActive(false);
        }
    }

    private void CreateMaterials()
    {
        groundMaterial = CreateMaterial("Lab Ground", new Color(0.35f, 0.58f, 0.35f));
        bridgeMaterial = CreateMaterial("Lab Bridge", new Color(0.52f, 0.39f, 0.24f));
        wallMaterial = CreateMaterial("Lab Wall", new Color(0.25f, 0.25f, 0.3f));
        playerMaterial = CreateMaterial("Lab Player", new Color(0.15f, 0.48f, 0.95f));
        itemMaterial = CreateMaterial("Lab Key", new Color(1f, 0.78f, 0.16f));
        closedDoorMaterial = CreateMaterial("Lab Closed Door", new Color(0.55f, 0.19f, 0.15f));
        openDoorMaterial = CreateMaterial("Lab Open Door", new Color(0.15f, 0.55f, 0.26f));
        buttonIdleMaterial = CreateMaterial("Lab Button Idle", new Color(0.95f, 0.65f, 0.18f));
        buttonActiveMaterial = CreateMaterial("Lab Button Active", new Color(0.12f, 0.72f, 0.34f));
        hazardMaterial = CreateMaterial("Lab Hazard", new Color(0.95f, 0.1f, 0.08f));
        finishMaterial = CreateMaterial("Lab Finish", new Color(0.1f, 0.8f, 0.38f));
        teleporterMaterial = CreateMaterial("Lab Teleporter", new Color(0.1f, 0.72f, 0.95f));
        waterMaterial = CreateMaterial("Lab Fall Zone", new Color(0.04f, 0.22f, 0.38f));
        propWoodMaterial = CreateMaterial("Lab Prop Wood", new Color(0.58f, 0.36f, 0.18f));
        propStoneMaterial = CreateMaterial("Lab Prop Stone", new Color(0.46f, 0.46f, 0.5f));
        propShipMaterial = CreateMaterial("Lab Prop Ship", new Color(0.48f, 0.29f, 0.14f));
        particleMaterial = CreateParticleMaterial("Lab Particle Material");
    }

    private void CreateLighting()
    {
        Light light = FindFirstObjectByType<Light>();

        if (light == null)
        {
            GameObject lightObject = new GameObject("Directional Light");
            light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
        }

        light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        light.intensity = 1.8f;
    }

    private void CreateLevelGeometry()
    {
        CreateBox("Start Platform", new Vector3(-4f, -0.1f, 0f), new Vector3(14f, 0.2f, 10f), groundMaterial, false);
        CreateBox("Button Walkway", new Vector3(-7f, -0.1f, 4.5f), new Vector3(3f, 0.2f, 5f), bridgeMaterial, false);
        CreateBox("Button Platform", new Vector3(-10f, -0.1f, 8f), new Vector3(5f, 0.2f, 5f), groundMaterial, false);
        CreateBox("Narrow Bridge", new Vector3(0f, -0.1f, 8f), new Vector3(3f, 0.2f, 8f), bridgeMaterial, false);
        CreateBox("Finish Platform", new Vector3(4f, -0.1f, 15f), new Vector3(12f, 0.2f, 8f), groundMaterial, false);
        CreateBox("Jump Platform Low", new Vector3(4.8f, 0.25f, 3.4f), new Vector3(2.6f, 0.25f, 2.6f), bridgeMaterial, false);
        CreateBox("Jump Platform High", new Vector3(7.7f, 0.75f, 5.4f), new Vector3(2.4f, 0.25f, 2.4f), bridgeMaterial, false);
        CreateBox("Cannon Island", new Vector3(11.6f, 0.05f, 8.2f), new Vector3(6.4f, 0.25f, 5.2f), groundMaterial, false);
        CreateBox("Cannon Dock", new Vector3(12.8f, -0.05f, 12.2f), new Vector3(4f, 0.2f, 3.2f), bridgeMaterial, false);

        CreateBox("Start Back Boundary", new Vector3(-4f, 0.45f, -5.1f), new Vector3(14f, 0.9f, 0.3f), wallMaterial, false);
        CreateBox("Start Left Boundary", new Vector3(-11.1f, 0.45f, -0.2f), new Vector3(0.3f, 0.9f, 9.6f), wallMaterial, false);
        CreateBox("Start Right Boundary", new Vector3(3.1f, 0.45f, -1.2f), new Vector3(0.3f, 0.9f, 7.6f), wallMaterial, false);
        CreateBox("Button Outer Boundary", new Vector3(-12.6f, 0.45f, 8f), new Vector3(0.3f, 0.9f, 5f), wallMaterial, false);
        CreateBox("Cannon Island Right Boundary", new Vector3(15f, 0.55f, 8.2f), new Vector3(0.3f, 1f, 5.2f), wallMaterial, false);
        CreateBox("Finish Right Boundary", new Vector3(10.1f, 0.45f, 15f), new Vector3(0.3f, 0.9f, 8f), wallMaterial, false);
        CreateBox("Finish Top Boundary", new Vector3(4f, 0.45f, 19.1f), new Vector3(12f, 0.9f, 0.3f), wallMaterial, false);
        CreateWorldLabel("SPACE", new Vector3(5.8f, 1.3f, 3.4f), Color.white);

        GameObject water = CreateBox("Visible Fall Area", new Vector3(2f, -1.35f, 7f), new Vector3(48f, 0.08f, 38f), waterMaterial, false);
        Destroy(water.GetComponent<Collider>());

        GameObject fallZone = CreateBox("Fall Trigger", new Vector3(2f, -1.7f, 7f), new Vector3(50f, 0.7f, 40f), waterMaterial, true);
        fallZone.GetComponent<Renderer>().enabled = false;
        fallZone.AddComponent<LabFallZone>().Init(this);
    }

    private void CreateSceneProps()
    {
        GameObject pirateShip = PrepareProp("ship-pirate-medium", new Vector3(6.8f, 0.05f, 14.8f), new Vector3(1.5f, 1.5f, 1.5f), Quaternion.Euler(0f, -35f, 0f), propShipMaterial, false, Vector3.zero, Vector3.zero);
        GameObject largeShip = PrepareProp("ship-large", new Vector3(-11.5f, -0.25f, 15f), new Vector3(1.25f, 1.25f, 1.25f), Quaternion.Euler(0f, 25f, 0f), propShipMaterial, false, Vector3.zero, Vector3.zero);
        GameObject boat = PrepareProp("boat-row-large", new Vector3(-7.2f, 0.12f, -3.9f), new Vector3(0.9f, 0.9f, 0.9f), Quaternion.Euler(0f, 90f, 0f), propWoodMaterial, false, Vector3.zero, Vector3.zero);
        PrepareProp("structure-platform", new Vector3(-10.2f, 0.15f, 8.2f), new Vector3(1.3f, 1.3f, 1.3f), Quaternion.identity, propWoodMaterial, false, Vector3.zero, Vector3.zero);
        GameObject flag = PrepareProp("flag", new Vector3(5.9f, 0.15f, 17.1f), new Vector3(1.35f, 1.35f, 1.35f), Quaternion.identity, finishMaterial, false, Vector3.zero, Vector3.zero);
        GameObject cat = PrepareProp("animal-cat", new Vector3(3.2f, 0.25f, 17.4f), new Vector3(1.2f, 1.2f, 1.2f), Quaternion.Euler(0f, 180f, 0f), playerMaterial, false, Vector3.zero, Vector3.zero);

        GameObject barrel = PrepareProp("barrel", new Vector3(-3.8f, 0.55f, 1.6f), new Vector3(1.25f, 1.25f, 1.25f), Quaternion.Euler(0f, 18f, 0f), propWoodMaterial, false, new Vector3(0f, 0.45f, 0f), new Vector3(1.25f, 1f, 1.25f));
        GameObject crate = PrepareProp("crate-bottles", new Vector3(-5.6f, 0.45f, 3.25f), new Vector3(1.2f, 1.2f, 1.2f), Quaternion.Euler(0f, -12f, 0f), propWoodMaterial, false, new Vector3(0f, 0.45f, 0f), new Vector3(1.2f, 0.9f, 1.2f));

        if (barrel != null)
        {
            barrel.name = "Solid Barrel Obstacle";
        }

        if (crate != null)
        {
            crate.name = "Solid Crate Obstacle";
        }

        AddFloatingAnimation(pirateShip, 0.05f, 0.8f, 0f, 0f);
        AddFloatingAnimation(largeShip, 0.04f, 0.7f, 0f, 0f);
        AddFloatingAnimation(boat, 0.06f, 1.1f, 0f, 0f);
        AddFloatingAnimation(flag, 0.04f, 1.6f, 6f, 0.02f);
        AddFloatingAnimation(cat, 0.03f, 2.4f, 0f, 0.03f);
    }

    private void CreatePlayer()
    {
        GameObject playerObject = new GameObject("Lab Player");
        playerObject.name = "Lab Player";
        playerObject.transform.SetParent(levelRoot);
        playerObject.transform.position = playerStartPosition;

        CapsuleCollider capsuleCollider = playerObject.AddComponent<CapsuleCollider>();
        capsuleCollider.radius = 0.42f;
        capsuleCollider.height = 2f;
        capsuleCollider.center = Vector3.zero;

        Rigidbody rb = playerObject.AddComponent<Rigidbody>();
        rb.mass = 1f;
        rb.linearDamping = 0f;
        rb.angularDamping = 0.05f;

        player = playerObject.AddComponent<LabPlayerController>();

        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        visual.name = "Player Visual";
        visual.transform.SetParent(playerObject.transform, false);
        visual.transform.localScale = new Vector3(0.85f, 1f, 0.85f);
        visual.GetComponent<Renderer>().material = playerMaterial;
        Destroy(visual.GetComponent<Collider>());

        LabCharacterAnimator animator = visual.AddComponent<LabCharacterAnimator>();
        animator.controller = player;
    }

    private void CreateInteractions()
    {
        CreateCollectible("Key 1", new Vector3(-8f, 0.75f, -2f));
        CreateCollectible("Key 2", new Vector3(11.1f, 0.8f, 9.1f));
        CreateCollectible("Key 3", new Vector3(-10f, 0.75f, 8.5f));

        GameObject teleporter = CreateBox("Teleport Pad", new Vector3(-1.5f, 0.05f, -3.4f), new Vector3(2f, 0.15f, 2f), teleporterMaterial, true);
        LabTeleporter teleporterScript = teleporter.AddComponent<LabTeleporter>();
        teleporterScript.targetPosition = new Vector3(-9.5f, 1.15f, 6.7f);
        teleporterScript.Init(this, "Нажми E: телепорт на платформу с кнопкой.");
        AddFloatingAnimation(teleporter, 0.04f, 2f, 0f, 0.05f);
        CreateLoopParticles("Teleport Particles", teleporter.transform, Vector3.up * 0.35f, teleporterMaterial.color, 18f, 1f);

        GameObject button = CreateBox("Door Button", new Vector3(-10f, 0.08f, 7.1f), new Vector3(1.7f, 0.18f, 1.7f), buttonIdleMaterial, true);
        buttonRenderer = button.GetComponent<Renderer>();
        LabDoorButton buttonScript = button.AddComponent<LabDoorButton>();
        buttonScript.Init(this, "Нажми E: открыть дверь, если собраны все ключи.");
        AddFloatingAnimation(button, 0.03f, 2.6f, 0f, 0.04f);
        CreateWorldLabel("КНОПКА  E", button.transform.position + Vector3.up * 0.9f, buttonActiveMaterial.color);

        door = PrepareProp("castle-door", new Vector3(0f, 1f, 10.3f), new Vector3(2f, 2f, 2f), Quaternion.identity, closedDoorMaterial, false, new Vector3(0f, 0f, 0f), new Vector3(3.5f, 2f, 0.5f));

        if (door == null)
        {
            door = CreateBox("Locked Door", new Vector3(0f, 1f, 10.3f), new Vector3(3.5f, 2f, 0.5f), closedDoorMaterial, false);
        }

        door.name = "Locked Door";
        doorRenderer = door.GetComponentInChildren<Renderer>();
        CreateWorldLabel("ДВЕРЬ", door.transform.position + Vector3.up * 1.8f, Color.white);

        GameObject spikes = CreateBox("Spike Hazard", new Vector3(-5.4f, 0.1f, 4.2f), new Vector3(1.4f, 0.2f, 1.4f), hazardMaterial, true);
        spikes.AddComponent<LabHazard>().Init(this, 1);
        AddFloatingAnimation(spikes, 0.02f, 5f, 0f, 0.08f);
        CreateLoopParticles("Hazard Sparks", spikes.transform, Vector3.up * 0.25f, hazardMaterial.color, 16f, 0.8f);
        CreateWorldLabel("ОПАСНО", spikes.transform.position + Vector3.up * 0.7f, hazardMaterial.color);

        GameObject movingHazard = PrepareProp("cannon", new Vector3(0f, 0.65f, 6.6f), new Vector3(1.1f, 1.1f, 1.1f), Quaternion.Euler(0f, 90f, 0f), hazardMaterial, true, new Vector3(0f, 0.25f, 0f), new Vector3(1.4f, 1.2f, 1.4f));

        if (movingHazard == null)
        {
            movingHazard = CreateBox("Moving Hazard", new Vector3(0f, 0.65f, 6.6f), new Vector3(0.8f, 1.1f, 0.8f), hazardMaterial, true);
        }

        movingHazard.name = "Moving Cannon Hazard";
        movingHazard.AddComponent<LabHazard>().Init(this, 1);
        movingHazard.AddComponent<LabMovingHazard>();
        AddCannonShooter(movingHazard, 2.7f, 7.5f, 15f);
        CreateLoopParticles("Cannon Danger Sparks", movingHazard.transform, Vector3.up * 0.4f, hazardMaterial.color, 12f, 0.7f);
        CreateWorldLabel("ПУШКА", movingHazard.transform.position + Vector3.up * 1.25f, hazardMaterial.color);

        GameObject islandCannon = CreatePrimitiveCannon("Island Cannon", new Vector3(13.7f, 0.38f, 7.1f), Quaternion.Euler(0f, -105f, 0f));
        AddCannonShooter(islandCannon, 2.2f, 8.8f, 19f);

        GameObject dockCannon = CreatePrimitiveCannon("Dock Cannon", new Vector3(12.5f, 0.22f, 12.7f), Quaternion.Euler(0f, -155f, 0f));
        AddCannonShooter(dockCannon, 3f, 9.2f, 17f);

        GameObject finish = CreateBox("Finish Zone", new Vector3(4f, 0.08f, 17f), new Vector3(3.8f, 0.16f, 2.2f), finishMaterial, true);
        finish.AddComponent<LabFinishZone>().Init(this);
        AddFloatingAnimation(finish, 0.03f, 1.8f, 0f, 0.04f);
        CreateLoopParticles("Finish Glow", finish.transform, Vector3.up * 0.35f, finishMaterial.color, 22f, 1.8f);
        CreateWorldLabel("ФИНИШ", finish.transform.position + Vector3.up * 1f, finishMaterial.color);
    }

    private void CreateCamera()
    {
        AudioListener[] listeners = FindObjectsByType<AudioListener>(FindObjectsSortMode.None);

        foreach (AudioListener listener in listeners)
        {
            listener.enabled = false;
        }

        GameObject cameraObject = new GameObject("Lab Camera");
        cameraObject.transform.SetParent(levelRoot);
        cameraObject.tag = "MainCamera";

        Camera mainCamera = cameraObject.AddComponent<Camera>();
        mainCamera.transform.position = player.transform.position + new Vector3(0f, 11f, -8f);
        mainCamera.transform.LookAt(player.transform.position + Vector3.up * 0.8f);
        mainCamera.fieldOfView = 55f;
        mainCamera.clearFlags = CameraClearFlags.SolidColor;
        mainCamera.backgroundColor = new Color(0.48f, 0.7f, 0.9f);
        mainCamera.nearClipPlane = 0.1f;
        mainCamera.farClipPlane = 250f;

        cameraObject.AddComponent<AudioListener>();
        cameraFollow = cameraObject.AddComponent<LabCameraFollow>();
        cameraFollow.target = player.transform;
    }

    private void CreateUi()
    {
        Font font = GetDefaultFont();

        GameObject canvasObject = new GameObject("Lab UI");
        canvasObject.transform.SetParent(levelRoot);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject panel = CreateUiPanel("Info Panel", canvasObject.transform, new Vector2(18f, -18f), new Vector2(360f, 160f), new Color(0f, 0f, 0f, 0.48f));
        scoreText = CreateText("Score Text", panel.transform, "Ключи: 0/3", 22, TextAnchor.UpperLeft, new Vector2(16f, -12f), new Vector2(330f, 30f), Color.white, font);
        healthText = CreateText("Health Text", panel.transform, "Здоровье: 3/3", 22, TextAnchor.UpperLeft, new Vector2(16f, -44f), new Vector2(330f, 30f), Color.white, font);
        stateText = CreateText("State Text", panel.transform, "Состояние: игра", 20, TextAnchor.UpperLeft, new Vector2(16f, -76f), new Vector2(330f, 28f), Color.white, font);
        progressText = CreateText("Progress Text", panel.transform, "Прогресс: 0%", 20, TextAnchor.UpperLeft, new Vector2(16f, -106f), new Vector2(330f, 28f), Color.white, font);

        GameObject progressBack = CreateUiPanel("Progress Back", panel.transform, new Vector2(16f, -136f), new Vector2(320f, 10f), new Color(1f, 1f, 1f, 0.22f));
        progressFill = CreateUiPanel("Progress Fill", progressBack.transform, Vector2.zero, new Vector2(0f, 10f), new Color(0.1f, 0.8f, 0.38f, 0.95f)).GetComponent<Image>();

        RectTransform fillRect = progressFill.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0.5f);
        fillRect.anchorMax = new Vector2(0f, 0.5f);
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.anchoredPosition = Vector2.zero;

        hintText = CreateText("Hint Text", canvasObject.transform, string.Empty, 22, TextAnchor.LowerCenter, new Vector2(0f, 24f), new Vector2(1100f, 80f), Color.white, font);
        RectTransform hintRect = hintText.GetComponent<RectTransform>();
        hintRect.anchorMin = new Vector2(0.5f, 0f);
        hintRect.anchorMax = new Vector2(0.5f, 0f);
        hintRect.pivot = new Vector2(0.5f, 0f);

        overlayPanel = CreateUiPanel("State Overlay", canvasObject.transform, Vector2.zero, new Vector2(620f, 260f), new Color(0f, 0f, 0f, 0.72f));
        RectTransform overlayRect = overlayPanel.GetComponent<RectTransform>();
        overlayRect.anchorMin = new Vector2(0.5f, 0.5f);
        overlayRect.anchorMax = new Vector2(0.5f, 0.5f);
        overlayRect.pivot = new Vector2(0.5f, 0.5f);

        overlayText = CreateText("Overlay Text", overlayPanel.transform, string.Empty, 30, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(580f, 220f), Color.white, font);
        RectTransform overlayTextRect = overlayText.GetComponent<RectTransform>();
        overlayTextRect.anchorMin = new Vector2(0.5f, 0.5f);
        overlayTextRect.anchorMax = new Vector2(0.5f, 0.5f);
        overlayTextRect.pivot = new Vector2(0.5f, 0.5f);

        overlayPanel.SetActive(false);
    }

    private GameObject CreateCollectible(string objectName, Vector3 position)
    {
        GameObject item = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        item.name = objectName;
        item.transform.SetParent(levelRoot);
        item.transform.position = position;
        item.transform.localScale = new Vector3(0.65f, 0.65f, 0.65f);
        item.GetComponent<Renderer>().material = itemMaterial;

        Collider collider = item.GetComponent<Collider>();
        collider.isTrigger = true;

        item.AddComponent<LabCollectible>().Init(this);
        AddFloatingAnimation(item, 0.2f, 2.4f, 120f, 0.08f);
        CreateLoopParticles("Key Sparkles", item.transform, Vector3.zero, itemMaterial.color, 14f, 0.55f);
        CreateWorldLabel("КЛЮЧ", position + Vector3.up * 0.75f, itemMaterial.color);
        return item;
    }

    private GameObject CreatePrimitiveCannon(string objectName, Vector3 position, Quaternion rotation)
    {
        GameObject cannonRoot = new GameObject(objectName);
        cannonRoot.transform.SetParent(levelRoot);
        cannonRoot.transform.position = position;
        cannonRoot.transform.rotation = rotation;

        GameObject baseObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        baseObject.name = objectName + " Base";
        baseObject.transform.SetParent(cannonRoot.transform, false);
        baseObject.transform.localPosition = new Vector3(0f, 0.18f, 0f);
        baseObject.transform.localScale = new Vector3(1.4f, 0.35f, 1f);
        baseObject.GetComponent<Renderer>().material = propWoodMaterial;
        Destroy(baseObject.GetComponent<Collider>());

        GameObject barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        barrel.name = objectName + " Barrel";
        barrel.transform.SetParent(cannonRoot.transform, false);
        barrel.transform.localPosition = new Vector3(0f, 0.55f, 0.45f);
        barrel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        barrel.transform.localScale = new Vector3(0.28f, 0.85f, 0.28f);
        barrel.GetComponent<Renderer>().material = hazardMaterial;
        Destroy(barrel.GetComponent<Collider>());

        GameObject wheelLeft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        wheelLeft.name = objectName + " Wheel L";
        wheelLeft.transform.SetParent(cannonRoot.transform, false);
        wheelLeft.transform.localPosition = new Vector3(-0.65f, 0.17f, 0f);
        wheelLeft.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        wheelLeft.transform.localScale = new Vector3(0.28f, 0.12f, 0.28f);
        wheelLeft.GetComponent<Renderer>().material = propWoodMaterial;
        Destroy(wheelLeft.GetComponent<Collider>());

        GameObject wheelRight = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        wheelRight.name = objectName + " Wheel R";
        wheelRight.transform.SetParent(cannonRoot.transform, false);
        wheelRight.transform.localPosition = new Vector3(0.65f, 0.17f, 0f);
        wheelRight.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        wheelRight.transform.localScale = new Vector3(0.28f, 0.12f, 0.28f);
        wheelRight.GetComponent<Renderer>().material = propWoodMaterial;
        Destroy(wheelRight.GetComponent<Collider>());

        BoxCollider collider = cannonRoot.AddComponent<BoxCollider>();
        collider.center = new Vector3(0f, 0.45f, 0.25f);
        collider.size = new Vector3(1.65f, 0.9f, 1.5f);

        CreateLoopParticles(objectName + " Fuse Sparks", cannonRoot.transform, Vector3.up * 0.75f, hazardMaterial.color, 8f, 0.35f);
        CreateWorldLabel("ПУШКА", position + Vector3.up * 1.35f, hazardMaterial.color);
        return cannonRoot;
    }

    private void AddCannonShooter(GameObject cannonObject, float fireInterval, float projectileSpeed, float fireRange)
    {
        if (cannonObject == null || player == null)
        {
            return;
        }

        LabCannonShooter shooter = cannonObject.AddComponent<LabCannonShooter>();
        shooter.Init(this, player.transform, hazardMaterial, fireInterval, projectileSpeed, fireRange);
    }

    private GameObject PrepareProp(string objectName, Vector3 position, Vector3 scale, Quaternion rotation, Material material, bool isTrigger, Vector3 colliderCenter, Vector3 colliderSize)
    {
        GameObject prop = FindSceneObject(objectName);

        if (prop == null)
        {
            return null;
        }

        prop.SetActive(true);
        prop.transform.SetParent(levelRoot);
        prop.transform.position = position;
        prop.transform.rotation = rotation;
        prop.transform.localScale = scale;
        ApplyMaterial(prop, material);

        Collider[] colliders = prop.GetComponentsInChildren<Collider>();

        foreach (Collider collider in colliders)
        {
            collider.enabled = false;
        }

        if (colliderSize != Vector3.zero)
        {
            BoxCollider boxCollider = prop.GetComponent<BoxCollider>();

            if (boxCollider == null)
            {
                boxCollider = prop.AddComponent<BoxCollider>();
            }

            boxCollider.enabled = true;
            boxCollider.isTrigger = isTrigger;
            boxCollider.center = colliderCenter;
            boxCollider.size = colliderSize;
        }

        return prop;
    }

    private static GameObject FindSceneObject(string objectName)
    {
        GameObject activeObject = GameObject.Find(objectName);

        if (activeObject != null)
        {
            return activeObject;
        }

        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();

        foreach (GameObject sceneObject in allObjects)
        {
            if (sceneObject.name == objectName && sceneObject.scene.IsValid() && sceneObject.hideFlags == HideFlags.None)
            {
                return sceneObject;
            }
        }

        return null;
    }

    private void CreateWorldLabel(string label, Vector3 position, Color color)
    {
        GameObject labelObject = new GameObject("Label " + label);
        labelObject.transform.SetParent(levelRoot);
        labelObject.transform.position = position;

        TextMesh textMesh = labelObject.AddComponent<TextMesh>();
        textMesh.text = label;
        textMesh.fontSize = 64;
        textMesh.characterSize = 0.12f;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.color = color;

        labelObject.AddComponent<LabBillboardLabel>();
    }

    private GameObject CreateBox(string objectName, Vector3 position, Vector3 scale, Material material, bool isTrigger)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = objectName;
        box.transform.SetParent(levelRoot);
        box.transform.position = position;
        box.transform.localScale = scale;
        box.GetComponent<Renderer>().material = material;
        box.GetComponent<Collider>().isTrigger = isTrigger;
        return box;
    }

    private static bool IsLabPropName(string objectName)
    {
        switch (objectName)
        {
            case "ship-pirate-medium":
            case "ship-large":
            case "boat-row-large":
            case "structure-platform":
            case "barrel":
            case "crate-bottles":
            case "castle-door":
            case "cannon":
            case "flag":
            case "animal-cat":
                return true;
            default:
                return false;
        }
    }

    private static void ApplyMaterial(GameObject root, Material material)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);

        foreach (Renderer renderer in renderers)
        {
            renderer.material = material;
        }
    }

    private void AddFloatingAnimation(GameObject target, float bobHeight, float bobSpeed, float rotationSpeed, float pulseAmount)
    {
        if (target == null)
        {
            return;
        }

        LabFloatingAnimation animation = target.AddComponent<LabFloatingAnimation>();
        animation.bobHeight = bobHeight;
        animation.bobSpeed = bobSpeed;
        animation.rotationSpeed = rotationSpeed;
        animation.pulseAmount = pulseAmount;
    }

    private void CreateAudio()
    {
        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.spatialBlend = 0f;

        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.volume = 0.16f;
        musicSource.spatialBlend = 0f;

        pickupClip = CreateTone("Pickup Tone", 880f, 0.18f, 0.22f, 300f);
        buttonClip = CreateTone("Button Tone", 440f, 0.28f, 0.26f, -120f);
        teleportClip = CreateTone("Teleport Tone", 720f, 0.35f, 0.24f, 500f);
        damageClip = CreateTone("Damage Tone", 180f, 0.25f, 0.28f, -80f);
        victoryClip = CreateTone("Victory Tone", 660f, 0.75f, 0.24f, 420f);
        defeatClip = CreateTone("Defeat Tone", 140f, 0.65f, 0.24f, -70f);
        stepClip = CreateTone("Step Tone", 95f, 0.08f, 0.09f, -20f);
        cannonClip = CreateTone("Cannon Shot Tone", 95f, 0.32f, 0.32f, -45f);

        musicSource.clip = CreateMusicClip();

        if (musicEnabled)
        {
            musicSource.Play();
        }
    }

    private void HandleFootsteps()
    {
        if (state != GameState.Playing || player == null || !player.IsMoving)
        {
            footstepTimer = 0.05f;
            return;
        }

        footstepTimer -= Time.deltaTime;

        if (footstepTimer > 0f)
        {
            return;
        }

        PlaySfx(stepClip, player.IsSprinting ? 0.45f : 0.32f);
        footstepTimer = player.IsSprinting ? 0.18f : 0.28f;
    }

    private void ToggleMusic()
    {
        musicEnabled = !musicEnabled;

        if (musicSource == null)
        {
            return;
        }

        if (musicEnabled)
        {
            musicSource.Play();
            ShowHint("Музыка включена.", 2f);
        }
        else
        {
            musicSource.Stop();
            ShowHint("Музыка выключена.", 2f);
        }
    }

    private void PlaySfx(AudioClip clip, float volume)
    {
        if (sfxSource != null && clip != null)
        {
            sfxSource.PlayOneShot(clip, volume);
        }
    }

    private void ShakeCamera(float intensity, float duration)
    {
        if (cameraFollow != null)
        {
            cameraFollow.Shake(intensity, duration);
        }
    }

    private void CreateLoopParticles(string objectName, Transform parent, Vector3 localPosition, Color color, float rate, float radius)
    {
        GameObject particleObject = new GameObject(objectName);
        particleObject.transform.SetParent(parent, false);
        particleObject.transform.localPosition = localPosition;

        ParticleSystem particles = particleObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 1.1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.75f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
        main.startColor = color;
        main.maxParticles = 120;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = rate;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = radius;

        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.material = particleMaterial;
    }

    private void SpawnBurstEffect(Vector3 position, Color color, int count, float speed, bool unscaledTime = false)
    {
        GameObject particleObject = new GameObject("Burst Effect");
        particleObject.transform.position = position;

        ParticleSystem particles = particleObject.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = particles.main;
        main.loop = false;
        main.duration = 0.8f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 1.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
        main.startColor = color;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.useUnscaledTime = unscaledTime;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.25f;

        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.material = particleMaterial;

        LabAutoDestroy autoDestroy = particleObject.AddComponent<LabAutoDestroy>();
        autoDestroy.lifetime = unscaledTime ? 3f : 2.2f;
        particles.Play();
    }

    private static AudioClip CreateTone(string clipName, float frequency, float length, float volume, float frequencySlide)
    {
        const int sampleRate = 44100;
        int samples = Mathf.CeilToInt(sampleRate * length);
        float[] data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)sampleRate;
            float normalizedTime = Mathf.Clamp01(t / length);
            float envelope = Mathf.Clamp01(t / 0.025f) * (1f - normalizedTime);
            float currentFrequency = frequency + frequencySlide * normalizedTime;
            data[i] = Mathf.Sin(2f * Mathf.PI * currentFrequency * t) * volume * envelope;
        }

        AudioClip clip = AudioClip.Create(clipName, samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private static AudioClip CreateMusicClip()
    {
        const int sampleRate = 44100;
        const float length = 8f;
        int samples = Mathf.CeilToInt(sampleRate * length);
        float[] data = new float[samples];
        float[] notes = { 261.63f, 329.63f, 392f, 523.25f, 392f, 329.63f, 293.66f, 392f };

        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)sampleRate;
            int noteIndex = Mathf.FloorToInt(t / 0.5f) % notes.Length;
            float localTime = t % 0.5f;
            float envelope = Mathf.Clamp01(localTime / 0.05f) * Mathf.Clamp01((0.5f - localTime) / 0.12f);
            float melody = Mathf.Sin(2f * Mathf.PI * notes[noteIndex] * t) * 0.06f * envelope;
            float bass = Mathf.Sin(2f * Mathf.PI * (notes[noteIndex] * 0.5f) * t) * 0.035f;
            data[i] = melody + bass;
        }

        AudioClip clip = AudioClip.Create("Generated Background Music", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private static GameObject CreateUiPanel(string objectName, Transform parent, Vector2 anchoredPosition, Vector2 size, Color color)
    {
        GameObject panel = new GameObject(objectName);
        panel.transform.SetParent(parent, false);

        RectTransform rect = panel.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Image image = panel.AddComponent<Image>();
        image.color = color;

        return panel;
    }

    private static Text CreateText(string objectName, Transform parent, string value, int fontSize, TextAnchor alignment, Vector2 anchoredPosition, Vector2 size, Color color, Font font)
    {
        GameObject textObject = new GameObject(objectName);
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Text text = textObject.AddComponent<Text>();
        text.text = value;
        text.font = font;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        return text;
    }

    private static Material CreateMaterial(string materialName, Color color)
    {
        bool usesRenderPipeline =
            UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null ||
            QualitySettings.renderPipeline != null;

        string[] shaderNames = usesRenderPipeline
            ? new[] { "Universal Render Pipeline/Lit", "Universal Render Pipeline/Unlit", "Standard", "Unlit/Color" }
            : new[] { "Standard", "Unlit/Color", "Universal Render Pipeline/Lit", "Universal Render Pipeline/Unlit" };

        Shader shader = null;

        foreach (string shaderName in shaderNames)
        {
            shader = Shader.Find(shaderName);

            if (shader != null)
            {
                break;
            }
        }

        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        Material material = new Material(shader);
        material.name = materialName;
        material.color = color;

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        return material;
    }

    private static Material CreateParticleMaterial(string materialName)
    {
        string[] shaderNames =
        {
            "Universal Render Pipeline/Particles/Unlit",
            "Particles/Standard Unlit",
            "Sprites/Default"
        };

        Shader shader = null;

        foreach (string shaderName in shaderNames)
        {
            shader = Shader.Find(shaderName);

            if (shader != null)
            {
                break;
            }
        }

        Material material = new Material(shader);
        material.name = materialName;
        material.color = Color.white;
        return material;
    }

    private static Font GetDefaultFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        if (font == null)
        {
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        return font;
    }

    private IEnumerator OpenDoorRoutine()
    {
        Collider doorCollider = door.GetComponent<Collider>();

        if (doorCollider != null)
        {
            doorCollider.enabled = false;
        }

        Vector3 start = door.transform.position;
        Vector3 end = start + Vector3.up * 2.8f;
        float elapsed = 0f;

        while (elapsed < 1f)
        {
            elapsed += Time.deltaTime;
            door.transform.position = Vector3.Lerp(start, end, elapsed);
            yield return null;
        }

        door.transform.position = end;
    }

    private IEnumerator ResetRespawnFlag()
    {
        yield return new WaitForSeconds(0.4f);
        isRespawning = false;
    }

    private void RespawnPlayer()
    {
        player.TeleportTo(playerStartPosition);
    }

    private void TogglePause()
    {
        if (state == GameState.Victory || state == GameState.Defeat)
        {
            return;
        }

        if (state == GameState.Paused)
        {
            state = GameState.Playing;
            Time.timeScale = 1f;
            player.InputEnabled = true;
            overlayPanel.SetActive(false);
        }
        else
        {
            state = GameState.Paused;
            Time.timeScale = 0f;
            player.InputEnabled = false;
            overlayPanel.SetActive(true);
            overlayText.text = "Пауза\nEsc - продолжить\nR - рестарт";
        }

        UpdateUi();
    }

    private void RestartScene()
    {
        Time.timeScale = 1f;
        Scene activeScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(activeScene.buildIndex);
    }

    private void UpdateUi()
    {
        if (scoreText == null)
        {
            return;
        }

        scoreText.text = "Ключи: " + collectedItems + "/" + TotalItems;
        healthText.text = "Здоровье: " + health + "/" + MaxHealth;
        stateText.text = "Состояние: " + GetStateLabel();

        int completedSteps = collectedItems + (doorOpened ? 1 : 0);
        int totalSteps = TotalItems + 1;

        if (state == GameState.Victory)
        {
            completedSteps = totalSteps;
        }

        float progress = Mathf.Clamp01((float)completedSteps / totalSteps);
        progressText.text = "Прогресс: " + Mathf.RoundToInt(progress * 100f) + "%";
        progressFill.rectTransform.sizeDelta = new Vector2(320f * progress, 10f);
    }

    private string GetStateLabel()
    {
        switch (state)
        {
            case GameState.Paused:
                return "пауза";
            case GameState.Victory:
                return "победа";
            case GameState.Defeat:
                return "поражение";
            default:
                return "игра";
        }
    }

    private void ShowHint(string message, float seconds)
    {
        temporaryMessage = message;
        messageTimer = seconds;
        RefreshHint();
    }

    private void RefreshHint()
    {
        if (hintText == null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(temporaryMessage) && messageTimer > 0f)
        {
            hintText.text = temporaryMessage;
            return;
        }

        if (!string.IsNullOrEmpty(zoneHint))
        {
            hintText.text = zoneHint;
            return;
        }

        hintText.text = "Собери 3 ключа, нажми кнопку, пройди дверь и дойди до финиша. Space - прыжок, Shift - ускорение, M - музыка. Уклоняйся от ядер.";
    }
}
