using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class TestRespawnManager : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private Transform respawnPoint;
    [SerializeField] private Key respawnKey = Key.T;
    [SerializeField] private EyeballFlyAI[] monstersToReset;

    private Rigidbody playerBody;

    private void Awake()
    {
        CacheReferences();
    }

    private void OnValidate()
    {
        CacheReferences();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard[respawnKey].wasPressedThisFrame)
        {
            RespawnPlayer();
            ResetMonsters();
        }
    }

    public void RespawnPlayer()
    {
        CacheReferences();
        if (player == null || respawnPoint == null)
        {
            Debug.LogWarning("[TestRespawnManager] Player or Respawn Point is not assigned.", this);
            return;
        }

        if (playerBody != null)
        {
            playerBody.linearVelocity = Vector3.zero;
            playerBody.angularVelocity = Vector3.zero;
            playerBody.position = respawnPoint.position;
            return;
        }

        player.position = respawnPoint.position;
    }

    public void ResetMonsters()
    {
        if (monstersToReset == null)
        {
            return;
        }

        foreach (EyeballFlyAI monster in monstersToReset)
        {
            if (monster == null)
            {
                continue;
            }

            EyeballFlyHealth health = monster.GetComponent<EyeballFlyHealth>();
            if (health != null)
            {
                health.ResetHealth();
            }
            else
            {
                monster.ResetMonster();
            }
        }
    }

    private void CacheReferences()
    {
        if (player == null)
        {
            PlatformerPlayer3D foundPlayer = FindFirstObjectByType<PlatformerPlayer3D>();
            if (foundPlayer != null)
            {
                player = foundPlayer.transform;
            }
        }

        if (respawnPoint == null)
        {
            PlayerRespawnPoint foundPoint = FindFirstObjectByType<PlayerRespawnPoint>();
            if (foundPoint != null)
            {
                respawnPoint = foundPoint.transform;
            }
        }

        playerBody = player != null ? player.GetComponent<Rigidbody>() : null;
    }
}
