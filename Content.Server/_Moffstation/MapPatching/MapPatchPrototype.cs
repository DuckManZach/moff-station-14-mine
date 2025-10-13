using System.Numerics;
using Robust.Shared.Prototypes;

namespace Content.Server._Moffstation.MapPatching;

public abstract partial class MoffMapPatching;

[DataDefinition]
public sealed partial class MoffMapPatch: MoffMapPatching
{
    public List
}

public sealed partial class MoffMapPatchEntity
{
    /// <summary>
    /// The Entity to spawn in.
    /// </summary>
    [DataField]
    public EntProtoId? EntProtoId;

    [DataField]
    public Vector2 WorldPosition;

    [DataField]
    public Angle WorldRotation;
}
