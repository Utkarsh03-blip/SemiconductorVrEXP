using System;
using System.Collections.Generic;
using System.Text;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using Object = UnityEngine.Object;

namespace PNJunctionLab.Editor
{
    /// <summary>
    /// Configures XR Origin player collision (CharacterController + CharacterControllerDriver),
    /// audits all table/workbench/equipment BoxColliders, upgrades all XRGrabInteractables to
    /// ContinuousDynamic + VelocityTracking + ThrowOnDetach cleanly above 0.90m tabletops,
    /// repositions all 8 NPCStandPoints to the player's left side beside each workbench, sets NPC radius=0.26,
    /// and rebakes LabNavMeshData without modifying lesson logic, formulas, or station scripts.
    /// </summary>
    public static class CollisionAndNavMeshAuditFixer
    {
        private const string ScenePath = "Assets/PNJunctionLab/Scenes/PNJunctionLab.unity";

        private struct StandPointPlacement
        {
            public string StationId;
            public float LeftOffsetMeters;
            public float ForwardOffsetMeters;
        }

        private static readonly StandPointPlacement[] LeftSideStandPoints =
        {
            new StandPointPlacement { StationId = "T", LeftOffsetMeters = 1.35f, ForwardOffsetMeters = 0.55f },
            new StandPointPlacement { StationId = "1", LeftOffsetMeters = 1.48f, ForwardOffsetMeters = 0.40f },
            new StandPointPlacement { StationId = "2", LeftOffsetMeters = 1.52f, ForwardOffsetMeters = 0.35f },
            new StandPointPlacement { StationId = "3", LeftOffsetMeters = 1.65f, ForwardOffsetMeters = 0.65f },
            new StandPointPlacement { StationId = "4", LeftOffsetMeters = 1.72f, ForwardOffsetMeters = 0.60f },
            new StandPointPlacement { StationId = "5", LeftOffsetMeters = 1.65f, ForwardOffsetMeters = 0.60f },
            new StandPointPlacement { StationId = "6", LeftOffsetMeters = 1.48f, ForwardOffsetMeters = 0.40f },
            new StandPointPlacement { StationId = "A", LeftOffsetMeters = 1.24f, ForwardOffsetMeters = 0.53f },
        };

        [MenuItem("PNJunctionLab/Fix Collisions & NPC Walkway")]
        public static void ApplyAllFixesMenu()
        {
            Debug.Log(ApplyAllFixes());
        }

        /// <summary>
        /// Applies all player collision, table collider, grabbable physics, NPC corner placement,
        /// and NavMesh rebuild fixes cleanly in Edit Mode and saves the scene & PNJunctionLab prefabs.
        /// </summary>
        public static string ApplyAllFixes()
        {
            Scene activeScene = EditorSceneManager.GetActiveScene();
            if (!activeScene.path.EndsWith("PNJunctionLab.unity", StringComparison.OrdinalIgnoreCase))
            {
                activeScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            StringBuilder report = new StringBuilder();
            report.AppendLine(ConfigurePlayerCollision());
            report.AppendLine(AuditAndFixStationAndBenchColliders());
            report.AppendLine(FixBatteryLeadsAndTabletDockClearances());
            report.AppendLine(ConfigureAllGrabbables());
            report.AppendLine(RepositionStandPointsAndRebuildNavMesh());

            ApplyProjectPrefabChanges();

            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return report.ToString();
        }

        private static string ConfigurePlayerCollision()
        {
            XROrigin xrOrigin = Object.FindAnyObjectByType<XROrigin>();
            if (xrOrigin == null)
            {
                return "PlayerCollision: XROrigin not found.";
            }

            CharacterController cc = xrOrigin.GetComponent<CharacterController>();
            if (cc == null)
            {
                cc = xrOrigin.gameObject.AddComponent<CharacterController>();
            }

            cc.radius = 0.28f;
            cc.height = 1.70f;
            cc.center = new Vector3(0f, 0.85f, 0f);
            cc.stepOffset = 0.20f;
            cc.skinWidth = 0.04f;
            cc.slopeLimit = 45f;
            EditorUtility.SetDirty(cc);

            ContinuousMoveProvider moveProvider = xrOrigin.GetComponentInChildren<ContinuousMoveProvider>(true);
            XRBodyTransformer bodyTransformer = xrOrigin.GetComponentInChildren<XRBodyTransformer>(true);
            if (bodyTransformer != null)
            {
                SerializedObject btSo = new SerializedObject(bodyTransformer);
                SerializedProperty useCcProp = btSo.FindProperty("m_UseCharacterControllerIfExists");
                if (useCcProp != null)
                {
                    useCcProp.boolValue = true;
                    btSo.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(bodyTransformer);
                }
            }

#pragma warning disable CS0618 // User explicitly requested XRI's CharacterControllerDriver alongside CharacterController.
            CharacterControllerDriver driver = xrOrigin.GetComponent<CharacterControllerDriver>();
            if (driver == null)
            {
                driver = xrOrigin.gameObject.AddComponent<CharacterControllerDriver>();
            }

            driver.locomotionProvider = moveProvider;
            driver.minHeight = 1.70f;
            driver.maxHeight = 2.00f;
            EditorUtility.SetDirty(driver);
#pragma warning restore CS0618

            // Replace inflated 0.75m-tall CapsuleColliders on floor AnchorPads with flat BoxColliders
            // so players can walk smoothly over teleport discs without hitting a 0.38m obstacle.
            int fixedPads = 0;
            TeleportationAnchor[] anchors = Object.FindObjectsByType<TeleportationAnchor>();
            for (int i = 0; i < anchors.Length; i++)
            {
                Transform pad = anchors[i].transform.Find("AnchorPad");
                if (pad == null) continue;

                CapsuleCollider oldCap = pad.GetComponent<CapsuleCollider>();
                if (oldCap != null)
                {
                    Object.DestroyImmediate(oldCap);
                }

                BoxCollider padBox = pad.GetComponent<BoxCollider>();
                if (padBox == null)
                {
                    padBox = pad.gameObject.AddComponent<BoxCollider>();
                }

                padBox.enabled = true;
                padBox.isTrigger = false;
                padBox.center = Vector3.zero;
                padBox.size = new Vector3(1f, 2f, 1f);
                EditorUtility.SetDirty(padBox);
                fixedPads++;
            }

            EditorUtility.SetDirty(xrOrigin.gameObject);
            return $"PlayerCollision: CC(r={cc.radius}, h={cc.height}, c={cc.center}), Driver={(driver != null)}, MoveProvider={(moveProvider != null ? moveProvider.name : "null")}, FlattenedAnchorPads={fixedPads}.";
        }

        private static string AuditAndFixStationAndBenchColliders()
        {
            int fixedCount = 0;
            LessonManager lm = Object.FindAnyObjectByType<LessonManager>();
            if (lm != null && lm.Stations != null)
            {
                for (int i = 0; i < lm.Stations.Count; i++)
                {
                    StationController st = lm.Stations[i];
                    if (st == null) continue;

                    Transform[] children = st.GetComponentsInChildren<Transform>(true);
                    for (int c = 0; c < children.Length; c++)
                    {
                        Transform tr = children[c];
                        string n = tr.name;
                        bool isTableOrFrame = n == "TableTop"
                            || n.StartsWith("Leg_", StringComparison.Ordinal)
                            || n == "ScreenFrame"
                            || n == "Frame"
                            || n == "Backing"
                            || n == "BoardFrame"
                            || n == "BoardStandL"
                            || n == "BoardStandR"
                            || n == "BoardFootL"
                            || n == "BoardFootR"
                            || n == "FurnaceDeck"
                            || n == "RackBase"
                            || n == "RackUpperTier"
                            || n == "Tray"
                            || n == "TrayDeck"
                            || n == "SamplesTray"
                            || n == "CircuitDeck"
                            || n == "JigDeck"
                            || n == "BatteryHousing"
                            || n == "ChamberBase"
                            || n == "AnalyzerHousing";

                        if (!isTableOrFrame) continue;

                        MeshFilter mf = tr.GetComponent<MeshFilter>();
                        if (mf == null) continue;

                        Collider existing = tr.GetComponent<Collider>();
                        if (existing != null && !(existing is BoxCollider))
                        {
                            Object.DestroyImmediate(existing);
                            existing = null;
                        }

                        BoxCollider box = existing as BoxCollider;
                        if (box == null)
                        {
                            box = tr.gameObject.AddComponent<BoxCollider>();
                        }

                        box.enabled = true;
                        box.isTrigger = false;
                        if (mf.sharedMesh != null)
                        {
                            box.center = mf.sharedMesh.bounds.center;
                            box.size = mf.sharedMesh.bounds.size;
                        }
                        EditorUtility.SetDirty(box);
                        fixedCount++;
                    }
                }
            }

            // Ensure room walls, floor, ceiling, LabTabletStationDock, and DebugPanelStand colliders are enabled & non-trigger.
            string[] rootNames = { "[LabRoom]", "LabTabletStationDock", "DebugPanelStand" };
            for (int r = 0; r < rootNames.Length; r++)
            {
                GameObject root = GameObject.Find(rootNames[r]);
                if (root == null) continue;

                BoxCollider[] boxes = root.GetComponentsInChildren<BoxCollider>(true);
                for (int b = 0; b < boxes.Length; b++)
                {
                    if (boxes[b].gameObject.name.Contains("Respawn")) continue;
                    boxes[b].enabled = true;
                    boxes[b].isTrigger = false;
                    EditorUtility.SetDirty(boxes[b]);
                    fixedCount++;
                }
            }

            return $"StationAndBenchColliders: Audited/enforced {fixedCount} non-trigger BoxColliders across T, 1..6, A, Room, Dock, and DebugStand.";
        }

        private static string FixBatteryLeadsAndTabletDockClearances()
        {
            int adjusted = 0;

            // Shift Station 5 battery leads + holsters slightly forward (-X in world space) so their 9cm x 6cm colliders
            // do not intersect BatteryHousing (world X = 5.420..5.720) on spawn or VelocityTracking pickup.
            GameObject st5 = GameObject.Find("Station_5_Biasing");
            if (st5 != null)
            {
                Transform batteryRoot = FindChildRecursive(st5.transform, "VariableDCBattery");
                if (batteryRoot != null)
                {
                    Renderer housingRend = FindChildRecursive(batteryRoot, "BatteryHousing")?.GetComponent<Renderer>();
                    float housingMinWorldX = housingRend != null ? housingRend.bounds.min.x : 5.42f;

                    string[] leadNames = { "Lead_Red_Positive", "Lead_Black_Negative" };
                    for (int i = 0; i < leadNames.Length; i++)
                    {
                        Transform lead = FindChildRecursive(batteryRoot, leadNames[i]);
                        Transform holster = FindChildRecursive(batteryRoot, leadNames[i] + "_Holster");
                        if (lead == null) continue;

                        Collider leadCol = lead.GetComponent<Collider>();
                        float leadHalfX = leadCol != null ? leadCol.bounds.extents.x : 0.045f;
                        float desiredWorldX = housingMinWorldX - leadHalfX - 0.015f;
                        if (lead.position.x > desiredWorldX)
                        {
                            Vector3 lp = lead.position;
                            lp.x = desiredWorldX;
                            lead.position = lp;
                            EditorUtility.SetDirty(lead);

                            RespawnableItem ri = lead.GetComponent<RespawnableItem>();
                            if (ri != null)
                            {
                                SerializedObject so = new SerializedObject(ri);
                                Transform ha = so.FindProperty("homeAnchor")?.objectReferenceValue as Transform;
                                if (ha != null)
                                {
                                    Vector3 hp = ha.position;
                                    hp.x = desiredWorldX;
                                    ha.position = hp;
                                    EditorUtility.SetDirty(ha);
                                }
                            }

                            if (holster != null)
                            {
                                Vector3 holPos = holster.position;
                                holPos.x = desiredWorldX;
                                holster.position = holPos;
                                EditorUtility.SetDirty(holster);
                            }

                            adjusted++;
                        }
                    }
                }
            }

            return $"ClearanceFixes: Adjusted {adjusted} Station 5 lead clearances.";
        }

        private static string ConfigureAllGrabbables()
        {
            XRGrabInteractable[] grabs = Object.FindObjectsByType<XRGrabInteractable>();
            int configured = 0;
            int liftedHomes = 0;
            int fixedNonConvex = 0;

            for (int i = 0; i < grabs.Length; i++)
            {
                XRGrabInteractable grab = grabs[i];
                if (grab == null) continue;

                // Ensure valid non-trigger primitive or convex collider.
                Collider primaryCol = EnsureValidGrabbableColliders(grab.gameObject, ref fixedNonConvex);

                // Configure Rigidbody for ContinuousDynamic collision detection.
                Rigidbody rb = grab.GetComponent<Rigidbody>();
                if (rb == null)
                {
                    rb = grab.gameObject.AddComponent<Rigidbody>();
                }
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                EditorUtility.SetDirty(rb);

                // Configure XRI VelocityTracking + ThrowOnDetach so held items collide solidly with tables.
                grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
                grab.throwOnDetach = true;
                EditorUtility.SetDirty(grab);

                // Verify home/initial Y sits cleanly above the supporting surface/tabletop (>= +0.01m).
                if (primaryCol != null && grab.name != "LabTablet")
                {
                    Physics.SyncTransforms();
                    Vector3 curPos = grab.transform.position;
                    float colCenterOffsetY = primaryCol.bounds.center.y - curPos.y;
                    float halfH = primaryCol.bounds.extents.y;

                    float supportSurfaceY = FindSupportingSurfaceY(grab.transform, curPos, 0.90f);
                    float requiredRootY = supportSurfaceY + halfH + 0.01f - colCenterOffsetY;

                    if (curPos.y < requiredRootY - 0.0005f)
                    {
                        curPos.y = requiredRootY;
                        grab.transform.position = curPos;
                        EditorUtility.SetDirty(grab.transform);
                        liftedHomes++;
                    }

                    RespawnableItem ri = grab.GetComponent<RespawnableItem>();
                    if (ri != null)
                    {
                        SerializedObject so = new SerializedObject(ri);
                        Transform ha = so.FindProperty("homeAnchor")?.objectReferenceValue as Transform;
                        if (ha != null)
                        {
                            float haSupportY = FindSupportingSurfaceY(grab.transform, ha.position, 0.90f);
                            float haReqY = haSupportY + halfH + 0.01f - colCenterOffsetY;
                            if (ha.position.y < haReqY - 0.0005f)
                            {
                                Vector3 hp = ha.position;
                                hp.y = haReqY;
                                ha.position = hp;
                                EditorUtility.SetDirty(ha);
                            }
                        }
                    }
                }

                configured++;
            }

            Physics.SyncTransforms();
            return $"Grabbables: Configured {configured} XRGrabInteractables (VelocityTracking + ThrowOnDetach + ContinuousDynamic), fixedNonConvex={fixedNonConvex}, liftedHomePos={liftedHomes}.";
        }

        private static Collider EnsureValidGrabbableColliders(GameObject root, ref int fixedNonConvex)
        {
            Collider[] cols = root.GetComponentsInChildren<Collider>(true);
            Collider primary = null;

            for (int i = 0; i < cols.Length; i++)
            {
                Collider c = cols[i];
                if (c is MeshCollider mc && !mc.convex)
                {
                    mc.convex = true;
                    fixedNonConvex++;
                    EditorUtility.SetDirty(mc);
                }

                c.enabled = true;
                c.isTrigger = false;
                EditorUtility.SetDirty(c);
                if (primary == null) primary = c;
            }

            if (primary == null)
            {
                BoxCollider added = root.AddComponent<BoxCollider>();
                added.size = new Vector3(0.08f, 0.08f, 0.08f);
                EditorUtility.SetDirty(added);
                primary = added;
            }

            return primary;
        }

        private static float FindSupportingSurfaceY(Transform ignoreRoot, Vector3 worldPos, float fallbackY)
        {
            RaycastHit[] hits = Physics.RaycastAll(new Vector3(worldPos.x, worldPos.y + 0.35f, worldPos.z), Vector3.down, 1.5f);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                Collider col = hits[i].collider;
                if (col == null || col.isTrigger) continue;
                if (col.transform == ignoreRoot || col.transform.IsChildOf(ignoreRoot)) continue;
                if (col.GetComponentInParent<XRGrabInteractable>() != null) continue;
                return hits[i].point.y;
            }

            return fallbackY;
        }

        private static string RepositionStandPointsAndRebuildNavMesh()
        {
            LessonManager lm = Object.FindAnyObjectByType<LessonManager>();
            NPCController npc = Object.FindAnyObjectByType<NPCController>();
            if (lm == null || npc == null)
            {
                return "NavMesh: Missing LessonManager or NPCController.";
            }

            // 1. Set NavMeshAgent radius to 0.26 so Dr. Vera navigates smoothly around/behind benches.
            NavMeshAgent agent = npc.GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                agent.radius = 0.26f;
                EditorUtility.SetDirty(agent);
            }

            // 2. Reposition all 8 station NPCStandPoints to the player's LEFT side beside each workbench
            // (-anchor.right * Left + anchor.forward * Forward), angled inward toward the student/table.
            int movedCount = 0;
            for (int i = 0; i < LeftSideStandPoints.Length; i++)
            {
                StandPointPlacement spec = LeftSideStandPoints[i];
                StationController st = FindStationById(lm, spec.StationId);
                if (st == null || st.NpcStandPoint == null) continue;

                Transform stand = st.NpcStandPoint;
                TeleportationAnchor anchor = st.GetComponentInChildren<TeleportationAnchor>(true);
                if (anchor != null)
                {
                    Vector3 fwd = anchor.transform.forward;
                    fwd.y = 0f;
                    fwd.Normalize();
                    Vector3 left = -anchor.transform.right;
                    left.y = 0f;
                    left.Normalize();
                    stand.position = anchor.transform.position + left * spec.LeftOffsetMeters + fwd * spec.ForwardOffsetMeters;
                }

                Vector3 inwardFocus = anchor != null
                    ? Vector3.Lerp(st.transform.position, anchor.transform.position, 0.65f)
                    : st.transform.position;
                Vector3 lookDir = inwardFocus - stand.position;
                lookDir.y = 0f;
                if (lookDir.sqrMagnitude > 0.001f)
                {
                    stand.rotation = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
                }

                EditorUtility.SetDirty(stand);
                movedCount++;
            }

            // Ensure Dr. Vera starts positioned on the left side of Station T's anchor as well.
            StationController tutorialSt = FindStationById(lm, "T");
            if (tutorialSt != null && tutorialSt.NpcStandPoint != null)
            {
                npc.transform.SetPositionAndRotation(tutorialSt.NpcStandPoint.position, tutorialSt.NpcStandPoint.rotation);
                EditorUtility.SetDirty(npc.transform);
            }

            // 3. Rebake LabNavMeshData with agentRadius = 0.26f from PhysicsColliders and verify all 8 standpoints.
            LabNavMeshSurface surface = Object.FindAnyObjectByType<LabNavMeshSurface>();
            if (surface == null)
            {
                return $"NavMesh: Moved {movedCount} standpoints, but LabNavMeshSurface not found.";
            }

            SerializedObject surfSo = new SerializedObject(surface);
            NavMeshData navData = surfSo.FindProperty("navMeshData")?.objectReferenceValue as NavMeshData;
            if (navData == null)
            {
                return $"NavMesh: Moved {movedCount} standpoints, but NavMeshData is null.";
            }

            Physics.SyncTransforms();

            NavMeshBuildSettings buildSettings = NavMesh.GetSettingsByID(0);
            buildSettings.agentRadius = 0.26f;
            buildSettings.agentHeight = 1.75f;
            buildSettings.agentSlope = 35f;
            buildSettings.agentClimb = 0.25f;

            Bounds bounds = new Bounds(Vector3.zero, new Vector3(18f, 4f, 14f));
            List<NavMeshBuildSource> sources = new List<NavMeshBuildSource>();
            List<NavMeshBuildMarkup> markups = new List<NavMeshBuildMarkup>
            {
                new NavMeshBuildMarkup { root = npc.transform, ignoreFromBuild = true }
            };
            XROrigin xrOrigin = Object.FindAnyObjectByType<XROrigin>();
            if (xrOrigin != null)
            {
                markups.Add(new NavMeshBuildMarkup { root = xrOrigin.transform, ignoreFromBuild = true });
            }

            NavMeshBuilder.CollectSources(bounds, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, markups, sources);
            sources.RemoveAll(s => s.component != null
                && (s.component.transform.IsChildOf(npc.transform)
                    || (xrOrigin != null && s.component.transform.IsChildOf(xrOrigin.transform))));
            NavMeshBuilder.UpdateNavMeshData(navData, buildSettings, sources, bounds);
            EditorUtility.SetDirty(navData);

            NavMeshDataInstance inst = NavMesh.AddNavMeshData(navData, surface.transform.position, surface.transform.rotation);
            int verifiedOnNav = 0;
            int pathsOk = 0;
            StringBuilder detail = new StringBuilder();
            try
            {
                Vector3? prevPt = null;
                NavMeshPath testPath = new NavMeshPath();
                for (int i = 0; i < LeftSideStandPoints.Length; i++)
                {
                    StationController st = FindStationById(lm, LeftSideStandPoints[i].StationId);
                    if (st == null || st.NpcStandPoint == null) continue;

                    if (NavMesh.SamplePosition(st.NpcStandPoint.position, out NavMeshHit hit, 0.75f, NavMesh.AllAreas))
                    {
                        verifiedOnNav++;
                        st.NpcStandPoint.position = hit.position;
                        EditorUtility.SetDirty(st.NpcStandPoint);

                        if (prevPt.HasValue
                            && NavMesh.CalculatePath(prevPt.Value, hit.position, NavMesh.AllAreas, testPath)
                            && testPath.status == NavMeshPathStatus.PathComplete)
                        {
                            pathsOk++;
                        }

                        prevPt = hit.position;
                        detail.Append($"{st.StationId}:{hit.position:F2} ");
                    }
                    else
                    {
                        detail.Append($"{st.StationId}:OFFMESH! ");
                    }
                }
            }
            finally
            {
                if (inst.valid) inst.Remove();
            }

            // Re-sync NPC starting pose with Station T snapped NavMesh point.
            if (tutorialSt != null && tutorialSt.NpcStandPoint != null)
            {
                npc.transform.SetPositionAndRotation(tutorialSt.NpcStandPoint.position, tutorialSt.NpcStandPoint.rotation);
                EditorUtility.SetDirty(npc.transform);
            }

            return $"NavMesh: radius=0.26, moved={movedCount}, onMesh={verifiedOnNav}/{LeftSideStandPoints.Length}, sequentialCompletePaths={pathsOk}/{LeftSideStandPoints.Length - 1} [{detail}]";
        }

        private static void ApplyProjectPrefabChanges()
        {
            LessonManager lm = Object.FindAnyObjectByType<LessonManager>();
            if (lm != null && lm.Stations != null)
            {
                for (int i = 0; i < lm.Stations.Count; i++)
                {
                    StationController st = lm.Stations[i];
                    if (st == null) continue;
                    ApplyIfProjectPrefab(st.gameObject);
                }
            }

            NPCController npc = Object.FindAnyObjectByType<NPCController>();
            if (npc != null)
            {
                ApplyIfProjectPrefab(npc.gameObject);
            }

            GameObject tablet = GameObject.Find("LabTablet");
            if (tablet != null)
            {
                ApplyIfProjectPrefab(tablet);
            }

            GameObject dock = GameObject.Find("LabTabletStationDock");
            if (dock != null)
            {
                ApplyIfProjectPrefab(dock);
            }
        }

        private static void ApplyIfProjectPrefab(GameObject instance)
        {
            if (instance == null || !PrefabUtility.IsPartOfPrefabInstance(instance))
            {
                return;
            }

            string assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instance);
            if (!string.IsNullOrEmpty(assetPath) && assetPath.StartsWith("Assets/PNJunctionLab/", StringComparison.OrdinalIgnoreCase))
            {
                PrefabUtility.ApplyPrefabInstance(instance, InteractionMode.AutomatedAction);
            }
        }

        private static StationController FindStationById(LessonManager lm, string id)
        {
            if (lm == null || lm.Stations == null) return null;
            for (int i = 0; i < lm.Stations.Count; i++)
            {
                StationController s = lm.Stations[i];
                if (s != null && string.Equals(s.StationId, id, StringComparison.OrdinalIgnoreCase))
                {
                    return s;
                }
            }
            return null;
        }

        private static Transform FindChildRecursive(Transform root, string targetName)
        {
            if (root == null) return null;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform c = root.GetChild(i);
                if (c.name == targetName) return c;
                Transform nested = FindChildRecursive(c, targetName);
                if (nested != null) return nested;
            }
            return null;
        }
    }
}
