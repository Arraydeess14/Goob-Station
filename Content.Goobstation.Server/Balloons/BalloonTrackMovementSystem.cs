using Content.Goobstation.Shared.Balloons;
using Robust.Shared.Map;
using System.Numerics;
using Content.Goobstation.Server.Balloons;

namespace Content.Goobstation.Server.Balloons;

public sealed class BalloonTrackMovementSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly BloonTrackEndSystem _trackEnd = default!;
    [Dependency] private readonly BloonTrackSystem _track = default!;
    [Dependency] private readonly BalloonSpawnerSystem _spawner = default!;
    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<BalloonComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var balloon, out var xform))
        {
            if (balloon.MoveTarget == null || balloon.TravelDirection == null)
                continue;

            if (balloon.CurrentTrackPiece is { } piece && Deleted(piece))
            {
                balloon.CurrentTrackPiece = null;
                balloon.CurrentTrackEndTarget = null;
                balloon.MoveTarget = null;
                continue;
            }

            if (balloon.CurrentTrackEndTarget is { } end && Deleted(end))
            {
                balloon.CurrentTrackEndTarget = null;
                balloon.MoveTarget = null;
                continue;
            }

            var remainingMove = balloon.Speed * frameTime;

            while (remainingMove > 0f)
            {
                if (balloon.MoveTarget == null)
                    break;

                var target = balloon.MoveTarget.Value;
                var current = xform.Coordinates;

                if (current.EntityId != target.EntityId)
                    break;

                var delta = target.Position - current.Position;
                var distance = delta.Length();

                if (distance <= 0.01f)
                {
                    // Snap cleanly to target center first.
                    xform.Coordinates = target;

                    if (balloon.CurrentTrackEndTarget is { } trackEndUid &&
                      TryComp<BloonTrackEndComponent>(trackEndUid, out var trackEnd))
                    {
                        if (balloon.IsValidator)
                        {
                            if (balloon.ValidatorSpawner is { } spawnerUid)
                                _spawner.CompleteValidation(spawnerUid, balloon.ValidatedPieces);

                            balloon.MoveTarget = null;
                            balloon.CurrentTrackPiece = null;
                            balloon.CurrentTrackEndTarget = null;
                            QueueDel(uid);
                            break;
                        }

                        if (!balloon.ProcessedPop)
                        {
                            balloon.ProcessedPop = true;
                            _trackEnd.TakeLives(trackEndUid, balloon.Lives, trackEnd);
                        }

                        balloon.MoveTarget = null;
                        balloon.CurrentTrackPiece = null;
                        balloon.CurrentTrackEndTarget = null;
                        QueueDel(uid);
                        break;
                    }

                    AdvanceToNextTrackPiece(uid, ref balloon, xform);

                    if (balloon.MoveTarget == null)
                        break;

                    continue;
                }

                var moveAmount = MathF.Min(remainingMove, distance);
                var direction = Vector2.Normalize(delta);
                var newPos = current.Position + direction * moveAmount;

                xform.Coordinates = new EntityCoordinates(current.EntityId, newPos);
                remainingMove -= moveAmount;

                // If we exactly reached the target this iteration, loop again so leftover
                // movement can carry into the next track piece this same frame.
                if (moveAmount < distance)
                    break;
            }
        }
    }

    private void AdvanceToNextTrackPiece(EntityUid uid, ref BalloonComponent balloon, TransformComponent xform)
    {
        if (balloon.CurrentTrackPiece == null || balloon.TravelDirection == null)
            return;

        var currentTrackUid = balloon.CurrentTrackPiece.Value;

        if (Deleted(currentTrackUid) || !TryComp<BloonTrackPieceComponent>(currentTrackUid, out var trackComp))
        {
            balloon.CurrentTrackPiece = null;
            balloon.CurrentTrackEndTarget = null;
            balloon.MoveTarget = null;

            if (balloon.IsValidator)
                FailAndDeleteValidator(uid, balloon);

            return;
        }

        var incoming = balloon.TravelDirection.Value;

        var exitDirection = _track.GetExitDirection(currentTrackUid, incoming, trackComp);
        if (exitDirection == null)
        {
            balloon.CurrentTrackPiece = null;
            balloon.CurrentTrackEndTarget = null;
            balloon.MoveTarget = null;

            if (balloon.IsValidator)
                FailAndDeleteValidator(uid, balloon);

            return;
        }

        balloon.TravelDirection = exitDirection.Value;

        if (Deleted(currentTrackUid))
        {
            balloon.CurrentTrackPiece = null;
            balloon.CurrentTrackEndTarget = null;
            balloon.MoveTarget = null;

            if (balloon.IsValidator)
                FailAndDeleteValidator(uid, balloon);

            return;
        }

        var currentTrackCoords = Transform(currentTrackUid).Coordinates;

        Vector2 offset = exitDirection.Value switch
        {
            Direction.East => new Vector2(1f, 0f),
            Direction.West => new Vector2(-1f, 0f),
            Direction.North => new Vector2(0f, 1f),
            Direction.South => new Vector2(0f, -1f),
            _ => Vector2.Zero
        };

        var nextTile = currentTrackCoords.Offset(offset);

        EntityUid? bestTrack = null;
        EntityUid? bestEnd = null;
        float bestDist = float.MaxValue;

        var lookup = _lookup.GetEntitiesInRange(nextTile, 0.35f);

        foreach (var ent in lookup)
        {
            if (Deleted(ent))
                continue;

            var coords = Transform(ent).Coordinates;

            if (coords.EntityId != nextTile.EntityId)
                continue;

            var dist = (coords.Position - nextTile.Position).LengthSquared();

            if (TryComp<BloonTrackPieceComponent>(ent, out _))
            {
                if (dist < bestDist)
                {
                    bestTrack = ent;
                    bestEnd = null;
                    bestDist = dist;
                }
            }
            else if (TryComp<BloonTrackEndComponent>(ent, out _) &&
                     balloon.LinkedTrackEnd == ent)
            {
                if (dist < bestDist)
                {
                    bestTrack = null;
                    bestEnd = ent;
                    bestDist = dist;
                }
            }
        }

        if (bestTrack != null)
        {
            balloon.CurrentTrackPiece = bestTrack;
            balloon.CurrentTrackEndTarget = null;
            balloon.MoveTarget = Transform(bestTrack.Value).Coordinates;

            if (balloon.IsValidator)
            {
                var visitKey = (bestTrack.Value, balloon.TravelDirection!.Value);

                if (balloon.ValidatedVisits.Contains(visitKey))
                {
                    FailAndDeleteValidator(uid, balloon);
                    return;
                }

                balloon.ValidatedVisits.Add(visitKey);
                balloon.ValidatedPieces.Add(bestTrack.Value);
                balloon.ValidatorStuckTimer = 0f;
                balloon.LastValidatorPosition = xform.Coordinates.Position;
            }
            return;
        }

        if (bestEnd != null)
        {
            balloon.CurrentTrackPiece = null;
            balloon.CurrentTrackEndTarget = bestEnd;
            balloon.MoveTarget = Transform(bestEnd.Value).Coordinates;

            if (balloon.IsValidator)
            {
                balloon.ValidatorStuckTimer = 0f;
                balloon.LastValidatorPosition = xform.Coordinates.Position;
            }

            return;
        }

        balloon.CurrentTrackPiece = null;
        balloon.CurrentTrackEndTarget = null;
        balloon.MoveTarget = null;

        if (balloon.IsValidator)
            FailAndDeleteValidator(uid, balloon);
    }
    private void FailAndDeleteValidator(EntityUid uid, BalloonComponent balloon)
    {
        if (balloon.ValidatorSpawner is { } spawnerUid)
            _spawner.FailValidation(spawnerUid);

        QueueDel(uid);
    }
}
