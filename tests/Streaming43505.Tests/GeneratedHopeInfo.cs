namespace TownOfHost {
    public class HopeInfo
    {
        public string Comment;
        public string PlayerName;
        public string AccountName;
        public string AccountID;
        public int Id;

        public static HopeInfo TryCrateHopeInfo(string comment, string accountid, string accountname)
        {
            if (!string.IsNullOrWhiteSpace(Main.JoinWord.Value) && !string.IsNullOrWhiteSpace(accountid) && comment.Contains(Main.JoinWord.Value))
            {
                var playername = comment.RemoveDeltext(Main.JoinWord.Value);
                return new HopeInfo(comment, accountid, accountname, playername);
            }
            return null;
        }
        HopeInfo(string comment, string accountid, string accountname, string playername)
        {
            this.Comment = comment;
            this.AccountID = accountid;
            this.AccountName = accountname;
            this.PlayerName = playername;
            Id = -1;
        }

        public bool IsUpdate(HopeInfo info)
        {
            //アカウントIDが一緒かつ、AmongUsのプレイヤー名が異なる
            if (this.AccountID == info.AccountID && info.PlayerName != this.PlayerName) return true;
            else return false;
        }

        public void SetId(int id)
        {
            this.Id = id;
        }
    }

}