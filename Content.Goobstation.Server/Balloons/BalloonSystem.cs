using Content.Goobstation.Shared.Balloons;
using Content.Server.Stack;
using Content.Shared.Destructible;
using Content.Shared.Stacks;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

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

        var coords = Transform(ent).Coordinates;

        foreach (var proto in ent.Comp.SpawnOnPop)
        {
            var spawned = Spawn(proto, coords);

            if (!TryComp<BalloonComponent>(spawned, out var child))
                continue;
            // have the baby bloons inherit from their papa
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
