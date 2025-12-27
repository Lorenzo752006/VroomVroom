using UnityEngine;

[CreateAssetMenu(fileName = "NewCarData", menuName = "Racing/Car Data")]
public class CarData : ScriptableObject
{
    [Header("Engine & Power")]
    public float maxMotorTorque = 400f; // Newton Meters
    public float maxSpeed = 200f;       // km/h
    public float brakePower = 2000f;
    
    [Header("Handling")]
    public float maxSteeringAngle = 30f;
    public float tractionControl = 0.5f; // 0 = slippery, 1 = sticky

    [Header("Suspension (Optional overrides)")]
    public float suspensionDistance = 0.2f;
    public float suspensionSpring = 35000f;
    public float suspensionDamper = 4500f;
    
    [Header("Physical Attributes")]
    public float mass = 1500f;
    public Vector3 centerOfMassOffset = new Vector3(0, -0.5f, 0); // Crucial for stability
}