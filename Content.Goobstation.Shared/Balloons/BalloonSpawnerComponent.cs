using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Content.Goobstation.Shared.Balloons.Prototypes;

namespace Content.Goobstation.Shared.Balloons;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class BalloonSpawnerComponent : Component
{
    [DataField, AutoNetworkedField]
    public List<ProtoId<BalloonRoundPrototype>> Rounds = new();

    [DataField, AutoNetworkedField]
    public int CurrentRound = 0;

    [DataField, AutoNetworkedField]
    public bool Active = false;

    [DataField, AutoNetworkedField]
    public string? LinkId;

    [DataField]
    public Queue<QueuedBalloonSpawn> Pending = new();

    [DataField]
    public float NextSpawnTimer = 0f;

    public EntityUid? LinkedTrackEnd;
}

[DataDefinition]
public sealed partial class QueuedBalloonSpawn
{
    [DataField]
    public EntProtoId Balloon = default!;

    [DataField]
    public float Delay = 0.5f;
}
