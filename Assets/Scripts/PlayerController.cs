using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NewBehaviourScript : MonoBehaviour
{

    [Header("References")]
    public Rigidbody rb;
    public Transform head;
    public Camera playerCamera;

    [Header("Configurations")]
    public float walkSpeed;
    public float runSpeed;
    public float jumpSpeed;
    public float impactThreshold;

    [Header("Camera Effects")]
    public float baseCameraFov = 60f;
    public float baseCameraHeight = 0.85f;

    public float walkBobbingRate = 0.75f;
    public float runBobbingRate = 1f;
    public float maxWalkBobbingOffset = 0.2f;
    public float maxRunBobbingOffset = 0.3f;

    [Header("Runtime")]
    Vector3 newVelocity;
    bool isGrounded = false;
    bool isJumping = false;
    float vyCache;


    // Start is called before the first frame update
    void Start()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

    }

    // Update is called once per frame
    void Update()
    {
        // If we're in battle, do NOT move or look around
        if (GameModeManager.Instance != null && GameModeManager.Instance.IsBattle)
        {
            // Freeze velocity so you don't keep sliding
            rb.linearVelocity = Vector3.zero;
            return;
        }

        // Horizontal rotation
        transform.Rotate(Vector3.up * Input.GetAxis("Mouse X") * 2f);

        newVelocity = Vector3.up * rb.linearVelocity.y;
        // new Vector3(0f, rb.Velocity.y, 0f)
        float speed = Input.GetKey(KeyCode.LeftShift) ? runSpeed : walkSpeed;
        newVelocity.x = Input.GetAxis("Horizontal") * speed;
        newVelocity.z = Input.GetAxis("Vertical") * speed;

        if (isGrounded)
        {
            if (Input.GetKeyDown(KeyCode.Space) && !isJumping)
            {
                newVelocity.y = jumpSpeed;
                isJumping = true;
            }
        }

        if ((Input.GetAxis("Vertical") != 0f || Input.GetAxis("Horizontal") != 0f) && isGrounded)
        {
            float bobbingRate = Input.GetKey(KeyCode.LeftShift) ? runBobbingRate : walkBobbingRate;
            float bobbingOffset = Input.GetKey(KeyCode.LeftShift) ? maxRunBobbingOffset : maxWalkBobbingOffset;
            Vector3 targetHeadPosition = Vector3.up * baseCameraHeight + Vector3.up * (Mathf.PingPong(Time.time * runBobbingRate, bobbingOffset) - bobbingOffset * 0.5f);
            head.localPosition = Vector3.Lerp(head.localPosition, targetHeadPosition, 0.1f);

        }
        rb.linearVelocity = transform.TransformDirection(newVelocity);

    }

    void FixedUpdate()
    {

        //Altenrate way of jumping
        //if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 1f))
        //    isGrounded = true;
        //else isGrounded = false;

        // You *can* let this run in battle since it just tracks vyCache,
        // but if you want to be extra safe, guard it too:
        if (GameModeManager.Instance != null && GameModeManager.Instance.IsBattle)
            return;

        vyCache = rb.linearVelocity.y;
    }

    void LateUpdate()
    {
        // If we're in battle, do NOT rotate camera or change FOV
        if (GameModeManager.Instance != null && GameModeManager.Instance.IsBattle)
            return;

        // If camera wasn't wired, try to find it safely.
        if (playerCamera == null)
        {
            // Prefer camera on head or children
            if (head != null) playerCamera = head.GetComponentInChildren<Camera>(true);

            // Fallback: Main Camera
            if (playerCamera == null) playerCamera = Camera.main;

            if (playerCamera == null)
            {
                Debug.LogError("[PlayerController] playerCamera is null. Assign it in the inspector or ensure a Camera exists.");
                return;
            }
        }

        // Vertical rotation (head pitch)
        if (head != null)
        {
            Vector3 e = head.eulerAngles;
            e.x -= Input.GetAxis("Mouse Y") * 2f;
            e.x = RestrictAngle(e.x, -85f, 85f);
            head.eulerAngles = e;
        }

        // FOV
        float fovOffset = (rb.linearVelocity.y < 0f) ? Mathf.Sqrt(Mathf.Abs(rb.linearVelocity.y)) : 0f;
        playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, baseCameraFov + fovOffset, 0.25f);
    }

    // Clamp the vertical head rotation (prevent bending backwards)
    public static float RestrictAngle(float angle, float angleMin, float angleMax)
    {
        if (angle > 180)
            angle -= 360;
        else if (angle < -180)
            angle += 360;

        if (angle > angleMax)
            angle = angleMax;
        if (angle < angleMin)
            angle = angleMin;

        return angle;
    }

    void OnCollisionStay(Collision col)
    {
        isGrounded = true;
        isJumping = false;

    }
    void OnCollisionExit(Collision col)
    {
        isGrounded = false;
    }

    void OnCollisionEnter(Collision col)
    {
        if (Vector3.Dot(col.GetContact(0).normal, Vector3.up) < 0.5f)
        {
            if (rb.linearVelocity.y < -5f)
            {
                rb.linearVelocity = Vector3.up * rb.linearVelocity.y;
                return;
            }
        }
        float acceleration = (rb.linearVelocity.y - vyCache) / Time.fixedDeltaTime;
        float impactForce = rb.mass * Mathf.Abs(acceleration);

        if (impactForce >= impactThreshold)
            Debug.Log("Fall Damage!");
    }
}
