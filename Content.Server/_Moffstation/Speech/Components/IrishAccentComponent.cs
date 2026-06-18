using Content.Server._Moffstation.Speech.EntitySystems;

namespace Content.Server._Moffstation.Speech.Components;

[RegisterComponent, Access(typeof(IrishAccentSystem))]
public sealed partial class IrishAccentComponent : Component;
