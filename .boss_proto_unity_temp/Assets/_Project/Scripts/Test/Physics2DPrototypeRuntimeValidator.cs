using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class Physics2DPrototypeRuntimeValidator : MonoBehaviour
{
    [SerializeField] private Physics2DPrototypePlayer player;
    [SerializeField] private TilemapCollider2D groundCollider;
    [SerializeField] private TilemapCollider2D wallCollider;

    private IEnumerator Start()
    {
        yield return new WaitForSeconds(1f);
        yield return new WaitForFixedUpdate();

        bool noMixedPlayerPhysics = player != null &&
            player.GetComponent<Rigidbody2D>() != null &&
            player.GetComponent<Collider2D>() != null &&
            player.GetComponent<Rigidbody>() == null &&
            player.GetComponent<Collider>() == null;
        bool tilemapPhysicsReady = IsReady(groundCollider) && IsReady(wallCollider);
        int groundMask = 1 << groundCollider.gameObject.layer;
        int wallMask = 1 << wallCollider.gameObject.layer;
        bool groundRayHit = Physics2D.Raycast(new Vector2(0f, 3f), Vector2.down, 5f, groundMask).collider != null;
        bool wallRayHit = Physics2D.Raycast(new Vector2(0f, 2f), Vector2.right, 20f, wallMask).collider != null;
        bool playerLanded = player != null && player.IsGrounded;

        Debug.Log(
            $"[Physics2D Prototype] Runtime state: GroundTiles={groundCollider.GetComponent<Tilemap>().GetUsedTilesCount()}, " +
            $"GroundTilemapShapes={groundCollider.shapeCount}, GroundCompositeShapes={groundCollider.GetComponent<CompositeCollider2D>().shapeCount}, " +
            $"WallTiles={wallCollider.GetComponent<Tilemap>().GetUsedTilesCount()}, " +
            $"WallTilemapShapes={wallCollider.shapeCount}, WallCompositeShapes={wallCollider.GetComponent<CompositeCollider2D>().shapeCount}, " +
            $"PlayerPosition={player.transform.position}",
            this);

        if (noMixedPlayerPhysics && tilemapPhysicsReady && groundRayHit && wallRayHit && playerLanded)
        {
            Debug.Log("[Physics2D Prototype] PASS: 2D-only Player and TilemapCollider2D/CompositeCollider2D environment are active.", this);
#if !UNITY_EDITOR
            if (Application.isBatchMode) Application.Quit(0);
#endif
        }
        else
        {
            Debug.LogError(
                $"[Physics2D Prototype] FAIL: NoMixedPlayerPhysics={noMixedPlayerPhysics}, " +
                $"TilemapPhysicsReady={tilemapPhysicsReady}, GroundRayHit={groundRayHit}, " +
                $"WallRayHit={wallRayHit}, PlayerLanded={playerLanded}",
                this);
#if !UNITY_EDITOR
            if (Application.isBatchMode) Application.Quit(1);
#endif
        }
    }

    private static bool IsReady(TilemapCollider2D tilemapCollider)
    {
        CompositeCollider2D composite = tilemapCollider != null
            ? tilemapCollider.GetComponent<CompositeCollider2D>()
            : null;
        bool collisionGeometryReady = tilemapCollider != null &&
            (tilemapCollider.shapeCount > 0 || (composite != null && composite.shapeCount > 0));
        return tilemapCollider != null &&
               composite != null && collisionGeometryReady &&
               tilemapCollider.GetComponent<Rigidbody2D>()?.bodyType == RigidbodyType2D.Static &&
               tilemapCollider.GetComponent<Tilemap>() != null;
    }
}
