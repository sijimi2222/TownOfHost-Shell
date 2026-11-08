using TownOfHost;using TownOfHost.Roles.Core;
namespace TownOfHost {
static class SheriffCompatibility {
public static Dictionary<CustomRoles,OptionItem> KillTargetOptions=new();
public static bool CanKillNewTarget(CustomRoles cRole){
        if (cRole == CustomRoles.Eliminator)
            return CanKillNeutrals.GetValue() == 0 || (KillTargetOptions.TryGetValue(cRole, out var eliminatorTargetOption) && eliminatorTargetOption.GetBool());
throw new Exception("not the new role");}
public static SimpleRoleInfo RoleInfo=new(){ConfigId=35100};public static OptionItem CanKillNeutrals=new();public static Dictionary<string,int> Ids=new();
public static void SetUpKillTargetOption(CustomRoles r,int offset,bool def,OptionItem parent)=>Ids.Add(r.ToString(),RoleInfo.ConfigId+offset);
public static void SetUpSchrodingerCatKillTargetOption(ISchrodingerCatOwner.TeamType r,int offset,bool def,OptionItem parent)=>Ids.Add("Cat:"+r,RoleInfo.ConfigId+offset);
public static void SetUpNeutralOptions(int idOffset)

    {

        foreach (var neutral in CustomRolesHelper.AllStandardRoles.Where(x => x.IsNeutral()).ToArray())

        {

            if (Event.CheckRole(neutral) is false) continue;

            if (neutral is CustomRoles.SchrodingerCat) continue;

            // New roles must not shift existing sequential target-option IDs.
            if (neutral is CustomRoles.Romeo or CustomRoles.Juliet)
            {
                SetUpKillTargetOption(neutral, (neutral == CustomRoles.Romeo ? 992716 : 992816) - RoleInfo.ConfigId, true, CanKillNeutrals);
                continue;
            }
            if (neutral == CustomRoles.Eliminator)
            {
                SetUpKillTargetOption(neutral, 993013 - RoleInfo.ConfigId, true, CanKillNeutrals);
                continue;
            }
            if (neutral == CustomRoles.LessThan)
            {
                SetUpKillTargetOption(neutral, 40317 - RoleInfo.ConfigId, true, CanKillNeutrals);
                continue;
            }
            SetUpKillTargetOption(neutral, idOffset, true, CanKillNeutrals);

            idOffset++;

        }

        foreach (var catType in EnumHelper.GetAllValues<ISchrodingerCatOwner.TeamType>())

        {

            if ((byte)catType < 50) continue;

            SetUpSchrodingerCatKillTargetOption(catType, idOffset, true, CanKillNeutrals);

            idOffset++;

        }

    }}
static class FactionCompatibility {
public static Dictionary<CustomRoles,OptionItem> OptionRole=new();static OptionItem CanSeeFactionMate,CantKillFaction;
public static void SetUpOption()

    {

        Options.SetupRoleOptions(70700, TabGroup.Addons, CustomRoles.Faction, new(1, 1, 1), fromtext: UtilsOption.GetFrom(From.TownOfHost_K));

        CanSeeFactionMate = BooleanOptionItem.Create(70711, "CanSeeFactionMate", false, TabGroup.Addons, false).SetSubRoleOptionItem(CustomRoles.Faction);

        CantKillFaction = BooleanOptionItem.Create(70713, "CantKillFaction", false, TabGroup.Addons, false).SetSubRoleOptionItem(CustomRoles.Faction);

        ObjectOptionitem.Create(70712, "AddonOption", true, "", TabGroup.Addons).SetOptionName(() => "Assign Option").SetSubRoleOptionItem(CustomRoles.Faction);



        var id = 70720;

        List<CustomRoles> AddWinners = [CustomRoles.Amanojaku];

        foreach (var role in EnumHelper.GetAllValues<CustomRoles>().Where(role => role.IsNeutral() && role != CustomRoles.LessThan && role is not CustomRoles.Romeo and not CustomRoles.Juliet and not CustomRoles.Eliminator).OrderBy(x => x.GetRoleInfo()?.ConfigId ?? 100000))

        {

            if (role.IsCombinationRole()) continue;

            if (role is CustomRoles.Madonna) continue;

            if (role is CustomRoles.Cupid) continue;

            if (SoloWinOption.AllData.ContainsKey(role) is false)

            {

                if (role is CustomRoles.Jackaldoll or CustomRoles.JackalAlien or CustomRoles.JackalMafia or CustomRoles.JackalWolf) continue;



                AddWinners.Add(role);

                continue;

            }

            var option = BooleanOptionItem.Create(id++, "AssingroleType", true, TabGroup.Addons, false).SetSubRoleOptionItem(CustomRoles.Faction).SetEnabled(() =>

            {

                if (role is not CustomRoles.Jackal) return role.IsEnable();



                return CustomRoles.Jackal.IsEnable() || CustomRoles.JackalAlien.IsEnable() || CustomRoles.JackalMafia.IsEnable() || CustomRoles.Jackaldoll.IsEnable() || CustomRoles.JackalWolf.IsEnable();

            });

            option.ReplacementDictionary = new Dictionary<string, string> { { "%roletype%", UtilsRoleText.GetRoleColorAndtext(role) } };



            if (!OptionRole.TryAdd(role, option))

            {

                Logger.Error($"{role}重複したよ!!!", "Faction");

            }

        }



        foreach (var role in AddWinners)

        {

            var option = BooleanOptionItem.Create(id++, "AssingroleType", true, TabGroup.Addons, false).SetSubRoleOptionItem(CustomRoles.Faction).SetEnabled(() => role.IsEnable());

            option.ReplacementDictionary = new Dictionary<string, string> { { "%roletype%", UtilsRoleText.GetRoleColorAndtext(role) } };



            if (!OptionRole.TryAdd(role, option))

            {

                Logger.Error($"{role}重複したよ!!!", "Faction");

            }

        }

        // Keep every historical sequential ID; the new role has its own reserved slot.
        var eliminatorOption = BooleanOptionItem.Create(993014, "AssingroleType", true, TabGroup.Addons, false)
            .SetSubRoleOptionItem(CustomRoles.Faction).SetEnabled(() => CustomRoles.Eliminator.IsEnable());
        eliminatorOption.ReplacementDictionary = new Dictionary<string, string>
            { { "%roletype%", UtilsRoleText.GetRoleColorAndtext(CustomRoles.Eliminator) } };
        OptionRole.TryAdd(CustomRoles.Eliminator, eliminatorOption);
    }}
public static class EnumHelper {public static bool IncludeNew;public static IEnumerable<T> GetAllValues<T>() where T:struct,Enum=>Enum.GetValues<T>().Where(r=>IncludeNew||typeof(T)!=typeof(CustomRoles)||!r.Equals((T)(object)CustomRoles.Eliminator));}
public static class CustomRolesHelper {public static IEnumerable<CustomRoles> AllStandardRoles=>EnumHelper.GetAllValues<CustomRoles>().Where(r=>((int)r<500||r==CustomRoles.Nova)&&r is not CustomRoles.HASFox and not CustomRoles.HASTroll and not CustomRoles.TaskPlayerB and not CustomRoles.NDPlayer);}
public static class CompatibilityExt {public static bool IsCombinationRole(this CustomRoles r)=>r is CustomRoles.Romeo or CustomRoles.Juliet;public static bool IsEnable(this CustomRoles r)=>true;}
public static class Event {public static bool CheckRole(CustomRoles r)=>true;}
public static class SoloWinOption {public static Dictionary<CustomRoles,object> AllData=new();}
public static class Options {public static void SetupRoleOptions(int id,TabGroup tab,CustomRoles role,ClampedIntegerValueRule r,string fromtext){}}
public static class UtilsOption {public static string GetFrom(From f)=>f.ToString();}
public static class UtilsRoleText {public static string GetRoleColorAndtext(CustomRoles r)=>r.ToString();public static string GetRoleName(CustomRoles r)=>r.ToString();public static string GetRoleColor(CustomRoles r)=>"color";}
public static class Logger {public static void Error(string m,string c)=>throw new Exception("duplicate compatibility role");}
public static class ObjectOptionitem {public static OptionItem Create(int id,string n,bool b,string s,TabGroup t)=>OptionItem.Raw(id,n);}
}
public interface ISchrodingerCatOwner {public enum TeamType {Crew=0,NeutralA=50,NeutralB=51}}

namespace TownOfHost {static class ActualNeutralKillerHelper {
public static bool IsNeutralKiller(this CustomRoles role)

        {

            return

                role is

                CustomRoles.Egoist or

                CustomRoles.Banker or

                CustomRoles.DoppelGanger or

                CustomRoles.Jackal or

                CustomRoles.JackalMafia or

                CustomRoles.JackalAlien or

                CustomRoles.JackalHadouHo or

                CustomRoles.JackalWolf or

                CustomRoles.Remotekiller or

                CustomRoles.CountKiller or

                CustomRoles.Fool or

                CustomRoles.Altair or

                CustomRoles.Eater or
                CustomRoles.Hunter or
                CustomRoles.Mermaid or
                CustomRoles.Obolus or
                CustomRoles.DarkHide or
                CustomRoles.LessThan or
                CustomRoles.Eliminator;

        }
}
static class ObolusCompatibility {public static SimpleRoleInfo RoleInfo=new(){ConfigId=553000};public static OptionItem OptionCanWinOtherKiller=new();public static Dictionary<CustomRoles,OptionItem> ExtraWinKillTargetOptions=new();enum OptionName{ObolusCanKill}public static void Build()=>SetUpExtraWinKillTargetOptions(17);
private static void SetUpExtraWinKillTargetOptions(int idOffset)
    {
        foreach (var role in CustomRolesHelper.AllStandardRoles.Where(x => x.IsNeutralKiller() && x != CustomRoles.Obolus))
        {
            if (Event.CheckRole(role) is false) continue;

            var id = role == CustomRoles.LessThan ? 40318 : role == CustomRoles.Eliminator ? 993015 : RoleInfo.ConfigId + idOffset;
            var roleName = UtilsRoleText.GetRoleName(role);
            var replacementDic = new Dictionary<string, string>
        {
            { "%role%", Utils.ColorString(UtilsRoleText.GetRoleColor(role), roleName) }
        };

            var item = BooleanOptionItem
                .Create(id, OptionName.ObolusCanKill + "%role%", false, RoleInfo.Tab, false)
                .SetParent(OptionCanWinOtherKiller)
                .SetParentRole(CustomRoles.Obolus);
            item.ReplacementDictionary = replacementDic;

            ExtraWinKillTargetOptions[role] = item;
            if (role is not CustomRoles.LessThan and not CustomRoles.Eliminator) idOffset++;
        }
    }
}}
namespace TownOfHost {public static class Utils{public static string ColorString(string color,string text)=>text;}}