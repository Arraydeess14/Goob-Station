using Content.Goobstation.Shared.Balloons;
using Content.Server.Stack;
using Content.Shared.Destructible;
using Content.Shared.Stacks;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using System.Numerics;

namespace Content.Goobstation.Server.Balloons;

public sealed class BalloonSystem : EntitySystem
{
    [Dependency] private readonly StackSystem _stack = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly BloonTrackEndSystem _trackEnd = default!;
    public override void Initialize()
    {
        SubscribeLocalEvent<BalloonComponent, DestructionEventArgs>(OnBalloonDestroyed);
    }

    // When Balloon is popped
    private void OnBalloonDestroyed(Entity<BalloonComponent> ent, ref DestructionEventArgs args)
    {
        if (ent.Comp.ProcessedPop)
            return;

        ent.Comp.ProcessedPop = true;

        HandleCashReward(ent);
        HandleChildSpawns(ent);
    }

    // Spawn cash
    private void HandleCashReward(Entity<BalloonComponent> ent)
    {
        var amount = _random.Next(ent.Comp.MinCash, ent.Comp.MaxCash + 1);
        if (amount <= 0)
            return;

        if (ent.Comp.LinkedTrackEnd is { } trackEndUid &&
            !Deleted(trackEndUid) &&
            TryComp<BloonTrackEndComponent>(trackEndUid, out _))
        {
            _trackEnd.AddCash(trackEndUid, amount);
            return;
        }

        SpawnOrMergeCash(ent, amount);
    }

    // Spawn Balloons
    private void HandleChildSpawns(Entity<BalloonComponent> ent)
    {
        if (ent.Comp.SpawnOnPop.Count == 0)
            return;

        var parentCoords = Transform(ent).Coordinates;
        var parentPos = parentCoords.Position;

        var backward = ent.Comp.TravelDirection switch
        {
            Direction.East => new Vector2(-1f, 0f),
            Direction.West => new Vector2(1f, 0f),
            Direction.North => new Vector2(0f, -1f),
            Direction.South => new Vector2(0f, 1f),
            _ => Vector2.Zero
        };

        const float spacing = 0.30f; // Spacing for children balloons

        for (var i = 0; i < ent.Comp.SpawnOnPop.Count; i++)
        {
            var proto = ent.Comp.SpawnOnPop[i];
            var offsetPos = parentPos + backward * (spacing * (i + 1));
            var spawnCoords = new EntityCoordinates(parentCoords.EntityId, offsetPos);

            var spawned = Spawn(proto, spawnCoords);

            if (!TryComp<BalloonComponent>(spawned, out var child))
                continue;

            child.LinkedTrackEnd = ent.Comp.LinkedTrackEnd;
            child.CurrentTrackPiece = ent.Comp.CurrentTrackPiece;
            child.TravelDirection = ent.Comp.TravelDirection;
            child.MoveTarget = ent.Comp.MoveTarget;
            child.CurrentTrackEndTarget = ent.Comp.CurrentTrackEndTarget;
        }
    }
    // handle where cash goes
    private void SpawnOrMergeCash(Entity<BalloonComponent> ent, int amount)
    {
        var coords = Transform(ent).Coordinates;

        EntityUid? foundCash = null;

        foreach (var uid in _lookup.GetEntitiesInRange(coords, ent.Comp.MergeRange))
        {
            if (uid == ent.Owner)
                continue;

            if (!TryComp<StackComponent>(uid, out var stack))
                continue;

            if (stack.StackTypeId != ent.Comp.CashStackType)
                continue;

            foundCash = uid;
            break;
        }

        if (foundCash is { } cashUid)
        {
            if (TryComp<StackComponent>(cashUid, out var stack))
                _stack.SetCount(cashUid, stack.Count + amount, stack);

            return;
        }

        var spawned = Spawn(ent.Comp.CashPrototype, coords);

        if (TryComp<StackComponent>(spawned, out var newStack))
            _stack.SetCount(spawned, amount, newStack);
    }
}
