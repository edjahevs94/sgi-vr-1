using System;
using UnityEngine;

public class FlashlightToggle : MonoBehaviour
{
    [SerializeField] Light spotLight;

    public void Toggle()
    {
        spotLight.enabled = !spotLight.enabled;
       Debug.Log("Luz " + spotLight.name + " enabled = " + spotLight.enabled, spotLight);
    }
}