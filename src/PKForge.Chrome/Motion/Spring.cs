namespace PKForge.App.Services;

/// <summary>The original Android spring, shared without changing its integration or tolerances.</summary>
public sealed class Spring(float value)
{
    public float Value { get; private set; } = value;
    public float Target { get; set; } = value;
    public float Velocity { get; private set; }
    public bool Settled => MathF.Abs(Target - Value) < 0.001f && MathF.Abs(Velocity) < 0.001f;

    public void Snap(float value)
    {
        Value = Target = value;
        Velocity = 0;
    }

    public void Step(float dt, float stiffness = 420f, float damping = 41f)
    {
        var steps = Math.Max(1, (int)MathF.Ceiling(dt / (1f / 240f)));
        var h = dt / steps;
        for (var i = 0; i < steps; i++)
        {
            Velocity += (stiffness * (Target - Value) - damping * Velocity) * h;
            Value += Velocity * h;
        }
        if (Settled) Snap(Target);
    }
}
