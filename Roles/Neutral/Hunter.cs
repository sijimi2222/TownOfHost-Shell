using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Modules;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using TownOfHost.Roles.Crewmate;
using TownOfHost.Roles.Impostor;
using UnityEngine;

namespace TownOfHost.Roles.Neutral
    {
        public sealed class Hunter : RoleBase, ILNKiller
        {
        bool IKiller.CanKill => false;
        // The chase is an attack, but does not grant the ordinary kill button.
        bool IKiller.IsKiller => true;
        int generation;
        bool disposed;
        bool Current => !disposed && Player != null && ReferenceEquals(Player.GetRoleClass(), this);
        public static readonly SimpleRoleInfo RoleInfo =
                SimpleRoleInfo.Create(
                    typeof(Hunter),
                    player => new Hunter(player),
                    CustomRoles.Hunter,
                    () => RoleTypes.Shapeshifter,
                    CustomRoleTypes.Neutral,
                    56400,
                    SetupOptionItem,
                    "hun",
                    "#cd853f",
                    (1, 5),
                    true,
                    countType: CountTypes.Hunter,
                    assignInfo: new RoleAssignInfo(CustomRoles.Hunter, CustomRoleTypes.Neutral)
                    {
                        AssignCountRule = new(1, 15, 1)
                    }
                );
        public Hunter(PlayerControl player)
        : base(
            RoleInfo,
            player,
            () => HasTask.False
        )
        {
            darkenedPlayers = null;
            CanVent = OptionCanVent.GetBool();
            Cooldown = OptionCooldown.GetFloat();
            targeted = false;
            targetId = 255;
            PublicRoleColor = false;
            changetimer = OptionchangeColorTime.GetFloat();
            BoostTime = OptionSpeedBoostTime.GetFloat();
            savedSpeeds.Clear();
            SpeedBoost = OptionSpeedBoost.GetFloat();
            DarkenTime = darkenTimer = optionDarkenDuration.GetFloat();
        }

        private float darkenTimer;

        readonly float DarkenTime;
        readonly float BoostTime;
        readonly float SpeedBoost;
        public static OptionItem OptionSpeedBoost;
        public static OptionItem OptionKillCooldown;
        private static OptionItem OptionCooldown;
        public static OptionItem OptionCanVent;
        static OptionItem OptionHasImpostorVision;
        readonly float Cooldown;
        readonly bool CanVent;

        bool targeted;
        public bool PublicRoleColor;

        PlayerControl KillWaitPlayer;
        public static OptionItem OptionchangeColorTime;
        public float changetimer;
        public static OptionItem OptionSpeedBoostTime;
        public static FloatOptionItem optionDarkenDuration;
        public static OptionItem OptionDarkenRange;

        private PlayerControl[] darkenedPlayers;
        readonly object blackoutOwner = new();
        static readonly HunterVisionLeases blackouts = new();
        internal static bool IsDarkened(byte playerId) => blackouts.Contains(playerId);
        [Attributes.GameModuleInitializer]
        public static void ResetForGame() => blackouts.Clear();

        enum OptionName
        {
            HunterchangeColorTime,
            HunterSpeedBoostTime,
            HunterSpeedBoost,
            StealthDarkenDuration,
            HunterDarkenRange
        }

        private static void SetupOptionItem()
        {
            SoloWinOption.Create(RoleInfo, 8, defo: 0);
            OptionCooldown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.Cooldown, new(0f, 180f, 0.5f), 40f, false).SetValueFormat(OptionFormat.Seconds);
            OptionCanVent = BooleanOptionItem.Create(RoleInfo, 11, GeneralOption.CanVent, true, false);
            OptionHasImpostorVision = BooleanOptionItem.Create(RoleInfo, 12, GeneralOption.ImpostorVision, true, false);
            OptionchangeColorTime = FloatOptionItem.Create(RoleInfo, 13, OptionName.HunterchangeColorTime,
                new(0f, 180f, 0.5f), 7.5f, false).SetValueFormat(OptionFormat.Seconds);
            OptionSpeedBoostTime = FloatOptionItem.Create(RoleInfo, 14, OptionName.HunterSpeedBoostTime,
                new(0f, 180f, 0.5f), 7.5f, false).SetValueFormat(OptionFormat.Seconds);
            OptionSpeedBoost = FloatOptionItem.Create(RoleInfo, 15, OptionName.HunterSpeedBoost,
                new(1f, 10f, 0.25f), 1.75f, false).SetValueFormat(OptionFormat.Multiplier);
            optionDarkenDuration = FloatOptionItem.Create(RoleInfo, 16, OptionName.StealthDarkenDuration, new(0.5f, 30f, 0.5f), 1f, false);
            optionDarkenDuration.SetValueFormat(OptionFormat.Seconds);
            OptionDarkenRange = FloatOptionItem.Create(RoleInfo, 17, OptionName.HunterDarkenRange, new(0f, 10f, 0.5f), 2.5f, false)
                .SetValueFormat(OptionFormat.Multiplier);
            RoleAddAddons.Create(RoleInfo, 20, NeutralKiller: true);
        }

        public float CalculateKillCooldown() => 0f;
        public bool CanUseSabotageButton() => false;
        public bool CanUseImpostorVentButton() => CanVent;

        readonly Dictionary<byte, float> savedSpeeds = new();

        private Vector2 HunterPos;
        private Vector2 targetPos;

        byte targetId;


        bool targetDied;
        public override void ApplyGameOptions(IGameOptions opt)
        {
            opt.SetVision(OptionHasImpostorVision.GetBool());
            AURoleOptions.ShapeshifterCooldown = Cooldown;
        }
        public override void CheckWinner(GameOverReason reason)
        {
            if (3 <= MyState.GetKillCount()) Achievements.RpcCompleteAchievement(Player.PlayerId, 0, achievements[0]);
            if (Player.IsWinner(CustomWinner.Hunter) && !Player.IsLovers())
                Achievements.RpcCompleteAchievement(Player.PlayerId, 0, achievements[2]);
        }

        public override void OnFixedUpdate(PlayerControl player)
        {
            if (AmongUsClient.Instance?.AmHost != true) return;
            if (!Current || !Player.IsAlive() || !GameStates.IsInTask || GameStates.IsMeeting || GameStates.CalledMeeting)
            {
                if (targeted || PublicRoleColor || savedSpeeds.Count > 0 || darkenedPlayers != null) ClearEffects();
                return;
            }
            if (AmongUsClient.Instance.AmHost)
            {
                if (darkenedPlayers != null)
                {
                    // タイマーを減らす
                    darkenTimer -= Time.fixedDeltaTime;
                    // タイマーが0になったらみんなの視界を戻してタイマーと暗転プレイヤーをリセットする
                    if (darkenTimer <= 0)
                    {
                        ResetDarkenState();
                    }
                }
            }
            if (!targeted || KillWaitPlayer == null || !KillWaitPlayer.IsAlive() || KillWaitPlayer.Data.Disconnected)
            {
                targeted = false;
                return;
            }
            // TOHYありがとう!!!!!!!!
            if (targeted && KillWaitPlayer)
            {
                HunterPos = Player.transform.position;
                targetPos = KillWaitPlayer.GetTruePosition();
            }
            Vector2 difference = targetPos - HunterPos;
            // 距離の2乗を取得
            float sqrDistance = difference.sqrMagnitude;
            float checkRadius = 5f;
            var target = KillWaitPlayer;
            if (target.PlayerId == Player.PlayerId || !Player.IsAlive())
            {
                targeted = false;
                KillWaitPlayer = null;
                return;
            }
            if (sqrDistance <= checkRadius * checkRadius)
            {   
                if (!AmongUsClient.Instance.AmHost) return;             
                if (!CustomRoleManager.OnCheckMurder(Player, target, target, target, true, false, 2, CustomDeathReason.Kill))
                {
                    targeted = false;
                    KillWaitPlayer = null;
                    if (Player.IsAlive()) Player.RpcResetAbilityCooldown();
                    return;
                }
                {
                    Player.RpcSnapToForced(target.transform.position);
                    RPC.PlaySoundRPC(Player.PlayerId, Sounds.KillSound);
                    UtilsNotifyRoles.NotifyRoles(SpecifySeer: Player);
                }
                KillWaitPlayer = null;
                target = null;
                Player.MarkDirtySettings();
                PublicRoleColor = true;
                ApplySpeedEffect(Player, SpeedBoost);

                var DarkenRange = OptionDarkenRange.GetFloat();
                var playersToDarken = PlayerCatch.AllAlivePlayerControls
                    .Where(targetdark => !targetdark.Is(CustomRoles.Hunter))
                    .Where(targetdark => targetdark.PlayerId != Player.PlayerId)
                    .Where(targetdark => Ballooner.IsInExplosionRange(Player, targetdark, DarkenRange))
                    .ToArray();

                if (playersToDarken.Length > 0)
                {
                    DarkenPlayers(playersToDarken);
                }
                int captured = generation;
                _ = new LateTask(() =>
                {
                    if (!Current || captured != generation || !Player.IsAlive() || !GameStates.IsInTask || GameStates.IsMeeting || GameStates.CalledMeeting) return;
                    targeted = false;
                    PublicRoleColor = false;
                    Player.RpcResetAbilityCooldown();
                }, changetimer, "", true);
            }
        }

        private void ResetDarkenState()
        {
            if (darkenedPlayers != null)
            {
                foreach (byte id in blackouts.Release(blackoutOwner))
                    PlayerCatch.GetPlayerById(id)?.MarkDirtySettings();
                darkenedPlayers = null;
            }
            darkenTimer = DarkenTime;
            UtilsNotifyRoles.NotifyRoles(SpecifySeer: Player);
        }

        private void DarkenPlayers(IEnumerable<PlayerControl> playersToDarken)
        {
            darkenedPlayers = playersToDarken.ToArray();
            foreach (byte id in blackouts.Replace(blackoutOwner, darkenedPlayers.Select(p => p.PlayerId)))
                PlayerCatch.GetPlayerById(id)?.MarkDirtySettings();
            if (0 < playersToDarken.Count()) Achievements.RpcCompleteAchievement(Player.PlayerId, 0, achievements[0]);
        }

        void ApplySpeedEffect(PlayerControl Player, float multiplier)
        {
            byte id = Player.PlayerId;
            if (!savedSpeeds.ContainsKey(id))
                savedSpeeds[id] = Main.AllPlayerSpeed.TryGetValue(id, out float s) ? s : 1f;
            Main.AllPlayerSpeed[id] = savedSpeeds[id] * multiplier;
            Player.MarkDirtySettings();
            int captured = generation;
            _ = new LateTask(() =>
            {
                if (!Current || captured != generation) return;
                RemoveEffect(Player.PlayerId);
            }, BoostTime, "", true);
        }

        void RemoveEffect(byte playerId)
        {
            if (!savedSpeeds.TryGetValue(playerId, out float orig)) return;
            Main.AllPlayerSpeed[playerId] = orig;
            PlayerCatch.GetPlayerById(playerId)?.MarkDirtySettings();
            savedSpeeds.Remove(playerId);
        }
        public static bool KnowTargetRoleColor(PlayerControl target, bool isMeeting)
            => !isMeeting && target?.GetRoleClass() is Hunter hunter && hunter.PublicRoleColor;

        public override bool CheckShapeshift(PlayerControl target, ref bool animate)
        {
            animate = false;
            if (AmongUsClient.Instance?.AmHost != true || !Current || !Player.IsAlive() || target == null || target.PlayerId == Player.PlayerId
                || !GameStates.IsInTask || GameStates.IsMeeting || GameStates.CalledMeeting) return false;
            if (!targeted)
            {
                if (target.IsAlive())
                {
                    KillWaitPlayer = target;
                    targeted = true;
                    targetId = target.PlayerId;
                    targetDied = false;

                }
                else
                {
                    targetDied = true;
                    int captured = generation;
                    _ = new LateTask(() =>
                    {
                        if (!Current || captured != generation) return;
                        targetDied = false;
                    }, 5f, "", true);
                }
            }
            return false;
        }


        public override string GetLowerText(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
        {
            seen ??= seer;
            if (targetDied)
            {
                var mes = $"<color={RoleInfo.RoleColorCode}>ターゲットが死亡しています。ターゲットを選びなおしてください</color>";
                return isForHud ? mes : $"<size=40%>{mes}</size>";

            }
            if (!targeted)
            {
                var mes = $"<color={RoleInfo.RoleColorCode}>シェイプシフトでターゲット指定</color>";
                return isForHud ? mes : $"<size=40%>{mes}</size>";
            }
            else
            {
                var mes = $"<color={RoleInfo.RoleColorCode}>ターゲットに近づいてキル!</color>";
                return isForHud ? mes : $"<size=40%>{mes}</size>";
            }
        }
        void ClearEffects()
        {
            generation++;
            RemoveEffect(Player.PlayerId);
            ResetDarkenState();
            PublicRoleColor = false;
            targetDied = false;
            targetId = 255;
            targeted = false;
            KillWaitPlayer = null;
        }

        public override void OnDead(PlayerControl player) { if (player?.PlayerId == Player.PlayerId) ClearEffects(); }
        public override void OnStartMeeting() => ClearEffects();
        public override void OnReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo body) => ClearEffects();
        public override void AfterMeetingTasks() => ClearEffects();
        public override void OnDestroy() { ClearEffects(); disposed = true; }

        public static System.Collections.Generic.Dictionary<int, Achievement> achievements = new();
        [Attributes.PluginModuleInitializer]
        public static void Load()
        {
            var n1 = new Achievement(RoleInfo, 0, 1, 0, 0);
            var l1 = new Achievement(RoleInfo, 1, 1, 0, 1);
            var sp1 = new Achievement(RoleInfo, 2, 1, 0, 3, true);
            achievements.Add(0, n1);
            achievements.Add(1, l1);
            achievements.Add(2, sp1);
        }
    }
}
