// Extracted production methods; source hash is checked at runtime.
using System;using System.Collections;using System.Collections.Generic;using System.Linq;
using AmongUs.GameOptions;using TownOfHost.Roles.Core;using TownOfHost.Roles.Core.Interfaces;
using TownOfHost.Roles.Impostor;using TownOfHost.Roles.Neutral;using UnityEngine;
namespace TownOfHost {
internal static class GameEndChecker {
static GameEndPredicate predicate;static bool _isEndProcessing;const float EndGameDelay=.2f;
internal static void SetPredicate(GameEndPredicate p){predicate=p;_isEndProcessing=false;}
static void EnsurePredicate()=>predicate=new NormalGameEndPredicate();
static void SetRoleSummaryText(){Probe.Summaries++;}
public static bool Prefix()

        {

            if (!AmongUsClient.Instance.AmHost) return true;

            if (_isEndProcessing) return false;



            if (predicate == null)

            {

                if (CustomWinnerHolder.WinnerTeam == CustomWinner.Default)

                {

                    Logger.Warn("ゲーム終了判定が未初期化のため、現在のゲームモードに合わせて初期化します", "GameEndChecker");

                    EnsurePredicate();

                }

                return false;

            }



            if ((Main.DontGameSet || (NaturalDisasters.IsThisMode && Options.NoGameEnd.GetBool()))
                && CustomWinnerHolder.WinnerTeam != CustomWinner.Draw) return false;



            if (CustomSpawnEditor.ActiveEditMode) return false;



            //後追い処理等が終わってないなら中断

            if (predicate is NormalGameEndPredicate && 0 < Main.AfterMeetingDeathPlayers.Count)

            {

                if (3 < GameStates.turntimer && GameStates.task)

                {

                    var processedIds = new List<byte>();

                    foreach (var x in Main.AfterMeetingDeathPlayers.ToList())

                    {

                        try

                        {

                            var player = PlayerCatch.GetPlayerById(x.Key);

                            if (player == null)

                            {

                                // 既に切断/破棄されている等でプレイヤーが取得できない場合はスキップして処理済み扱いにする

                                Logger.Warn($"AfterMeetingDeathPlayers: PlayerId={x.Key} が見つからないためスキップします", "GameEndDeath");

                                processedIds.Add(x.Key);

                                continue;

                            }

                            var roleClass = CustomRoleManager.GetByPlayerId(x.Key);

                            var requireResetCam = player.GetCustomRole().GetRoleInfo()?.IsDesyncImpostor == true;

                            var state = PlayerState.GetByPlayerId(x.Key);

                            if (state == null)

                            {

                                Logger.Warn($"AfterMeetingDeathPlayers: PlayerId={x.Key} のPlayerStateが見つからないためスキップします", "GameEndDeath");

                                processedIds.Add(x.Key);

                                continue;

                            }

                            Logger.Info($"{player.GetNameWithRole().RemoveHtmlTags()}を{x.Value}で死亡させました", "GameEndChecker");

                            state.DeathReason = x.Value;

                            player.RpcExileV3();

                            state.SetDead();

                            if (x.Value == CustomDeathReason.Suicide)

                                player.SetRealKiller(player, true);

                            if (requireResetCam)

                                player.ResetPlayerCam(1f);

                            if (roleClass is Executioner executioner && executioner.TargetId == x.Key)

                                Executioner.ChangeRoleByTarget(x.Key);

                            processedIds.Add(x.Key);

                        }

                        catch (Exception ex)

                        {

                            // 1人の処理で例外が起きても他のプレイヤーの処理とリストのクリアを妨げない。

                            // 処理済み扱いにして、このプレイヤーがゲーム終了判定を永久にブロックしないようにする。

                            Logger.Error($"AfterMeetingDeathPlayers: PlayerId={x.Key} の処理中に例外が発生しました。ゲーム終了判定のブロックを避けるため処理済み扱いにします: {ex}", "GameEndDeath");

                            processedIds.Add(x.Key);

                        }

                    }

                    foreach (var id in processedIds)

                        Main.AfterMeetingDeathPlayers.Remove(id);

                }

                else return false;

            }

            //廃村用に初期値を設定

            var reason = GameOverReason.ImpostorsByKill;



            predicate.CheckForEndGame(out reason);

            var isSabotageEnd = reason == GameOverReason.ImpostorsBySabotage;

            var lockSabotageWinner = isSabotageEnd

                && CustomWinnerHolder.WinnerTeam is not CustomWinner.Default and not CustomWinner.None;

            var lockDrawWinner = CustomWinnerHolder.WinnerTeam == CustomWinner.Draw;

            var lockWinner = lockSabotageWinner || lockDrawWinner;

            var team = CustomWinnerHolder.WinnerTeam;

            if (!lockWinner)

            {
                DarkHide.CheckWin(ref reason);

                Zombie.TryTakeOverCrewWin(ref reason, team);

                Onmyoji.TryTakeOverCrewWin(ref reason);

                BatGirl.TryTakeOverSoloWin(ref reason);
            }



            if (!lockWinner) LessThan.TryChooseWinner();

            if (CustomWinnerHolder.WinnerTeam is not CustomWinner.Default)

            {

                Camouflage.RestoreAllForGameEnd();



                if (Options.CurrentGameMode != CustomGameMode.Standard || !SuddenDeathMode.NowSuddenDeathMode)

                    switch (CustomWinnerHolder.WinnerTeam)

                    {

                        case CustomWinner.Crewmate:

                            PlayerCatch.AllPlayerControls

                                .Where(pc => pc.Is(CustomRoleTypes.Crewmate) && !pc.GetCustomRole().IsLovers()

                                && !pc.Is(CustomRoles.Amanojaku) && !pc.Is(CustomRoles.Jackaldoll) && !pc.Is(CustomRoles.SKMadmate) && !pc.Is(CustomRoles.Tama)

                                && MagicalGirl.CanWinAsCrewmateStaff(pc))

                                .Do(pc => CustomWinnerHolder.WinnerIds.Add(pc.PlayerId));

                            if (Monochromer.CheckWin(reason)) break;

                            foreach (var pc in PlayerCatch.AllPlayerControls)

                            {

                                if (pc.GetCustomRole() is CustomRoles.SKMadmate or CustomRoles.Jackaldoll ||

                                    pc.IsLovers())

                                    CustomWinnerHolder.CantWinPlayerIds.Add(pc.PlayerId);

                            }

                            break;

                        case CustomWinner.Impostor:

                            PlayerCatch.AllPlayerControls

                                .Where(pc => (pc.Is(CustomRoleTypes.Impostor) || pc.Is(CustomRoleTypes.Madmate) || pc.Is(CustomRoles.SKMadmate)) && (!pc.GetCustomRole().IsLovers() || !pc.Is(CustomRoles.Jackaldoll) || !pc.Is(CustomRoles.Tama)))

                                .Do(pc => CustomWinnerHolder.WinnerIds.Add(pc.PlayerId));

                            if (!lockWinner && Egoist.CheckWin()) break;

                            if (!lockWinner)

                            {

                                foreach (var pc in PlayerCatch.AllPlayerControls)

                                {

                                    if (pc.GetCustomRole() is CustomRoles.Jackaldoll ||

                                        pc.IsLovers())

                                        CustomWinnerHolder.CantWinPlayerIds.Add(pc.PlayerId);

                                }

                            }

                            break;

                        default:

                            //ラバー勝利以外の時にラバーをしめt...勝利を剥奪する処理。

                            //どーせ追加なら追加勝利するやろし乗っ取りなら乗っ取りやし。

                            if (lockWinner || CustomWinnerHolder.WinnerTeam.IsLovers())

                                break;

                            PlayerCatch.AllPlayerControls

                                .Where(p => p.IsLovers())

                                .Do(p => CustomWinnerHolder.CantWinPlayerIds.Add(p.PlayerId));

                            break;

                    }



                // 勝者固定経路でも、乗っ取りではない個人勝利条件は必ず適用する。

                foreach (var beginner in CustomRoleManager.AllActiveRoles.Values.OfType<BeginnerImpostor>())

                    beginner.EnforceDummyKillWinRequirement();



                // 勝者固定経路でも、加虐者・被虐者の個別条件を必ず反映する。

                foreach (var abuser in CustomRoleManager.AllActiveRoles.Values.OfType<Abuser>())

                    abuser.EnforceWinRequirement();

                foreach (var victim in CustomRoleManager.AllActiveRoles.Values.OfType<Victim>())

                    victim.EnforceFactionWin();





                if (!lockWinner && SuddenDeathMode.NowSuddenDeathTemeMode && !(CustomWinnerHolder.WinnerTeam is CustomWinner.SuddenDeathRed or CustomWinner.SuddenDeathBlue or CustomWinner.SuddenDeathGreen or CustomWinner.SuddenDeathYellow or CustomWinner.PurpleLovers))

                {

                    SuddenDeathMode.TeamAllWin();

                }

                if (!lockWinner && CustomWinnerHolder.WinnerTeam is not CustomWinner.Draw and not CustomWinner.None)

                {

                    // タスク勝利でもラバーズの単独勝利・追加勝利を適用する。
                    // タスク勝利時だけラバーズをCantWinへ入れていたため、
                    // 全タスク完了時にラバーズが勝者になれなかった。
                    Lovers.LoversSoloWin(ref reason);
                    Lovers.LoversAddWin();



                    foreach (var pc in PlayerCatch.AllPlayerControls.Where(pc => !CustomWinnerHolder.WinnerIds.Contains(pc.PlayerId) || pc.GetCustomRole() is CustomRoles.Turncoat or CustomRoles.AllArounder))

                    {

                        if (!pc.IsLovers() && !pc.Is(CustomRoles.Amanojaku))

                        {

                            if (pc.GetRoleClass() is IAdditionalWinner additionalWinner)

                            {

                                var winnerRole = pc.GetCustomRole();

                                if (additionalWinner.CheckWin(ref winnerRole))

                                {

                                    // ResetAndSetAndChWinner内部で優先度に応じて「単独勝利」(Reset実行)か

                                    // 「追加勝利」かが決まる。CheckWin呼び出し後の状態を見て、

                                    // この役職単独の勝利(単独勝利)が成立している場合は、

                                    // 追加勝利としての二重登録をスキップする。

                                    // (以前は無条件にAdditionalWinnerRoles.Add(winnerRole)していたため、

                                    //  単独勝利のはずが追加勝利として扱われてしまう不具合があった。

                                    //  例: Sleeper/Nullを単独勝利設定にしても追加勝利表示になる)

                                    bool isSoloWin = CustomWinnerHolder.WinnerTeam == (CustomWinner)winnerRole

                                        && CustomWinnerHolder.WinnerIds.Count == 1

                                        && CustomWinnerHolder.WinnerIds.Contains(pc.PlayerId)

                                        && CustomWinnerHolder.AdditionalWinnerRoles.Count == 0;

                                    if (isSoloWin)

                                    {

                                        Logger.Info($"{pc.Data.GetLogPlayerName()}:{winnerRole}での単独勝利", "AdditinalWinner");

                                        continue;

                                    }

                                    Logger.Info($"{pc.Data.GetLogPlayerName()}:{winnerRole}での追加勝利", "AdditinalWinner");

                                    CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);

                                    CustomWinnerHolder.AdditionalWinnerRoles.Add(winnerRole);

                                    continue;

                                }

                            }

                        }

                        LastNeutral.CheckAddWin(pc, reason);

                        Amanojaku.CheckWin(pc, reason);

                    }

                }

                if (!lockWinner && CustomWinnerHolder.WinnerTeam is not CustomWinner.Draw)

                {

                    CurseMaker.CheckWin();
                    DarkHide.CheckWin(ref reason);

                    Fox.SFoxCheckWin(ref reason);

                    Tuna.CheckWin(ref reason);
                    TownOfHost.Roles.Neutral.Flatfish.CheckWin(ref reason);
                    TownOfHost.Roles.Neutral.Parity.CheckWin();
                    TownOfHost.Roles.Neutral.Lupin.CheckWin(ref reason);

                    TownOfHost.Roles.Neutral.DarkSheriff.CheckWin(ref reason);

                    TownOfHost.Roles.Neutral.RaccoonParent.CheckWin(ref reason);

                    Spelunker.CheckWin(ref reason);

                    Chatter.CheckWin(ref reason);
                    TownOfHost.Roles.Neutral.Tiger.TryChooseWinner();

                    Zombie.TryTakeOverCrewWin(ref reason, team);



                    // ★ 神の勝利チェック

                    foreach (var pc in PlayerCatch.AllAlivePlayerControls.Where(p => p.Is(CustomRoles.God)))

                    {

                        if (TownOfHost.Roles.Neutral.God.RequireTasksToWinOpt?.GetBool() == true)

                        {

                            var taskState = PlayerState.GetByPlayerId(pc.PlayerId)?.GetTaskState();

                            if (taskState == null) continue;

                            if (!UtilsTask.HasTasks(pc.Data, false)) continue;



                            var required = TownOfHost.Roles.Neutral.God.TaskCountOpt?.GetInt() ?? 0;

                            if (required > 0)

                            {

                                if (taskState.CompletedTasksCount < required) continue;

                            }

                            else

                            {

                                if (taskState.AllTasksCount <= 0) continue;

                                if (!taskState.IsTaskFinished) continue;

                            }

                        }



                        if (CustomWinnerHolder.ResetAndSetAndChWinner(CustomWinner.God, byte.MaxValue))

                        {

                            CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);

                            CustomWinnerHolder.CantWinPlayerIds.Remove(pc.PlayerId);

                            reason = GameOverReason.ImpostorsByKill;

                        }

                    }

                }

                if (!lockWinner)

                {

                    AsistingAngel.CheckAddWin();

                    foreach (var phantomthiefplayer in PlayerCatch.AllAlivePlayerControls.Where(pc => pc.GetCustomRole() is CustomRoles.PhantomThief))

                    {

                        if (phantomthiefplayer.GetRoleClass() is PhantomThief phantomThief)

                        {

                            phantomThief.CheckWin();

                        }

                    }

                    foreach (var player in PlayerCatch.AllPlayerControls)

                    {

                        var roleclass = player.GetRoleClass();

                        roleclass?.CheckWinner(reason);

                    }

                    Twins.CheckAddWin();

                    Triplets.CheckAddWin();

                    Faction.CheckWin();

                }



                if (!lockWinner) LessThan.TryChooseWinner();

                if (lockDrawWinner)

                {

                    CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Draw);

                }



                Ruler.ApplyGameEndRules();
                TownOfHost.Roles.Neutral.Streamer.ApplyEndWinners();
                TownOfHost.Roles.Neutral.Prankster.ApplyAdditionalWinners();
                if (!lockDrawWinner) TownOfHost.Roles.AddOns.Common.UserAddons2026.CheckWin(reason);
                LessThan.FinalizeSoloWinner();



                if (isSabotageEnd && Options.OptionSabotageFinAllKill.GetBool())

                {

                    PlayerCatch.AllAlivePlayerControls

                        .Where(player => !player.IsWinner())

                        .Do(player =>

                        {

                            player.GetPlayerState().DeathReason = CustomDeathReason.Kill;

                            player.RpcMurderPlayer(player);

                        });

                }



                // This role follows the final faction, after all takeovers and end-of-game deaths.
                Eliminator.ApplyAdditionalWinners();

                ShipStatus.Instance.enabled = false;

                if (CustomWinnerHolder.WinnerTeam != CustomWinner.Crewmate && (reason.Equals(GameOverReason.CrewmatesByTask) || reason.Equals(GameOverReason.CrewmatesByVote)))

                    reason = GameOverReason.ImpostorsByKill;



                Logger.Info($"{CustomWinnerHolder.WinnerTeam} ({reason})", "Winner");

                CustomWinnerHolder.winners.Do(winner => Logger.Info($"winnerteam:{winner} ({reason})", "Winner"));



                if (Options.OutroCrewWinreasonchenge.GetBool() && (reason.Equals(GameOverReason.CrewmatesByTask) || reason.Equals(GameOverReason.CrewmatesByVote)))

                    reason = GameOverReason.ImpostorsByVote;



                StartEndGame(reason);

                predicate = null;

            }

            return false;

        }
public static void StartEndGame(GameOverReason reason)

        {

            AmongUsClient.Instance.StartCoroutine(CoEndGame(AmongUsClient.Instance, reason).WrapToIl2Cpp());

        }

        private static IEnumerator CoEndGame(AmongUsClient self, GameOverReason reason)

        {

            NaturalDisasters.FinishSurvival();
            LessThan.EndRound();
            Eliminator.EndRound();
            GameStates.IsOutro = true;



            List<byte> ReviveRequiredPlayerIds = new();

            var winner = CustomWinnerHolder.WinnerTeam;

            foreach (var pc in PlayerCatch.AllPlayerControls)

            {

                if (pc.Data?.Role?.Role == RoleTypes.SpiritGuide &&
                    PlayerState.GetByPlayerId(pc.PlayerId) is { } state)
                    state.WasInfluencer = true;
                if (winner == CustomWinner.Draw)

                {

                    SetGhostRole(ToGhostImpostor: true);

                    continue;

                }

                bool canWin = CustomWinnerHolder.WinnerIds.Contains(pc.PlayerId) ||

                        CustomWinnerHolder.WinnerRoles.Contains(pc.GetCustomRole());

                canWin &= !CustomWinnerHolder.CantWinPlayerIds.Contains(pc.PlayerId);

                bool isCrewmateWin = reason.Equals(GameOverReason.CrewmatesByVote) || reason.Equals(GameOverReason.CrewmatesByTask);

                SetGhostRole(ToGhostImpostor: canWin ^ isCrewmateWin);



                void SetGhostRole(bool ToGhostImpostor)

                {

                    var isDead = pc.Data.IsDead;

                    if (!isDead) ReviveRequiredPlayerIds.Add(pc.PlayerId);

                    if (ToGhostImpostor)

                    {

                        Logger.Info($"{pc.GetNameWithRole().RemoveHtmlTags()}: ImpostorGhostに変更", "ResetRoleAndEndGame");

                        pc.RpcSetRole(RoleTypes.ImpostorGhost, false);

                    }

                    else

                    {

                        Logger.Info($"{pc.GetNameWithRole().RemoveHtmlTags()}: CrewmateGhostに変更", "ResetRoleAndEndGame");

                        pc.RpcSetRole(RoleTypes.CrewmateGhost, false);

                    }

                    pc.Data.IsDead = isDead;

                }

            }



            if (PlayerCatch.AnyModClient())

            {

                var winnerWriter = self.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.EndGame, Hazel.SendOption.Reliable);

                CustomWinnerHolder.WriteTo(winnerWriter);

                self.FinishRpcImmediately(winnerWriter);

            }



            yield return new WaitForSeconds(EndGameDelay);



            if (ReviveRequiredPlayerIds.Count > 0)

            {

                for (int i = 0; i < ReviveRequiredPlayerIds.Count; i++)

                {

                    var playerId = ReviveRequiredPlayerIds[i];

                    var playerInfo = GameData.Instance.GetPlayerById(playerId);

                    playerInfo.IsDead = false;

                }

                GameDataSerializePatch.SerializeMessageCount++;

                RPC.RpcSyncAllNetworkedPlayer();

                GameDataSerializePatch.SerializeMessageCount--;

                yield return new WaitForSeconds(EndGameDelay);

            }

            yield return new WaitForSeconds(EndGameDelay);

            // ゲーム終了直後の一斉役職変更(ImpostorGhost/CrewmateGhost)RPCの影響で、

            // 直後はGetClientId()がまだ解決できない(暗転の原因になる)ことがあったため、

            // SetRoleSummaryText呼び出し前にもう少し余裕を持たせる。

            yield return new WaitForSeconds(EndGameDelay);

            try

            {

                SetRoleSummaryText();

            }

            catch (Exception ex)

            {

                Logger.Exception(ex, "SetRoleSummaryText");

                Logger.seeingame("非クライアントへのアウトロテキスト生成中にエラーが発生しました。");

            }

            yield return new WaitForSeconds(EndGameDelay);



            GameManager.Instance.RpcEndGame(reason, false);

            CheckGetNomalAchievement.CallEndGame(reason);

        }
}
class NormalGameEndPredicate:GameEndPredicate {
public override bool CheckForEndGame(out GameOverReason reason){
reason=GameOverReason.ImpostorsByKill;if(CustomWinnerHolder.WinnerTeam!=CustomWinner.Default)return false;
int Imp=PlayerCatch.AllAlivePlayerControls.Count(p=>p.Team==CustomRoleTypes.Impostor),FoxAndCrew=PlayerCatch.AllAlivePlayerControls.Count(p=>p.Team==CustomRoleTypes.Crewmate);
int Jackal=0,Remotekiller=0,MilkyWay=0,MadBetrayer=0,Pavlov=0,StandMasterCount=0,EaterCount=0,DaoshiAlive=0,LessThanAlive=0;
if(Imp+FoxAndCrew==0)return false;
if (Jackal == 0 && Remotekiller == 0 && MilkyWay == 0 && MadBetrayer == 0

                && Pavlov == 0 && StandMasterCount == 0 && EaterCount == 0 && DaoshiAlive == 0 && LessThanAlive == 0 && FoxAndCrew <= Imp) {CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Impostor);return true;}return false;
}}
}
