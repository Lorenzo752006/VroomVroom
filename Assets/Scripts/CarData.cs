using UnityEngine;

[CreateAssetMenu(fileName = "NewCarData", menuName = "Racing/Car Data")]
public class CarData : ScriptableObject
{
    [Header("Engine & Power")]
    public float maxMotorTorque = 400f; // Newton Meters
    public float maxSpeed = 200f;       // km/h
    public float brakePower = 2000f;
    public float brakeFrontBias = 0.65f; // 0.5-0.7 typical (front brakes harder)
    public float engineBrakePower = 500f; // Braking force when off throttle
    public float handBrakePower = 3000f; // Rear wheel braking for drifting
    
    [Header("Engine Curve")]
    public float minRPM = 1000f;
    public float maxRPM = 7000f;
    public float peakTorqueRPM = 4500f; // Where max torque occurs
    public AnimationCurve torqueCurve = AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1f); // RPM curve
    
    [Header("Transmission")]
    public int gearCount = 6;
    public float[] gearRatios = new float[] { 3.5f, 2.5f, 1.8f, 1.3f, 1.0f, 0.8f };
    public float finalDriveRatio = 3.5f;
    public float shiftUpRPM = 6500f;
    public float shiftDownRPM = 3000f;
    
    [Header("Handling")]
    public float maxSteeringAngle = 30f;
    public float tractionControl = 0.5f; // 0 = slippery, 1 = sticky
    public float tractionControlMinKph = 10f; // Do not limit torque below this speed
    public float wheelRadius = 0.34f; // For RPM calculation
    
    [Header("Differential")]
    public float differentialLockRatio = 0.5f; // 0 = open diff, 1 = locked diff
    public float differentialSlipThreshold = 50f; // RPM difference before slip
    
    [Header("Tire Load Sensitivity")]
    public bool enableLoadSensitivity = true;
    public float loadSensitivityFactor = 0.6f; // How much load affects grip (0-1)
    public float optimalLoad = 5000f; // Peak grip at this load (Newtons)
    public float steerAtMaxSpeed = 0.5f; // Steering sensitivity at max speed (0-1)
    public bool useSpeedSensitiveSteering = true;
    public float minSteerScale = 0.6f; // Prevent steering from getting too weak at speed
    public float downforce = 50f; // Adds grip at speed
    public float antiRollStiffness = 8000f; // Higher = less body roll
    public float forwardFrictionStiffness = 1.2f; // Higher = more grip
    public float sidewaysFrictionStiffness = 1.5f; // Higher = more grip

    [Header("Suspension (Optional overrides)")]
    public float suspensionDistance = 0.2f;
    public float suspensionSpring = 35000f;
    public float suspensionDamper = 4500f;
    
    [Header("Physical Attributes")]
    public float mass = 1500f;
    public Vector3 centerOfMassOffset = new Vector3(0, -0.9f, 0); // Crucial for stability - lower = more stable
}