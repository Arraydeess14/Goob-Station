using Content.Goobstation.Shared.Balloons;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;

namespace Content.Goobstation.Client.Balloons;

public sealed class BloonTrackPieceVisualizerSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<BloonTrackPieceComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<BloonTrackPieceComponent, AfterAutoHandleStateEvent>(OnAfterState);
    }

    private void OnStartup(Entity<BloonTrackPieceComponent> ent, ref ComponentStartup args)
    {
        UpdateTrackVisual(ent.Owner, ent.Comp);
    }

    private void OnAfterState(Entity<BloonTrackPieceComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateTrackVisual(ent.Owner, ent.Comp);
    }

    private void UpdateTrackVisual(EntityUid uid, BloonTrackPieceComponent comp)
    {
        _sprite.LayerSetVisible(uid, "anchored", comp.AnchoredVisual);
        _sprite.LayerSetVisible(uid, "flow", comp.Validated);
    }
}
