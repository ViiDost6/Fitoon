using UnityEngine;
using System.Collections;
using Unity.MLAgents;
using Cinemachine; // Asegúrate de tener el paquete instalado

public class BotCameraAttacher : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private bool followSoloBot = true;
    [SerializeField] private float searchInterval = 0.5f;

    private Transform _currentTarget;

    private void Start()
    {
        if (followSoloBot)
        {
            // Usamos InvokeRepeating para que si el bot reaparece, la cámara lo encuentre
            InvokeRepeating(nameof(ValidateAndAttach), 0.1f, searchInterval);
        }
    }

    private void ValidateAndAttach()
    {
        // Si ya tenemos un target y sigue vivo, no buscamos más
        if (_currentTarget != null) return;

        RunnerAgent[] bots = Object.FindObjectsByType<RunnerAgent>(FindObjectsSortMode.None);
        
        // Solo nos interesa si hay exactamente uno (modo debug/test)
        if (bots.Length == 1)
        {
            _currentTarget = bots[0].transform;
            ApplyTargetToCameras(_currentTarget);
        }
    }

    private void ApplyTargetToCameras(Transform target)
    {
        Debug.Log($"[BotCameraAttacher] Vinculando cámara a: {target.name}");

        // 1. Cinemachine (La forma más limpia si usas el paquete)
        CinemachineVirtualCamera vcam = Object.FindObjectOfType<CinemachineVirtualCamera>();
        if (vcam != null)
        {
            vcam.Follow = target;
            vcam.LookAt = target;
        }

        CinemachineFreeLook freeLook = Object.FindObjectOfType<CinemachineFreeLook>();
        if (freeLook != null)
        {
            freeLook.Follow = target;
            freeLook.LookAt = target;
        }

        // 2. Tu script personalizado
        CameraFollowPlayer followScript = Object.FindObjectOfType<CameraFollowPlayer>();
        if (followScript != null)
        {
            followScript.target = target;
        }
    }
}