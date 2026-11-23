using System;
namespace ShellDebugController.Shared;

public sealed class ControlRequest
{
    public int Protocol { get; set; } = 1;
    public long Sequence { get; set; }
    public int SessionEpoch { get; set; }
    public bool Debug { get; set; }
    public bool Zoom { get; set; }
    public bool NoClip { get; set; }
    public bool Telemetry { get; set; }
    public bool Simulation { get; set; }
    public ControlRequest Sanitized() => new() { Protocol = Protocol, Sequence = Sequence, SessionEpoch = SessionEpoch, Debug = Debug,
        Zoom = Debug && Zoom, NoClip = Debug && NoClip, Telemetry = Debug && Telemetry, Simulation = Debug && Simulation };
}
public sealed class DebugSnapshot
{
    public int Protocol { get; set; } = 1;
    public string Build { get; set; } = "ShellDevelopment";
    public string Version { get; set; } = "不明";
    public int ProcessId { get; set; }
    public int SessionEpoch { get; set; }
    public bool Connected { get; set; }
    public bool Debug { get; set; }
    public bool ZoomApplied { get; set; }
    public bool NoClipApplied { get; set; }
    public string Eligibility { get; set; } = "待機中";
    public int Participants { get; set; }
    public bool RawHookAvailable { get; set; }
    public bool ErrorHookAvailable { get; set; }
    public long RawSendCount { get; set; }
    public long RawSendBytes { get; set; }
    public long LastTenSeconds { get; set; }
    public bool TenSecondWindowTruncated { get; set; }
    public long SendErrors { get; set; }
    public int? MapId { get; set; }
    public bool? IsFungle { get; set; }
    public int? NovaPoolTarget { get; set; }
    public int NovaDummies { get; set; }
    public string NovaState { get; set; } = "Novaなし";
    public PhaseTraffic[] NovaTraffic { get; set; } = Array.Empty<PhaseTraffic>();
    public long[] RecentSeconds { get; set; } = new long[60];
    public int MaximumNameBytes { get; set; }
    public int ObservedEmptyNameBytes { get; set; }
    public int ObservedSpawnBytes { get; set; }
    public double NovaChargeSeconds { get; set; } = 3;
    public double NovaDelaySeconds { get; set; } = 1;
}
public sealed class PhaseTraffic
{
    public string Phase { get; set; } = "";
    public long Messages { get; set; }
    public long Bytes { get; set; }
    public int MaximumBytes { get; set; }
}

// Pure policy/lease. Background IPC never accesses Unity or game objects.
public sealed class ControlLease
{
    private readonly object sync = new();
    private ControlRequest state = new();
    private long lastSequence;
    private double lastContact = double.NegativeInfinity;
    public const double ExpirySeconds = 3;
    public void Open() { lock (sync) { state = new(); lastSequence = 0; lastContact = double.NegativeInfinity; } }
    public bool Accept(ControlRequest request, double now)
    {
        lock (sync)
        {
            if (request == null || request.Protocol != 1 || request.Sequence <= lastSequence || request.Sequence <= 0
                || !double.IsFinite(now) || now<0) return false;
            lastSequence = request.Sequence; lastContact = now; state = request.Sanitized(); return true;
        }
    }
    public ControlRequest Read(double now)
    {
        lock (sync)
        {
            if (!double.IsFinite(now) || now<0 || now < lastContact || now - lastContact >= ExpirySeconds) { state = new(); return state.Sanitized(); }
            return state.Sanitized();
        }
    }
    public ControlRequest Read(double now,int epoch){var value=Read(now);return value.SessionEpoch==epoch?value:new ControlRequest();}
    public void Close() { Open(); }
}

public readonly struct DebugEligibility
{
    public readonly bool Pc, Host, Owner, Private, Online, Task, Alive, CanMove, SpecialMovement;
    public DebugEligibility(bool pc, bool host, bool owner, bool isPrivate, bool online, bool task, bool alive, bool canMove, bool specialMovement)
    { Pc=pc; Host=host; Owner=owner; Private=isPrivate; Online=online; Task=task; Alive=alive; CanMove=canMove; SpecialMovement=specialMovement; }
    public bool LivingHost => Pc && Host && Owner && Private && Online && Task && Alive;
    public bool Movement => LivingHost && CanMove && !SpecialMovement;
}
