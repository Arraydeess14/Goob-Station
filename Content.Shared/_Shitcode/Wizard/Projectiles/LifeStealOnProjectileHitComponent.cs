// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Aviu00 <93730715+Aviu00@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Misandry <mary@thughunt.ing>
// SPDX-FileCopyrightText: 2025 gus <august.eymann@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Whitelist;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Goobstation.Wizard.Projectiles;

[RegisterComponent, NetworkedComponent]
public sealed partial class LifeStealOnProjectileHitComponent : Component
{
    [DataField]
    public EntityWhitelist Whitelist;

    [DataField]
    public FixedPoint2 LifeStealAmount = 20;

    [DataField]
    public FixedPoint2 BloodStealAmount = 25;

    [DataField]
    public EntProtoId Effect = "SanguineBloodEffect";
}
