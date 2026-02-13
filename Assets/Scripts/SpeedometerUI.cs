using UnityEngine;
using TMPro;

public class SpeedometerUI : MonoBehaviour
{
    [Header("Target")]
    public Rigidbody targetRigidbody;
    public RealisticCarController carController;

    [Header("UI")]
    public TMP_Text speedText;
    public TMP_Text gearText;
    public RectTransform needleTransform;

    [Header("Speed")]
    public bool useKph = true;
    public float speedSmoothTime = 0.12f;
    public int speedRounding = 0;
    public string speedSuffix = "KM/H";

    [Header("Needle")]
    public float minNeedleAngle = -130f;
    public float maxNeedleAngle = 130f;

    [Header("Gears")]
    public int gearCount = 6;
    public float neutralSpeedThreshold = 2f;

    private float _speedVelocity;
    private float _smoothedSpeed;

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
    }

    private void Update()
    {
        if (targetRigidbody == null) return;

        float speedMps = targetRigidbody.linearVelocity.magnitude;
        float speed = useKph ? speedMps * 3.6f : speedMps * 2.23693629f;

        _smoothedSpeed = Mathf.SmoothDamp(_smoothedSpeed, speed, ref _speedVelocity, speedSmoothTime);

        UpdateSpeedText(_smoothedSpeed);
        UpdateNeedle(_smoothedSpeed);
        UpdateGear(_smoothedSpeed);
    }

    private void UpdateSpeedText(float speed)
    {
        if (speedText == null) return;
        float displaySpeed = Mathf.Round(speed * Mathf.Pow(10f, speedRounding)) / Mathf.Pow(10f, speedRounding);
        speedText.text = string.Format("{0} {1}", displaySpeed.ToString("F" + speedRounding), speedSuffix);
    }

    private void UpdateNeedle(float speed)
    {
        if (needleTransform == null) return;

        float maxSpeed = GetMaxSpeed();
        float t = maxSpeed <= 0.01f ? 0f : Mathf.Clamp01(speed / maxSpeed);
        float angle = Mathf.Lerp(minNeedleAngle, maxNeedleAngle, t);
        needleTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void UpdateGear(float speed)
    {
        if (gearText == null) return;

        if (speed < neutralSpeedThreshold)
        {
            gearText.text = "N";
            return;
        }

        int gear = CalculateGear(speed);
        gearText.text = gear.ToString();
    }

    private int CalculateGear(float speed)
    {
        int gears = Mathf.Max(1, gearCount);
        float maxSpeed = Mathf.Max(neutralSpeedThreshold + 1f, GetMaxSpeed());
        float perGear = maxSpeed / gears;
        int gearIndex = Mathf.Clamp(Mathf.FloorToInt(speed / perGear) + 1, 1, gears);
        return gearIndex;
    }

    private float GetMaxSpeed()
    {
        if (carController != null && carController.carData != null)
        {
            return carController.carData.maxSpeed;
        }

        return useKph ? 200f : 120f;
    }
}
