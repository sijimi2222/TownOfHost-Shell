using System.Collections.Generic;

using System.Data;

using System.Linq;

using System.Text;

using TownOfHost.Roles.Core;

using TownOfHost.Roles.AddOns.Impostor;

using TownOfHost.Roles.AddOns.Neutral;

using TownOfHost.Roles.AddOns.Common;



namespace TownOfHost

{

    public static class PlayerCatch

    {

        private static Dictionary<byte, PlayerControl> cachedPlayers = new(15);

        public static PlayerControl GetPlayerControl(this byte playerId) => GetPlayerById(playerId);

        public static PlayerControl GetPlayerById(int playerId) => GetPlayerById((byte)playerId);

        public static PlayerControl GetPlayerById(byte playerId)

        {

            if (cachedPlayers.TryGetValue(playerId, out var cachedPlayer) && cachedPlayer != null)

            {

                return cachedPlayer;

            }

            var player = PlayerCatch.AllPlayerControls.Where(pc => pc.PlayerId == playerId).FirstOrDefault();

            cachedPlayers[playerId] = player;

            return player;

        }

        public static TaskState GetPlayerTaskState(this PlayerControl player)

        {

            return PlayerState.GetByPlayerId(player.PlayerId).GetTaskState();

        }

        public static PlayerState GetPlayerState(this PlayerControl player)

        {

            return PlayerState.GetByPlayerId(player.PlayerId);

        }

        public static PlayerState GetPlayerState(this byte player)

        {

            return PlayerState.GetByPlayerId(player);

        }



        // 軽量化: 一覧を丸ごとコピーせず、前から順に探す(最初に見つかったものを返すのは今まで通り)
        public static NetworkedPlayerInfo GetPlayerInfoById(int PlayerId)
        {
            var players = GameData.Instance.AllPlayers;
            for (int i = 0; i < players.Count; i++)
            {
                var info = players[i];
                if (info.PlayerId == PlayerId) return info;
            }
            return null;
        }



        public static string GetLogPlayerName(this NetworkedPlayerInfo info)

        {

            if (info.Trygetname().Contains("<"))

            {

                return info.name.RemoveDeltext("Data");

            }

            return info.Trygetname();

        }

        static string Trygetname(this NetworkedPlayerInfo info)

        {

            if (info == null) return "";

            if (GameStates.IsLobby)

            {

                var pc = info?.Object?.GetClient()?.PlayerName;

                return pc == null ? info?.PlayerName : pc;

            }

            if (Camouflage.PlayerSkins.TryGetValue(info.PlayerId, out var cos)) return cos.PlayerName;



            return info?.PlayerName;

        }



        public static void CountAlivePlayers(bool sendLog = false)

        {

            var playerControls = AllAlivePlayerControls;

            int AliveImpostorCount = playerControls.Count(pc => pc.Is(CustomRoleTypes.Impostor));

            int AliveNeutalCount = playerControls.Count(pc => pc.Is(CustomRoleTypes.Neutral));

            int AliveCrewCount = playerControls.Count(pc => pc.GetCustomRole() is CustomRoles.Crewmate);



            if (PlayerCatch.AliveImpostorCount != AliveImpostorCount)

            {

                Logger.Info("生存しているインポスター:" + AliveImpostorCount + "人", "CountAliveImpostors");

                PlayerCatch.AliveImpostorCount = AliveImpostorCount;

                LastImpostor.SetSubRole();

                Amnesia.CheckImpostorCount();

            }

            if (PlayerCatch.AliveNeutalCount != AliveNeutalCount)

            {

                Logger.Info("生存しているニュートラル:" + AliveNeutalCount + "人", "CountAliveNeutral");

                PlayerCatch.AliveNeutalCount = AliveNeutalCount;

                LastNeutral.SetSubRole();

            }

            if (PlayerCatch.AliveCrewCount != AliveCrewCount)

            {

                Logger.Info("生存しているクルーメイト:" + AliveCrewCount + "人", "CountAliveCrew");

                PlayerCatch.AliveCrewCount = AliveCrewCount;

            }



            // クルー以外の死亡でも全体生存人数が減れば配役条件を確認する。

            LastCrewmate.OnAlivePlayerCountChanged(AllAlivePlayersCount);



            if (sendLog)

            {

                if (Options.CantUseVentMode.GetBool() && (playerControls.Count() <= Options.CantUseVentTrueCount.GetFloat()))

                    Utils.CantUseVent = true;

                else Utils.CantUseVent = false;



                var sb = new StringBuilder(100);

                foreach (var countTypes in EnumHelper.GetAllValues<CountTypes>())

                {

                    var playersCount = PlayersCount(countTypes);

                    if (playersCount == 0) continue;

                    sb.Append($"{countTypes}:{AlivePlayersCount(countTypes)}/{playersCount}, ");

                }

                sb.Append($"All:{AllAlivePlayersCount}/{AllPlayersCount}");

                Logger.Info(sb.ToString(), "CountAlivePlayers");

            }

        }

        public static int AliveImpostorCount;

        public static int AliveNeutalCount;

        public static int AliveCrewCount;

        public static int SKMadmateNowCount;

        public static int AllPlayersCount => PlayerState.AllPlayerStates.Values.Count(state => state.CountType != CountTypes.OutOfGame);

        public static int AllAlivePlayersCount => AllAlivePlayerControls.Count(pc => !pc.Is(CountTypes.OutOfGame));

        public static bool IsAllAlive => PlayerState.AllPlayerStates.Values.All(state => state.CountType == CountTypes.OutOfGame || !state.IsDead);

        public static int PlayersCount(CountTypes countTypes) => PlayerState.AllPlayerStates.Values.Count(state => state.CountType == countTypes);

        public static int AlivePlayersCount(CountTypes countTypes) => AllAlivePlayerControls.Count(pc => pc.Is(countTypes));

        public static Dictionary<byte, CustomRoleTypes> AllPlayerFirstTypes = new();

        public static IEnumerable<PlayerControl> AllPlayerControls => PlayerSnapshot().Where(p => p != null && p.PlayerId <= 15);

        public static IEnumerable<PlayerControl> AllAlivePlayerControls => PlayerSnapshot().Where(p => p != null && p.IsAlive() && p.PlayerId <= 15);

        // 軽量化: ゲーム側のプレイヤー一覧(Il2Cpp のリスト)のコピーを、同じフレームの間は使い回す。
        // 以前は呼ばれるたびに全員分をコピーしており(1フレームに何百回も呼ばれる)、重かった。
        // 生存・null・PlayerId の判定は今まで通り毎回その場で行うので、結果は変わらない。
        // リスト本体(Pointer)・人数・リストの変更回数(_version)のどれかが変わったら、同じフレーム内でもすぐ作り直す
        // (途中のプレイヤーだけが入れ替わった場合も、変更回数が増えるので古い一覧は返さない)。
        private static readonly FrameSnapshotCache<PlayerControl> playerSnapshot = new();
        private static readonly System.Func<Il2CppSystem.Collections.Generic.List<PlayerControl>, int, PlayerControl> playerAt = (list, i) => list[i];

        private static PlayerControl[] PlayerSnapshot()
        {
            var list = PlayerControl.AllPlayerControls;
            if (list == null) return System.Array.Empty<PlayerControl>();
            int count = list.Count;
            int frame;
            try { frame = UnityEngine.Time.frameCount; }
            catch { return FrameSnapshotCache<PlayerControl>.Copy(count, list, playerAt); } // メインスレッド以外からは、今まで通り毎回コピーする
            return playerSnapshot.Get(frame, list.Pointer, count, list._version, list, playerAt);
        }

        //1ターン前に生きてた人達のリスト

        public static List<PlayerControl> OldAlivePlayerControles = new();

        public static Dictionary<byte, uint> AllPlayerNetId = new();



        public static bool CachedAnyModClient { get; private set; }

        public static bool AnyModClient() => GameStates.IsInGame ? CachedAnyModClient : CachedAnyModClient = AllPlayerControls.Any(pc => pc.PlayerId is not 0 && pc.IsModClient());

    }

}