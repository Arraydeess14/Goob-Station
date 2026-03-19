using Content.Goobstation.Shared.Balloons;

namespace Content.Goobstation.Server.Balloons;

public sealed class BloonTrackSystem : EntitySystem
{
    [Dependency] private readonly BalloonSpawnerSystem _spawner = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<BloonTrackPieceComponent, ComponentShutdown>(OnTrackPieceShutdown);
        SubscribeLocalEvent<BloonTrackPieceComponent, AnchorStateChangedEvent>(OnAnchorChanged);
        SubscribeLocalEvent<BloonTrackPieceComponent, MapInitEvent>(OnTrackMapInit);
    }
    private void OnTrackMapInit(Entity<BloonTrackPieceComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.AnchoredVisual = Transform(ent).Anchored;
        Dirty(ent);
    }
    private void OnTrackPieceShutdown(Entity<BloonTrackPieceComponent> ent, ref ComponentShutdown args)
    {
        InvalidateUsingPiece(ent.Owner);
    }

    private void OnAnchorChanged(Entity<BloonTrackPieceComponent> ent, ref AnchorStateChangedEvent args)
    {
        ent.Comp.AnchoredVisual = args.Anchored;
        Dirty(ent);

        if (args.Anchored)
            return;

        InvalidateUsingPiece(ent.Owner);
    }

    private void InvalidateUsingPiece(EntityUid pieceUid)
    {
        var query = EntityQueryEnumerator<BalloonSpawnerComponent>();

        while (query.MoveNext(out var spawnerUid, out var spawner))
        {
            if (!spawner.ValidatedTrackPieces.Contains(pieceUid))
                continue;

            _spawner.InvalidateTrack(spawnerUid, spawner);
        }
    }

    public Direction? GetExitDirection(EntityUid uid, Direction incoming, BloonTrackPieceComponent? comp = null, TransformComponent? xform = null)
    {
        if (!Resolve(uid, ref comp, ref xform))
            return null;

        var rot = (4 - ToCardinalTurns(xform.LocalRotation)) % 4;

        return comp.PieceType switch
        {
            BloonTrackPieceType.Straight => GetStraightExit(incoming, rot),
            BloonTrackPieceType.LeftTurn => GetLeftTurnExit(incoming, rot),
            BloonTrackPieceType.RightTurn => GetRightTurnExit(incoming, rot),
            BloonTrackPieceType.Cross => GetCrossExit(incoming),
            _ => null
        };
    }

    private Direction GetCrossExit(Direction incoming)
    {
        return incoming;
    }

    private Direction? GetStraightExit(Direction incoming, int rot)
    {
        return rot switch
        {
            0 or 2 => incoming switch
            {
                Direction.East => Direction.East,
                Direction.West => Direction.West,
                _ => null
            },
            1 or 3 => incoming switch
            {
                Direction.North => Direction.North,
                Direction.South => Direction.South,
                _ => null
            },
            _ => null
        };
    }

    private Direction? GetLeftTurnExit(Direction incoming, int rot)
    {
        return rot switch
        {
            0 => incoming switch
            {
                Direction.East => Direction.North,
                Direction.South => Direction.West,
                _ => null
            },
            1 => incoming switch
            {
                Direction.South => Direction.East,
                Direction.West => Direction.North,
                _ => null
            },
            2 => incoming switch
            {
                Direction.West => Direction.South,
                Direction.North => Direction.East,
                _ => null
            },
            3 => incoming switch
            {
                Direction.North => Direction.West,
                Direction.East => Direction.South,
                _ => null
            },
            _ => null
        };
    }

    private Direction? GetRightTurnExit(Direction incoming, int rot)
    {
        return rot switch
        {
            0 => incoming switch
            {
                Direction.East => Direction.South,
                Direction.North => Direction.West,
                _ => null
            },
            1 => incoming switch
            {
                Direction.South => Direction.West,
                Direction.East => Direction.North,
                _ => null
            },
            2 => incoming switch
            {
                Direction.West => Direction.North,
                Direction.South => Direction.East,
                _ => null
            },
            3 => incoming switch
            {
                Direction.North => Direction.East,
                Direction.West => Direction.South,
                _ => null
            },
            _ => null
        };
    }

    private int ToCardinalTurns(Angle angle)
    {
        var degrees = (float) angle.Theta * 180f / MathF.PI;
        var turns = (int) MathF.Round(degrees / 90f) % 4;

        if (turns < 0)
            turns += 4;

        return turns;
    }
}
