using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class MonsterWorldSimulationGate3D : MonoBehaviour
{
    private readonly List<ColliderPair> ignoredPlayerPairs = new List<ColliderPair>(16);
    private readonly Dictionary<WorldPresence, bool> presenceStates = new Dictionary<WorldPresence, bool>();
    private bool playerInteractionAllowed = true;
    private bool worldPhysicsSuspended;
    private bool rigidbodyStatesCaptured;
    private RigidbodyState[] rigidbodyStates = System.Array.Empty<RigidbodyState>();
    private Vector3[] suspendedLinearVelocities = System.Array.Empty<Vector3>();
    private Vector3[] suspendedAngularVelocities = System.Array.Empty<Vector3>();

    public bool PlayerInteractionAllowed => playerInteractionAllowed;
    public bool IsWorldPhysicsSuspended => worldPhysicsSuspended;

    public void SetPresence(WorldPresence source, bool present)
    {
        if (source == null) return;
        presenceStates[source] = present;
        bool allowed = false;
        foreach (KeyValuePair<WorldPresence, bool> state in presenceStates)
        {
            if (state.Key != null && state.Value) { allowed = true; break; }
        }
        ApplyAggregatePresence(allowed);
    }

    public void RemovePresence(WorldPresence source)
    {
        if (source == null || !presenceStates.Remove(source)) return;
        bool allowed = presenceStates.Count == 0;
        if (!allowed)
        {
            foreach (KeyValuePair<WorldPresence, bool> state in presenceStates)
            {
                if (state.Key != null && state.Value) { allowed = true; break; }
            }
        }
        ApplyAggregatePresence(allowed);
    }

    public static bool AllowsPlayerInteraction(Component source)
    {
        if (source == null) return true;
        MonsterWorldSimulationGate3D gate = source.GetComponentInParent<MonsterWorldSimulationGate3D>();
        return gate == null || gate.playerInteractionAllowed;
    }

    public static bool IsPhysicsSuspended(Component source)
    {
        if (source == null) return false;
        MonsterWorldSimulationGate3D gate = source.GetComponentInParent<MonsterWorldSimulationGate3D>();
        return gate != null && gate.worldPhysicsSuspended;
    }

    private void OnDestroy()
    {
        RestoreIgnoredPairs();
        RestoreWorldPhysics();
    }

    private void ApplyAggregatePresence(bool present)
    {
        if (playerInteractionAllowed != present)
        {
            playerInteractionAllowed = present;
            RestoreIgnoredPairs();
            if (!present) IgnorePlayerCollisions();
        }

        if (present) RestoreWorldPhysics();
        else SuspendWorldPhysics();
    }

    private void CaptureRigidbodyStatesOnce()
    {
        if (rigidbodyStatesCaptured) return;
        Rigidbody[] bodies = GetComponentsInChildren<Rigidbody>(true);
        rigidbodyStates = new RigidbodyState[bodies.Length];
        suspendedLinearVelocities = new Vector3[bodies.Length];
        suspendedAngularVelocities = new Vector3[bodies.Length];
        for (int i = 0; i < bodies.Length; i++) rigidbodyStates[i] = new RigidbodyState(bodies[i]);
        rigidbodyStatesCaptured = true;
    }

    private void SuspendWorldPhysics()
    {
        CaptureRigidbodyStatesOnce();
        bool captureMotion = !worldPhysicsSuspended;
        for (int i = 0; i < rigidbodyStates.Length; i++)
        {
            Rigidbody body = rigidbodyStates[i].Body;
            if (body == null) continue;
            if (!body.isKinematic)
            {
                if (captureMotion)
                {
                    suspendedLinearVelocities[i] = body.linearVelocity;
                    suspendedAngularVelocities[i] = body.angularVelocity;
                }
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            body.detectCollisions = false;
            body.useGravity = false;
            body.isKinematic = true;
        }
        worldPhysicsSuspended = true;
    }

    private void RestoreWorldPhysics()
    {
        if (!worldPhysicsSuspended) return;
        for (int i = 0; i < rigidbodyStates.Length; i++)
        {
            RigidbodyState state = rigidbodyStates[i];
            Rigidbody body = state.Body;
            if (body == null) continue;
            body.constraints = state.Constraints;
            body.interpolation = state.Interpolation;
            body.useGravity = state.UseGravity;
            body.isKinematic = state.IsKinematic;
            body.detectCollisions = state.DetectCollisions;
            if (!state.IsKinematic)
            {
                body.linearVelocity = suspendedLinearVelocities[i];
                body.angularVelocity = suspendedAngularVelocities[i];
            }
        }
        worldPhysicsSuspended = false;
    }

    private void IgnorePlayerCollisions()
    {
        GameObject player;
        try { player = GameObject.FindGameObjectWithTag("Player"); }
        catch (UnityException) { return; }
        if (player == null) return;

        Collider[] monsterColliders = GetComponentsInChildren<Collider>(true);
        Collider[] playerColliders = player.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < monsterColliders.Length; i++)
        {
            Collider monsterCollider = monsterColliders[i];
            if (monsterCollider == null) continue;
            for (int j = 0; j < playerColliders.Length; j++)
            {
                Collider playerCollider = playerColliders[j];
                if (playerCollider == null) continue;
                if (Physics.GetIgnoreCollision(monsterCollider, playerCollider)) continue;
                Physics.IgnoreCollision(monsterCollider, playerCollider, true);
                ignoredPlayerPairs.Add(new ColliderPair(monsterCollider, playerCollider));
            }
        }
    }

    private void RestoreIgnoredPairs()
    {
        for (int i = 0; i < ignoredPlayerPairs.Count; i++)
        {
            ColliderPair pair = ignoredPlayerPairs[i];
            if (pair.Monster != null && pair.Player != null) Physics.IgnoreCollision(pair.Monster, pair.Player, false);
        }
        ignoredPlayerPairs.Clear();
    }

    private readonly struct ColliderPair
    {
        public readonly Collider Monster;
        public readonly Collider Player;
        public ColliderPair(Collider monster, Collider player) { Monster = monster; Player = player; }
    }

    private readonly struct RigidbodyState
    {
        public readonly Rigidbody Body;
        public readonly bool IsKinematic;
        public readonly bool UseGravity;
        public readonly bool DetectCollisions;
        public readonly RigidbodyConstraints Constraints;
        public readonly RigidbodyInterpolation Interpolation;

        public RigidbodyState(Rigidbody body)
        {
            Body = body;
            IsKinematic = body != null && body.isKinematic;
            UseGravity = body != null && body.useGravity;
            DetectCollisions = body != null && body.detectCollisions;
            Constraints = body != null ? body.constraints : RigidbodyConstraints.None;
            Interpolation = body != null ? body.interpolation : RigidbodyInterpolation.None;
        }
    }
}
