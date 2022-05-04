using UnityEngine;
using UnityEngine.UI;
using System;

namespace PluginZero
{
    abstract class BaseClass
    {
        SaveSystem
        protected Ability Ability1 { get; }
        protected Ability Ability2 { get; }
        protected Ability Ability3 { get; }

        bool firstRun = true;

        public BaseClass(Ability ability1, Ability ability2, Ability ability3, Type patches)
        {
            Ability1 = ability1;
            Ability2 = ability2;
            Ability3 = ability3;

            Ability1.Hotkey = PluginZero.Ability1_Hotkey.Value.MainKey;
            Ability2.Hotkey = PluginZero.Ability2_Hotkey.Value.MainKey;
            Ability3.Hotkey = PluginZero.Ability3_Hotkey.Value.MainKey;

            PluginZero.logger.LogInfo("Applying class patches...");
            PluginZero.harmony.PatchAll(patches);
        }

        public static class Patches { };

        public virtual void ProcessInput(Player player)
        {
            if (player != null && WorldFocus(player) && !player.InPlaceMode())
            {
                if (Input.GetKeyDown(Ability1.Hotkey))
                {
                    Ability1.ProcessInput(player, down: true);
                }
                else if (Input.GetKey(Ability1.Hotkey))
                {
                    Ability1.ProcessInput(player, pressed: true);
                }
                else if (Input.GetKeyUp(Ability1.Hotkey))
                {
                    Ability1.ProcessInput(player, up: true);
                }

                if (Input.GetKeyDown(Ability2.Hotkey))
                {
                    Ability2.ProcessInput(player, down: true);
                }
                else if (Input.GetKey(Ability2.Hotkey))
                {
                    Ability2.ProcessInput(player, pressed: true);
                }
                else if (Input.GetKeyUp(Ability2.Hotkey))
                {
                    Ability2.ProcessInput(player, up: true);
                }

                if (Input.GetKeyDown(Ability3.Hotkey))
                {
                    Ability3.ProcessInput(player, down: true);
                }
                else if (Input.GetKey(Ability3.Hotkey))
                {
                    Ability3.ProcessInput(player, pressed: true);
                }
                else if (Input.GetKeyUp(Ability3.Hotkey))
                {
                    Ability3.ProcessInput(player, up: true);
                }

            }
        }

        public void MakeIcons(Hud hud)
        {
            float xMod = (Screen.width / 1920f);
            float yMod = (Screen.height / 1080f);
            float xStep = 75;
            float yStep = 0;
            float xOffset = (PluginZero.AbilityBarXOffset.Value * xMod);
            float yOffset = (PluginZero.AbilityBarYOffset.Value * yMod);

            Ability1.MakeIcon(hud, PluginZero.AbilityIconScale.Value, new Vector3(xOffset + (xStep * 1), yOffset + (yStep * 1), 0));
            Ability2.MakeIcon(hud, PluginZero.AbilityIconScale.Value, new Vector3(xOffset + (xStep * 2), yOffset + (yStep * 2), 0));
            Ability3.MakeIcon(hud, PluginZero.AbilityIconScale.Value, new Vector3(xOffset + (xStep * 3), yOffset + (yStep * 3), 0));
        }
        public void UpdateIcons(Hud hud)
        {
            if (firstRun)
            {
                firstRun = false;
                MakeIcons(hud);
            }

            Ability1.UpdateIcon(hud);
            Ability2.UpdateIcon(hud);
            Ability3.UpdateIcon(hud);
        }
        public void DestroyIcons()
        {
            Ability1.DestroyIcon();
            Ability2.DestroyIcon();
            Ability3.DestroyIcon();
        }

        public static bool WorldFocus(Player p)
        {
            bool result = (!(bool)Chat.instance || !Chat.instance.HasFocus()) &&
                          !Console.IsVisible() &&
                          !TextInput.IsVisible() &&
                          !StoreGui.IsVisible() &&
                          !InventoryGui.IsVisible() &&
                          !Menu.IsVisible() &&
                          (!(bool)TextViewer.instance || !TextViewer.instance.IsVisible()) &&
                          !Minimap.IsOpen() &&
                          !GameCamera.InFreeFly();
            if (p.IsDead() || p.InCutscene() || p.IsTeleporting())
            {
                result = false;
            }
            return result;
        }
    }

    class Ability
    {
        public virtual string Name { get; set; }
        public string CooldownName { get => $"SE_PZ_{Name}_CD"; }

        public KeyCode Hotkey;

        public Spell Main { get => _main; set { value.parentCooldown = CooldownName; _main = value; } }
        private Spell _main;
        public Spell Blocking { get => _blocking; set { value.parentCooldown = CooldownName; _blocking = value; } }
        private Spell _blocking;
        public Spell Crouching { get => _crouching; set { value.parentCooldown = CooldownName; _crouching = value; } }
        private Spell _crouching;

        public virtual void ProcessInput(Player player, bool down = false, bool pressed = false, bool up = false)
        {
            if (!player.GetSEMan().HaveStatusEffect(CooldownName))
            {
                if (Blocking != null && player.IsBlocking())
                {
                    CallSpell(Blocking, player, down, pressed, up);
                }
                else if (Crouching != null && player.IsCrouching())
                {
                    CallSpell(Crouching, player, down, pressed, up);
                }
                else
                {
                    CallSpell(Main, player, down, pressed, up);
                }
            }
        }

        void CallSpell(Spell spell, Player player, bool down, bool pressed, bool up)
        {
            if (spell is Spell.IChantable chantable_spell)
            {
                if (down)
                {
                    if (player.HaveStamina(spell.StaminaCost))
                    {
                        spell.ProcessInput(player, down, pressed, up);
                    }
                    else
                    {
                        Hud.instance.StaminaBarNoStaminaFlash();
                        player.Message(MessageHud.MessageType.TopLeft, $"Not enough stamina to cast {spell.Name}: ({player.GetStamina():n0}/{spell.StaminaCost})");
                    }
                }
                else if (pressed)
                {
                    if (player.HaveStamina(chantable_spell.StaminaPerTick))
                    {
                        spell.ProcessInput(player, down, pressed, up);
                    }
                    else
                    {
                        Hud.instance.StaminaBarNoStaminaFlash();
                    }
                }
                else
                {
                    spell.ProcessInput(player, down, pressed, up);
                }
            }
            else
            {
                if (player.HaveStamina(spell.StaminaCost) || up)
                {
                    spell.ProcessInput(player, down, pressed, up);
                }
                else
                {
                    Hud.instance.StaminaBarNoStaminaFlash();
                    if (down)
                        player.Message(MessageHud.MessageType.TopLeft, $"Not enough stamina to cast {spell.Name}: ({player.GetStamina():n0}/{spell.StaminaCost})");
                }
            }
        }

        public static StatusEffect ApplyCooldown(Player player, float time, string name)
        {
            StatusEffect se_cd = SE_PZ_AbilityCooldown.CreateInstance(name);
            se_cd.m_ttl = time;
            player.GetSEMan().AddStatusEffect(se_cd);
            return player.GetSEMan().GetStatusEffect(name);
        }

        protected RectTransform Icon;
        protected Image IconImage;
        protected string SpritePath => $"icon_ability_{Name.ToLower()}";
        protected Text IconText;
        protected string HotkeyString;

        public void MakeIcon(Hud hud, float scale, Vector3 position)
        {
            PluginZero.logger.LogDebug($"Trying to create ability icon from {SpritePath}");

            HotkeyString = Hotkey.ToString().ToUpper();

            Icon = UnityEngine.Object.Instantiate(hud.m_statusEffectTemplate, position, new Quaternion(0, 0, 0, 1), hud.m_statusEffectListRoot);
            Icon.Find("Icon").localScale = new Vector3(scale, scale, Icon.transform.localScale.z);

            Icon.GetComponentInChildren<Text>().text = Name;

            IconText = Icon.Find("TimeText").GetComponent<Text>();
            IconText.text = HotkeyString;

            IconImage = Icon.Find("Icon").GetComponent<Image>();

            Sprite sprite = PluginZero.LoadSprite(SpritePath);
            PluginZero.logger.LogDebug($"Loaded sprite {sprite}");
            IconImage.sprite = PluginZero.LoadSprite(SpritePath);
            IconImage.color = Color.white;

            Icon.gameObject.SetActive(value: true);
        }
        public virtual void UpdateIcon(Hud hud)
        {
            if (Player.m_localPlayer.GetSEMan().HaveStatusEffect(CooldownName))
            {
                IconImage.color = PluginZero.abilityCooldownColor;
                IconText.text = StatusEffect.GetTimeString(Player.m_localPlayer.GetSEMan().GetStatusEffect(CooldownName).GetRemaningTime());
            }
            else
            {
                IconImage.color = Color.white;
                IconText.text = HotkeyString;
            }

            if (Main != null) Main.UpdateIcon(hud, Icon, Name);
            if (Blocking != null) Blocking.UpdateIcon(hud, Icon, Name);
            if (Crouching != null) Crouching.UpdateIcon(hud, Icon, Name);
        }
        public void DestroyIcon()
        {
            if (Icon)
            {
                UnityEngine.Object.Destroy(Icon.gameObject);
            }
        }
    }

    abstract class Spell
    {
        public interface IChargable
        {
            bool IsCharging { get; set; }
            int Charge { get; set; }
            int ChargeMax { get; }
            float ChargeDamage { get; }

            void StartCharge(Player player);

            void AddCharge(Player player);

            void EndCharge(Player player);
        }

        public interface IChantable
        {
            bool IsChanting { get; set; }
            int Chant { get; set; }
            int ChantPerTick { get; }
            float StaminaPerTick { get; }

            void StartChant(Player player);

            void AddChant(Player player);

            void EndChant(Player player);
        }

        public abstract string Name { get; }
        public virtual float StaminaCost => PluginZero.GlobalStaminaCostMultiplier.Value;
        public virtual float CooldownTime => PluginZero.GlobalCooldownMultiplier.Value;
        public virtual float SkillGain => PluginZero.GlobalExpGainMultiplier.Value;
        public virtual float DamageMultiplier => PluginZero.GlobalDamageMultiplier.Value;

        public string parentCooldown;
        public virtual void ProcessInput(Player player, bool down = false, bool pressed = false, bool up = false)
        {
            PluginZero.GetPlayerZanim().SetTrigger("knockdown");
            PluginZero.QueueAttack(this);
            Ability.ApplyCooldown(player, CooldownTime, parentCooldown);
        }

        public virtual void UpdateIcon(Hud hud, RectTransform icon, string name) { }

        public virtual void DoAttack(Player player)
        {
            player.Message(MessageHud.MessageType.Center, $"{Name}...");
        }
    }

    internal class SE_PZ_AbilityCooldown : StatusEffect
    {
        new public static SE_PZ_AbilityCooldown CreateInstance(string se_name)
        {
            var data = CreateInstance<SE_PZ_AbilityCooldown>();
            data.name = se_name;
            return data;
        }

        public override bool CanAdd(Character character)
        {
            return character.IsPlayer();
        }
    }
}
