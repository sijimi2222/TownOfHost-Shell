// Ability-option initializer extracted verbatim from the renamed product role.
using TownOfHost;using TownOfHost.Roles.Core;namespace AuditDefs {static class EliminatorDefinition {
public static SimpleRoleInfo RoleInfo=new(){RoleName=CustomRoles.Eliminator,ConfigId=993000,CustomRoleType=CustomRoleTypes.Neutral,Tab=TabGroup.NeutralRoles,OptionSort=(0,-1),OptionCreator=SetupOptionItem};
static OptionItem KillCooldown,RequiredKills,MustSurvive;
enum OptionName { EliminatorRequiredKills, EliminatorMustSurvive }
static void SetupOptionItem()
    {
        KillCooldown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.KillCooldown,
            new ClampedFloatValueRule(2.5f, 100f, 2.5f), 30f, false).SetValueFormat(OptionFormat.Seconds);
        RequiredKills = IntegerOptionItem.Create(RoleInfo, 11, OptionName.EliminatorRequiredKills,
            new ClampedIntegerValueRule(1, 15, 1), 2, false).SetValueFormat(OptionFormat.Players);
        MustSurvive = BooleanOptionItem.Create(RoleInfo, 12, OptionName.EliminatorMustSurvive, false, false);
    }
}}