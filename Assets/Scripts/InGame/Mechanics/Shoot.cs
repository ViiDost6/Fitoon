using FishNet.Object;
using UnityEngine;

/// <summary>
/// This class is responsible for shooting projectiles.
/// </summary>
public class Shoot : NetworkBehaviour
{
    public GameObject projectilePrefab;
    public float projectileSpeed;

    private Animator animator;

	public override void OnStartNetwork()
    {
        animator = GetComponent<Animator>();
    }

    public void ShootProjectile()
    {
        ShootProjectileServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    void ShootProjectileServerRpc()
    {
		if (projectilePrefab != null)
		{
			GameObject projectile = Instantiate(projectilePrefab, transform);
			Spawn(projectile);
			projectile.GetComponent<Rigidbody>().linearVelocity = -transform.forward * projectileSpeed;
            ShootProjectileObserverRpc();
		}
	}

    [ObserversRpc]
	void ShootProjectileObserverRpc()
    {
		animator.SetTrigger("Shoot");
	}
}
