# TSA Prison Commons

**Fork** of [Prison Commons](https://steamcommunity.com/sharedfiles/filedetails/?id=2630896782) by **Ben Lubar**, as continued by **Mlie** ([emipa606/PrisonCommons](https://github.com/emipa606/PrisonCommons)).

This TSA fork keeps the MIT license and adds RimWorld **1.7** behaviour (private cells, commons hangout/recreation, recruiting, air/temperature through bars, etc.). See [CREDITS.md](CREDITS.md).

**GitHub only** — not on Steam Workshop. Do **not** run alongside `Mlie.PrisonCommons`.

## What it does (simple)

1. **Commons + tiny cells** — Prisoners sleep in small private cells and share a nicer commons (tables, art, recreation). Mark commons stuff as *Prison Commons*. Separate cells from commons with **ReBuild cell doors / cell walls**.
2. **Pretty cells without huge rooms** — Tiny cells **borrow beauty, space, wealth, and cleanliness** from the adjacent commons, so impressiveness isn’t stuck on a bare alcove.
3. **Fewer riots / breaks** — Proper private cells (small owned bed rooms next to the commons) **cut prison-break chance** a lot (about 2.5× longer between breaks).
4. **Faster recruiting** — A nicer prison and better prisoner mood **speed up recruiting** (mood can push resistance reduction higher than vanilla).
5. **Recreation helps** — By day, prisoners hang out in the commons and can play chess/poker/etc. for a short mood boost — which also feeds into recruiting.
6. **Food & bathrooms** — Hungry prisoners can use commons food. Mark DBH fixtures as Prison Commons so they can use bathrooms, then leave. Cell bars keep prison “rooms” separate but still share temperature (and gas) with the commons.

## Requirements

- [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077)
- [ReBuild: Doors and Corners](https://steamcommunity.com/sharedfiles/filedetails/?id=3262718980) (**required** — cell doors / cell walls for private cells)

Optional soft support: Dubs Bad Hygiene, Resurrectionem / VFE cell doors.

## Install

Clone or copy into RimWorld `Mods`, enable **after** Harmony and ReBuild: Doors and Corners.

## Build

```bash
dotnet build Source/TSA_PrisonCommons.csproj -c Release
```

Output: `1.7/Assemblies/TSA_PrisonCommons.dll`

## License

MIT — [LICENSE.md](LICENSE.md). Original work © Ben Lubar; continued by Mlie; TSA fork changes also under MIT.
