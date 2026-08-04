# Cosmos Lift (M_OBJ_005)

`CosmosLift3D` is an immortal, monster-classified environmental lift. It does not use the monster health or attack systems.

## Setup

- Use `Assets/_Project/Prefabs/Enemies/CosmosLift.prefab`, or run **Tools > Summer Camp > Cosmos Lift > Build Prefab**.
- Set **Maximum Height** on `CosmosLift3D` per placed instance.
- **Rise Duration**, **Retract Duration**, and **Darkness Hold Duration** control the full motion cycle.
- The bud owns a kinematic 3D `Rigidbody` and a non-trigger 3D `BoxCollider`. Its collider is disabled while fully nested and becomes a platform as it rises.
- Active scene `Light` components tagged `light` are detected automatically. Runtime-created camera lights already receive this tag and are picked up on the next scan. Other authored lights can be assigned under **Additional Lights**.
- For authored animation, assign an Animator and either a float parameter named `Growth` or a state in **Growth State Name**. Normalized time is driven from 0 to 1 and naturally runs backward during retraction.

`Rigidbody2D` and `Collider2D` are intentionally unsupported.
