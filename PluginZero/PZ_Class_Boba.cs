namespace PluginZero
{
    internal class PZ_Class_Boba : BaseClass
    {
        public PZ_Class_Boba() : base(
            new Ability()
            {
                Name = "Fus",
                Main = new PZ_Spell_Fus()
            },
            new Ability()
            {
                Name = "Ro",
                Main = new PZ_Spell_Ro()
            },
            new Ability()
            {
                Name = "Dah",
                Main = new PZ_Spell_Dah()
            },
            typeof(Patches)
        ){}
    }

    internal class PZ_Spell_Fus : Spell
    {
        public override string Name => "Fus";
        public override float StaminaCost => 10f * base.StaminaCost;
        public override float CooldownTime => 3f * base.CooldownTime;
        public override float SkillGain => .3f * base.SkillGain;
    }

    internal class PZ_Spell_Ro : Spell
    {
        public override string Name => "Ro";
        public override float StaminaCost => 20f * base.StaminaCost;
        public override float CooldownTime => 6f * base.StaminaCost;
        public override float SkillGain => .6f * base.SkillGain;
    }

    internal class PZ_Spell_Dah : Spell
    {
        public override string Name => "Dah";
        public override float StaminaCost => 40f * base.StaminaCost;
        public override float CooldownTime => 12f * base.StaminaCost;
        public override float SkillGain => 1.2f * base.SkillGain;
    }
}
