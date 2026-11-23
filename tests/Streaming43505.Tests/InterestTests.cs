using TownOfHost.Modules;
using TownOfHost;
partial class Program
{
static LiveStreamComment Interest(string channel="channel",string name="Viewer",string text="join:") => new("synthetic-message",channel,name,text);
static void Apply(StreamInterestList list,LiveStreamComment comment,int? generation=null,bool live=true) => list.OnComment(comment,live,generation??list.Generation,"join:","withdraw");
static void RunInterestTests()
{
Test("intake starts stopped",()=>{var l=new StreamInterestList();Apply(l,Interest());Check(!l.Accepting&&l.Count==0);});
Test("keyword alone accepts YouTube name",()=>{var l=new StreamInterestList();l.Start();Apply(l,Interest(name:"YouTube Display"));Check(l.Count==1&&l.Snapshot()[0]=="YouTube Display");});
Test("same channel duplicates do not increase count or revision",()=>{var l=new StreamInterestList();l.Start();Apply(l,Interest());var v=l.Version;for(int i=0;i<100;i++)Apply(l,Interest());Check(l.Count==1&&l.Version==v);});
Test("same display name different channels remain separate",()=>{var l=new StreamInterestList();l.Start();Apply(l,Interest("a","同じ名前"));Apply(l,Interest("b","同じ名前"));Check(l.Count==2&&l.Snapshot().SequenceEqual(new[]{"同じ名前","同じ名前"}));});
Test("display-name update keeps original order",()=>{var l=new StreamInterestList();l.Start();Apply(l,Interest("a","Old"));Apply(l,Interest("b","Second"));Apply(l,Interest("a","New"));Check(l.Count==2&&l.Snapshot().SequenceEqual(new[]{"New","Second"}));});
Test("stopped intake freezes existing list",()=>{var l=new StreamInterestList();l.Start();Apply(l,Interest());l.Stop();Apply(l,Interest("second"));Apply(l,Interest(text:"withdraw"));Check(l.Count==1&&!l.Accepting);});
Test("repeat start or stop does not churn state",()=>{var l=new StreamInterestList();l.Start();int g=l.Generation,v=l.Version;l.Start();Check(l.Generation==g&&l.Version==v);l.Stop();g=l.Generation;v=l.Version;l.Stop();Check(l.Generation==g&&l.Version==v);});
Test("reset keeps intake state but invalidates queued comments",()=>{var l=new StreamInterestList();l.Start();int g=l.Generation;Apply(l,Interest());l.Reset();Apply(l,Interest(),g);Check(l.Accepting&&l.Count==0);Apply(l,Interest());Check(l.Count==1);});
Test("comments queued before start are not accepted",()=>{var l=new StreamInterestList();int g=l.Generation;l.Start();Apply(l,Interest(),g);Check(l.Count==0);});
Test("comments from previous intake cannot reappear after restart",()=>{var l=new StreamInterestList();l.Start();int g=l.Generation;l.Stop();l.Start();Apply(l,Interest(),g);Check(l.Count==0);});
Test("clear stops reception and removes all personal data",()=>{var l=new StreamInterestList();l.Start();Apply(l,Interest());l.Clear();Check(!l.Accepting&&l.Count==0&&l.Snapshot().Length==0&&!l.Overflowed);});
Test("historical comments do not register",()=>{var l=new StreamInterestList();l.Start();Apply(l,Interest(),live:false);Check(l.Count==0);});
foreach(var channel in new[]{null,""," ",new string('x',129)})Test("invalid channel rejected "+(channel?.Length??-1),()=>{var l=new StreamInterestList();l.Start();Apply(l,Interest(channel));Check(l.Count==0);});
foreach(var name in new[]{null,""," "})Test("missing display name rejected "+(name?.Length??-1),()=>{var l=new StreamInterestList();l.Start();Apply(l,Interest(name:name));Check(l.Count==0);});
Test("unsafe display name rendered as plain text",()=>{var l=new StreamInterestList();l.Start();Apply(l,Interest(name:"<size=999>日本語😀"));Check(l.Snapshot()[0]=="＜size=999＞日本語😀");});
Test("names bounded by Unicode scalar count",()=>{var l=new StreamInterestList();l.Start();Apply(l,Interest(name:string.Concat(Enumerable.Repeat("😀",100))));Check(l.Snapshot()[0].EnumerateRunes().Count()==80);});
Test("blank keyword never enrolls ordinary comments",()=>{var l=new StreamInterestList();l.Start();l.OnComment(Interest(),true,l.Generation," ","withdraw");Check(l.Count==0);});
Test("withdraw does not depend on in-game name",()=>{var l=new StreamInterestList();l.Start();Apply(l,Interest("a","YouTube A"));Apply(l,Interest("b","YouTube B"));Apply(l,Interest("a","Changed", "withdraw"));Check(l.Snapshot().SequenceEqual(new[]{"YouTube B"}));});
Test("withdraw has precedence over join",()=>{var l=new StreamInterestList();l.Start();Apply(l,Interest());Apply(l,Interest(text:"join: withdraw"));Check(l.Count==0);});
Test("capacity bound permits existing-name updates and withdrawal",()=>{var l=new StreamInterestList();l.Start();for(int i=0;i<201;i++)Apply(l,Interest("c"+i,"V"+i));Check(l.Count==200&&l.Overflowed);Apply(l,Interest("c0","Updated"));Check(l.Count==200&&l.Snapshot()[0]=="Updated");Apply(l,Interest("c1",text:"withdraw"));Apply(l,Interest("new","Added"));Check(l.Count==200&&l.Snapshot().Last()=="Added");l.Reset();Check(!l.Overflowed&&l.Count==0);});
Test("empty page still has one logical page",()=>Check(StreamInterestList.PageCount(0)==1&&StreamInterestList.Page(Array.Empty<string>(),0).Length==0));
foreach(int count in new[]{1,15,30,31,60,199,200})Test("all names reachable once through pages "+count,()=>{var names=Enumerable.Range(0,count).Select(i=>"V"+i).ToArray();var shown=Enumerable.Range(0,StreamInterestList.PageCount(count)).SelectMany(i=>StreamInterestList.Page(names,i)).ToArray();Check(shown.SequenceEqual(names));Check(StreamInterestList.Page(names,-1).SequenceEqual(StreamInterestList.Page(names,0)));});
Test("count is independent of game players",()=>{var l=new StreamInterestList();l.Start();Apply(l,Interest(name:"YouTube only"));PlayerControl.AllPlayerControls.Clear();Check(l.Count==1&&l.Snapshot()[0]=="YouTube only");});
Test("retired flags do not initiate requests",()=>{ResetStream();YouTubeMapVote.Enabled.Value=true;TownOfHost.Modules.ExtraFeatureOptions.JoinByCommentOnly.Value=true;var(i,h)=FakeStream();StreamerInfo.CommentFrameTick(.02f);Check(h.Disposed&&h.Paths.Count==0);});
Test("intake start alone never starts legacy HTTP",()=>{ResetStream();StreamerInfo.StartInterestReception();StreamerInfo.CommentFrameTick(.02f);Check(StreamerInfo.Interests.Accepting);StreamerInfo.StopInterestReception();});
Test("queue from old intake discarded while comment display remains",()=>{ResetStream();StreamerInfo.StartInterestReception();var(i,h)=FakeStream();Queue(i,"queued","a","join:");StreamerInfo.ResetInterestList();Drain();Check(StreamerInfo.Interests.Count==0&&StreamerInfo.RecentComments.Count==1);StreamerInfo.StopNetwork();});
Test("nonhost cannot start intake",()=>{ResetStream();AmongUsClient.Instance.AmHost=false;StreamerInfo.StartInterestReception();Check(!StreamerInfo.Interests.Accepting);AmongUsClient.Instance.AmHost=true;});
Test("disconnect clears all names and stops intake",()=>{ResetStream();StreamerInfo.StartInterestReception();var(i,h)=FakeStream();Queue(i,"one","a","join:");Drain();StreamerInfo.DisconnectInternal();Check(StreamerInfo.Interests.Count==0&&!StreamerInfo.Interests.Accepting&&h.Disposed);});
Test("stop leaves a readable frozen list",()=>{ResetStream();StreamerInfo.StartInterestReception();var(i,h)=FakeStream();Queue(i,"one","a","join:");Drain();StreamerInfo.StopInterestReception();Check(StreamerInfo.Interests.Count==1&&!StreamerInfo.Interests.Accepting);StreamerInfo.DisconnectInternal();});
Test("URL switch stops and clears reception",()=>{ResetStream();StreamerInfo.StartInterestReception();var(i,h)=FakeStream();Queue(i,"one","a","join:");Drain();StreamerInfo.TrySetStreamURL("https://youtu.be/NEWstream01");Check(StreamerInfo.Interests.Count==0&&!StreamerInfo.Interests.Accepting);});
}
}
