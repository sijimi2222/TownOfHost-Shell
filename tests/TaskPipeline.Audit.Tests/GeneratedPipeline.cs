using System;using System.Linq;using TownOfHost.Roles.Core;using TownOfHost.Roles.AddOns.Common;using AmongUs.GameOptions;
namespace TownOfHost{public static class UtilsTask{public static bool HasTasks(NetworkedPlayerInfo p, bool ForRecompute = true)
        {
            try
            {
                if (GameStates.IsLobby) return false;
                //Tasksがnullの場合があるのでその場合タスク無しとする
                if (p?.Tasks == null
                || p?.Role == null
                || (p?.Disconnected ?? true)) return false;

                var hasTasks = true;
                var States = PlayerState.GetByPlayerId(p.PlayerId);
                if (States is null) return false;
                if (p.Role.IsImpostor)
                    hasTasks = false; //タスクはCustomRoleを元に判定する
                if (Options.CurrentGameMode == CustomGameMode.HideAndSeek)
                {
                    if (p.IsDead) hasTasks = false;
                    if (States.MainRole is CustomRoles.HASFox or CustomRoles.HASTroll) hasTasks = false;
                }
                else
                {
                    // 死んでいて，死人のタスク免除が有効なら確定でfalse
                    if (p.IsDead && Options.GhostIgnoreTasks.GetBool() && Main.DisableTaskPlayerList.Contains(p.PlayerId))
                    {
                        return false;
                    }
                    var role = States.MainRole;
                    var roleClass = CustomRoleManager.GetByPlayerId(p.PlayerId);
                    if (roleClass != null)
                    {
                        switch (roleClass.HasTasks)
                        {
                            case HasTask.True:
                                hasTasks = true;
                                break;
                            case HasTask.False:
                                hasTasks = false;
                                break;
                            case HasTask.ForRecompute:
                                hasTasks = !ForRecompute;
                                break;
                        }
                    }
                    switch (role)
                    {
                        case CustomRoles.GM:
                        case CustomRoles.SKMadmate:
                        case CustomRoles.Jackaldoll:
                            hasTasks = false;
                            break;
                        case CustomRoles.Experiencer:
                            hasTasks = !ForRecompute;
                            break;
                        case CustomRoles.Clockworker:
                            hasTasks = roleClass is TownOfHost.Roles.Impostor.Clockworker cw && cw.InTaskMode && !ForRecompute;
                            break;
                        default:
                            if (role.IsImpostor()) hasTasks = false;
                            break;
                    }

                    foreach (var subRole in States.SubRoles)
                        switch (subRole)
                        {
                            case CustomRoles.Amanojaku:
                            case CustomRoles.OneLove:
                            case CustomRoles.Murder:
                                hasTasks &= !ForRecompute; break;
                            case CustomRoles.Amnesia:
                                {
                                    var ch = false;
                                    switch (role.GetRoleInfo()?.BaseRoleType.Invoke())
                                    {
                                        case RoleTypes.Crewmate:
                                        case RoleTypes.Engineer:
                                        case RoleTypes.Scientist:
                                        case RoleTypes.Noisemaker:
                                        case RoleTypes.Tracker:
                                        case RoleTypes.Detective:
                                        case RoleTypes.Judge:
                                            ch = true;
                                            break;
                                    }
                                    hasTasks = role.IsCrewmate() && ch ? hasTasks : (!ForRecompute && !States.MainRole.IsImpostor());
                                }
                                break;
                        }
                    if (States.GhostRole is CustomRoles.AsistingAngel) hasTasks = false;
                    if (States.GhostRole is CustomRoles.Kyonshi) hasTasks &= !ForRecompute;

                    //ラバーズはタスクを勝利用にカウントしない
                    //回線落ちになってもタスクは復活しない
                    if (Lovers.HaveLoverDontTaskPlayers.Contains(p.PlayerId))
                        hasTasks &= !ForRecompute;
                }
                return hasTasks;
            }
            catch (Exception ex)
            {
                Logger.Error(ex.ToString(), "HasTask");
                return false;
            }
        }}public static class TaskCompleteAdapter{public static bool Prefix(PlayerControl __instance, uint taskid)
        {
            var pc = __instance;

            Logger.Info($"TaskComplete:{pc.GetNameWithRole().RemoveHtmlTags()} {taskid}", "CompleteTask");
            var taskState = pc.GetPlayerTaskState();
            taskState.Update(pc);

            var roleClass = pc.GetRoleClass();
            var roleinfo = pc.GetCustomRole().GetRoleInfo();
            var ret = true;

            //タスクバトルの処理　タスバトならこれ以外の処理いらない。
            if (Options.CurrentGameMode == CustomGameMode.TaskBattle)
            {
                ret &= TaskBattle.TaskBattleCompleteTask(pc, taskState);
                return ret;
            }
            if (Options.CurrentGameMode == CustomGameMode.MurderMystery)
            {
                ret &= true;
                MurderMystery.OnCompleteTask(pc);
                UtilsNotifyRoles.NotifyRoles();
                UtilsGameLog.AddGameLog("Task", string.Format(Translator.GetString("Taskfin"), UtilsName.GetPlayerColor(pc, true)));
                return ret;
            }

            if (roleClass != null)
            {
                if (Amnesia.CheckAbility(pc))
                    ret = roleClass.OnCompleteTask(taskid);
            }

            CustomRoleManager.OnTaskCompleteOthers(__instance, ret);

            if (pc.Is(CustomRoles.Amnesia))
                if (Amnesia.OptionCanRealizeTask.GetBool() && taskState.HasCompletedEnoughCountOfTasks(Amnesia.OptionRealizeTaskCount.GetInt()))
                {
                    if (!Utils.RoleSendList.Contains(pc.PlayerId)) Utils.RoleSendList.Add(pc.PlayerId);
                    Amnesia.RemoveAmnesia(pc.PlayerId);

                    taskState.hasTasks = UtilsTask.HasTasks(pc.Data, false);

                    if (AmongUsClient.Instance.AmHost)
                    {
                        if (pc.PlayerId != PlayerControl.LocalPlayer.PlayerId)
                            pc.RpcSetRoleDesync(roleinfo.BaseRoleType.Invoke(), pc.GetClientId());
                        else
                            if (pc.PlayerId == PlayerControl.LocalPlayer.PlayerId)
                                if (roleinfo?.IsDesyncImpostor == true && roleinfo?.BaseRoleType.Invoke() is not null and not RoleTypes.Impostor)
                                    RoleManager.Instance.SetRole(PlayerControl.LocalPlayer, roleinfo.BaseRoleType.Invoke());

                        pc.SyncSettings();
                        _ = new LateTask(() =>
                        {
                            pc.SetKillCooldown(Main.AllPlayerKillCooldown[pc.PlayerId], force: true, delay: true);
                            pc.RpcResetAbilityCooldown(Sync: true);
                        }, 0.2f, "ResetAbility");
                    }
                }

            //属性クラスの扱いを決定するまで仮置き
            ret &= Workhorse.OnCompleteTask(pc);
            UtilsNotifyRoles.NotifyRoles();

            if (taskState.IsTaskFinished && pc.PlayerId == PlayerControl.LocalPlayer.PlayerId)
            {
                RemoveDisableDevicesPatch.UpdateDisableDevices();
            }
            if (ret)
            {
                if (taskState.CompletedTasksCount < taskState.AllTasksCount) return ret;
                if (!UtilsTask.HasTasks(pc.Data)) return ret;
                UtilsGameLog.AddGameLog("Task", string.Format(Translator.GetString("Taskfin"), UtilsName.GetPlayerColor(pc, true)));
            }
            return ret;
        }}public static class ConsoleAdapter{public static bool Prefix(ref float __result, FakeConsole __instance, NetworkedPlayerInfo pc, out bool canUse, out bool couldUse)
        {
            if (GameStates.IsFreePlay)
            {
                canUse = couldUse = false;
                return true;
            }
            canUse = couldUse = false;
            var role = PlayerControl.LocalPlayer.GetCustomRole();
            var hastask = UtilsTask.HasTasks(PlayerControl.LocalPlayer.Data, false);
            var isMotogaCrew = PlayerControl.LocalPlayer.IsAlive() && !hastask && PlayerControl.LocalPlayer.Data.RoleType.IsCrewmate() && !PlayerControl.LocalPlayer.CanUseKillButton() && !role.IsImpostor();
            var Rolecanuse = isMotogaCrew || (hastask && (PlayerControl.LocalPlayer.GetRoleClass()?.CanTask() ?? true));
            var isAmn = PlayerControl.LocalPlayer.Is(CustomRoles.Amnesia) && !PlayerControl.LocalPlayer.Is(CustomRoleTypes.Impostor);

            //こいつをfalseでreturnしても、タスク(サボ含む)以外の使用可能な物は使えるまま(ボタンなど)
            if (!GameStates.InGame)
                return __instance.AllowImpostor || hastask;
            else
                return __instance.AllowImpostor || Rolecanuse || isAmn;
        }}}