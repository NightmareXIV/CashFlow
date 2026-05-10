# CashFlow

A [Dalamud](https://github.com/goatcorp/Dalamud) plugin for **Final Fantasy XIV** that logs and analyzes your in-game financial activity — trades, market board purchases, retainer sales, NPC transactions, and gil history.

> **Fork notice:** This is a fork of [NightmareXIV/CashFlow](https://github.com/NightmareXIV/CashFlow) with additional features focused on buy/resell analytics, improved exports, and quality-of-life improvements.

## Features

### Transaction Logging

All financial activity is automatically recorded to a local SQLite database:

- **Trade Log** — Player-to-player trades (gil and items, with multi-item support)
- **Purchase Log** — Market board purchases (with 5% tax tracking)
- **Sale Log** — Retainer sales on the market board
- **NPC Sale Log** — Items sold to NPC vendors
- **NPC Purchase Log** — Items bought from NPC vendors (including buybacks)
- **Gil History** — Daily snapshots of total gil (player + retainers) with graph, averages, and top-5 rankings

### Buy/Resell Analytics

A dedicated **Buy/Resell** tab provides analytics exclusively for market board flipping — only items you purchased on the MB and then resold are counted:

- **Summary** — Total purchases, total sales, net profit, global ROI%, distinct items, fully sold count, estimated remaining stock value
- **Items** — Sortable table of all purchased items with average buy/sell prices, margin, ROI%, profit, and remaining quantity (includes unsold items to track pending stock)
- **Per Server** — Cross-references Purchase Log, Trade Log, and Sale Log to show inventory status per server (useful for cross-world reselling via alts)
- **Retainers** — Per-retainer performance (revenue, sales count, average sale price, top item) filtered to resold items only
- **Date Presets** — Quick filters: 7d, 30d, 90d, 1y, All

### Export

Every tab supports exporting data in two formats:

- **CSV** — With UTF-8 BOM for proper Excel compatibility
- **XLSX** — Via [ClosedXML](https://github.com/ClosedXML/ClosedXML) with styled headers (bold, colored), frozen header row, auto-filter, auto-width columns, and proper numeric typing
- **Export All** — One-click export of all tabs into a single multi-sheet XLSX file
- **Server Inventory Export** — Dedicated CSV/XLSX export from the Per Server view
- **Chat Notification** — Confirmation message in Dalamud chat after each export

### Row Management

All log tabs include per-row actions:

- **Hide** — Temporarily hide rows (persists across tab switches, resets on filter change)
- **Delete** — Permanently remove records from the SQLite database (with confirmation popup)
- **Row Hover Highlight** — Visual feedback when hovering over table rows
- **Hidden Row Toggle** — Button in the filter bar to show/hide hidden rows with count

### Settings

- Records per page (1,000–10,000)
- Merge sequential gil-only trades
- Date format (Month/Day or Day.Month)
- Trade history overlay during trades
- Name censoring with configurable seed
- UTC/local time display with custom format support
- Fixed graph start date
- **Exclude hidden rows from Buy/Resell analytics** — When enabled, hidden rows in Purchase Log, Sale Log, and Trade Log are excluded from Buy/Resell calculations

## Installation

### Prerequisites

- [FFXIV with Dalamud](https://goatcorp.github.io/faq/) installed
- .NET 10 SDK (for building from source)

### From Release

1. In Dalamud settings (`/xlsettings`), go to **Experimental**
2. Add the plugin DLL output path to **Dev Plugin Locations**
3. Load the plugin from `/xlplugins`

### Building from Source

```bash
git clone --recurse-submodules https://github.com/Ngapi/CashFlow.git
cd CashFlow
dotnet build CashFlow/CashFlow.csproj -c Release
```

The compiled plugin will be at `CashFlow/bin/Release/CashFlow.dll`.

> **Note:** The [NightmareUI](https://github.com/NightmareXIV/NightmareUI) submodule must be initialized. If you cloned without `--recurse-submodules`, run:
> ```bash
> git submodule update --init --recursive
> ```

## Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| [ClosedXML](https://github.com/ClosedXML/ClosedXML) | 0.105.0 | XLSX export |
| [CsvHelper](https://joshclose.github.io/CsvHelper/) | 33.1.0 | CSV export |
| [MessagePack](https://github.com/MessagePack-CSharp/MessagePack-CSharp) | 3.1.4 | Item serialization |
| [Microsoft.Data.Sqlite](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/) | 10.0.1 | Local database |
| [SqlKata](https://sqlkata.com/) | 4.0.1 | Query builder |
| [ECommons](https://github.com/NightmareXIV/ECommons) | 3.2.0.6 | Dalamud utilities |
| [NightmareUI](https://github.com/NightmareXIV/NightmareUI) | submodule | UI components |

## Database

All data is stored locally in `DefaultDbV2.sqlite` inside the plugin's config directory. Tables:

- `CidMap` — Player name/world mapping
- `Trades` — Player-to-player trades (items stored as MessagePack blob)
- `RetainerSales` — Market board retainer sales
- `ShopPurchases` — Market board purchases
- `NpcPurchases` — NPC vendor purchases
- `NpcSales` — NPC vendor sales
- `GilRecords` — Daily gil snapshots

## Credits

- Original plugin by [NightmareXIV / Limiana](https://github.com/NightmareXIV), Asuna, and Klia
- Fork enhancements by [Ngapi](https://github.com/Ngapi)

## License

This project follows the same license as the original [CashFlow](https://github.com/NightmareXIV/CashFlow) repository.
