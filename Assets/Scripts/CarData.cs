using UnityEngine;

[CreateAssetMenu(fileName = "NewCarData", menuName = "Racing/Car Data")]
public class CarData : ScriptableObject
{
    [Header("Engine & Power")]
    public float maxMotorTorque = 400f; // Newton Meters
    public float maxSpeed = 200f;       // km/h
    public float brakePower = 2000f;
    public float lowSpeedTorqueMultiplier = 1.5f; // Extra torque at low speed
    public float lowSpeedTorqueKph = 30f; // Apply boost below this speed
    
    [Header("Handling")]
    public float maxSteeringAngle = 30f;
    public float tractionControl = 0.5f; // 0 = slippery, 1 = sticky
    public float tractionControlMinKph = 10f; // Do not limit torque below this speed
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