using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;
using FishNet.Connection;
using FishNet.Component.Transforming;
using TMPro;

public class PlayerController : BaseRunner
{
    [Header("UI & Feedback")]
    [SerializeField] private TextMeshProUGUI positionText; 

    private FaceTrackingToMovement faceTracking;
    private Transform activePlatform;
    private Rigidbody _rb;
    private NetworkTransform _netTransform;

    public override void OnStartClient()
    {
        base.OnStartClient();
        _rb = GetComponent<Rigidbody>();
        _netTransform = GetComponent<NetworkTransform>();
        faceTracking = GetComponent<FaceTrackingToMovement>();
        
        BaseAwake();

        if (Owner.IsLocalClient)
        {
            if (_rb != null) 
            {
                _rb.isKinematic = false;
                // Usamos Interpolate para que el renderizado de Unity suavice el movimiento entre FixedUpdates
                _rb.interpolation = RigidbodyInterpolation.Interpolate;
                _rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            }

            var mainCam = Camera.main;
            if (mainCam != null && mainCam.TryGetComponent<CameraFollowPlayer>(out var follow))
            {
                follow.target = transform;
            }
            
            SaveData.ReadFromJson();
            if (SaveData.player != null && SaveData.player.playerCharacterData != null)
            {
                SetCharacter(SaveData.player.playerCharacterData, SaveData.player.username);
            }
        }
        else
        {
            if (_rb != null) 
            {
                _rb.isKinematic = true; 
                _rb.useGravity = false;
                // Los proxies no necesitan interpolación de RB si el NetworkTransform ya la hace
                _rb.interpolation = RigidbodyInterpolation.None; 
            }
            if (faceTracking != null) faceTracking.enabled = false;
        }
    }

    private void FixedUpdate()
    {
        // Solo el dueño local simula para evitar jitter por doble corrección (servidor-cliente)
        if (!IsOwner || !canMove || _rb == null) return;

        HandleLocomotion();
        HandleEnvironment();

        BaseFixedUpdate();
    }

    private void HandleLocomotion()
    {
        Vector3 moveInput = Vector3.zero;

#if !UNITY_EDITOR
        if (faceTracking != null && faceTracking.detectado)
        {
            if (faceTracking.faceRotation != Quaternion.identity)
                _rb.rotation = Quaternion.Slerp(_rb.rotation, faceTracking.faceRotation, rotationSpeed);

            moveInput = transform.forward * baseSpeed * faceTracking.speed * Mathf.Max(0.1f, speedMultiplier);
        }
#else
        moveInput = transform.forward * baseSpeed * Mathf.Max(0.05f, speedMultiplier);
#endif

        Vector3 vel = _rb.linearVelocity;
        _rb.linearVelocity = new Vector3(moveInput.x, vel.y, moveInput.z);
    }

    private void HandleEnvironment()
    {
        float rayLength = runnerHeight * 0.5f + 0.4f; 
        
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, rayLength, whatIsGround))
        {
            // Solo cambiamos el damping si es diferente para evitar jitter físico
            if (_rb.linearDamping != groundDrag) _rb.linearDamping = groundDrag;
            
            if (hit.collider.TryGetComponent<NetworkObject>(out var netObj))
            {
                if (activePlatform != hit.collider.transform)
                {
                    activePlatform = hit.collider.transform;
                    SetParentServerRpc(netObj);
                }
            }

            // CORRECCIÓN ANTI-JITTER:
            // En lugar de asignar la posición directamente, nos acercamos suavemente
            float targetY = hit.point.y + (runnerHeight * 0.5f) + 0.01f;
            if (Mathf.Abs(transform.position.y - targetY) > 0.005f) // Umbral de tolerancia
            {
                float smoothY = Mathf.MoveTowards(transform.position.y, targetY, Time.fixedDeltaTime * 5f);
                transform.position = new Vector3(transform.position.x, smoothY, transform.position.z);
            }
        }
        else
        {
            if (_rb.linearDamping != 0) _rb.linearDamping = 0;
            if (activePlatform != null)
            {
                activePlatform = null;
                SetParentServerRpc(null);
            }
        }
    }

    [ServerRpc]
    private void SetParentServerRpc(NetworkObject parentNetObj)
    {
        if (parentNetObj != null)
            transform.SetParent(parentNetObj.transform, true);
        else
            transform.SetParent(null);
    }

    // --- RPCs de Puntuación (Sin cambios) ---
    public void SetPosition(int pos, int runnerAmount)
    {
        if (!IsOwner) return;
        SetPositionServerRpc(pos, runnerAmount);
    }

    [ServerRpc]
    void SetPositionServerRpc(int pos, int runnerAmount, NetworkConnection conn = null)
    {
        SetPositionTargetRpc(conn, pos, runnerAmount);
    }

    [TargetRpc]
    void SetPositionTargetRpc(NetworkConnection conn, int pos, int runnerAmount)
    {
        if (SaveData.player == null) return;
        if (runnerAmount >= 3)
        {
            int medals = Mathf.RoundToInt((runnerAmount - pos + 1 - runnerAmount / 2f) * Mathf.Lerp(15, 5, runnerAmount / 32f));
            if (positionText != null) positionText.text = $"{pos}/{runnerAmount}";
            SaveData.player.medals += medals;
            if (pos == 1) SaveData.player.wins++;
        }
        if (faceTracking != null)
            SaveData.player.runnedDistance += (int)faceTracking.GetTotalDistance();
        SaveData.SaveToJson();
    }
}