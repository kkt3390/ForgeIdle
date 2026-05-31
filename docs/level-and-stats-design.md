# Level and Stats Draft

## Goals

- Weapon enhancement remains the main long-term progression system.
- Levels reward both automatic and manual hunting without replacing enhancement.
- Stats provide small choices and visible progress without creating a mandatory build.

## Level System

- Initial level cap: 100
- Stat points: 1 point per level gained
- Automatic hunting grants gold and experience together.
- Manual hunting grants 150% gold efficiency and 125% experience efficiency compared with automatic hunting.
- Level progress is permanent when a weapon is destroyed.

Suggested experience curve:

```text
Required experience = round(100 * 1.12 ^ (current level - 1))
```

The exact area experience rewards should be tuned after enhancement timing simulation.

## Stats

Each stat has an initial cap of 20 points. Reset is allowed with gold so players can experiment.

| Stat | Effect per point | Maximum effect | Notes |
|---|---:|---:|---|
| Dual Wield | Manual hunts have a 0.5% chance to repeat once | 10% | The repeated hunt grants gold and experience. It cannot recursively trigger. |
| Gold Gain | Gold earned from hunting increases by 1% | 20% | Applies to automatic and manual hunting. |
| Experience Gain | Experience earned from hunting increases by 1% | 20% | Applies to automatic and manual hunting. |
| Artisan's Touch | Enhancement success chance is multiplied by 0.5% | 10% multiplier | A 1% success rate becomes 1.1%, not 2%. The increase is taken from the keep probability. Destruction probability does not change. |
| Recovery | After destruction, gain 2% bonus automatic-hunt gold for 30 minutes | 40% | Gives a small recovery feeling without weakening destruction itself. |

## Why Artisan's Touch Uses Multiplication

Adding a flat percentage point is too strong at high enhancement levels:

```text
Flat +1 percentage point: 1% -> 2% success, a 100% improvement
Multiplicative +10%:      1% -> 1.1% success, a 10% improvement
```

The stat should feel pleasant without making enhancement mandatory or invalidating the published rate table.

## Recommended First Release

Start with four stats:

- Dual Wield
- Gold Gain
- Experience Gain
- Artisan's Touch

Keep Recovery as a later addition if destruction feels too punishing in play tests.
