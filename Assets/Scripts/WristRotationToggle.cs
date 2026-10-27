using UnityEngine;

public class WristRotationToggle : MonoBehaviour
{
    [SerializeField] Transform controller;   // Left Controller
    [SerializeField] GameObject menu;        // Canvas WristMenu

    [Header("Condición de rotación")]
    [SerializeField] Vector3 controllerAxis = Vector3.up;   // eje LOCAL del mando
    [SerializeField] Vector3 worldTarget = Vector3.up;      // dirección del MUNDO
    [SerializeField] float showAngle = 40f;   // se muestra si el ángulo es menor
    [SerializeField] float hideAngle = 55f;   // se oculta si es mayor (histéresis)

    [SerializeField] bool debugAngle;

    void Update()
    {
        Vector3 axisWorld = controller.TransformDirection(controllerAxis);
        float angle = Vector3.Angle(axisWorld, worldTarget);

        if (debugAngle) Debug.Log($"Ángulo: {angle:F0}");

        if (!menu.activeSelf && angle < showAngle) menu.SetActive(true);
        else if (menu.activeSelf && angle > hideAngle) menu.SetActive(false);
    }
}