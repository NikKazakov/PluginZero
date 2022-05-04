
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using System.Reflection;
using System.Collections.Generic;

namespace PluginZero
{
    internal class PZ_Class_Mage : BaseClass
    {
        public PZ_Class_Mage() : base(
            new Ability()
            {
                Name = "Fire",
                Main = new PZ_Spell_Fireball(),
                Blocking = new PZ_Spell_Inferno()
            },
            new Ability()
            {
                Name = "Ice",
                Main = new PZ_Spell_IceDagger(),
                Blocking = new PZ_Spell_IceNova()
            },
            new Ability()
            {
                Name = "Lightning",
                Main = new PZ_Spell_LightningStorm()
            },
            typeof(Patches)
        ){}

        static bool CanMove = true;

        new public static class Patches
        {
            [HarmonyPatch(typeof(Player), "GetRunSpeedFactor"), HarmonyPostfix]
            public static void SlowRun_Postfix(Player __instance, ref float __result)
            {
                if (__instance.IsTargeted())
                {
                    __result *= .8f;
                }
            }

            [HarmonyPatch(typeof(Player), "GetJogSpeedFactor"), HarmonyPostfix]
            public static void SlowJog_Postfix(Player __instance, ref float __result)
            {
                if (__instance.IsTargeted())
                {
                    __result *= .8f;
                }
            }

            [HarmonyPatch(typeof(Player), "CanMove"), HarmonyPostfix]
            public static void LockMovement_Postfix(Player __instance, ref bool __result)
            {
                if (__result == true)
                    __result = CanMove;
            }
        }

        class PZ_Spell_Fireball : Spell, Spell.IChargable
        {
            public override string Name => "Fireball";
            public override float StaminaCost => 20 * base.StaminaCost;
            public override float SkillGain => 0.5f * base.SkillGain;
            public override float CooldownTime => 7f * base.CooldownTime;

            public bool IsCharging { get; set; }
            public int Charge { get; set; }
            public int ChargeMax => 40;
            public float ChargeDamage => 2f;

            static int Script_Layermask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "piece_nonsolid", "terrain", "character", "character_net", "character_ghost", "hitbox", "character_noenv", "vehicle");

            static GameObject fx_Flames = PluginZero.LoadGameObject("fx_flames");

            bool oldWalk;

            public override void ProcessInput(Player player, bool down = false, bool pressed = false, bool up = false)
            {
                if (down) StartCharge(player);
                if (pressed) AddCharge(player);
                if (up) EndCharge(player);
            }

            public void StartCharge(Player player)
            {
                PluginZero.logger.LogDebug($"Player {player.GetPlayerName()} started charging {Name}...");

                Object.Instantiate(fx_Flames, player.transform.position, Quaternion.identity);
                PluginZero.GetPlayerZanim().SetBool("fishingrod_charge", true);
                    
                IsCharging = true;
                Charge = 0;
                Charge++;

                oldWalk = player.GetWalk();  // get current walk state to restore later
                PluginZero.ForceLookRotation = true;
            }

            public void AddCharge(Player player)
            {
                if (IsCharging)
                {
                    player.SetWalk(true);  // force player to walk when charging Fireball

                    Charge++;
                    if (Charge >= ChargeMax)
                    {
                        EndCharge(player);
                    }
                }
            }

            public void EndCharge(Player player)
            {
                if (IsCharging)
                {
                    PluginZero.logger.LogInfo($"Player {player.GetPlayerName()} used {Name}, charged {Charge}/{ChargeMax}");

                    Ability.ApplyCooldown(player, CooldownTime, parentCooldown);
                    player.UseStamina(StaminaCost);

                    PluginZero.GetPlayerZanim().SetBool("fishingrod_charge", false);
                    PluginZero.GetPlayerZanim().SetTrigger("mace_secondary");
                    PluginZero.QueueAttack(this, .7f);

                    player.RaiseSkill(PluginZero.SkillType_Destruction, SkillGain);

                    IsCharging = false;
                    Charge = 0;
                }
            }

            public override void DoAttack(Player player)
            {
                CastFireball(player);

                player.SetWalk(oldWalk);
                PluginZero.ForceLookRotation = false;
            }

            public void CastFireball(Player player)
            {
                PluginZero.logger.LogDebug($"Casting {Name}");

                float sLevel = player.GetSkills().GetSkillLevel(PluginZero.SkillType_Destruction);
                Vector3 lookDir = player.GetLookDir();

                Vector3 spawnPoint = player.GetHeadPoint() + lookDir * .9f;
                GameObject prefab = ZNetScene.instance.GetPrefab("Imp_fireball_projectile");
                GameObject GO_Fireball = Object.Instantiate(prefab, spawnPoint, Quaternion.identity);
                Projectile P_Fireball = GO_Fireball.GetComponent<Projectile>();
                P_Fireball.name = "Fireball";
                P_Fireball.m_ttl = 60f;
                P_Fireball.m_gravity = 5f;
                P_Fireball.m_rayRadius = .1f;
                P_Fireball.m_aoe = 3f + (.03f * sLevel);

                P_Fireball.transform.localRotation = Quaternion.LookRotation(lookDir);
                GO_Fireball.transform.localScale = Vector3.zero;

                Vector3 position = player.transform.position;
                Vector3 target = (!Physics.Raycast(spawnPoint, lookDir, out RaycastHit hitInfo, float.PositiveInfinity, Script_Layermask) || !(bool)hitInfo.collider) ? (position + lookDir * 1000f) : hitInfo.point;
                target.y += target.y * Mathf.Clamp01(Vector3.Distance(GO_Fireball.transform.position, target) / 350f);
                HitData hitData = new HitData();
                hitData.m_damage.m_fire = Random.Range(1f + (.05f * sLevel), 1.5f + (.1f * sLevel)) * DamageMultiplier * (ChargeDamage * (1 + Charge / ChargeMax));
                hitData.m_damage.m_blunt = Random.Range(1f + (.03f * sLevel), 1.5f + (.07f * sLevel)) * DamageMultiplier * (ChargeDamage * (1 + Charge / ChargeMax));
                hitData.m_pushForce = 1f;
                hitData.m_skill = PluginZero.SkillType_Destruction;
                hitData.SetAttacker(player);
                Vector3 moveVec = Vector3.MoveTowards(GO_Fireball.transform.position, target, 1f);
                P_Fireball.Setup(player, (moveVec - GO_Fireball.transform.position) * 15f, -1f, hitData, null);
                Traverse.Create(root: P_Fireball).Field("m_skill").SetValue(PluginZero.SkillType_Destruction);
            }

        }

        class PZ_Spell_Inferno : Spell, Spell.IChargable
        {
            public override string Name => "Inferno";
            public override float StaminaCost => 45 * base.StaminaCost;
            public override float CooldownTime => 30f * base.CooldownTime;
            public override float SkillGain => 0.9f * base.SkillGain;

            public bool IsCharging { get; set; }
            public int Charge { get; set; }
            public int ChargeMax => 40;
            public float ChargeDamage => 2f;

            private static GameObject fx_Flames = PluginZero.LoadGameObject("fx_flames");
            static GameObject fx_FlameBurst = PluginZero.LoadGameObject("fx_flame_burst");

            public override void ProcessInput(Player player, bool down = false, bool pressed = false, bool up = false)
            {
                if (down) StartCharge(player);
                if (pressed) AddCharge(player);
                if (up) EndCharge(player);
            }

            public void StartCharge(Player player)
            {
                PluginZero.logger.LogDebug($"Player {player.GetPlayerName()} started charging {Name}...");

                PluginZero.GetPlayerZanim().SetTrigger("gpower");
                PluginZero.GetPlayerZanim().SetSpeed(.5f);

                Object.Instantiate(fx_Flames, player.transform.position, Quaternion.identity);

                IsCharging = true;
                Charge = 0;
                Charge++;

                CanMove = false;
            }

            public void AddCharge(Player player)
            {
                if (IsCharging)
                {
                    Charge++;
                    if (Charge >= ChargeMax)
                    {
                        EndCharge(player);
                    }
                }
            }

            public void EndCharge(Player player)
            {
                if (IsCharging)
                {
                    PluginZero.logger.LogInfo($"Player {player.GetPlayerName()} used {Name}, charged {Charge}/{ChargeMax}");

                    Ability.ApplyCooldown(player, CooldownTime, parentCooldown);
                    player.UseStamina(StaminaCost);

                    PluginZero.QueueAttack(this, .01f);

                    player.RaiseSkill(PluginZero.SkillType_Destruction, SkillGain);

                    IsCharging = false;
                    Charge = 0;


                }
            }

            public override void DoAttack(Player player)
            {
                PluginZero.logger.LogDebug($"Player CanMove: {player.CanMove()}, should be {CanMove}");

                CastInferno(player);

                CanMove = true;
            }

            void CastInferno(Player player)
            {
                PluginZero.logger.LogDebug($"Casting {Name}");
                float sLevel = player.GetSkills().GetSkillLevel(PluginZero.SkillType_Destruction);

                Object.Instantiate(fx_FlameBurst, player.transform.position, Quaternion.identity);

                List<Character> allCharacters = Character.GetAllCharacters();
                foreach (Character ch in allCharacters)
                {
                    if (BaseAI.IsEnemy(player, ch) && ((ch.transform.position - player.transform.position).magnitude <= (8f + (.1f * sLevel))) && PluginZero.LOS_IsValid(ch, player.GetCenterPoint(), player.transform.position + player.transform.up * .2f))
                    {
                        Vector3 direction = (ch.transform.position - player.transform.position);
                        HitData hitData = new HitData();
                        hitData.m_damage.m_fire = Random.Range(2 + (1.5f * sLevel), 5 + (2f * sLevel)) * DamageMultiplier * (ChargeDamage * (1 + Charge / ChargeMax));
                        hitData.m_pushForce = 6f;
                        hitData.m_point = ch.GetEyePoint();
                        hitData.m_dir = direction;
                        hitData.m_skill = PluginZero.SkillType_Destruction;
                        ch.Damage(hitData);
                    }
                }

                GameCamera.instance.AddShake(player.transform.position, 15f, 2f, false);
            }
        }

        class PZ_Spell_IceDagger : Spell
        {
            public override string Name => "Ice Dagger";
            public override float StaminaCost => 10f * base.StaminaCost;
            public override float SkillGain => 0.3f * base.SkillGain;
            public override float CooldownTime => 21f * base.CooldownTime;

            public int TotalAmmo = 3;
            public int AmmoCount = 3;
            public float AmmoRechargeTime => 7f * base.CooldownTime;

            private StatusEffect RechargeEffect = SE_PZ_AbilityCooldown.CreateInstance("PZ_SE_IceDaggerRecharge");

            static int Script_Layermask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "piece_nonsolid", "terrain", "character", "character_net", "character_ghost", "hitbox", "character_noenv", "vehicle");

            private static GameObject IceDagger = PluginZero.LoadGameObject("spell_ice_dagger");

            public override void ProcessInput(Player player, bool down = false, bool pressed = false, bool up = false)
            {
                if (down)
                {
                    AmmoCount--;
                    if (AmmoCount == 0)
                    {
                        Ability.ApplyCooldown(player, CooldownTime, parentCooldown);
                        AmmoCount = 3;
                    }
                    else
                    {
                        RechargeEffect = Ability.ApplyCooldown(player, AmmoRechargeTime, RechargeEffect.name);
                    }

                    player.UseStamina(StaminaCost);

                    PluginZero.ForceLookRotation = true;
                    PluginZero.GetPlayerZanim().SetTrigger("knife_stab0");
                    PluginZero.QueueAttack(this, .13f);

                    player.RaiseSkill(PluginZero.SkillType_Destruction, SkillGain);
                }
            }

            public override void DoAttack(Player player)
            {
                CastIceDagger(player);

                PluginZero.ForceLookRotation = false;
            }

            public void CastIceDagger(Player player)
            {
                PluginZero.logger.LogDebug($"Casting {Name}");

                float sLevel = player.GetSkills().GetSkillLevel(PluginZero.SkillType_Destruction);
                Vector3 lookDir = player.GetLookDir();

                Vector3 spawnPoint = player.GetHeadPoint() + lookDir * .4f;
                GameObject GO_IceDagger = Object.Instantiate(IceDagger, spawnPoint, Quaternion.identity);
                Projectile P_IceDagger = GO_IceDagger.GetComponent<Projectile>();
                P_IceDagger.m_ttl = 60f;
                P_IceDagger.m_gravity = .1f;
                P_IceDagger.m_rayRadius = .1f;
                P_IceDagger.transform.localRotation = Quaternion.LookRotation(lookDir);

                Vector3 position = player.transform.position;
                Vector3 target = (!Physics.Raycast(spawnPoint, lookDir, out RaycastHit hitInfo, float.PositiveInfinity, Script_Layermask) || !(bool)hitInfo.collider) ? (position + lookDir * 1000f) : hitInfo.point;
                HitData hitData = new HitData();
                hitData.m_damage.m_pierce = Random.Range(1f + (.12f * sLevel), 3f + (.36f * sLevel)) * DamageMultiplier;
                hitData.m_damage.m_frost = Random.Range((.2f + (.1f * sLevel)), 1f + (.5f * sLevel)) * DamageMultiplier;
                hitData.m_skill = PluginZero.SkillType_Destruction;
                hitData.SetAttacker(player);
                Vector3 a = Vector3.MoveTowards(GO_IceDagger.transform.position, target, 1f);
                P_IceDagger.Setup(player, (a - GO_IceDagger.transform.position) * 55f, -1f, hitData, null);

                Traverse.Create(root: P_IceDagger).Field("m_skill").SetValue(PluginZero.SkillType_Destruction);
            }

            public override void UpdateIcon(Hud hud, RectTransform icon, string name)
            {
                if (Player.m_localPlayer.GetSEMan().HaveStatusEffect(parentCooldown))
                {
                    icon.GetComponentInChildren<Text>().text = $"{name} (0/{TotalAmmo})";
                }
                else
                {
                    if (AmmoCount < TotalAmmo && RechargeEffect.IsDone())
                    {
                        AmmoCount++;
                        if (AmmoCount < TotalAmmo)
                        {
                            RechargeEffect = Ability.ApplyCooldown(Player.m_localPlayer, AmmoRechargeTime, RechargeEffect.name);
                        }
                    }
                    icon.GetComponentInChildren<Text>().text = $"{name} ({AmmoCount}/{TotalAmmo})";
                }
            }
        }

        class PZ_Spell_IceNova : Spell, Spell.IChargable
        {
            public override string Name => "Ice Nova";
            public override float StaminaCost => 60 * base.StaminaCost;
            public override float SkillGain => 0.9f * base.SkillGain;
            public override float CooldownTime => 40f * base.CooldownTime;

            public bool IsCharging { get; set; }
            public int Charge { get; set; }
            public int ChargeMax => 60;
            public float ChargeDamage => 2f;

            GameObject fx_Frost;
            GameObject fx_FrostBurst;

            bool firstRun = true;

            void Init()
            {
                firstRun = false;
                fx_Frost = ZNetScene.instance.GetPrefab("fx_guardstone_activate");
                fx_FrostBurst = ZNetScene.instance.GetPrefab("fx_eikthyr_stomp");
            }

            public override void ProcessInput(Player player, bool down = false, bool pressed = false, bool up = false)
            {
                if (firstRun) Init();
                if (down) StartCharge(player);
                if (pressed) AddCharge(player);
                if (up) EndCharge(player);
            }

            public void StartCharge(Player player)
            {
                PluginZero.logger.LogDebug($"Player {player.GetPlayerName()} started charging {Name}...");

                PluginZero.GetPlayerZanim().SetTrigger("gpower");
                PluginZero.GetPlayerZanim().SetSpeed(.5f);
                Object.Instantiate(fx_Frost, player.transform.position, Quaternion.identity);

                IsCharging = true;
                Charge = 0;
                Charge++;

                CanMove = false;
            }

            public void AddCharge(Player player)
            {
                if (IsCharging)
                {
                    Charge++;
                    if (Charge >= ChargeMax)
                    {
                        EndCharge(player);
                    }
                }
            }

            public void EndCharge(Player player)
            {
                if (IsCharging)
                {
                    PluginZero.logger.LogInfo($"Player {player.GetPlayerName()} used {Name}, charged {Charge}/{ChargeMax}");

                    Ability.ApplyCooldown(player, CooldownTime, parentCooldown);
                    player.UseStamina(StaminaCost);

                    PluginZero.QueueAttack(this, .01f);

                    player.RaiseSkill(PluginZero.SkillType_Destruction, SkillGain);

                    IsCharging = false;
                    Charge = 0;
                }
            }

            public override void DoAttack(Player player)
            {
                CastIceNova(player);

                CanMove = true;
            }

            public void CastIceNova(Player player)
            {
                PluginZero.logger.LogDebug($"Casting {Name}");

                float sLevel = player.GetSkills().GetSkillLevel(PluginZero.SkillType_Destruction);

                Object.Instantiate(fx_FrostBurst, player.transform.position, Quaternion.identity);

                //Apply effects
                if (player.GetSEMan().HaveStatusEffect("Burning"))
                {
                    player.GetSEMan().RemoveStatusEffect("Burning");
                }

                List<Character> allCharacters = Character.GetAllCharacters();
                foreach (Character ch in allCharacters)
                {
                    if (BaseAI.IsEnemy(player, ch) && ((ch.transform.position - player.transform.position).magnitude <= (10f + (.1f * sLevel))) && PluginZero.LOS_IsValid(ch, player.GetCenterPoint(), player.transform.position + player.transform.up * .15f))
                    {
                        Vector3 direction = (ch.transform.position - player.transform.position);
                        HitData hitData = new HitData();
                        hitData.m_damage.m_frost = Random.Range(6 + (.2f * sLevel), 14 + (.6f * sLevel)) * DamageMultiplier * (ChargeDamage * (1 + Charge / ChargeMax));
                        hitData.m_pushForce = 20f;
                        hitData.m_point = ch.GetEyePoint();
                        hitData.m_dir = direction;
                        hitData.m_skill = PluginZero.SkillType_Destruction;
                        ch.Damage(hitData);
                        SE_Frost se_frost = (SE_Frost)ScriptableObject.CreateInstance(typeof(SE_Frost));
                        ch.GetSEMan().AddStatusEffect(se_frost.name, true);
                    }
                }


            }
        }

        class PZ_Spell_LightningStorm : Spell, Spell.IChantable
        {
            public override string Name => "Lightning Storm";
            public override float StaminaCost => 20 * base.StaminaCost;
            public override float SkillGain => 2f * base.SkillGain;
            public override float CooldownTime => 90f * base.CooldownTime;

            public bool IsChanting { get; set; }
            public int Chant { get; set; }
            public int ChantPerTick => 20;
            public float StaminaPerTick => 10f * base.StaminaCost;

            public float StrikesPerLevel => .06f;
            public float Range => 15f;

            GameObject fx_Lightning_prefab;
            GameObject fx_Lightning;
            GameObject LightningStrike = PluginZero.LoadGameObject("spell_lightning_strike");
            //GameObject LightningStrike;

            bool firstRun = true;

            void Init()
            {
                firstRun = false;
                fx_Lightning_prefab = ZNetScene.instance.GetPrefab("fx_Lightning");
                //LightningStrike = ZNetScene.instance.GetPrefab("lightningAOE");
            }

            public override void ProcessInput(Player player, bool down = false, bool pressed = false, bool up = false)
            {
                if (firstRun) Init(); 
                if (down) StartChant(player);
                if (pressed) AddChant(player);
                if (up) EndChant(player);
            }
            public void StartChant(Player player)
            {
                PluginZero.logger.LogDebug($"Player {player.GetPlayerName()} started chanting {Name}...");

                player.UseStamina(StaminaCost);

                PluginZero.GetPlayerZanim().SetTrigger("emote_cheer");

                fx_Lightning = Object.Instantiate(fx_Lightning_prefab, player.transform.position, Quaternion.identity);

                IsChanting = true;
                Chant = 0;
                Chant++;

                CanMove = false;
            }
            public void AddChant(Player player)
            {
                if (IsChanting)
                {
                    Chant++;
                    if (Chant >= ChantPerTick)
                    {
                        if (!player.InEmote())
                            PluginZero.GetPlayerZanim().SetTrigger("emote_cheer");

                        Tick(player);
                        if (player.HaveStamina(StaminaPerTick))
                        {
                            player.UseStamina(StaminaPerTick);
                        }
                        else
                        {
                            EndChant(player);
                        }

                        Chant = 0;
                    }
                }
            }
            public void EndChant(Player player)
            {
                if (IsChanting)
                {
                    PluginZero.logger.LogInfo($"Player {player.GetPlayerName()} finished chanting {Name}");

                    Ability.ApplyCooldown(player, CooldownTime, parentCooldown);

                    if (player.HaveStamina(StaminaPerTick)) player.UseStamina(StaminaPerTick);
                    else Hud.instance.StaminaBarNoStaminaFlash();

                    PluginZero.GetPlayerZanim().SetTrigger("emote_stop");

                    float sLevel = player.GetSkills().GetSkillLevel(PluginZero.SkillType_Destruction);
                    int quantity = Mathf.FloorToInt(StrikesPerLevel * sLevel * (Chant / ChantPerTick) * Mathf.Clamp01(player.GetStamina() / StaminaPerTick));
                    if (quantity == 0) quantity = 1;
                    Tick(player, quantity);
                    Object.Destroy(fx_Lightning);

                    player.RaiseSkill(PluginZero.SkillType_Destruction, SkillGain);

                    IsChanting = false;
                    Chant = 0;

                    CanMove = true;
                }
            }

            private void Tick(Player player, int forceQuantity = 0)
            {
                float sLevel = player.GetSkills().GetSkillLevel(PluginZero.SkillType_Destruction);

                List<Vector3> strikes = new List<Vector3>();

                List<Character> enemies = FindEnemies(player);

                int quantity = forceQuantity;

                if (forceQuantity <= 0)
                {
                    quantity = Mathf.FloorToInt(StrikesPerLevel * sLevel);
                    if (quantity == 0)
                    {
                        quantity++;
                    }
                }

                // strike random enemy no more than once
                for (int i = 0; i < quantity; i++)
                {
                    if (enemies.Count > 0)
                    {
                        int enemy = Random.Range(0, enemies.Count);
                        strikes.Add(enemies[enemy].transform.position);

                        enemies.RemoveAt(enemy);
                    }
                    else
                    {
                        Vector3 randomSpot = player.transform.position + new Vector3(Random.Range(-Range, Range), 9999f, Random.Range(-Range, Range));
                        if (Physics.Raycast(randomSpot, Vector3.down, out RaycastHit hit))
                        {
                            Debug.DrawRay(randomSpot, Vector3.down * hit.distance, Color.yellow);
                            randomSpot.y -= hit.distance;
                        }
                            
                        strikes.Add(randomSpot);
                    }

                }

                foreach (Vector3 strike in strikes)
                {
                    MakeLightning(player, strike, sLevel);
                }

            }
            private void MakeLightning(Player player, Vector3 position, float sLevel)
            {
                PluginZero.logger.LogDebug($"Creating Lightning at {position}");

                GameObject lightning = Object.Instantiate(LightningStrike, position, Quaternion.identity);

                lightning.transform.Find("shockwave_plane").localScale = new Vector3(.15f, 1, .15f);

                HitData AOE_ROD_hitData = new HitData();
                AOE_ROD_hitData.m_damage.m_lightning = Random.Range(1f + (.05f * sLevel), 1.5f + (.1f * sLevel)) * DamageMultiplier;
                AOE_ROD_hitData.m_skill = PluginZero.SkillType_Destruction;
                AOE_ROD_hitData.SetAttacker(player);
                lightning.transform.Find("AOE_ROD").GetComponent<Aoe>().Setup(player, Vector3.zero, 0f, AOE_ROD_hitData, null);

                HitData AOE_AREA_hitData = new HitData();
                AOE_AREA_hitData.m_damage.m_lightning = Random.Range(.1f + (.01f * sLevel), .5f + (.05f * sLevel)) * DamageMultiplier;
                AOE_AREA_hitData.m_skill = PluginZero.SkillType_Destruction;
                AOE_AREA_hitData.SetAttacker(player);
                lightning.transform.Find("AOE_AREA").GetComponent<Aoe>().Setup(player, Vector3.zero, 0f, AOE_AREA_hitData, null);

                //Transform AOE_ROD_transform = lightning.transform.Find("AOE_ROD");
                //Aoe AOE_ROD_old = AOE_ROD_transform.GetComponent<Aoe>();  // old Aoe keeps damage for some reason, just create new one with same params
                //Aoe AOE_ROD = AOE_ROD_transform.gameObject.AddComponent<Aoe>();
                //AOE_ROD.m_useCollider = AOE_ROD_old.m_useCollider;
                //AOE_ROD.m_hitFriendly = false;
                //AOE_ROD.m_hitEffects = AOE_ROD_old.m_hitEffects;
                //HitData AOE_ROD_hitData = new HitData();
                //AOE_ROD_hitData.m_damage.m_lightning = Random.Range(1f + (.05f * sLevel), 1.5f + (.1f * sLevel)) * DamageMultiplier;
                //AOE_ROD_hitData.m_skill = PluginZero.SkillType_Destruction;
                //AOE_ROD_hitData.SetAttacker(player);
                //AOE_ROD.Setup(player, Vector3.zero, 0f, AOE_ROD_hitData, null);
                //Object.DestroyImmediate(AOE_ROD_old);

                //Transform AOE_AREA_transform = lightning.transform.Find("AOE_AREA");
                //AOE_AREA_transform.localScale = new Vector3(.15f, 1, .15f);
                //Aoe AOE_AREA_old = AOE_AREA_transform.GetComponent<Aoe>();  // old Aoe keeps damage for some reason, just create new one with same params
                //Aoe AOE_AREA = AOE_AREA_transform.gameObject.AddComponent<Aoe>();
                //AOE_AREA.m_useCollider = AOE_AREA_old.m_useCollider;
                //AOE_AREA.m_hitFriendly = false;
                //AOE_AREA.m_hitEffects = AOE_AREA_old.m_hitEffects;
                //HitData AOE_AREA_hitData = new HitData();
                //AOE_AREA_hitData.m_damage.m_lightning = Random.Range(.1f + (.01f * sLevel), .5f + (.05f * sLevel)) * DamageMultiplier;
                //AOE_AREA_hitData.m_skill = PluginZero.SkillType_Destruction;
                //AOE_AREA_hitData.SetAttacker(player);
                //AOE_AREA.Setup(player, Vector3.zero, 0f, AOE_AREA_hitData, null);
                //Object.DestroyImmediate(AOE_AREA_old);

                PluginZero.logger.LogDebug("Current aoes:");
                Aoe[] aoes = lightning.GetComponentsInChildren<Aoe>();
                foreach (Aoe aoe in aoes)
                {
                    PluginZero.logger.LogDebug(aoe.name);
                }
            }

            private List<Character> FindEnemies(Player player)
            {
                List<Character> enemies = new List<Character>();
                List<Character> allCharacters = Character.GetAllCharacters();
                foreach (Character ch in allCharacters)
                {
                    if (BaseAI.IsEnemy(player, ch) && ((ch.transform.position - player.transform.position).magnitude <= Range))
                    {
                        enemies.Add(ch);
                    }
                }
                return enemies;
            }
        }
    }
}
