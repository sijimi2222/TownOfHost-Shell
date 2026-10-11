using System;

namespace TownOfHost;

// Opt-in rules: existing numeric options keep their default wrapping behavior.
public sealed class ClampedIntegerValueRule : IntegerValueRule
{
    public ClampedIntegerValueRule(int minValue, int maxValue, int step)
        : base(minValue, maxValue, step) { }

    public override int RepeatIndex(int value)
        => Math.Clamp(value, 0, (MaxValue - MinValue) / Step);
}

public sealed class ClampedFloatValueRule : FloatValueRule
{
    public ClampedFloatValueRule(float minValue, float maxValue, float step)
        : base(minValue, maxValue, step) { }

    public override int RepeatIndex(int value)
        => Math.Clamp(value, 0, (int)((MaxValue - MinValue) / Step));
}
