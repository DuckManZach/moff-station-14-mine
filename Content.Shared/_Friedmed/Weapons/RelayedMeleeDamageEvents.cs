using Content.Shared.Weapons.Melee.Events;

namespace Content.Shared._Friedmed.Weapons;

[ByRefEvent]
public record struct RelayedGetMeleeDamageEvent(GetMeleeDamageEvent Args);

[ByRefEvent]
public record struct RelayedGetMeleeAttackRateEvent(GetMeleeAttackRateEvent Args);
