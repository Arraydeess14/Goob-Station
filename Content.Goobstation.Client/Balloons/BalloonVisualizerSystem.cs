using Content.Goobstation.Shared.Balloons;
using Robust.Client.GameObjects;
using Robust.Shared.GameStates;

namespace Content.Goobstation.Client.Balloons;

public sealed class BalloonVisualizerSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<BalloonComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<BalloonComponent, AfterAutoHandleStateEvent>(OnAfterState);
    }

    private void OnStartup(Entity<BalloonComponent> ent, ref ComponentStartup args)
    {
        UpdateVisuals(ent.Owner, ent.Comp);
    }

    private void OnAfterState(Entity<BalloonComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateVisuals(ent.Owner, ent.Comp);
    }

    private void UpdateVisuals(EntityUid uid, BalloonComponent comp)
    {
        _sprite.LayerSetVisible(uid, "regrow", comp.IsRegrow);
        _sprite.LayerSetVisible(uid, "camo", comp.IsCamo);
    }
}
