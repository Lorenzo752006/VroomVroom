using UnityEngine;

[CreateAssetMenu(fileName = "MuscleCarData_1990", menuName = "Racing/Car Data (1990 Muscle)")]
public class MuscleCarData : ScriptableObject
{
    [Header("Engine & Power")]
    public float maxMotorTorque = 520f; // Newton Meters
    public float maxSpeed = 240f;       // km/h
    public float brakePower = 1800f;
    
    [Header("Handling")]
    public float maxSteeringAngle = 25f;
    public float tractionControl = 0.2f; // 0 = slippery, 1 = sticky

    [Header("Suspension (Optional overrides)")]
    public float suspensionDistance = 0.18f;
    public float suspensionSpring = 28000f;
    public float suspensionDamper = 8000f;
    
    [Header("Physical Attributes")]
    public float mass = 1750f;
    public Vector3 centerOfMassOffset = new Vector3(0, -0.35f, 0);
}
