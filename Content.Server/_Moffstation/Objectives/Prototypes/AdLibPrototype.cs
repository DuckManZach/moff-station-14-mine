using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Server._Moffstation.Objectives.Prototypes;

/// <summary>
/// This is a prototype for...
/// </summary>
[Prototype]
public sealed partial class AdLibPrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public string Text { get; private set; } = string.Empty;

    [DataField]
    public HashSet<string> Tags { get; private set; } = new();
}

