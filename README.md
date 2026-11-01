# D&D Loot Roller v3.1 — Shared loot history

## Build and use

1. Extract the ZIP to a writable folder.
2. In the packaged v3.1 ZIP, open `Windows-x64\DNDLootRoller.exe` (Windows x64; no separate .NET install required). When using this source repository, double-click **Publish-Windows-x64.bat** to build the app. Requires the .NET 8 SDK / Visual Studio's **.NET desktop development** workload. The publisher opens the folder containing `DNDLootRoller.exe` when successful.
3. Launch the app. Click **Import Spreadsheet…** and select an `.xlsx` workbook.
4. Review the warnings, worksheet-to-D6 mapping, loot rows, and subtables. Click **Import** (or **Import with warnings**) to activate it. Cancel leaves your current tables intact.
5. Roll D6, choose a table manually, roll or enter D100, or click **Roll Both**, as before. For nested loot, click the **Roll D8/D12 Subtable** button, as in the original project.

The **Active** label identifies the workbook. **Data Issues** always describes the active data. **Use Original Workbook** restores the embedded tables and saves that choice. Shared History retains records across restarts and workbook changes; the main window list shows recent rolls from this session.

## Clickable D&D Beyond item links

After rolling an item, click **View item on D&D Beyond** beneath the result to open your default browser. Roll a nested subtable first to get the final item's link. Currency and missing results have no link. Long loot results can be scrolled and selected.

The bundled catalog covers every named result in the original workbook. Some variants (armor bonuses/types, figurines, instruments, healing potion strengths) open the shared item-family page containing their rules. The loot text retains the exact variant you rolled. Links were collected from the public D&D Beyond 2014 Basic Rules and item catalog on September 12, 2026; pages may show legacy or updated rules as maintained by D&D Beyond.

For imported workbooks, known item names receive catalog links automatically. Unrecognized names show **Search for this item on D&D Beyond** instead. You can specify an exact version or variant by pasting its full HTTPS D&D Beyond item URL into optional **column C**, on either the main row or a subtable row. Leave C blank for automatic matching. Enter the URL as plain cell text, not a HYPERLINK formula or a hyperlink hidden behind display text. Columns A and B stay the same. Existing saved imports remain compatible, and imported URLs persist with the tables.

Links open only when clicked. Rolling still works offline; viewing D&D Beyond needs internet access and follows that website's normal sign-in/content access requirements. The app stores names and URLs, not copied item rules.

Catalog sources:
- https://www.dndbeyond.com/sources/dnd/basic-rules-2014/magic-items
- https://www.dndbeyond.com/magic-items?filter-source=2
- Name-filtered pages in the same public magic-item catalog.

## Spreadsheet format

Use the included `Loot Table.xlsx` as a starting point; save your changes under a new filename.

- Exactly **six worksheet tabs**. Their left-to-right order maps to D6 results 1–6; tab names can be anything. Hidden tabs count and are flagged in the preview.
- **Column A:** a roll number (`1`, `100`) or inclusive range (`01-15`, `16–20`). Hyphens, en dashes, and em dashes are accepted. Values must be within 1–100. Use `100`, not `00`.
- **Column B:** the loot description, or a numeric reward such as `50`.
- A header is optional. Use `Roll` / `Loot`, `D100` / `Description`, or `Range` / `Item`.
- Empty rows are skipped. Column C is an optional D&D Beyond item-page URL; columns after C are ignored.
- Format range cells as **Text** before typing to prevent Excel turning them into dates. Formula cells in A/B/C are rejected: copy and paste as values first. Other formatting is not imported.
- Only `.xlsx` is supported. Convert `.xls`, `.xlsm`, CSV, or password-protected files to an ordinary unencrypted `.xlsx` first. Excel does not need to be installed to run the app.
- Workbook size is limited to 32 MB uncompressed and 2,000 ZIP parts.

Example rows:

| A: Roll | B: Loot |
|---|---|
| 1-50 | 50 gold pieces |
| 51-99 | Potion of healing |
| 100 | Figurine (roll d8) |
| - | 1-4: Bronze griffon |
| - | 5-8: Onyx dog |

Subtable rows immediately follow their parent: column A contains `-`; column B contains a sub-roll range, colon, and description. Parent descriptions include `(roll d8)` or `(roll d12)`; dice D2–D100 are supported. The original workbook's `roll dl2` spelling is also supported. Only one nested level is supported.

## Validation and saved imports

Malformed ranges, blank descriptions, formula/error cells, empty tables, invalid tab counts, and malformed or missing subtables block import. The error identifies the worksheet/row where possible. Fix the spreadsheet and import again.

Coverage gaps and overlapping ranges are warnings requiring **Import with warnings**. Gaps display “No loot entry”; overlaps use the first matching row. The original Table 2 overlap at 27 and Table 3 gap at 33–35 are preserved.

The app saves one active imported dataset to `%LOCALAPPDATA%\DNDLootRoller\active-loot.json`. This is a snapshot of the data, not a live link: moving/deleting the source spreadsheet does not affect it. To pick up spreadsheet edits or switch to another workbook, import again. A new import replaces the saved dataset only after validation, review, and a successful save. The source workbook is never modified.

If saved data is damaged or unreadable, the app shows a warning and starts with the original tables. Import again or use **Use Original Workbook** to recover. Shared history is stored separately and is unaffected by restoring the original workbook.

## Developer details and verification

Open `DNDLootRoller.csproj` in Visual Studio, or use the included publisher. No additional NuGet libraries were introduced. Import reads XLSX XML values using the .NET compression and XML APIs; it does not execute macros or evaluate formulas.

From the project directory, run the regression checks with:

```
dotnet run --project Tests/ImportTests.csproj -- .
```

Validation for v3.1: Release build and self-contained Windows x64 publish succeeded with zero warnings/errors. 1,032 regression assertions passed, covering all 600 original D6/D100 outcomes, subtables, import validation, history save/reload, combined inclusive filters, correction/deletion, backups, corruption protection and Excel text export. Main and history windows opened and initialized successfully in a Windows Forms control smoke check. A full manual visual walkthrough has not been performed.

## Shared history in v3.1

- Before rolling, type a player name or select one previously recorded. Blank names are recorded as `Unassigned`.
- `Use D100`, `Roll D100` and `Roll Both` automatically save the timestamp with time-zone offset, player, workbook filename, worksheet name, D6, D100 and loot description. Missing loot results are saved too. Rolling only D6 does not award loot and does not create a record.
- Subtable rolls create additional records containing the parent D6/D100, sub die, sub roll and full loot result. The original parent record remains, so repeated subtable rolls are visible.
- Open **Shared History…** for the persistent records across every campaign/workbook. Select a player and/or spreadsheet, and check either date box to limit dates. Both endpoints are inclusive and use this computer's local dates. Blank player/spreadsheet filters mean all records. **Refresh** reads current saved data.
- **Correct selected…** edits the chosen record's timestamp, player, workbook, sheet, dice and description. **Delete selected…** asks before removing that record. Changes are saved immediately; use Refresh to see changes made by another open app instance.
- **Export filtered Excel…** writes only the currently filtered records to a real `.xlsx` file, with headers even for an empty selection. Values are written as literal text to preserve descriptions and avoid interpreting player text as formulas. The source loot workbook is never changed.
- Spreadsheet identity is its filename, matching v3.0's imported snapshot. Use distinct filenames for campaign workbooks if you need distinct spreadsheet filters.

History lives at `%LOCALAPPDATA%\DNDLootRoller\loot-history.json` for this Windows user, shared by all copies of the app on this computer. It is independent of `active-loot.json` and is not cloud-synced. Each successful change keeps the previous file as `loot-history.json.bak`. Writes are atomic and coordinated across app instances. A save failure is shown explicitly; unreadable history is never silently replaced.

To back up or move history, close the app and copy the history JSON (and optionally its backup). To recover a damaged history file, close every app instance, preserve the damaged file elsewhere, and copy a known-good backup to `loot-history.json`. Player choices are derived from saved records. v3.0 session-only history cannot be recovered after the old app was closed.

Suggested manual acceptance check: record rolls for two players in the original and an imported workbook, restart, verify each filter and both inclusive date endpoints, correct and delete a record, export a filtered selection and open it in Excel. Then test Table 3 / 76 and Table 5 / 12 subtables and restore the original workbook.
