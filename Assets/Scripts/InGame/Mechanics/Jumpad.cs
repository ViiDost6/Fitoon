using UnityEngine;

public class Jumpad : MonoBehaviour
{
    public bool isRepel = false;

    public float jumpForce = 10f;       // Fuerza vertical
    public float forwardForce = 5f;     // Fuerza horizontal hacia adelante

    private void OnTriggerEnter(Collider other)
    {
		BaseRunner player = other.GetComponent<BaseRunner>();
		if (player!= null && player.IsOwner) {
            Bounce(other.gameObject);
        }
    }

    public void Bounce(GameObject other)
    {
        Rigidbody playerRigidbody = other.GetComponent<Rigidbody>();
        if (playerRigidbody != null) {

            Vector3 playerPosition = other.transform.position;
            Vector3 directionToPlayer = new Vector3(playerPosition.x - transform.position.x, 0, playerPosition.z - transform.position.z);
            //directionToPlayer.Normalize();

            playerRigidbody.linearVelocity = Vector3.zero;
            playerRigidbody.angularVelocity = Vector3.zero;

            Vector3 jumpDirection;

            if (isRepel) {
                jumpDirection = other.transform.up * jumpForce + (other.transform.right * directionToPlayer.x + other.transform.forward * directionToPlayer.z) * forwardForce; //Calcular la direcci�n combinada hacia el Player y hacia arriba
            }
            else {
                playerRigidbody.rotation = Quaternion.identity;
                jumpDirection = other.transform.up * jumpForce + other.transform.forward * forwardForce; //Calcular la direcci�n combinada hacia adelante y hacia arriba
            }

            // Aplicar la fuerza combinada
            playerRigidbody.AddForce(jumpDirection, ForceMode.VelocityChange);
        }
    }
}
