#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class DionaeaAssetBuilder
{
    private const string DataPath = "Assets/_Project/Data/Monsters/DionaeaData.asset";
    private const string PrefabPath = "Assets/_Project/Prefabs/Enemies/Dionaea.prefab";

    static DionaeaAssetBuilder()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null ||
            prefab.GetComponent<DionaeaAttack>() == null ||
            prefab.GetComponentInChildren<DionaeaLightReceiver>(true) == null ||
            prefab.transform.Find("BodyCollider") == null ||
            prefab.GetComponent<DionaeaAnimatorBridge>() == null ||
            prefab.GetComponentInChildren<SpriteRenderer>(true) == null)
        {
            EditorApplication.delayCall += Build;
        }
    }

    [MenuItem("Tools/Project/Build Dionaea (M_OBJ_004)")]
    public static void Build()
    {
        DionaeaAnimationSetupUtility.PrepareAssets();
        MonsterData data = BuildData();
        BuildPrefab(data);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        DionaeaValidationUtility.ValidatePrefab();
        Debug.Log("[DionaeaAssetBuilder] DionaeaData and Dionaea prefab built successfully.");
    }

    private static MonsterData BuildData()
    {
        MonsterData data = AssetDatabase.LoadAssetAtPath<MonsterData>(DataPath);
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<MonsterData>();
            AssetDatabase.CreateAsset(data, DataPath);
        }

        data.monsterId = "M_OBJ_004";
        data.displayName = "Dionaea";
        data.monsterKind = MonsterKind.Dionaea;
        data.maxHp = 1;
        data.contactDamage = 0;
        data.canDetectPlayer = true;
        data.canDetectLight = true;
        data.prioritizePlayer = false;
        data.playerDetectRange = 3f;
        data.lightDetectRange = 5f;
        data.chaseRange = 3f;
        data.requireLineOfSight = true;
        data.obstacleLayerMask = LayerMask.GetMask("Ground", "Wall", "TileObstacle", "Platform", "EnvironmentObstacle");
        data.moveType = MonsterMoveType.None;
        data.moveSpeed = 0f;
        data.returnSpeed = 0f;
        data.lockZPosition = true;
        data.useGravity = false;
        data.attackType = MonsterAttackType.Melee;
        data.attackDamage = 2;
        data.attackRange = 1.2f;
        data.attackCooldown = 1f;
        data.attackDuration = 0.25f;
        data.canAttackPlayer = true;
        data.canAttackLight = false;
        data.lightContractionDelay = 1f;
        data.lightRecoveryDelay = 1f;
        data.deadBoolName = string.Empty;
        EditorUtility.SetDirty(data);
        return data;
    }

    private static void BuildPrefab(MonsterData data)
    {
        GameObject root = new GameObject("Dionaea");
        try
        {
            Transform bodyColliderRoot = CreateAnchor(root.transform, "BodyCollider", new Vector3(0f, 0.28f, 0f));
            BoxCollider collider = bodyColliderRoot.gameObject.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.65f, 0.5f, 0.4f);
            collider.isTrigger = true;

            Rigidbody body = root.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.isKinematic = true;
            body.constraints = RigidbodyConstraints.FreezeAll;

            Transform visual = BuildVisual(root.transform);
            Animator visualAnimator = visual.GetComponent<Animator>();
            Transform detectionOrigin = CreateAnchor(root.transform, "DetectionOrigin", new Vector3(0f, 0.8f, 0f));
            Transform attackOrigin = CreateAnchor(root.transform, "Head_AttackOrigin", new Vector3(0f, 0.8f, 0f));
            Transform lightReceiverRoot = CreateAnchor(root.transform, "LightReceiver", new Vector3(0f, 0.75f, 0f));
            MonsterCore core = root.AddComponent<MonsterCore>();
            core.visualRoot = visual;
            core.monsterRigidbody = body;
            core.mainCollider = collider;

            MonsterDetection detection = root.AddComponent<MonsterDetection>();
            detection.enableDetection = true;
            detection.canDetectPlayer = true;
            detection.canDetectLight = true;
            detection.prioritizePlayer = false;
            detection.playerDetectRange = data.playerDetectRange;
            detection.lightDetectRange = data.lightDetectRange;
            detection.chaseRange = data.chaseRange;
            detection.requireLineOfSight = true;
            detection.obstacleLayerMask = data.obstacleLayerMask;
            detection.lineOfSightStartOffset = new Vector3(0f, 0.8f, 0f);
            detection.targetCheckOffset = new Vector3(0f, 0.5f, 0f);

            MonsterMovement movement = root.AddComponent<MonsterMovement>();
            movement.enableMovement = false;
            movement.movementType = MonsterMovementType.Flying;
            movement.moveSpeed = 0f;
            movement.returnSpeed = 0f;
            movement.returnToHomeWhenLost = false;
            movement.useGravityForGround = false;

            MonsterAttack attack = root.AddComponent<MonsterAttack>();
            attack.enableAttack = true;
            attack.attackRange = data.attackRange;
            attack.attackDamage = 2;
            attack.attackWindup = 0.2f;
            attack.attackInterval = data.attackCooldown;
            attack.attackCooldown = data.attackCooldown;
            attack.allowLightAttackVisual = false;

            MonsterAnimatorBridge bridge = root.AddComponent<MonsterAnimatorBridge>();
            bridge.enableAnimatorBridge = false;
            bridge.animator = visualAnimator;
            bridge.useIsDead = false;

            DionaeaAnimatorBridge dionaeaAnimatorBridge = root.AddComponent<DionaeaAnimatorBridge>();
            dionaeaAnimatorBridge.Animator = visualAnimator;

            // The shipped Player prefab intentionally uses Default layer with Player tag.
            // Keep Player layer too so scene variants on either layer are detected.
            LayerMask playerMask = LayerMask.GetMask("Default", "Player");
            DionaeaAttack dionaeaAttack = root.AddComponent<DionaeaAttack>();
            dionaeaAttack.AttackOrigin = attackOrigin;
            dionaeaAttack.VisualRoot = visual;
            dionaeaAttack.Configure(2, playerMask);

            DionaeaAI dionaeaAI = root.AddComponent<DionaeaAI>();
            DionaeaLightReceiver lightReceiver = lightReceiverRoot.gameObject.AddComponent<DionaeaLightReceiver>();
            lightReceiver.Configure(dionaeaAI, data.lightContractionDelay);

            SerializedObject serializedAI = new SerializedObject(dionaeaAI);
            serializedAI.FindProperty("detectionOrigin").objectReferenceValue = detectionOrigin;
            serializedAI.FindProperty("forwardRoot").objectReferenceValue = root.transform;
            serializedAI.FindProperty("forwardDirection").vector3Value = Vector3.up;
            serializedAI.FindProperty("detectionBoxOffset").vector3Value = new Vector3(0f, 1.5f, 0f);
            serializedAI.FindProperty("playerLayerMask").intValue = playerMask.value;
            serializedAI.FindProperty("obstacleLayerMask").intValue = data.obstacleLayerMask.value;
            serializedAI.FindProperty("attackTargetLayerMask").intValue = playerMask.value;
            serializedAI.FindProperty("attackRange").floatValue = data.attackRange;
            serializedAI.FindProperty("attackCooldown").floatValue = data.attackCooldown;
            serializedAI.FindProperty("attackWindup").floatValue = 0.25f;
            serializedAI.FindProperty("attackDamage").intValue = 2;
            serializedAI.FindProperty("requiredLightExposureTime").floatValue = data.lightContractionDelay;
            serializedAI.FindProperty("retractAnimationDuration").floatValue = 1f;
            serializedAI.FindProperty("recoverAnimationDuration").floatValue = 1f;
            serializedAI.FindProperty("postRecoverAttackLockTime").floatValue = 0.5f;
            serializedAI.FindProperty("recoverFromLightDelay").floatValue = data.lightRecoveryDelay;
            serializedAI.FindProperty("waitRetractAnimationBeforeFullRetracted").boolValue = true;
            serializedAI.FindProperty("canDie").boolValue = false;
            serializedAI.FindProperty("isIndestructible").boolValue = true;
            serializedAI.FindProperty("dionaeaAttack").objectReferenceValue = dionaeaAttack;
            serializedAI.FindProperty("lightReceiver").objectReferenceValue = lightReceiver;
            serializedAI.FindProperty("visualRoot").objectReferenceValue = visual;
            serializedAI.FindProperty("dionaeaAnimatorBridge").objectReferenceValue = dionaeaAnimatorBridge;
            serializedAI.ApplyModifiedPropertiesWithoutUndo();

            DataDrivenMonsterController controller = root.AddComponent<DataDrivenMonsterController>();
            SerializedObject serializedController = new SerializedObject(controller);
            serializedController.FindProperty("monsterData").objectReferenceValue = data;
            serializedController.FindProperty("applyOnAwake").boolValue = false;
            serializedController.FindProperty("applyOnStart").boolValue = false;
            serializedController.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static Transform CreateAnchor(Transform parent, string name, Vector3 localPosition)
    {
        GameObject anchor = new GameObject(name);
        anchor.transform.SetParent(parent, false);
        anchor.transform.localPosition = localPosition;
        return anchor.transform;
    }

    private static Transform BuildVisual(Transform parent)
    {
        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(parent, false);
        SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
        renderer.sprite = DionaeaAnimationSetupUtility.LoadDefaultSprite();
        renderer.sortingOrder = 10;
        Animator animator = visual.AddComponent<Animator>();
        animator.runtimeAnimatorController = DionaeaAnimationSetupUtility.LoadAnimatorController();
        animator.applyRootMotion = false;
        return visual.transform;
    }
}
#endif
