using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace PNJunctionLab.Editor
{
    /// <summary>
    /// Creates station-specific "Ask Dr. Vera" GlossaryTopic ScriptableObjects for every lab table (T, 1..6, A)
    /// and binds them into NPCController.stationGuides and DialogueSystem so speech bubble options match the player's station.
    /// </summary>
    public static class StationTopicsBuilder
    {
        private const string ScenePath = "Assets/PNJunctionLab/Scenes/PNJunctionLab.unity";
        private const string GlossaryFolder = "Assets/PNJunctionLab/Data/Glossary";
        private const string StationTopicsFolder = GlossaryFolder + "/StationTopics";
        private const string DialogueFolder = "Assets/PNJunctionLab/Data/Dialogue";

        [MenuItem("PNJunctionLab/Bind Station-Aware NPC Topics")]
        public static void BuildAllStationTopicsMenu()
        {
            Debug.Log(BuildAllStationTopics());
        }

        /// <summary>
        /// Generates all station-specific GlossaryTopic/DialogueLine assets and binds them to Stations T, 1..6, A.
        /// </summary>
        public static string BuildAllStationTopics()
        {
            Scene activeScene = EditorSceneManager.GetActiveScene();
            if (!activeScene.path.EndsWith("PNJunctionLab.unity"))
            {
                activeScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            if (!AssetDatabase.IsValidFolder(StationTopicsFolder))
            {
                AssetDatabase.CreateFolder(GlossaryFolder, "StationTopics");
            }

            // Re-use core glossary assets where exact fit, and build crisp station-tailored topics for each table.
            GlossaryTopic gIntrinsic = AssetDatabase.LoadAssetAtPath<GlossaryTopic>(GlossaryFolder + "/Glossary_Intrinsic.asset");
            GlossaryTopic gNType = AssetDatabase.LoadAssetAtPath<GlossaryTopic>(GlossaryFolder + "/Glossary_NType.asset");
            GlossaryTopic gPType = AssetDatabase.LoadAssetAtPath<GlossaryTopic>(GlossaryFolder + "/Glossary_PType.asset");
            GlossaryTopic gDepletion = AssetDatabase.LoadAssetAtPath<GlossaryTopic>(GlossaryFolder + "/Glossary_Depletion.asset");
            GlossaryTopic gBiasing = AssetDatabase.LoadAssetAtPath<GlossaryTopic>(GlossaryFolder + "/Glossary_Biasing.asset");
            GlossaryTopic gShockley = AssetDatabase.LoadAssetAtPath<GlossaryTopic>(GlossaryFolder + "/Glossary_Shockley.asset");

            // Station T — Tutorial Pad
            GlossaryTopic tControls = CreateTopic("Topic_T_Controls", "t_controls", "VR Controls",
                "Use Teleport (I / Trigger), Snap Turn (J/L), Grip (G) to grab objects, and Trigger/Pinch (T/M/N) for buttons & UI.",
                "To move around the lab, aim at any glowing floor pad and press Teleport (I). Grab cubes or tablet with Grip (G), and press Trigger or Pinch (T / M / N) to click UI.");
            GlossaryTopic tSafety = CreateTopic("Topic_T_Safety", "t_safety", "Safety & Reset",
                "If a cube or lead drops outside the room, RespawnZone automatically returns it to its tray. You can also press Return or Reset anytime.",
                "Every bench has an automatic Respawn Zone: if you drop a prop off the table, it snaps right back to its tray! You can also click Reset Station anytime.");
            GlossaryTopic tOverview = CreateTopic("Topic_T_Overview", "t_overview", "Semiconductors",
                "Journey from pure Intrinsic Si (Station 1) -> N & P Doping (2-3) -> P-N Junction (4) -> Diode Biasing & I-V Curves (5-6).",
                "Today we trace the entire life of a diode: pure Silicon crystals, N and P doping, building a P-N junction, and testing Forward/Reverse biasing and I-V curves!");

            // Station 1 — Intrinsic Silicon
            GlossaryTopic s1Lattice = CreateTopic("Topic_1_Lattice", "s1_lattice", "Group IV Lattice",
                "Carbon, Silicon, and Germanium share 4 valence electrons in strong tetrahedral covalent bonds.",
                "Group IV elements like Silicon have 4 outer electrons shared equally with 4 neighbors in a diamond-cubic lattice, leaving no free carriers at absolute zero.");
            GlossaryTopic s1Temp = CreateTopic("Topic_1_Temperature", "s1_temp", "Temperature & ni",
                "At 0 K all covalent bonds stay intact (insulator). Heating breaks bonds, generating equal electron-hole pairs: n = p = ni(T).",
                "At 0 Kelvin, intrinsic silicon is a pure insulator. Raising temperature shakes lattice bonds free, creating equal numbers of blue free electrons and red holes: n = p = ni.");
            GlossaryTopic s1Gap = CreateTopic("Topic_1_SiVsGe", "s1_sige", "Si vs Ge Gap",
                "Smaller bandgap Eg makes thermal carrier generation exponentially easier: Ge (0.67 eV, ni=2.4e13) conducts far more than Si (1.12 eV, ni=1.5e10) at 300 K.",
                "Germanium has a smaller 0.67 eV bandgap than Silicon's 1.12 eV, so at 300 K thermal energy frees over 1,000 times more carriers in Germanium!");

            // Station 2 — N-Type Doping
            GlossaryTopic s2Donors = CreateTopic("Topic_2_Donors", "s2_donors", "Group V Donors",
                "Phosphorus, Arsenic, and Antimony carry 5 valence electrons: 4 covalent bonds + 1 loosely bound donor electron (Ed ~0.045 eV below Ec).",
                "Group V donors like Phosphorus bring 5 valence electrons. Four form bonds and the fifth breaks loose easily just 0.045 eV below the conduction band!");
            GlossaryTopic s2Neutrality = CreateTopic("Topic_2_Neutrality", "s2_neutral", "e- & +Donor Ions",
                "N-Type Si has electrons as majority carriers (n ~ ND >> p = ni^2/ND), yet remains electrically neutral because ionized + donor cores cancel free e-.",
                "Even though N-Type Silicon is packed with negative free electrons, the crystal stays electrically neutral because each donor atom remains locked as a fixed positive orange ion!");
            GlossaryTopic s2Fermi = gNType != null ? gNType : CreateTopic("Topic_2_Fermi", "s2_fermi", "Fermi Shift EF",
                "Adding pentavalent donors pulls EF up toward Ec and increases conductivity.",
                "Raising donor density ND shifts the Fermi level EF upward toward the conduction band Ec and proportionally boosts conductivity.");

            // Station 3 — P-Type Doping
            GlossaryTopic s3Acceptors = CreateTopic("Topic_3_Acceptors", "s3_acceptors", "Group III Acceptors",
                "Boron, Aluminum, Gallium, and Indium have 3 valence electrons, leaving an incomplete bond that accepts valence electrons (Ea ~0.045 eV above Ev).",
                "Trivalent Group III dopants like Boron only have 3 valence electrons, creating an empty bonding orbital just 0.045 eV above the valence band that acts as a mobile hole!");
            GlossaryTopic s3Holes = CreateTopic("Topic_3_Holes", "s3_holes", "Holes & Hopping",
                "A hole moves when a nearby valence electron hops in the opposite direction, leaving fixed negative teal acceptor ions locked in the lattice.",
                "Holes aren't extra particles — they move as neighboring valence electrons hop left-to-right to fill the gap, leaving fixed negative teal acceptor ions behind!");
            GlossaryTopic s3Fermi = gPType != null ? gPType : CreateTopic("Topic_3_Fermi", "s3_fermi", "Fermi Shift EF",
                "Trivalent acceptors lower EF toward Ev and make p ~ NA >> n = ni^2/NA.",
                "Increasing acceptor density NA makes holes the dominant majority carriers and pulls the Fermi level EF downward toward the valence band Ev.");

            // Station 4 — Junction Formation
            GlossaryTopic s4DiffDrift = CreateTopic("Topic_4_DiffDrift", "s4_diffdrift", "Diffusion vs Drift",
                "Concentration steps drive Diffusion (e- N->P, h+ P->N); uncovered ions build an N->P E-field causing opposite Drift until net current = 0.",
                "At t=0, electrons diffuse from N to P and holes from P to N. That uncovers fixed ions forming an N-to-P electric field whose drift current exactly cancels diffusion at equilibrium!");
            GlossaryTopic s4Depletion = gDepletion != null ? gDepletion : CreateTopic("Topic_4_Depletion", "s4_dep", "Depletion & V0",
                "Uncovered fixed ions build built-in barrier potential V0 (~0.69 V for 1e16 Si) and flat Fermi level EF.",
                "In equilibrium, the Fermi level EF is completely flat across P and N, bending Ec and Ev down by qV0 (~0.69 eV at 1e16/cm^3).");
            GlossaryTopic s4Asym = CreateTopic("Topic_4_Asymmetric", "s4_asym", "Asymmetric W",
                "Total depleted charge balances (NA * xp = ND * xn), so depletion W pushes deepest into whichever side is lighter doped.",
                "Because total uncovered positive and negative charge must balance (NA*xp = ND*xn), the depletion region penetrates farthest into the lighter-doped side!");

            // Station 5 — Diode Biasing
            GlossaryTopic s5Fwd = CreateTopic("Topic_5_Forward", "s5_fwd", "Forward Bias",
                "Red (+) -> P, Black (-) -> N lowers the barrier to V0 - V, narrows W, and sends exponential majority current (LED lights above ~0.70 V Si knee).",
                "In Forward Bias (Red to P, Black to N), the battery opposes Ebi, dropping the barrier to V0 - V and narrowing depletion so majority carriers flood across!");
            GlossaryTopic s5Rev = CreateTopic("Topic_5_Reverse", "s5_rev", "Reverse Bias",
                "Red (+) -> N, Black (-) -> P raises the barrier to V0 + VR, widens W, blocking majority carriers so only tiny Is (~2.5 nA) leaks.",
                "In Reverse Bias (Red to N, Black to P), carriers are pulled away from the junction, widening W and blocking conduction except for tiny minority leakage current Is.");
            GlossaryTopic s5Break = CreateTopic("Topic_5_Breakdown", "s5_break", "Breakdown & R",
                "Above -50 V reverse bias, impact ionization causes Avalanche Breakdown; our 220 ohm series resistor prevents thermal diode burnout.",
                "Past -50 V in reverse bias, high-speed carriers smash covalent bonds in an Avalanche cascade! Without the 220 ohm series resistor, excess heat would destroy the diode.");

            // Station 6 — I-V Characteristics
            GlossaryTopic s6Types = CreateTopic("Topic_6_DiodeTypes", "s6_types", "Si, Ge, LED, Zener",
                "Different bandgaps & doping set knees: Ge ~0.3 V, Si ~0.7 V, LED ~2.0 V, and Zener diodes conduct crisply in reverse at -5.1 V.",
                "Try swapping all 4 modules! Bandgap sets the forward knee (Ge ~0.3 V, Si ~0.7 V, LED ~2.0 V), while Zener diodes use controlled tunnelling to hold -5.1 V in reverse.");
            GlossaryTopic s6Shockley = gShockley != null ? gShockley : CreateTopic("Topic_6_Shockley", "s6_shockley", "Shockley vs R",
                "Ideal Shockley rises exponentially while a 100 ohm load resistor adds V = I*R voltage drop.",
                "Toggle the gold Shockley Overlay to see why an ideal zero-ohm diode shoots nearly straight up, whereas a real 100 ohm series resistor creates a linear load slope!");
            GlossaryTopic s6Temp = CreateTopic("Topic_6_Thermal", "s6_temp", "Thermal Shift",
                "Heating (220-420 K) boosts Is(T) exponentially and drops forward knee voltage by roughly -2 mV/C.",
                "Watch the Reverse Inset Zoom when you heat a diode: Is(T) shoots upward by orders of magnitude, while the forward knee shifts left by roughly -2 mV per degree C!");

            // Station A — Assessment Review
            GlossaryTopic sADoping = gNType != null ? gNType : s2Fermi;
            GlossaryTopic sAJunction = s4DiffDrift;
            GlossaryTopic sABias = gBiasing != null ? gBiasing : s5Fwd;

            DialogueLine dlGuideA = CreateDialogueAsset("DL_Guide_StA_Physics", "guide_A_physics",
                "Ready for the Assessment Bench? Apply what you mastered across Intrinsic Si, Doping, P-N Junctions, Biasing, and I-V curves!",
                NPCEmotion.Encouraging);

            NPCController npcCtrl = Object.FindAnyObjectByType<NPCController>();
            DialogueSystem dlgSys = Object.FindAnyObjectByType<DialogueSystem>();

            (string id, string goName, string guidePath, GlossaryTopic[] topics, DialogueLine fallbackGuide)[] specs =
            {
                ("T", "Station_T_TutorialPad", DialogueFolder + "/DL_Guide_T_Physics.asset", new[] { tControls, tSafety, tOverview }, null),
                ("1", "Station_1_Intrinsic", DialogueFolder + "/DL_Guide_St1_Physics.asset", new[] { s1Lattice, s1Temp, s1Gap }, null),
                ("2", "Station_2_NType", DialogueFolder + "/DL_Guide_St2_Physics.asset", new[] { s2Donors, s2Neutrality, s2Fermi }, null),
                ("3", "Station_3_PType", DialogueFolder + "/DL_Guide_St3_Physics.asset", new[] { s3Acceptors, s3Holes, s3Fermi }, null),
                ("4", "Station_4_Junction", DialogueFolder + "/DL_Guide_St4_Physics.asset", new[] { s4DiffDrift, s4Depletion, s4Asym }, null),
                ("5", "Station_5_Biasing", DialogueFolder + "/DL_Guide_St5_Physics.asset", new[] { s5Fwd, s5Rev, s5Break }, null),
                ("6", "Station_6_IV", DialogueFolder + "/DL_Guide_St6_Physics.asset", new[] { s6Types, s6Shockley, s6Temp }, null),
                ("A", "Station_A_Assessment", DialogueFolder + "/DL_Guide_StA_Physics.asset", new[] { sADoping, sAJunction, sABias }, dlGuideA)
            };

            if (npcCtrl != null)
            {
                SerializedObject npcSo = new SerializedObject(npcCtrl);
                SerializedProperty guidesProp = npcSo.FindProperty("stationGuides");
                if (guidesProp != null)
                {
                    guidesProp.arraySize = specs.Length;
                    for (int i = 0; i < specs.Length; i++)
                    {
                        var sp = specs[i];
                        GameObject sGo = GameObject.Find(sp.goName);
                        StationController sc = sGo != null ? sGo.GetComponent<StationController>() : null;
                        TeleportationAnchor anch = sc != null ? sc.GetComponentInChildren<TeleportationAnchor>(true) : null;
                        Transform stand = sc != null ? sc.NpcStandPoint : null;
                        DialogueLine guide = AssetDatabase.LoadAssetAtPath<DialogueLine>(sp.guidePath) ?? sp.fallbackGuide;

                        SerializedProperty elem = guidesProp.GetArrayElementAtIndex(i);
                        elem.FindPropertyRelative("stationId").stringValue = sp.id;
                        elem.FindPropertyRelative("teleportAnchor").objectReferenceValue = anch;
                        elem.FindPropertyRelative("standPoint").objectReferenceValue = stand;
                        elem.FindPropertyRelative("guideDialogue").objectReferenceValue = guide;

                        SerializedProperty topicsProp = elem.FindPropertyRelative("askTopics");
                        if (topicsProp != null)
                        {
                            topicsProp.arraySize = sp.topics.Length;
                            for (int t = 0; t < sp.topics.Length; t++)
                            {
                                topicsProp.GetArrayElementAtIndex(t).objectReferenceValue = sp.topics[t];
                            }
                        }
                    }
                }

                npcSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(npcCtrl);
                if (PrefabUtility.IsPartOfPrefabInstance(npcCtrl.gameObject))
                {
                    PrefabUtility.ApplyPrefabInstance(npcCtrl.gameObject, InteractionMode.AutomatedAction);
                }
            }

            // Initialize DialogueSystem's default stationAskTopics to Tutorial Pad's topics.
            if (dlgSys != null)
            {
                SerializedObject dsSo = new SerializedObject(dlgSys);
                LabTextStyleHelper.SetObjectArray(dsSo, "stationAskTopics", new[] { tControls, tSafety, tOverview });
                dsSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(dlgSys);
            }

            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return "Station-aware Ask topics created and wired for Stations T, 1, 2, 3, 4, 5, 6, A!";
        }

        private static GlossaryTopic CreateTopic(string fileName, string id, string title, string summary, string spokenText)
        {
            DialogueLine spokenLine = CreateDialogueAsset("DL_" + fileName, "ask_" + id, spokenText, NPCEmotion.Explaining);
            string assetPath = StationTopicsFolder + "/" + fileName + ".asset";
            GlossaryTopic topic = AssetDatabase.LoadAssetAtPath<GlossaryTopic>(assetPath);
            if (topic == null)
            {
                topic = ScriptableObject.CreateInstance<GlossaryTopic>();
                AssetDatabase.CreateAsset(topic, assetPath);
            }

            topic.Configure(id, title, summary, spokenLine);
            EditorUtility.SetDirty(topic);
            return topic;
        }

        private static DialogueLine CreateDialogueAsset(string fileName, string id, string text, NPCEmotion emotion)
        {
            string path = DialogueFolder + "/" + fileName + ".asset";
            DialogueLine line = AssetDatabase.LoadAssetAtPath<DialogueLine>(path);
            if (line == null)
            {
                line = ScriptableObject.CreateInstance<DialogueLine>();
                AssetDatabase.CreateAsset(line, path);
            }

            line.Configure(id, text, emotion, null, null);
            EditorUtility.SetDirty(line);
            return line;
        }
    }
}
