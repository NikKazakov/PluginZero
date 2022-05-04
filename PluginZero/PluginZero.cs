using BepInEx;
using HarmonyLib;
using UnityEngine;
using BepInEx.Configuration;
using BepInEx.Logging;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine.UI;

namespace PluginZero
{
    [BepInPlugin(ModId, "PluginZero", "1.0.0")]
    [BepInProcess("valheim.exe")]
    [BepInProcess("valheim_server.exe")]
    internal class PluginZero : BaseUnityPlugin
    {
        public const string ModId = "dev.plox.pluginzero";

        public static ManualLogSource logger;

        public static readonly Harmony harmony = new Harmony(ModId);

        public static readonly Color abilityCooldownColor = new Color(1f, .3f, .3f, .5f);
        public static readonly string assetsPath = "PluginZero.pz_assets";

        public static ConfigEntry<KeyboardShortcut> Ability1_Hotkey;
        public static ConfigEntry<KeyboardShortcut> Ability2_Hotkey;
        public static ConfigEntry<KeyboardShortcut> Ability3_Hotkey;

        public static ConfigEntry<float> AbilityBarXOffset;
        public static ConfigEntry<float> AbilityBarYOffset;
        public static ConfigEntry<float> AbilityIconScale;

        public static ConfigEntry<float> GlobalDamageMultiplier;
        public static ConfigEntry<float> GlobalStaminaCostMultiplier;
        public static ConfigEntry<float> GlobalExpGainMultiplier;
        public static ConfigEntry<float> GlobalCooldownMultiplier;

        public enum EClass
        {
            Boba,
            Mage
        }

        public static ConfigEntry<EClass> PlayerClass_conf;

        public static Dictionary<EClass, Type> EClassToClass = new Dictionary<EClass, Type>()
        {
            { EClass.Boba, typeof(PZ_Class_Boba) },
            { EClass.Mage, typeof(PZ_Class_Mage) },
        };

        public static BaseClass PlayerClass;

        public static Skills.SkillType SkillType_Illusion = (Skills.SkillType)791;
        public static Skills.SkillType SkillType_Conjuration = (Skills.SkillType)792;
        public static Skills.SkillType SkillType_Destruction = (Skills.SkillType)793;
        public static Skills.SkillType SkillType_Restoration = (Skills.SkillType)794;
        public static Skills.SkillType SkillType_Alteration = (Skills.SkillType)795;

        static List<Skills.SkillType> SkillTypes = new List<Skills.SkillType>()
        {
            SkillType_Illusion,
            SkillType_Conjuration,
            SkillType_Destruction,
            SkillType_Restoration,
            SkillType_Alteration
        };

        public static List<Skills.SkillDef> m_skills = new List<Skills.SkillDef>();

        private BaseUnityPlugin ConfigurationManager;
        private bool ConfigurationManagerWindowShown;

        static Player player;
        static ZSyncAnimation player_zanim;

        static Spell QueuedAttack;
        static float QueuedAttackTimer = .9f;
        public static bool ForceLookRotation = false;

        void Awake()
        {
            logger = Logger;

            Ability1_Hotkey = Config.Bind("Keybinds", "Ability1_Hotkey", new KeyboardShortcut(KeyCode.Z), "Ability 1 Hotkey");
            Ability2_Hotkey = Config.Bind("Keybinds", "Ability2_Hotkey", new KeyboardShortcut(KeyCode.X), "Ability 2 Hotkey");
            Ability3_Hotkey = Config.Bind("Keybinds", "Ability3_Hotkey", new KeyboardShortcut(KeyCode.C), "Ability 3 Hotkey");

            AbilityBarXOffset = Config.Bind("Interface", "AbilityBarXOffset", 155f, "Ability Bar X Offset");
            AbilityBarYOffset = Config.Bind("Interface", "AbilityBarYOffset", 100f, "Ability Bar Y Offset");
            AbilityIconScale = Config.Bind("Interface", "AbilityIconScale", 1f, "Ability Icon Scale");

            GlobalDamageMultiplier = Config.Bind("General", "GlobalDamageMultiplier", 1f, "Global Damage Multiplier");
            GlobalStaminaCostMultiplier = Config.Bind("General", "GlobalStaminaCostMultiplier", 1f, "Global StaminaCost Multiplier");
            GlobalExpGainMultiplier = Config.Bind("General", "GlobalExpGainMultiplier", 1f, "Global ExpGain Multiplier");
            GlobalCooldownMultiplier = Config.Bind("General", "GlobalCooldownMultiplier", 1f, "Global Cooldown Multiplier");

            PlayerClass_conf = Config.Bind("General", "PlayerClass", EClass.Boba, "Player Class");

            logger.LogDebug("Initialized config");

            LoadAssets();

            m_skills = new List<Skills.SkillDef>()
            {
                new Skills.SkillDef
                {
                    m_skill = SkillType_Illusion,
                    m_icon = LoadSprite("icon_skill_illusion"),
                    m_description = "Skill in creating convincing illusions",
                    m_increseStep = 1f
                },
                new Skills.SkillDef
                {
                    m_skill = SkillType_Conjuration,
                    m_icon = LoadSprite("icon_skill_conjuration"),
                    m_description = "Skill in summoning creatures and spirites",
                    m_increseStep = 1f
                },
                new Skills.SkillDef
                {
                    m_skill = SkillType_Destruction,
                    m_icon = LoadSprite("icon_skill_destruction"),
                    m_description = "Skill in creating and manipulating destructive energy",
                    m_increseStep = 1f
                },
                new Skills.SkillDef
                {
                    m_skill = SkillType_Restoration,
                    m_icon = LoadSprite("icon_skill_restoration"),
                    m_description = "Skill in restorative and holy magic",
                    m_increseStep = 1f
                },
                new Skills.SkillDef
                {
                    m_skill = SkillType_Alteration,
                    m_icon = LoadSprite("icon_skill_alteration"),
                    m_description = "Skill in temporarily enhancing or modifying attributes",
                    m_increseStep = 1f
                }
            };

            Init();

            harmony.PatchAll(typeof(Patches));

            Logger.LogDebug("Trying to hook config manager");

            ConfigurationManager = FindObjectsOfType(typeof(BaseUnityPlugin)).Cast<BaseUnityPlugin>().ToArray()
                .FirstOrDefault(x => x.Info?.Metadata?.GUID == "com.bepis.bepinex.configurationmanager");

            if (ConfigurationManager)
            {
                Logger.LogDebug("Configuration manager found, trying to hook DisplayingWindowChanged");
                var eventinfo = ConfigurationManager.GetType().GetEvent("DisplayingWindowChanged");
                if (eventinfo != null)
                {
                    Action<object, object> local = ConfigurationManager_DisplayingWindowChanged;
                    var converted = Delegate.CreateDelegate(eventinfo.EventHandlerType, local.Target, local.Method);

                    eventinfo.AddEventHandler(ConfigurationManager, converted);
                }
            }

            /*
             * Some space for testing
             */
            UnityEngine.Local
        }

        static void Init()
        {
            if (PlayerClass != null)
            {
                PlayerClass.DestroyIcons();
            }

            PlayerClass = (BaseClass)Activator.CreateInstance(EClassToClass[PlayerClass_conf.Value]);

        }

        void OnDestroy()
        {
            if (PlayerClass != null)
                PlayerClass.DestroyIcons();



            harmony.UnpatchSelf();
        }

        class Patches
        {
            [HarmonyPatch(typeof(Player), "Awake"), HarmonyPostfix]
            static void SavePlayer_Patch(Player __instance)
            {
                Init();
                player = __instance;
                player_zanim = (ZSyncAnimation)typeof(Player).GetField("m_zanim", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(player);
            }

            [HarmonyPatch(typeof(Player), "Update"), HarmonyPostfix]
            static void CustomInput_Patch(Player __instance)
            {
                if (ForceLookRotation)
                {
                    logger.LogDebug($"Player rotation: {player.transform.rotation}, required {Quaternion.LookRotation(player.GetLookDir())}, in {(1 - QueuedAttackTimer)} s");
                    player.transform.rotation = Quaternion.Slerp(player.transform.rotation, Quaternion.LookRotation(player.GetLookDir()), (1 - QueuedAttackTimer));
                    logger.LogDebug($"New player rotation: {player.transform.rotation}");
                }
                if (QueuedAttack != null && QueuedAttackTimer > 0)
                {
                    QueuedAttackTimer -= Time.fixedDeltaTime;
                    if (QueuedAttackTimer <= 0)
                    {
                        QueuedAttack.DoAttack(__instance);
                        QueuedAttack = null;
                        QueuedAttackTimer = .9f;
                    }
                }

                PlayerClass.ProcessInput(Player.m_localPlayer);
            }

            [HarmonyPatch(typeof(Hud), "UpdateStatusEffects"), HarmonyPostfix]
            public static void UpdateAbilityIcons_Patch(Hud __instance)
            {
                PlayerClass.UpdateIcons(__instance);
            }

            [HarmonyPatch(typeof(Skills), "GetSkillDef"), HarmonyPostfix]
            public static void GetSkillDef_Patch(Skills __instance, Skills.SkillType type, List<Skills.SkillDef> ___m_skills, ref Skills.SkillDef __result)
            {
                logger.LogDebug("Current skills:");
                foreach (var skill in ___m_skills)
                {
                    logger.LogDebug($"\t{skill.m_skill}");
                }
                if (__result == null)
                {
                    foreach (Skills.SkillDef skill in m_skills)
                    {
                        if (!___m_skills.Contains(skill))
                        {
                            ___m_skills.Add(skill);
                        }
                        if (skill.m_skill == type)
                        {
                            __result = skill;
                        }
                    }
                }
                logger.LogDebug("Current skills:");
                foreach (var skill in ___m_skills)
                {
                    logger.LogDebug($"\t{skill.m_skill}");
                }
            }

            [HarmonyPatch(typeof(Skills), "IsSkillValid"), HarmonyPostfix]
            public static void ValidSkill_Patch(Skills __instance, Skills.SkillType type, ref bool __result)
            {
                __result = SkillTypes.Contains(type);
            }
        }

        public static ZSyncAnimation GetPlayerZanim()
        {
            if (player_zanim == null)
            {
                player = Player.m_localPlayer;
                player_zanim = (ZSyncAnimation)typeof(Player).GetField("m_zanim", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(player);
            }

            return player_zanim;
        }

        public static bool QueueAttack(Spell attack, float time=.5f)
        {
            if (QueuedAttack == null)
            {
                QueuedAttack = attack;
                QueuedAttackTimer = time;
                return true;
            }
            return false;
        }

        public static Dictionary<string, GameObject> LoadedAssets = new Dictionary<string, GameObject>();

        public void LoadAssets()
        {
            var assembly = Assembly.GetExecutingAssembly();

            Stream assetBundleStream = assembly.GetManifestResourceStream(assetsPath);
            //Stream assetBundleStream = assembly.GetManifestResourceStream("PluginZero.assets.vl_assetbundle");
            AssetBundle assetBundle = AssetBundle.LoadFromStream(assetBundleStream);

            Texture2D[] sprites = assetBundle.LoadAllAssets<Texture2D>();
            GameObject[] assets = assetBundle.LoadAllAssets<GameObject>();

            logger.LogDebug($"Loaded asset bundle with {assets.Count()} assets:");

            foreach (GameObject asset in assets)
            {
                logger.LogDebug($"\t{asset.name}");
                if (!LoadedAssets.ContainsKey(asset.name))
                    LoadedAssets.Add(asset.name, asset);
            }

            assetBundle.Unload(false);
            assetBundle = null;

            assetBundleStream.Close();
        }

        public static Sprite LoadSprite(string name)
        {
            GameObject GO_Sprite = LoadGameObject(name);
            if (GO_Sprite != null)
            {
                SpriteRenderer m_SpriteRenderer = GO_Sprite.GetComponent<SpriteRenderer>();
                return m_SpriteRenderer.sprite;
            }
            return null;
        }

        public static GameObject LoadGameObject(string name)
        {
            if (LoadedAssets.ContainsKey(name))
                return LoadedAssets[name];
            return null;
        }

        public static bool LOS_IsValid(Character hit_char, Vector3 splash_center, Vector3 splash_alternate = default(Vector3))
        {
            bool los = false;

            if (splash_alternate == default(Vector3))
            {
                splash_alternate = splash_center + new Vector3(0f, .2f, 0f);
            }
            if (hit_char != null)
            {
                RaycastHit hitInfo = default(RaycastHit);
                var rayDirection = hit_char.GetCenterPoint() - splash_center;
                if (Physics.Raycast(splash_center, rayDirection, out hitInfo))
                {
                    if (CollidedWithTarget(hit_char, hit_char.GetCollider(), hitInfo))
                    {
                        los = true;
                    }
                    else
                    {
                        for (int i = 0; i < 8; i++)
                        {
                            Vector3 char_col_size = hit_char.GetCollider().bounds.size;
                            var rayDirectionMod = (hit_char.GetCenterPoint() + new Vector3(char_col_size.x * (UnityEngine.Random.Range(-i, i) / 6f), char_col_size.y * (UnityEngine.Random.Range(-i, i) / 4f), char_col_size.z * (UnityEngine.Random.Range(-i, i) / 6f))) - splash_center;
                            if (Physics.Raycast(splash_center, rayDirectionMod, out hitInfo))
                            {
                                if (CollidedWithTarget(hit_char, hit_char.GetCollider(), hitInfo))
                                {
                                    los = true;
                                    break;
                                }
                            }
                        }
                    }
                }
                if (!los && splash_alternate != default(Vector3) && splash_alternate != splash_center)
                {
                    var rayDirectionAlt = hit_char.GetCenterPoint() - splash_alternate;
                    if (Physics.Raycast(splash_alternate, rayDirectionAlt, out hitInfo))
                    {
                        if (CollidedWithTarget(hit_char, hit_char.GetCollider(), hitInfo))
                        {
                            los = true;
                        }
                        else
                        {
                            for (int i = 0; i < 8; i++)
                            {
                                Vector3 char_col_size = hit_char.GetCollider().bounds.size;
                                var rayDirectionMod = (hit_char.GetCenterPoint() + new Vector3(char_col_size.x * (UnityEngine.Random.Range(-i, i) / 6f), char_col_size.y * (UnityEngine.Random.Range(-i, i) / 4f), char_col_size.z * (UnityEngine.Random.Range(-i, i) / 6f))) - splash_alternate;
                                if (Physics.Raycast(splash_alternate, rayDirectionMod, out hitInfo))
                                {
                                    if (CollidedWithTarget(hit_char, hit_char.GetCollider(), hitInfo))
                                    {
                                        los = true;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return los;
        }

        private static bool CollidedWithTarget(Character chr, Collider col, RaycastHit hit)
        {
            if (hit.collider == chr.GetCollider())
            {
                return true;
            }
            Character ch = null;
            hit.collider.gameObject.TryGetComponent<Character>(out ch);
            bool flag = ch != null;
            List<Component> comps = new List<Component>();
            comps.Clear();
            hit.collider.gameObject.GetComponents<Component>(comps);
            if (ch == null)
            {
                ch = (Character)hit.collider.GetComponentInParent(typeof(Character));
                flag = ch != null;
                if (ch == null)
                {
                    ch = (Character)hit.collider.GetComponentInChildren<Character>();
                    flag = ch != null;
                }
            }
            if (flag && ch == chr)
            {
                return true;
            }
            return false;
        }

        private void ConfigurationManager_DisplayingWindowChanged(object sender, object e)
        {
            // Read configuration manager's DisplayingWindow property
            var pi = ConfigurationManager.GetType().GetProperty("DisplayingWindow");
            ConfigurationManagerWindowShown = (bool)pi.GetValue(ConfigurationManager, null);

            if (!ConfigurationManagerWindowShown)
            {
                // After closing the window check for changed configs
                Init();
            }
        }

    }
}
