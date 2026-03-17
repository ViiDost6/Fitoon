using UnityEngine;

[RequireComponent(typeof(BotRunner))]
public class ManualBotController : MonoBehaviour
{
    private BotRunner _runner;
    private Unity.MLAgents.Agent _agent;
    
    [Header("Configuración de Control")]
    public bool takeControl = true;
    
    [Header("Info de Teclas")]
    [SerializeField] private string forwardAxis = "Vertical";
    [SerializeField] private string lateralAxis = "Horizontal";

    void Awake()
    {
        _runner = GetComponent<BotRunner>();
        _agent = GetComponent<Unity.MLAgents.Agent>();
    }

    void OnEnable()
    {
        // Al activar el script, apagamos la IA para que no interfiera
        if (_agent != null)
        {
            _agent.enabled = false;
            Debug.Log($"<color=cyan>[ManualControl]</color> IA desactivada en {gameObject.name}. Control manual activo.");
        }
    }

    void OnDisable()
    {
        // Al desactivar el script, devolvemos el control a la IA
        if (_agent != null)
        {
            _agent.enabled = true;
            Debug.Log($"<color=orange>[ManualControl]</color> Control manual devuelto a la IA en {gameObject.name}.");
        }
        
        // Reset de movimiento para que no se quede pegado
        if (_runner != null) _runner.SetMovement(0, 0);
    }

    void Update()
    {
        if (!takeControl) return;

        // Leemos el input del teclado
        float moveV = Mathf.Clamp01(Input.GetAxis(forwardAxis)); // Solo adelante (0 a 1)
        float moveH = Input.GetAxis(lateralAxis); // Izquierda/Derecha (-1 a 1)

        // Enviamos los datos al controlador que ya usan los bots
        if (_runner != null)
        {
            _runner.SetMovement(moveV, moveH);
        }
    }
}