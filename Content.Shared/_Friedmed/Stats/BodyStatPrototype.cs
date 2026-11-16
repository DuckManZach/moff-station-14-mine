using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Shared._Friedmed.Stats;

/// <summary>
/// This is a prototype for denoting a stat influenced by the organs on the body.
/// The baseline for a stat should ALWAYS be assumed to be 1.0.
/// Anything making a stat less efficient should be less, and anything making it more efficient should be more.
/// </summary>
[UsedImplicitly]
[Prototype]
public sealed partial class BodyStatPrototype : IPrototype
{
    [ViewVariables]
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public LocId Name;

    [DataField]
    public LocId Description;

    [DataField]
    public List<BodyStatDependency> Dependencies = new();
}

[ImplicitDataDefinitionForInheritors]
public abstract partial class BodyStatDependency
{
    [DataField(required: true)]
    public ProtoId<BodyStatPrototype> Stat;

    public abstract float Apply(float self, float other);
}

public sealed partial class ScaledBy : BodyStatDependency
{
    // 0 = completely ignores the other stat, 1 = fully proportional to it
    [DataField]
    public float Weight = 1f;

    public override float Apply(float self, float other)
    {
        return self * MathHelper.Lerp(1f, other, Weight);
    }
}

public sealed partial class CappedBy : BodyStatDependency
{
    public override float Apply(float self, float other)
    {
        return MathF.Min(self, other);
    }
}
