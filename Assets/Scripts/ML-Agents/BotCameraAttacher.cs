using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.MLAgents;

#if UNITY_CINEMACHINE
using Cinemachine;
#endif

public class BotCameraAttacher : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private bool followSoloBot = true;
    [SerializeField] private float searchDelay = 0.5f;

    private void Start()
    {
        if (followSoloBot)
        {
            StartCoroutine(SearchAndAttach());
        }
    }

    private IEnumerator SearchAndAttach()
    {
        // Wait a bit for RaceManager to finish spawning
        yield return new WaitForSeconds(searchDelay);

        RunnerAgent[] bots = FindObjectsOfType<RunnerAgent>();
        
        // Only attach if there is exactly one bot (ideal for training/debugging)
        if (bots.Length == 1)
        {
            Transform botTransform = bots[0].transform;
            Debug.Log($"[BotCameraAttacher] Solo bot found: {bots[0].name}. Attaching cameras...");

            // 1. Try to attach to Cinemachine FreeLook
            AttachToCinemachine(botTransform);

            // 2. Try to attach to custom CameraFollowPlayer script
            AttachToCustomCamera(botTransform);
        }
    }

    private void AttachToCinemachine(Transform target)
    {
#if UNITY_CINEMACHINE
        var freeLook = FindObjectOfType<CinemachineFreeLook>();
        if (freeLook != null)
        {
            freeLook.Follow = target;
            freeLook.LookAt = target;
            Debug.Log("[BotCameraAttacher] Attached to Cinemachine FreeLook.");
        }
#else
        // If the symbol is not defined, try via reflection or generic GetComponent to avoid compile errors
        // but let's assume if they have the package, the DLL is there.
        // As a fallback, try to find a component named 'CinemachineFreeLook' through its string name
        GameObject mainCam = GameObject.Find("Main Camera");
        if (mainCam != null)
        {
            // Just searching for any component that might have 'Follow' and 'LookAt'
            Component[] allComponents = FindObjectsOfType<Component>();
            foreach (var comp in allComponents)
            {
                if (comp.GetType().Name.Contains("CinemachineFreeLook"))
                {
                    comp.GetType().GetProperty("Follow")?.SetValue(comp, target);
                    comp.GetType().GetProperty("LookAt")?.SetValue(comp, target);
                    Debug.Log("[BotCameraAttacher] Attached to Cinemachine via Reflection.");
                    break;
                }
            }
        }
#endif
    }

    private void AttachToCustomCamera(Transform target)
    {
        CameraFollowPlayer followScript = FindObjectOfType<CameraFollowPlayer>();
        if (followScript != null)
        {
            followScript.target = target;
            Debug.Log("[BotCameraAttacher] Attached to CameraFollowPlayer script.");
        }
    }
}
