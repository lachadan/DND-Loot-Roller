# D&D Loot Roller v3.0 — D&D Beyond links edition

## Build and use

1. Extract the ZIP to a writable folder.
2. Double-click **Publish-Windows-x64.bat**. Requires the .NET 8 SDK / Visual Studio's **.NET desktop development** workload. The publisher opens the folder containing `DNDLootRoller.exe` when successful.
3. Launch the app. Click **Import Spreadsheet…** and select an `.xlsx` workbook.
4. Review the warnings, worksheet-to-D6 mapping, loot rows, and subtables. Click **Import** (or **Import with warnings**) to activate it. Cancel leaves your current tables intact.
5. Roll D6, choose a table manually, roll or enter D100, or click **Roll Both**, as before. For nested loot, click the **Roll D8/D12 Subtable** button, as in the original project.

The **Active** label identifies the workbook. **Data Issues** always describes the active data. **Use Original Workbook** restores the embedded tables and saves that choice. History remains available for the current session, with a separator whenever the workbook changes.

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

If saved data is damaged or unreadable, the app shows a warning and starts with the original tables. Import again or use **Use Original Workbook** to recover. Roll history is session-only.

## Developer details and verification

Open `DNDLootRoller.csproj` in Visual Studio, or use the included publisher. No additional NuGet libraries were introduced. Import reads XLSX XML values using the .NET compression and XML APIs; it does not execute macros or evaluate formulas.

From the project directory, run the regression checks with:

```
dotnet run --project Tests/ImportTests.csproj -- .
```

The regression suite was compiled and run with the installed .NET SDK: 1,021 assertions passed. It checks all 600 original D6/D100 outcomes, D8/D12 results, persistence replacement/reloading, inline text, original warnings, and malformed imports.

The Windows Forms application was successfully published as a self-contained Windows x64 executable using .NET SDK 10.0.401, targeting .NET 8. A startup process check passed. An additional 11 Windows Forms control checks passed for link clearing, item/subtable changes, currency, missing results, and the scrollable output. Full visual interaction testing has not been performed. The standalone executable is provided separately in DND_Loot_Roller_v3.0_Windows.zip.

Suggested Windows smoke check after building: import the included workbook, accept its two warnings, resolve Table 2 / 27 and Table 3 / 33, try the Table 3 / 76 and Table 5 / 12 subtable buttons, restart to verify persistence, cancel a second import, and restore the original workbook.

