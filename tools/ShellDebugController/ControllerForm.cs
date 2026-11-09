using ShellDebugController.Shared;
using System.ComponentModel;
using TownOfHost.Modules;
namespace ShellDebugController;
internal sealed class ControllerForm : Form
{
    private readonly Color ink=Color.FromArgb(31,45,65),accent=Color.FromArgb(36,102,159);
    private readonly CheckBox master=new(),zoom=new(),noClip=new(),telemetry=new(),simulation=new();
    private readonly Label connection=new(),eligibility=new(),stats=new();
    private readonly TextBox simSummary=new(){ReadOnly=true,Multiline=true,ScrollBars=ScrollBars.Vertical,BorderStyle=BorderStyle.None,BackColor=Color.White};
    private readonly TrafficChart measuredChart=new(),simulatedChart=new();
    private readonly NumericUpDown pool=new(),beam=new(),nameBytes=new(),spawnBytes=new(),emptyBytes=new();
    private readonly DataGridView phaseGrid=new();
    private readonly TabControl tabs=new();
    private readonly ComboBox simulationMap=new(){DropDownStyle=ComboBoxStyle.DropDownList};
    private int? observedMap;
    private readonly ControllerConnection bridge;
    private readonly object stateLock=new();
    private ControlRequest requested=new();
    private bool internalChange,connected;
    private int epoch;
    private DebugSnapshot latest;
    private bool useObserved;
    internal ControllerForm(bool offlineTest)
    {
        Text="Shell Debug Controller";Font=new Font("Yu Gothic UI",10);BackColor=Color.FromArgb(243,246,250);ForeColor=ink;
        ClientSize=new Size(1120,800);MinimumSize=new Size(1020,780);StartPosition=FormStartPosition.CenterScreen;AutoScaleMode=AutoScaleMode.Dpi;
        var shell=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=3,Padding=new Padding(24)};
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,330));shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute,84));shell.RowStyles.Add(new RowStyle(SizeType.Percent,100));shell.RowStyles.Add(new RowStyle(SizeType.Absolute,48));Controls.Add(shell);
        var title=new Label{Text="Shell Debug Controller",Font=new Font(Font.FontFamily,23,FontStyle.Bold),Dock=DockStyle.Top,AutoSize=true};
        var header=new Panel{Dock=DockStyle.Fill};header.Controls.Add(new Label{Text="TOH-Shell  ·  LOCAL DEVELOPMENT ONLY",Dock=DockStyle.Bottom,Height=24,ForeColor=accent});header.Controls.Add(title);shell.Controls.Add(header,0,0);shell.SetColumnSpan(header,2);
        var switches=new TableLayoutPanel{Dock=DockStyle.Top,Width=306,Height=540,ColumnCount=1,RowCount=8,Margin=new Padding(0,0,18,0)};
        switches.RowStyles.Add(new RowStyle(SizeType.Absolute,94));for(int i=1;i<=4;i++)switches.RowStyles.Add(new RowStyle(SizeType.Absolute,62));
        switches.RowStyles.Add(new RowStyle(SizeType.Absolute,66));switches.RowStyles.Add(new RowStyle(SizeType.Absolute,86));switches.RowStyles.Add(new RowStyle(SizeType.Absolute,44));
        AddSwitch(switches,master,"1   デバッグモード",0,true);AddSwitch(switches,zoom,"2   ズーム",1,false);AddSwitch(switches,noClip,"3   壁すり抜け",2,false);AddSwitch(switches,telemetry,"4   通信状態",3,false);AddSwitch(switches,simulation,"5   15人用通信状態",4,false);
        switches.Controls.Add(new Label{Text="生存ホスト・非公開オンライン試合限定。\nズームはゲーム内の既存設定もONにしてください。",Dock=DockStyle.Fill,ForeColor=Color.DimGray,Padding=new Padding(2,10,2,0)},0,5);
        switches.Controls.Add(new Label{Text="アプリ切断時は自動解除。\n壁内でOFFにすると最後の安全位置へ復帰します。\nゲーム起動・DLL配置はこのアプリでは行いません。",Dock=DockStyle.Fill,ForeColor=Color.DimGray,Padding=new Padding(2,6,2,0)},0,6);
        eligibility.Dock=DockStyle.Fill;eligibility.ForeColor=accent;eligibility.AutoEllipsis=true;switches.Controls.Add(eligibility,0,7);shell.Controls.Add(switches,0,1);
        tabs.Dock=DockStyle.Fill;tabs.Font=Font;var measured=new TabPage("実測の通信状態"){BackColor=Color.White,Padding=new Padding(16)};var simulated=new TabPage("15人の推定シミュレーション"){BackColor=Color.White,Padding=new Padding(16)};tabs.TabPages.AddRange(new[]{measured,simulated});shell.Controls.Add(tabs,1,1);
        var realLayout=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=4,ColumnCount=1};foreach(var height in new[]{32,238,44})realLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,height));realLayout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        realLayout.Controls.Add(new Label{Text="実測：ゲームの送信コールバックを観測",Dock=DockStyle.Fill,ForeColor=accent,Font=new Font(Font,FontStyle.Bold)},0,0);
        stats.Dock=DockStyle.Fill;stats.Font=new Font(Font.FontFamily,12);realLayout.Controls.Add(stats,0,1);
        realLayout.Controls.Add(new Label{Text="直近60秒 / 1秒ごとの送信回数（受信成功や再送総量の保証ではありません）",Dock=DockStyle.Fill,ForeColor=Color.DimGray},0,2);measuredChart.Dock=DockStyle.Fill;realLayout.Controls.Add(measuredChart,0,3);measured.Controls.Add(realLayout);
        var simLayout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=6};foreach(var h in new[]{106,72,34,130})simLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,h));simLayout.RowStyles.Add(new RowStyle(SizeType.Percent,100));simLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,116));
        var mapHeader=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=2,ColumnCount=1};mapHeader.RowStyles.Add(new RowStyle(SizeType.Absolute,70));mapHeader.RowStyles.Add(new RowStyle(SizeType.Absolute,30));
        mapHeader.Controls.Add(new Label{Text="推定値です。実クライアントやサーバーへ送信しません。\nSpawn/空名フレームが未観測なら、下の仮定値を使用します。\n照準1体を含む総数: Airship15 / Fungle11 / その他10。プールは手動調整も可能。",Dock=DockStyle.Fill,ForeColor=accent},0,0);
        simulationMap.Items.AddRange(new object[]{"その他のマップ（10体）","The Fungle（11体）","The Airship（15体）"});simulationMap.SelectedIndex=0;simulationMap.Dock=DockStyle.Fill;mapHeader.Controls.Add(simulationMap,0,1);simLayout.Controls.Add(mapHeader,0,0);
        var inputs=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false};AddNumber(inputs,pool,"プール(計)",2,17,NovaVisualPoolRules.Count(false));AddNumber(inputs,beam,"ビーム用",1,16,3);AddNumber(inputs,nameBytes,"表示名B",0,65535,593);AddNumber(inputs,spawnBytes,"Spawn B",1,65535,256);AddNumber(inputs,emptyBytes,"空名B",1,65535,256);simLayout.Controls.Add(inputs,0,1);
        simulationMap.SelectedIndexChanged+=(_,_)=>{pool.Value=NovaVisualPoolRules.Count(simulationMap.SelectedIndex==1, simulationMap.SelectedIndex==2);UpdateSimulation();};
        var observed=new Button{Text="観測した最大サイズを入力に反映",Dock=DockStyle.Fill,FlatStyle=FlatStyle.Flat};observed.Click+=(_,_)=>{if(latest==null)return;internalChange=true;try{if(latest.MaximumNameBytes>0)nameBytes.Value=Math.Min(65535,latest.MaximumNameBytes);if(latest.ObservedSpawnBytes>0)spawnBytes.Value=Math.Min(65535,latest.ObservedSpawnBytes);if(latest.ObservedEmptyNameBytes>0)emptyBytes.Value=Math.Min(65535,latest.ObservedEmptyNameBytes);useObserved=true;}finally{internalChange=false;}UpdateSimulation();};simLayout.Controls.Add(observed,0,2);
        phaseGrid.Dock=DockStyle.Fill;phaseGrid.ReadOnly=true;phaseGrid.AllowUserToAddRows=phaseGrid.AllowUserToDeleteRows=false;phaseGrid.RowHeadersVisible=false;phaseGrid.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;phaseGrid.BackgroundColor=Color.White;phaseGrid.BorderStyle=BorderStyle.None;phaseGrid.Columns.Add("phase","段階（推定）");phaseGrid.Columns.Add("count","送信要求回数");phaseGrid.Columns.Add("bytes","フレームB");simLayout.Controls.Add(phaseGrid,0,3);simulatedChart.Dock=DockStyle.Fill;simulatedChart.BarColor=Color.FromArgb(213,137,50);simLayout.Controls.Add(simulatedChart,0,4);simSummary.Dock=DockStyle.Fill;simSummary.Font=new Font(Font.FontFamily,9);simLayout.Controls.Add(simSummary,0,5);simulated.Controls.Add(simLayout);
        connection.Dock=DockStyle.Fill;connection.TextAlign=ContentAlignment.MiddleLeft;connection.ForeColor=Color.DimGray;shell.Controls.Add(connection,0,2);shell.SetColumnSpan(connection,2);
        foreach(var toggle in new[]{master,zoom,noClip,telemetry,simulation})toggle.CheckedChanged+=Changed;
        SetConnected(false,"Among Us: 未起動 / 開発版: 未接続");
        if(!offlineTest){bridge=new(ReadRequest,Receive);Shown+=(_,_)=>bridge.Start();}
        FormClosed+=(_,_)=>{lock(stateLock)requested=new();bridge?.Dispose();};
    }
    private void AddSwitch(TableLayoutPanel target,CheckBox box,string text,int row,bool large)
    {
        box.Text=text+"   OFF";box.Appearance=Appearance.Button;box.FlatStyle=FlatStyle.Flat;box.FlatAppearance.BorderColor=Color.FromArgb(207,216,226);box.TextAlign=ContentAlignment.MiddleLeft;box.Padding=new Padding(12,0,0,0);box.Font=new Font(Font.FontFamily,large?14:12,large?FontStyle.Bold:FontStyle.Regular);box.Dock=DockStyle.Fill;box.Margin=new Padding(0,0,0,10);box.Tag=text;target.Controls.Add(box,0,row);
    }
    private void AddNumber(FlowLayoutPanel target,NumericUpDown input,string label,int minimum,int maximum,int value)
    {
        var panel=new Panel{Width=99,Height=62,Margin=new Padding(0,0,5,0)};panel.Controls.Add(new Label{Text=label,Dock=DockStyle.Top,Height=24});input.Minimum=minimum;input.Maximum=maximum;input.Value=value;input.Dock=DockStyle.Bottom;panel.Controls.Add(input);target.Controls.Add(panel);
        input.ValueChanged+=(_,_)=>{if(!internalChange)useObserved=false;UpdateSimulation();};
    }
    private ControlRequest ReadRequest(){lock(stateLock)return requested.Sanitized();}
    private void Changed(object sender,EventArgs args)
    {
        if(internalChange)return;
        if(!master.Checked){internalChange=true;zoom.Checked=noClip.Checked=telemetry.Checked=simulation.Checked=false;internalChange=false;}
        foreach(var toggle in new[]{master,zoom,noClip,telemetry,simulation}){toggle.Text=toggle.Tag+"   "+(toggle.Checked?"ON":"OFF");toggle.BackColor=toggle.Checked?Color.FromArgb(223,238,250):Color.White;}
        zoom.Enabled=noClip.Enabled=telemetry.Enabled=simulation.Enabled=connected&&master.Checked;
        lock(stateLock)requested=new ControlRequest{Debug=master.Checked,Zoom=zoom.Checked,NoClip=noClip.Checked,Telemetry=telemetry.Checked,Simulation=simulation.Checked,SessionEpoch=epoch};
        if(sender==simulation&&simulation.Checked)tabs.SelectedIndex=1;if(sender==telemetry&&telemetry.Checked)tabs.SelectedIndex=0;UpdatePanels();
    }
    private void SetConnected(bool value,string status)
    {
        connected=value;connection.Text=status;master.Enabled=value;
        if(!value){internalChange=true;master.Checked=zoom.Checked=noClip.Checked=telemetry.Checked=simulation.Checked=false;internalChange=false;latest=null;useObserved=false;}
        Changed(master,EventArgs.Empty);UpdatePanels();
    }
    private void Receive(DebugSnapshot value,string status)
    {
        if(IsDisposed||!IsHandleCreated)return;
        try{BeginInvoke((Action)(()=>{
            if(value!=null&&value.SessionEpoch!=epoch){epoch=value.SessionEpoch;internalChange=true;master.Checked=zoom.Checked=noClip.Checked=telemetry.Checked=simulation.Checked=false;internalChange=false;}
            latest=value;
            if(value?.MapId is int map && value.IsFungle.HasValue && observedMap!=map)
            { observedMap=map;simulationMap.SelectedIndex=map==4?2:value.IsFungle.Value?1:0;pool.Value=NovaVisualPoolRules.Count(value.IsFungle.Value, map==4); }
            SetConnected(value!=null,status);eligibility.Text=value?.Eligibility??"";UpdatePanels();
        }));}catch(InvalidOperationException){}
    }
    private void UpdatePanels()
    {
        if(!telemetry.Checked||latest==null){stats.Text="通信状態はOFFです。\n数値の取得・表示は停止しています。";measuredChart.Values=Array.Empty<long>();}
        else{
            string raw=latest.RawHookAvailable?$"送信コールバック回数   {latest.RawSendCount:N0}\n送信データ量（callback長）   {latest.RawSendBytes:N0} B\n直近10秒   {latest.LastTenSeconds:N0}{(latest.TenSecondWindowTruncated?"以上":"")} 回":"送信コールバック   取得不可";
            stats.Text=$"参加人数   {latest.Participants}\n{raw}\n送信エラー（Send戻り値/例外）   {(latest.ErrorHookAvailable?latest.SendErrors.ToString("N0"):"取得不可")}\nNovaダミー   {latest.NovaDummies}\nNova   {latest.NovaState}\nズーム適用 {latest.ZoomApplied} / 壁すり抜け適用 {latest.NoClipApplied}";measuredChart.Values=latest.RecentSeconds??Array.Empty<long>();
        }
        UpdateSimulation();
    }
    private void UpdateSimulation()
    {
        if(!simulation.Checked){phaseGrid.Rows.Clear();simulatedChart.Values=Array.Empty<long>();simSummary.Text="15人用通信状態はOFFです。";return;}
        try{
            var result=TrafficSimulator.Run(new SimulationInput{IsFungle=simulationMap.SelectedIndex==1,IsAirship=simulationMap.SelectedIndex==2,Dummies=(int)pool.Value,BeamDummies=(int)beam.Value,NameBytes=(int)nameBytes.Value,SpawnBytes=(int)spawnBytes.Value,EmptyNameFrameBytes=(int)emptyBytes.Value,
                SpawnMeasured=useObserved&&latest?.ObservedSpawnBytes>0,EmptyNameMeasured=useObserved&&latest?.ObservedEmptyNameBytes>0,ChargeSeconds=latest?.NovaChargeSeconds??3,DelaySeconds=latest?.NovaDelaySeconds??1});
            phaseGrid.Rows.Clear();foreach(var phase in result.Phases)phaseGrid.Rows.Add(phase.Phase,phase.Messages,phase.Bytes);simulatedChart.Values=result.Seconds;
            simSummary.Text=($"最大要求 {result.MaximumPacketBytes:N0} B / 10秒ピーク {result.PeakTenSeconds} 回\n{result.Basis}\n"+string.Join("\n",result.Warnings)).Replace("\n",Environment.NewLine);simSummary.ForeColor=result.MaximumPacketBytes>1000||(int)nameBytes.Value>800?Color.Firebrick:Color.DimGray;
        }catch(ArgumentOutOfRangeException){phaseGrid.Rows.Clear();simSummary.Text="ビーム用の数は、プール数から照準1体を除いた範囲で指定してください。";simulatedChart.Values=Array.Empty<long>();}
    }
    internal void SmokeTest()
    {
        simulationMap.SelectedIndex=2;if(pool.Value!=15)throw new Exception("Airship default pool failed");simulationMap.SelectedIndex=1;if(pool.Value!=11)throw new Exception("Fungle default pool failed");simulationMap.SelectedIndex=0;if(pool.Value!=10)throw new Exception("Normal default pool failed");
        Receive(new DebugSnapshot{SessionEpoch=1,MapId=4,IsFungle=false,NovaPoolTarget=15},"offline map test");Application.DoEvents();if(pool.Value!=15)throw new Exception("Live Airship map binding failed");
        Receive(new DebugSnapshot{SessionEpoch=1,MapId=5,IsFungle=true,NovaPoolTarget=11},"offline map test");Application.DoEvents();if(pool.Value!=11)throw new Exception("Live Fungle map binding failed");
        Receive(new DebugSnapshot{SessionEpoch=1,MapId=1,IsFungle=false,NovaPoolTarget=10},"offline map test");Application.DoEvents();if(pool.Value!=10)throw new Exception("Live normal map binding failed");
        epoch=1;latest=new DebugSnapshot{ProcessId=1234,SessionEpoch=1,Participants=15,RawHookAvailable=true,ErrorHookAvailable=true,RawSendCount=57,RawSendBytes=14280,LastTenSeconds=40,NovaDummies=10,NovaState="準備完了 / 非表示・回収待機 10/10",RecentSeconds=Enumerable.Range(0,60).Select(i=>(long)(i%8==0?12:i%4)).ToArray(),Eligibility="UIテスト（ゲーム通信なし）"};
        SetConnected(true,"UIテスト / 実際のゲーム通信・起動なし");eligibility.Text=latest.Eligibility;
        master.Checked=true;zoom.Checked=true;noClip.Checked=true;telemetry.Checked=true;simulation.Checked=true;
        if(!ReadRequest().NoClip||!ReadRequest().Simulation)throw new Exception("UI state binding failed");
        master.Checked=false;if(ReadRequest().Zoom||ReadRequest().NoClip||ReadRequest().Telemetry||ReadRequest().Simulation||zoom.Enabled)throw new Exception("Master OFF failed");
        master.Checked=true;telemetry.Checked=true;simulation.Checked=true;tabs.SelectedIndex=0;Application.DoEvents();
        if(master.Top>=zoom.Top||zoom.Top>=noClip.Top||noClip.Top>=telemetry.Top||telemetry.Top>=simulation.Top)throw new Exception("Feature ordering failed");
        if(tabs.Right>ClientSize.Width||tabs.Bottom>ClientSize.Height)throw new Exception("Main panel clipped");
    }
    internal void SimulatorPreview(string path)
    {
        simulationMap.SelectedIndex=1;tabs.SelectedIndex=1;Application.DoEvents();using var bitmap=new Bitmap(Width,Height);DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(path);
    }
}
internal sealed class TrafficChart : Control
{
    private long[] values=Array.Empty<long>();
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]public Color BarColor{get;set;}=Color.FromArgb(48,126,175);
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]public long[] Values{get=>values;set{values=value??Array.Empty<long>();Invalidate();}}
    public TrafficChart(){DoubleBuffered=true;BackColor=Color.FromArgb(247,249,252);}
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);if(values.Length==0){e.Graphics.DrawString("データなし",Font,Brushes.Gray,12,12);return;}
        long max=Math.Max(1,values.Max());float width=(Width-24f)/values.Length;using var brush=new SolidBrush(BarColor);
        for(int i=0;i<values.Length;i++){float h=(Height-32f)*values[i]/max;e.Graphics.FillRectangle(brush,12+i*width,Height-18-h,Math.Max(1,width-2),h);}
        e.Graphics.DrawString($"最大 {max:N0} 回/秒",Font,Brushes.DimGray,10,2);
    }
}
