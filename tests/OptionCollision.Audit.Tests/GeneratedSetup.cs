using System;using System.Linq;using System.Collections.Generic;using TownOfHost.Roles.Core;
namespace TownOfHost {public static partial class Options {
private static List<CombinationRoles> Combinations=new();public static Dictionary<CustomRoles,IntegerOptionItem> CustomRoleSpawnChances=new();public static Dictionary<CustomRoles,OptionItem> CustomRoleCounts=new();public static CustomGameMode CurrentGameMode;public static void Reset(){CustomRoleSpawnChances.Clear();CustomRoleCounts.Clear();Combinations.Clear();}
public static void SetupRoleOptions(SimpleRoleInfo info)=>SetupRoleOptions(info.ConfigId,info.Tab,info.RoleName,info.AssignInfo.AssignCountRule,combination:info.Combination);
public static OptionItem SetupRoleOptions(int id, TabGroup tab, CustomRoles role, IntegerValueRule assignCountRule = null, CustomGameMode customGameMode = CustomGameMode.Standard, string fromtext = "", CombinationRoles combination = CombinationRoles.None, int defo = -1)

        {

            if ((role is CustomRoles.Phantom) || (combination != CombinationRoles.None && Combinations.Contains(combination))) return null;

            if (role.IsVanilla())

            {

                switch (role)

                {

                    case CustomRoles.Impostor: id = 10; break;

                    case CustomRoles.Shapeshifter: id = 30; break;

                    case CustomRoles.Phantom: id = 40; break;

                    case CustomRoles.Viper: id = 23050; break;

                    case CustomRoles.Crewmate: id = 11; break;

                    case CustomRoles.Engineer: id = 200; break;

                    case CustomRoles.Scientist: id = 250; break;

                    case CustomRoles.Tracker: id = 300; break;

                    case CustomRoles.Noisemaker: id = 350; break;

                    case CustomRoles.Detective: id = 23100; break;

                    case CustomRoles.Judge: id = 25100; break;
                    case CustomRoles.Influencer: id = 25150; break;

                }

            }

            assignCountRule ??= new(1, 15, 1);

            // Shell attribution wins over upstream/default option text for the selected roles only.
            if (ShellRoleOrigins.HasOverride(role)) fromtext = UtilsOption.GetFrom(From.None, role);

            var from = "<line-height=25%><size=25%>\n</size><size=60%><pos=10%></color> <b>" + fromtext + "</b></size>";



            var tag = CustomOptionTags.Role;

            if (role is CustomRoles.MMArcher) customGameMode = CustomGameMode.MurderMystery;

            switch (customGameMode)

            {

                case CustomGameMode.HideAndSeek: tag = CustomOptionTags.HideAndSeek; break;

                                case CustomGameMode.MurderMystery: tag = CustomOptionTags.MurderMystery; break;
            }

            CustomRoleManager.SortCustomRoles.Add(role);

            if (role.GetCombination() is not CustomRoles.NotAssigned) CustomRoleManager.SortCustomRoles.Add(role.GetCombination());

            var spawnOption = IntegerOptionItem.Create(id, combination == CombinationRoles.None ? role.ToString() : combination.ToString(), new(0, 100, 10), 0, tab, false, from)

                    .SetColorcode(UtilsRoleText.GetRoleColorCode(role))

                    .SetColor(UtilsRoleText.GetRoleColor(role, true))

                    .SetCustomRole(role)

                    .SetValueFormat(OptionFormat.Percent)

                    .SetHeader(true)

                    .SetEnabled(() => role is not CustomRoles.Crewmate and not CustomRoles.Impostor and not CustomRoles.Eater)

                    // ===== イーターのリストラ =====

                    // コード自体は残すが、ホストが選択できないよう設定画面から完全に非表示にする。

                    // (常に0%扱いになるので role.IsEnable() も false のままになり、

                    //  役職プールにも一切出てこなくなる)

                    .SetHidden(role == CustomRoles.NotAssigned || role == CustomRoles.Eater)

                    .SetTag(tag) as IntegerOptionItem;

            var forceHidden = role == CustomRoles.NotAssigned || role == CustomRoles.Eater;

            var hidevalue = forceHidden || role.IsCombinationRole() || role.IsLovers() || (assignCountRule.MaxValue == assignCountRule.MinValue);



            if (role is CustomRoles.Crewmate or CustomRoles.Impostor) return spawnOption;



            var countOption = IntegerOptionItem.Create(id + 1, "Maximum", assignCountRule, defo is -1 ? assignCountRule.Step : defo, tab, false, HideValue: hidevalue)

                .SetParent(spawnOption)

                .SetValueFormat(assignCountRule.MaxValue is 7 ? OptionFormat.Set : OptionFormat.Players)

                .SetEnabled(() => !SlotRoleAssign.IsSeted(role))

                .SetTag(tag)

                .SetHidden(hidevalue)

                .SetParentRole(role)

                .RegisterUpdateValueEvent((object obj, OptionItem.UpdateValueEventArgs args) => spawnOption.Refresh());



            if (combination != CombinationRoles.None) Combinations.Add(combination);

            CustomRoleSpawnChances.Add(role, spawnOption);

            CustomRoleCounts.Add(role, countOption);

            return spawnOption;

        }
public static void CreateRoleOption(IOrderedEnumerable<SimpleRoleInfo> sortedRoleInfo, CustomRoleTypes roleTypes)

            {

                // ===== 修正 =====

                // 以前はタブ番号(OptionSort.TabNumber)を0から連番でチェックし、

                // 該当役職が0件のタブに当たった時点でループを打ち切っていた。

                // そのため、そのタイプの役職群に「欠番のタブ番号」(例:

                // Neutralはタブ0を使う役職が1つも無くタブ1から始まっていた、

                // Madmateはタブ6・7が欠番でタブ8が孤立していた)が存在すると、

                // 欠番以降のタブに属する役職が設定画面に一切表示されなくなる

                // 不具合があった。

                // 実際に使われているタブ番号だけを昇順に処理するよう変更し、

                // 欠番があっても後続のタブが表示されなくなることを防ぐ。

                var roleTypeList = sortedRoleInfo.Where(role => role.CustomRoleType == roleTypes);

                var usedTabNumbers = roleTypeList

                    .Select(role => role.OptionSort.TabNumber)

                    .Distinct()

                    .OrderBy(n => n);

                foreach (var tabNum in usedTabNumbers)

                {

                    var RoleList = roleTypeList.Where(role => role.OptionSort.TabNumber == tabNum);

                    foreach (var info in RoleList.OrderBy(role => role.OptionSort.SortNumber))

                    {

                        if (info.RoleName is CustomRoles.AlienHijack or CustomRoles.NDPlayer) continue;

                        SetupRoleOptions(info);

                        info.OptionCreator?.Invoke();

                        MonkeyBehaviorBanOption.Create(info);

                    }

                }

            }
} }