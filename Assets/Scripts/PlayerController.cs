using System.Collections;
using UnityEngine;
using UnityEngine.UI;
//using static UnityEditor.Searcher.SearcherWindow.Alignment;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEditor;

public class PlayerController : MonoBehaviour
{
    LevelManager lm;
    AfterLaunchAbility ala;

    Image arrowVisual;
    RectTransform arrowScale;

    GameObject player;
    Rigidbody rb;

    public float boardBounds;

    public float force = 20f;
    float forceRate = 10f;
    float forceMin = 20f;
    float forceMax = 40f;

    public float angle = 180f;
    float angleRate = 20f;
    float launchAngle = 1.6f;
    float directionRate = -0.36f;
    Vector3 angleApplied = Vector3.forward;

    public static bool playing;
    static bool turnStarted;
    static bool inPrep;
    static bool isLaunched;
    bool abilityUsed;
    static bool isInstant;

    InputAction movementKeys;
    InputAction jumpKey;

    private void Awake()
    {
        movementKeys = InputSystem.actions.FindAction("Move");
        jumpKey = InputSystem.actions.FindAction("Jump");            

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        lm = GameObject.FindGameObjectWithTag("Level Manager").GetComponent<LevelManager>();
        
        arrowScale = GameObject.Find("Arrow").GetComponent<RectTransform>();
        arrowVisual = GameObject.Find("Arrow").GetComponent<Image>();
    }

    void Update()
    {
        if (playing)
        {
            if (inPrep)
            {
                AimMovement();
            }

            if (!turnStarted)
            {
                // For each turn
                if (lm.turn < lm.turnMax)
                {
                    // Init the turn
                    StartTurn(lm.turn);
                }
                // After all the turns
                else if (lm.turn == lm.turnMax)
                {
                    // Scores
                    lm.ScoreRound();

                    // Freeze for endScreen
                    playing = false;
                }
            }
            else
            {
                // Run Game Functinaliy
                Play();
            }
        }
    }

    void AimMovement()
    {
        // Turn on Arrow UI
        arrowVisual.enabled = true;

        // Angle for Model & Arrow
        angle = angle + (angleRate * Time.deltaTime);
        // Bound for that Angle
        if (angle <= 125f || angle >= 235f)
        {
            angleRate = -angleRate;
        }
        // Rotate Model and Arrow
        rb.transform.rotation = Quaternion.Euler(-90f, angle, 0f);
        arrowScale.rotation = Quaternion.Euler(-90f, 0f, angle);

        //Adjust launch Angle
        launchAngle = launchAngle + (directionRate * Time.deltaTime);
        // Launch Angle Bounds
        if (launchAngle <= .6f || launchAngle >= 2.57f)
        {
            directionRate = -directionRate;
        }
        // Launch Angle set to Vector3
        angleApplied = new Vector3(Mathf.Cos(launchAngle), 0, Mathf.Sin(launchAngle));
        // Draw Launch Angle
        Debug.DrawRay(rb.transform.position, angleApplied, Color.green);

        // Take Input
        //(old input system) float horizontal = Input.GetAxis("Horizontal");
        float horizontal = movementKeys.ReadValue<Vector2>().x;

        // Set Input Bounds
        if (transform.position.x <= -6.5f || rb.transform.position.x <= -6.5f)
        {
            transform.position = new Vector3(-6.5f, transform.position.y, transform.position.z);
            rb.transform.position = new Vector3(-6.5f, rb.transform.position.y, rb.transform.position.z);
        }
        else if (transform.position.x >= 6.5f || rb.transform.position.x >= 6.5f)
        {
            transform.position = new Vector3(6.5f, transform.position.y, transform.position.z);
            rb.transform.position = new Vector3(6.5f, rb.transform.position.y, rb.transform.position.z);
        }

        // Move
        transform.Translate(Vector3.right * horizontal * 3 * Time.deltaTime);
        rb.transform.position = new Vector3(transform.position.x, 1.056f, -10.26124f);

        // When Space is Held, Charge Force Amount
        if (jumpKey.IsPressed())
        {
            // Stop Rotation
            angleRate = 0;
            directionRate = 0;

            // Charge
            force = force + (forceRate * Time.deltaTime);

            // Flux other direction
            if (force >= forceMax || force <= forceMin)
            {
                forceRate = -forceRate;
            }

            // Arrow Scales with Force
            arrowScale.sizeDelta = new Vector2(2.8f, 1f + (force / 10));
            Debug.DrawRay(rb.transform.position, angleApplied * (force / 10), Color.blue);
        }


    }
    void StartTurn(int turn)
    {
        // Start Turn
        turnStarted = true;
        inPrep = true;

        // Spawn Puck
        SpawnPuck(turn);
    }
    void SpawnPuck(int num)
    {
        // If player is not created, creat it.
        if (!isInstant)
        {
            isLaunched = false;

            player = Instantiate(lm.prefabPlayerPuck[num], new Vector3(transform.position.x, transform.position.y + .75f, transform.position.z), Quaternion.Euler(new Vector3(-90f, 180f, 0f)));
            rb = player.GetComponent<Rigidbody>();
            ala = player.GetComponent<AfterLaunchAbility>();

            if (ala != null) abilityUsed = false;
            else abilityUsed = true;

            Debug.Log("Ability? " + !abilityUsed + " Ala? " + ala);

            isInstant = true;
        }
    }

    void Play()
    {
        // In aiming phase
        if (!isLaunched)
        {
            // When space is released, launch
            if (jumpKey.WasReleasedThisFrame())
            {
                arrowVisual.enabled = false;
                inPrep = false;
                Launch();
            }
        }
        else
        {
            // Detect when puck has stopped
            if (rb.linearVelocity.magnitude <= 0.01 && lm.VelocityZero())
            {
                Debug.Log("Need to trigger ability? " + !abilityUsed);

                if(!abilityUsed)
                {
                    Debug.Log("Not moving, triggering additional actions");
                    StartCoroutine(Ability());
                }
                else
                {
                    Debug.Log("Not moving and no additional actions left");
                    ChangeTurn();
                }
            }
        }

    }

    void Launch()
    {
        //Apply Force
        Debug.Log("Launched");
        rb.AddForce(angleApplied * force, ForceMode.Impulse);
        StartCoroutine(DelayCheck());
    }

    void ChangeTurn()
    {
        // Reset variables
        force = forceMin;
        angle = 180f;
        launchAngle = 1.6f;
        angleApplied = Vector3.forward;
        angleRate = 20f;
        directionRate = -0.36f;

        //UI Set for PLACEHOLDER arrow
        arrowScale.sizeDelta = new Vector2(2.8f, 2.4f);

        // Add to turn count and prep for next turn
        lm.turn += 1;
        isInstant = false;
        turnStarted = false;
    }

    IEnumerator DelayCheck()
    {
        yield return new WaitForSeconds(0.1f);
        isLaunched = true;
        Debug.Log("Now checking for stop");
    }

    IEnumerator Ability()
    {
        // Activate After Launch Ability
        Debug.Log("Activate Ability");
        ala.UseAbility();

        yield return new WaitForSeconds(1f);

        abilityUsed = true;
    }
}
