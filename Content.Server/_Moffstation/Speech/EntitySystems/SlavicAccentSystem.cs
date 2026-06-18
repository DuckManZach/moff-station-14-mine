using Content.Server._Moffstation.Speech.Components;
using Content.Server.Speech.EntitySystems;
using Content.Shared.Speech;

namespace Content.Server._Moffstation.Speech.EntitySystems;

public sealed partial class SlavicAccentSystem : EntitySystem
{
    [Dependency] private ReplacementAccentSystem _replacement = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SlavicAccentComponent, AccentGetEvent>(OnAccent);
    }

    private void OnAccent(Entity<SlavicAccentComponent> ent, ref AccentGetEvent args)
    {
        args.Message = _replacement.ApplyReplacements(args.Message, "slavic");
    }
}
