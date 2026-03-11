using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Goobstation.Shared.Balloons;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BalloonComponent : Component
{
    [DataField, AutoNetworkedField]
    public int MinCash = 1;

    [DataField, AutoNetworkedField]
    public int MaxCash = 1;

    // Bloons speed on the track
    [DataField, AutoNetworkedField]
    public float Speed = 1f;

    // How many lives it removes when it reaches track end
    [DataField, AutoNetworkedField]
    public int Lives = 1;

    [DataField]
    public EntProtoId CashPrototype = "SpaceCash";

    [DataField]
    public string CashStackType = "Credit";

    [DataField]
    public float MergeRange = 4.5f;

    [DataField]
    public List<EntProtoId> SpawnOnPop = new();

    [DataField]
    public bool ProcessedPop = false;

    public EntityUid? LinkedTrackEnd;
}
