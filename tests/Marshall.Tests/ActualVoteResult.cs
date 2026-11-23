using System;using System.Linq;using System.Collections.Generic;using TownOfHost.Roles.Core;using static TownOfHost.Options;
namespace TownOfHost.Modules {public partial class MeetingVoteManager {
 static readonly QuietLog logger=new();public static string Voteresult="";public static string GetVoteName(byte id)=>id.ToString();
    public readonly struct VoteResult
    {
        /// <summary>
        /// Key: 投票された人<br/>
        /// Value: 得票数
        /// </summary>
        public IReadOnlyDictionary<byte, int> VotedCounts => votedCounts;
        private readonly Dictionary<byte, int> votedCounts;
        private readonly Dictionary<byte, int> Tievotecount;
        /// <summary>
        /// 追放されるプレイヤー
        /// </summary>
        public readonly NetworkedPlayerInfo Exiled;
        /// <summary>
        /// 同数投票かどうか
        /// </summary>
        public readonly bool IsTie;
        /// <summary>
        /// 上書き追放されるプレイヤー
        /// </summary>
        public readonly byte OverrideExiled;
        public readonly ushort OverruleNonce;

        public VoteResult(Dictionary<byte, int> votedCounts, Dictionary<byte, int> Tievotecount, bool ClearAndExile = false, byte overrideExileId = byte.MaxValue, ushort nonce = ushort.MinValue)
        {
            OverrideExiled = byte.MaxValue;
            // ジャッジ上書きがない通常会議のV11 VotingCompleteではnonceを0として送る。
            OverruleNonce = 0;
            this.votedCounts = votedCounts;

            // 票数順に整列された投票
            var orderedVotes = votedCounts.OrderByDescending(vote => vote.Value);
            // 最も票を得た人の票数
            var maxVoteNum = orderedVotes.FirstOrDefault().Value;
            // 最多票数のプレイヤー全員
            var mostVotedPlayers = votedCounts.Where(vote => vote.Value == maxVoteNum).Select(vote => vote.Key).ToArray();
            //最多投票以外のプレイヤー
            var NotmostVotedPlayers = votedCounts.Where(vote => vote.Value != maxVoteNum).Select(vote => vote.Key).ToArray();

            //タイブレ投票
            // タイブレーク計算で呼び出し元の投票辞書を直接書き換えると、
            // 後続の表示・再計算に誤った票数が残るためコピーを使う。
            this.Tievotecount = new Dictionary<byte, int>(Tievotecount);
            foreach (var pc in NotmostVotedPlayers)
            {//最多投票以外のプレイヤーはタイブレ投票0にする
                this.Tievotecount[pc] = 0;
            }

            // 票数順に整列された投票
            var TSe = this.Tievotecount.OrderByDescending(vote => vote.Value);
            // 最も票を得た人の票数
            var TCo = TSe.FirstOrDefault().Value;
            // 最多票数のプレイヤー全員
            var TMost = this.Tievotecount.Where(vote => vote.Value == TCo).Select(vote => vote.Key).ToArray();

            // Judge/forced exile is authoritative. A unanimous living Marshall preference
            // resolves a genuine tie before ordinary Tiebreaker; conflicts keep the old path.
            byte marshalPreferred = ClearAndExile ? NoVote : TownOfHost.Roles.Crewmate.Marshall.Preferred(votedCounts);
            if (mostVotedPlayers.Length > 1 && maxVoteNum > 0 && marshalPreferred != NoVote)
            {
                IsTie = false;
                Exiled = marshalPreferred == Skip ? null : PlayerCatch.GetPlayerInfoById(marshalPreferred);
            }
            else if (mostVotedPlayers.Length > 1)
            {
                if (TMost.Length == 1)//タイブレ投票
                {
                    IsTie = false;
                    Exiled = GameData.Instance.GetPlayerById(TMost[0]);
                    logger.Info($"-タイブレ-最多得票者: {GetVoteName(TMost[0])}");
                }
                else
                {
                    IsTie = true;
                    Exiled = null;
                    logger.Info($"{string.Join(',', mostVotedPlayers.Select(id => GetVoteName(id)))} が同数");
                }
            }
            else
            {
                IsTie = false;
                Exiled = GameData.Instance.GetPlayerById(mostVotedPlayers[0]);
                logger.Info($"最多得票者: {GetVoteName(mostVotedPlayers[0])}");
            }

            if (overrideExileId != byte.MaxValue)
            {
                Exiled = GameData.Instance.GetPlayerById(overrideExileId);
                OverrideExiled = overrideExileId;
                OverruleNonce = nonce;
                IsTie = false;
                Logger.Info($"Judge Exiled:{overrideExileId}", "VoteCount");
                MeetingHudPatch.SetJudgeOverrulePatch.OverruleNonce = ushort.MinValue;
                return;
            }

            var SkipVoteMode = false;
            foreach (var pc in PlayerCatch.AllPlayerControls)
            {
                if (pc.GetRoleClass()?.VotingResults(ref Exiled, ref IsTie, votedCounts, mostVotedPlayers, ClearAndExile) ?? false)
                    SkipVoteMode = true; //どれかがtrueを返すと以下の特殊モードが実行されなくなる
            }

            // 同数投票時の特殊モード
            if (IsTie && Options.VoteMode.GetBool() && !SkipVoteMode)
            {
                var tieMode = (TieMode)Options.WhenTie.GetValue();
                switch (tieMode)
                {
                    case TieMode.All:
                        Voteresult = "";

                        var toExile = mostVotedPlayers.Where(id => id != Skip).ToArray();
                        foreach (var playerId in toExile)
                        {
                            PlayerCatch.GetPlayerById(playerId)?.SetRealKiller(null);
                        }
                        MeetingHudPatch.TryAddAfterMeetingDeathPlayers(CustomDeathReason.Vote, toExile);
                        Voteresult = string.Join('\n', mostVotedPlayers.Select(id => UtilsRoleText.GetExpelledText(id, false, false)));
                        UtilsGameLog.AddGameLog("Vote", Voteresult);

                        bool winnoyatu = false;
                        Exiled = null;
                        logger.Info("全員追放します");
                        foreach (var playerId in toExile)
                        {
                            CustomRoleManager.GetByPlayerId(playerId)?.OnExileWrapUp(PlayerCatch.GetPlayerInfoById(playerId), ref winnoyatu);
                        }
                        break;
                    case TieMode.Random:
                        var exileId = mostVotedPlayers.OrderBy(_ => Guid.NewGuid()).FirstOrDefault();
                        Exiled = GameData.Instance.GetPlayerById(exileId);
                        IsTie = false;
                        logger.Info($"ランダム追放: {GetVoteName(exileId)}");
                        Voteresult = UtilsRoleText.GetExpelledText(exileId, false, false);
                        UtilsGameLog.AddGameLog("Vote", UtilsRoleText.GetExpelledText(exileId, false, false));
                        break;
                }
            }
        }
        /// <summary>
        /// Rpc受け取った側用のVoteResult。
        /// Dictionaryが正常値にならない。
        /// </summary>
        /// <param name="exileId"></param>
        /// <param name="Istie"></param>
public VoteResult(byte exileId, bool Istie, byte overrideexilid, ushort overrulenonce)
        {
            this.Exiled = exileId is byte.MaxValue ? null : PlayerCatch.GetPlayerInfoById(exileId);
            this.IsTie = Istie;
            this.OverrideExiled = overrideexilid;
            this.OverruleNonce = overrulenonce;
            Logger.Info($"{this.Exiled} - {this.IsTie} - {this.OverrideExiled}", "VoteResult");
        }
    }
}}
