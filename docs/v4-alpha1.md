# v4.0 Dynamic Loot Engine (Alpha 1)

Development branch: `feature/v4-dynamic-loot-engine`.

## Implemented foundation

`ItemCatalog.cs` introduces a campaign-independent catalog with:
- Four default source categories: SRD, Open5e, third-party, and custom, all enabled in settings.
- An explicit `Reviewed` flag on each source. Enabled does **not** mean content is licensed or approved; only enabled and reviewed sources are eligible.
- Item identity, category, rarity, ruleset, source, optional description, URL, license, and minimum party level.
- Source-scoped imports, duplicate ID validation, party-level/category eligibility, and JSON persistence with backup.

No online source is downloaded automatically. No third-party content is bundled. Source verification and license review must precede activation.

## Remaining Alpha 1 work

1. Build SRD and Open5e adapters, including provenance and license metadata.
2. Implement CSV/Excel/JSON import mapping and review screens.
3. Add a Source Manager Windows Forms screen, catalog browsing, and sync status.
4. Add tests for source gating, import replacement, serialization, and malformed input.
5. Implement the six-tier D6 → D100 table generator, party-level weighting, and locked tables in Alpha 2.
6. Preserve legacy spreadsheet mode and shared history during UI integration.

The existing v3.1 app remains the default runtime until these pieces are integrated. This commit is the model/storage foundation, not a complete v4 application.
