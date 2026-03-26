using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using System.Numerics;

namespace Content.Goobstation.Shared.Balloons;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class BalloonComponent : Component
{
    [DataField, AutoNetworkedField]
    public int MinCash = 10;

    [DataField, AutoNetworkedField]
    public int MaxCash = 10;

    // Bloons speed on the track
    [DataField, AutoNetworkedField]
    public float Speed = 1f;

    // How many lives it removes when it reaches track end
    [DataField, AutoNetworkedField]
    public int Lives = 1;

    [DataField, AutoNetworkedField]
    public int PopHealth = 4;

    [DataField, AutoNetworkedField]
    public int CurrentPopHealth = 4;

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

    // Regrow
    [DataField, AutoNetworkedField]
    public bool IsRegrow = false;

    [DataField]
    public float RegrowDelay = 1f;

    [DataField]
    public float RegrowTimer = 0f;

    [DataField]
    public EntProtoId? RegrowInto = null;

    [DataField, AutoNetworkedField]
    public EntProtoId? RegrowCap = null;
    // end regrow

    // Camo
    [DataField, AutoNetworkedField]
    public bool IsCamo = false;

    // Track Validation
    [DataField]
    public bool IsValidator = false;

    [DataField]
    public EntityUid? ValidatorSpawner;

    [DataField]
    public EntityUid? CurrentTrackEndTarget;

    [DataField]
    public HashSet<EntityUid> ValidatedPieces = new();

    [DataField]
    public HashSet<(EntityUid Piece, Direction Dir)> ValidatedVisits = new();

    [DataField]
    public float ValidatorLifetime = 5f;

    [DataField]
    public float ValidatorStuckTimer = 0f;

    [DataField]
    public float ValidatorStuckDelay = 0.5f;

    [DataField]
    public Vector2 LastValidatorPosition;

    public EntityUid? LinkedTrackEnd;
    public EntityUid? CurrentTrackPiece;
    public EntityCoordinates? MoveTarget;
    public Direction? TravelDirection;

    [DataField]
    public bool IsMoab = false;

}
