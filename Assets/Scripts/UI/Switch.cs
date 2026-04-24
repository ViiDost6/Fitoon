using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Switch : MonoBehaviour
{
    public GameObject enabledState;
    public GameObject disabledState;

    private void Update()
    {
        Debug.Log("Current Switch State: " + (enabledState.activeSelf ? "Enabled" : "Disabled"));
    }
    
    public void SetSwitchState(bool state)
    {
        enabledState.SetActive(state);
        disabledState.SetActive(!state);

        if (state)
        {
            DiscoveryHandler.isBotsEnabled = "BotsEnabled";
        }
        else
        {
            DiscoveryHandler.isBotsEnabled = "BotsDisabled";
        }
    }
}
