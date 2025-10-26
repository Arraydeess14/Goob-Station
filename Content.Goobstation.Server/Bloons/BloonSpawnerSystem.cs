using Content.Goobstation.Common.Chat;
using Content.Goobstation.Common.Traits;
using Content.Goobstation.Shared.Bloons;
using Content.Server.Power.Components;
using Content.Server.Radio;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Robust.Shared.GameStates;
using System.Threading.Tasks;

namespace Content.Goobstation.Server.Bloons;

public sealed class BloonSpawnerSystem : EntitySystem
{

    public override void Initialize()
    {
        SubscribeLocalEvent<BloonSpawnerComponent, ActivateInWorldEvent>(OnBloonSpawnerUse);
    }

    private void OnBloonSpawnerUse(EntityUid uid, BloonSpawnerComponent component, ref ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;
        var coords = Transform(uid).Coordinates;

        foreach(string bloon in component.BloonID)
        {
            Spawn(bloon, coords);
        }
    }
};

