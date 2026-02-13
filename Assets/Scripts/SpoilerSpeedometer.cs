using UnityEngine;
using TMPro;

public class SpoilerSpeedometer : MonoBehaviour
{
    [Header("Target")]
    public Rigidbody targetRigidbody;
    public RealisticCarController carController;

    [Header("UI")]
    public TMP_Text speedText;
    public TMP_Text gearText;
    public TMP_Text unitText;

    [Header("Units")]
    public bool useKph = true;
    public string kphSuffix = "KM/H";
    public string mphSuffix = "MPH";
    public int speedRounding = 0;

    [Header("Follow")]
    public Transform followTarget;
    public Vector3 positionOffset = new Vector3(0f, 0.2f, 0f);
    public bool faceCamera = true;
    public Camera targetCamera;
    public float followSmoothTime = 0.08f;

    private Vector3 _followVelocity;

    private void Awake()
    {
        if (targetRigidbody == null && carController != null)
        {
            targetRigidbody = carController.GetComponent<Rigidbody>();
        }

        if (carController == null && targetRigidbody != null)
        {
            carController = targetRigidbody.GetComponent<RealisticCarController>();
        }

        if (followTarget == null)
        {
            if (carController != null)
            {
                followTarget = carController.transform;
            }
            else if (targetRigidbody != null)
            {
                followTarget = targetRigidbody.transform;
            }
        }

        Rigidbody selfRb = GetComponent<Rigidbody>();
        if (selfRb != null)
        {
            selfRb.useGravity = false;
            selfRb.isKinematic = true;
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }
    }

    private void LateUpdate()
    {
        UpdateFollow();
        UpdateText();
        UpdateBillboard();
    }

    private void UpdateFollow()
    {
        if (followTarget == null) return;

        Vector3 targetPos = followTarget.position + positionOffset;
        transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref _followVelocity, followSmoothTime);
    }

    private void UpdateText()
    {
        if (targetRigidbody == null) return;

        float speedMps = targetRigidbody.linearVelocity.magnitude;
        float speed = useKph ? speedMps * 3.6f : speedMps * 2.23693629f;
        float displaySpeed = Mathf.Round(speed * Mathf.Pow(10f, speedRounding)) / Mathf.Pow(10f, speedRounding);

        if (speedText != null)
        {
            speedText.text = displaySpeed.ToString("F" + speedRounding);
        }

        if (unitText != null)
        {
            unitText.text = useKph ? kphSuffix : mphSuffix;
        }

        if (gearText != null)
        {
            if (carController != null)
            {
                gearText.text = carController.GetCurrentGear().ToString();
            }
            else
            {
                gearText.text = "N";
            }
        }
    }

    private void UpdateBillboard()
    {
        if (!faceCamera || targetCamera == null) return;

        Vector3 forward = targetCamera.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f) return;

        transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
    }
}
