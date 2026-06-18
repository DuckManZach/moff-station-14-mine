using Content.Server._Moffstation.Speech.Components;
using Content.Server.Speech.EntitySystems;
using Content.Shared.Speech;

namespace Content.Server._Moffstation.Speech.EntitySystems;

public sealed partial class IrishAccentSystem : EntitySystem
{
    [Dependency] private ReplacementAccentSystem _replacement = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<IrishAccentComponent, AccentGetEvent>(OnAccent);
    }

    private void OnAccent(Entity<IrishAccentComponent> ent, ref AccentGetEvent args)
    {
        args.Message = _replacement.ApplyReplacements(args.Message, "irish");
    }
}
