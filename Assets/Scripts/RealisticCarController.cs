using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class RealisticCarController : MonoBehaviour
{
    [Header("Car Configuration")]
    public CarData carData;

    [Header("Wheel Colliders (Physics)")]
    public WheelCollider frontLeftCollider;
    public WheelCollider frontRightCollider;
    public WheelCollider rearLeftCollider;
    public WheelCollider rearRightCollider;

    [Header("Wheel Bones (Visuals)")]
    public Transform frontLeftBone;
    public Transform frontRightBone;
    public Transform rearLeftBone;
    public Transform rearRightBone;

    [Header("Bone Fixes")]
    // If your wheels are rotated wrong, try (0, 90, 0) or (0, 0, 90)
    public Vector3 wheelRotationOffset; 

    private Rigidbody _rb;
    private float _currentSteerAngle;
    private float _currentAcceleration;
    private float _currentBrakeForce;

    private void Start()
    {
        _rb = GetComponent<Rigidbody>();
        ApplyCarData();
    }

    public void ApplyCarData()
    {
        if (carData == null) return;

        _rb.mass = carData.mass;
        _rb.centerOfMass = carData.centerOfMassOffset;

        UpdateWheelSuspension(frontLeftCollider);
        UpdateWheelSuspension(frontRightCollider);
        UpdateWheelSuspension(rearLeftCollider);
        UpdateWheelSuspension(rearRightCollider);
    }

    private void UpdateWheelSuspension(WheelCollider wheel)
    {
        JointSpring spring = wheel.suspensionSpring;
        spring.spring = carData.suspensionSpring;
        spring.damper = carData.suspensionDamper;
        wheel.suspensionSpring = spring;
        wheel.suspensionDistance = carData.suspensionDistance;
    }

    private void FixedUpdate()
    {
        HandleMotor();
        HandleSteering();
        UpdateWheels();
    }

    private void HandleMotor()
    {
        // ... (Same motor logic as previous script) ...
        float moveInput = Input.GetAxis("Vertical");
        float currentSpeed = _rb.linearVelocity.magnitude * 3.6f;

        if (currentSpeed < carData.maxSpeed)
        {
            _currentAcceleration = moveInput * carData.maxMotorTorque;
        }
        else
        {
            _currentAcceleration = 0;
        }

        rearLeftCollider.motorTorque = _currentAcceleration;
        rearRightCollider.motorTorque = _currentAcceleration;

        _currentBrakeForce = Input.GetKey(KeyCode.Space) ? carData.brakePower : 0f;
        
        frontLeftCollider.brakeTorque = _currentBrakeForce;
        frontRightCollider.brakeTorque = _currentBrakeForce;
        rearLeftCollider.brakeTorque = _currentBrakeForce;
        rearRightCollider.brakeTorque = _currentBrakeForce;
    }

    private void HandleSteering()
    {
        float steerInput = Input.GetAxis("Horizontal");
        _currentSteerAngle = carData.maxSteeringAngle * steerInput;

        frontLeftCollider.steerAngle = _currentSteerAngle;
        frontRightCollider.steerAngle = _currentSteerAngle;
    }

    private void UpdateWheels()
    {
        // We pass the wheel collider and the corresponding BONE
        UpdateBoneTransform(frontLeftCollider, frontLeftBone);
        UpdateBoneTransform(frontRightCollider, frontRightBone);
        UpdateBoneTransform(rearLeftCollider, rearLeftBone);
        UpdateBoneTransform(rearRightCollider, rearRightBone);
    }

    private void UpdateBoneTransform(WheelCollider collider, Transform bone)
    {
        Vector3 pos;
        Quaternion rot;
        
        // 1. Get the physics position/rotation
        collider.GetWorldPose(out pos, out rot);

        // 2. Apply position directly (bones handle position well)
        bone.position = pos;

        // 3. Apply rotation WITH the offset adjustment
        // This takes the physics rotation, and adds your offset on top
        bone.rotation = rot * Quaternion.Euler(wheelRotationOffset);
    }
}