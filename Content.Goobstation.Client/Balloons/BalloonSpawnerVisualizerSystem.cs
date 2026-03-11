using Content.Goobstation.Shared.Balloons;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;

namespace Content.Goobstation.Client.Balloons;

public sealed class BalloonSpawnerVisualizerSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;
    public override void Initialize()
    {
        SubscribeLocalEvent<BalloonSpawnerComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<BalloonSpawnerComponent, AfterAutoHandleStateEvent>(OnAfterState);
    }

    private void OnStartup(Entity<BalloonSpawnerComponent> ent, ref ComponentStartup args)
    {
        UpdateRoundDisplay(ent.Owner, ent.Comp);
    }

    private void OnAfterState(Entity<BalloonSpawnerComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateRoundDisplay(ent.Owner, ent.Comp);
    }

    private void UpdateRoundDisplay(EntityUid uid, BalloonSpawnerComponent comp)
    {
        var round = comp.CurrentRound + 1;

        var tens = round / 10;
        var ones = round % 10;

        _sprite.LayerSetRsiState(uid, "digit_tens", $"digit_{tens}");
        _sprite.LayerSetRsiState(uid, "digit_ones", $"digit_{ones}");
        _sprite.LayerSetVisible(uid, "digit_tens", true);

        var buttonState = comp.Active ? "button_off" : "button_on";
        _sprite.LayerSetRsiState(uid, "ready_button", buttonState);
    }
}
