// Extracted from the integrated production method; refresh after changes.
using UnityEngine;
using TownOfHost.Roles.Core;
namespace TownOfHost;
public static class CheckMurderPatch { public static Dictionary<byte,float> TimeSinceLastKill=new();
public static bool CheckForInvalidMurdering(MurderInfo info, bool force = false)
        {
            (var killer, var target) = info.AttemptTuple;
            // Killerが既に死んでいないかどうか
            if (force == false)
            {
                if (!killer.IsAlive())
                {
                    Logger.Info($"{killer.GetNameWithRole().RemoveHtmlTags()}は死亡しているためキャンセルされました。", "CheckMurder");
                    return false;
                }
                // targetがキル可能な状態か
                {
                    if (// PlayerDataがnullじゃないか確認
                        target.Data == null ||
                        // targetの状態をチェック
                        target.inVent ||
                        target.MyPhysics.Animations.IsPlayingEnterVentAnimation() ||
                        target.MyPhysics.Animations.IsPlayingAnyLadderAnimation() ||
                        target.inMovingPlat)
                    {
                        Logger.Info("targetは現在キルできない状態です。", "CheckMurder");
                        return false;
                    }
                }
            }
            // targetが既に死んでいないか
            if (!target.IsAlive())
            {
                Logger.Info("targetは既に死んでいたため、キルをキャンセルしました。", "CheckMurder");
                return false;
            }
            // 会議中のキルでないか
            if (MeetingHud.Instance != null || (GameStates.CalledMeeting && !force))
            {
                Logger.Info("会議が始まっていたため、キルをキャンセルしました。", "CheckMurder");
                return false;
            }

            if (force == false)
            {
                // 連打キルでないか
                float minTime = Mathf.Max(Main.LagTime, AmongUsClient.Instance.Ping / 1000f * 6f); //※AmongUsClient.Instance.Pingの値はミリ秒(ms)なので÷1000
                                                                                                   //TimeSinceLastKillに値が保存されていない || 保存されている時間がminTime以上 => キルを許可
                                                                                                   //↓許可されない場合
                if (TimeSinceLastKill.TryGetValue(killer.PlayerId, out var time) && time < minTime)
                {
                    Logger.Info($"前回のキルからの時間が早すぎるため、キルをブロックしました。{time} < {minTime}", "CheckMurder");
                    return false;
                }
            }
            TimeSinceLastKill[killer.PlayerId] = 0f;

            // HideAndSeek_キルボタンが使用可能か
            if ((Options.CurrentGameMode == CustomGameMode.HideAndSeek || Options.IsStandardHAS) && Options.HideAndSeekKillDelayTimer > 0)
            {
                Logger.Info("HideAndSeekの待機時間中だったため、キルをキャンセルしました。", "CheckMurder");
                return false;
            }
            if (GameModeManager.IsStandardClass() && Options.firstturnmeeting && MeetingStates.FirstMeeting)
            {
                Logger.Info(killer.GetNameWithRole().RemoveHtmlTags() + "が強制会議前にキルしようとしたから弾いたぞ！", "CheckMurder");
                return false;
            }
            // キルが可能なプレイヤーか(遠隔は除く)
            if (!info.IsFakeSuicide && !killer.CanUseKillButton() && !killer.Is(CustomRoles.UltraStar) && !TownOfHost.Roles.Madmate.Madpsycho.IsReflectionAttempt(info))
            {
                Logger.Info(killer.GetNameWithRole().RemoveHtmlTags() + "はKillできないので、キルはキャンセルされました。", "CheckMurder");
                return false;
            }
            return true;
        }
}
