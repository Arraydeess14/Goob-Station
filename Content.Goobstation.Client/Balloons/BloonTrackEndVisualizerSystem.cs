using Content.Goobstation.Shared.Balloons;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;

namespace Content.Goobstation.Client.Balloons;

public sealed class BloonTrackEndVisualizerSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<BloonTrackEndComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<BloonTrackEndComponent, AfterAutoHandleStateEvent>(OnAfterState);
    }

    private void OnStartup(Entity<BloonTrackEndComponent> ent, ref ComponentStartup args)
    {
        UpdateLivesDisplay(ent.Owner, ent.Comp);
    }

    private void OnAfterState(Entity<BloonTrackEndComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateLivesDisplay(ent.Owner, ent.Comp);
    }

    private void UpdateLivesDisplay(EntityUid uid, BloonTrackEndComponent comp)
    {
        var lives = comp.Lives;

        if (lives <= 0)
        {
            _sprite.LayerSetVisible(uid, "lives_heart", false);
            _sprite.LayerSetVisible(uid, "digit_hundreds", false);
            _sprite.LayerSetVisible(uid, "digit_tens", false);
            _sprite.LayerSetVisible(uid, "digit_ones", false);
            _sprite.LayerSetVisible(uid, "lives_skull", true);
            return;
        }

        _sprite.LayerSetVisible(uid, "lives_skull", false);
        _sprite.LayerSetVisible(uid, "lives_heart", true);

        var hundreds = lives / 100;
        var tens = (lives / 10) % 10;
        var ones = lives % 10;

        _sprite.LayerSetRsiState(uid, "digit_hundreds", $"digit_{hundreds}");
        _sprite.LayerSetRsiState(uid, "digit_tens", $"digit_{tens}");
        _sprite.LayerSetRsiState(uid, "digit_ones", $"digit_{ones}");

        _sprite.LayerSetVisible(uid, "digit_hundreds", hundreds > 0);
        _sprite.LayerSetVisible(uid, "digit_tens", lives >= 10);
        _sprite.LayerSetVisible(uid, "digit_ones", true);
    }
}
