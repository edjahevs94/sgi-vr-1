using UnityEngine;

public class ArmsForwardMove : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] Transform rigRoot;      // XR Origin (el que se mueve)
    [SerializeField] Transform head;         // Main Camera
    [SerializeField] Transform leftHand;     // Left Controller
    [SerializeField] Transform rightHand;    // Right Controller

    [Header("Gesto")]
    [SerializeField] float maxAngle = 35f;       // desviación máxima del mando respecto a la mirada
    [SerializeField] float minArmDistance = 0.35f; // distancia mínima cabeza-mando (brazo estirado)

    [Header("Movimiento")]
    [SerializeField] float speed = 2f;
    [SerializeField] float acceleration = 6f;
    [SerializeField] bool flatMovement = true;     // true: solo horizontal; false: vuela hacia donde miras
    [SerializeField] bool useHandsDirection = false; // true: dirección media de los mandos; false: mirada

    [Header("Colisiones")]
    [Tooltip("Desactiva el CharacterController mientras avanzas para atravesar todo")]
    [SerializeField] CharacterController characterController;

    [SerializeField] bool debug;

    float currentSpeed;

    void Reset()
    {
        rigRoot = transform;
        characterController = GetComponent<CharacterController>();
    }

    bool HandOk(Transform hand)
    {
        Vector3 toHand = hand.position - head.position;
        bool stretched = toHand.magnitude > minArmDistance;
        bool inFront = Vector3.Dot(toHand, head.forward) > 0f;
        bool pointing = Vector3.Angle(hand.forward, head.forward) < maxAngle;

        if (debug)
            Debug.Log($"{hand.name}: dist {toHand.magnitude:F2}, ángulo {Vector3.Angle(hand.forward, head.forward):F0}");

        return stretched && inFront && pointing;
    }

    void Update()
    {
        bool active = HandOk(leftHand) && HandOk(rightHand);

        float target = active ? speed : 0f;
        currentSpeed = Mathf.MoveTowards(currentSpeed, target, acceleration * Time.deltaTime);

        if (characterController != null)
            characterController.enabled = currentSpeed <= 0.001f;

        if (currentSpeed <= 0.001f) return;

        Vector3 dir = useHandsDirection
            ? (leftHand.forward + rightHand.forward).normalized
            : head.forward;

        if (flatMovement)
        {
            dir.y = 0f;
            dir.Normalize();
        }

        rigRoot.position += dir * currentSpeed * Time.deltaTime;
    }
}