using TMPro;
using UnityEngine;

public class SpotLightColorDropdown : MonoBehaviour
{
    [SerializeField] TMP_Dropdown dropdown;   // UI > Dropdown - TextMeshPro
    [SerializeField] Light spotLight;

    // El orden de este array es el orden del dropdown
    static readonly (string name, Color color)[] options =
    {
        ("Blanco",   Color.white),
        ("Amarillo", new Color(1f, 0.92f, 0.1f)),
        ("Verde",    Color.green),
        ("Azul",     new Color(0.2f, 0.4f, 1f)),
        ("Rojo",     Color.red),
        ("Morado",   new Color(0.6f, 0.1f, 1f)),
    };

    void Start()
    {
        // Rellena las opciones desde código para que siempre coincidan con los colores
        dropdown.ClearOptions();
        var names = new System.Collections.Generic.List<string>();
        foreach (var o in options) names.Add(o.name);
        dropdown.AddOptions(names);

        dropdown.onValueChanged.AddListener(SetColor);

        // Selecciona el color que ya tenga la luz si coincide, si no, blanco
        dropdown.SetValueWithoutNotify(0);
        SetColor(0);
    }

    void SetColor(int index)
    {
        if (spotLight != null)
            spotLight.color = options[index].color;
    }
}