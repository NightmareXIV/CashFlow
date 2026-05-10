using CashFlow.Data;
using CashFlow.Data.ExplicitStructs;
using CashFlow.Data.LegacyDescriptors;
using CashFlow.Data.SqlDescriptors;
using Dapper;
using ECommons.ChatMethods;
using ECommons.GameHelpers;
using MessagePack;
using SqlKata.Execution;

namespace CashFlow.DataProvider.Sqlite;
public unsafe class SqliteDataProvider : DataProviderBase
{
    public SqliteDataProvider()
    {
        using var db = new GilsightQueryFactory();
        db.Connection.Execute($"""
                CREATE TABLE IF NOT EXISTS {Tables.CidMap} (
                    Cid       NUMERIC UNIQUE,
                    Name      TEXT,
                    HomeWorld INTEGER
                );
                """);
        db.Connection.Execute($"""
                CREATE TABLE IF NOT EXISTS {Tables.Trades} (
                    Cid      NUMERIC,
                    Self     NUMERIC,
                    UnixTime NUMERIC,
                    Gil      INTEGER,
                    Items    BLOB
                );
                """);
        db.Connection.Execute($"""
                CREATE TABLE IF NOT EXISTS {Tables.RetainerSales} (
                    Cid       NUMERIC,
                    RetainerName      TEXT,
                    Item         INTEGER,
                    Price        INTEGER,
                    Quantity     INTEGER,
                    IsMannequinn INTEGER,
                    UnixTime         NUMERIC,
                    Buyer        TEXT
                );
                """);
        db.Connection.Execute($"""
                CREATE TABLE IF NOT EXISTS {Tables.ShopPurchases} (
                    Cid       NUMERIC,
                    RetainerName      TEXT,
                    Item         INTEGER,
                    Price        INTEGER,
                    Quantity     INTEGER,
                    IsMannequinn INTEGER,
                    UnixTime         NUMERIC
                );
                """);
        db.Connection.Execute($"""
                CREATE TABLE IF NOT EXISTS {Tables.NpcPurchases} (
                    Cid       NUMERIC,
                    Item         INTEGER,
                    Price        INTEGER,
                    Quantity     INTEGER,
                    IsBuyback INTEGER,
                    UnixTime         NUMERIC
                );
                """);
        db.Connection.Execute($"""
                CREATE TABLE IF NOT EXISTS {Tables.NpcSales} (
                    Cid       NUMERIC,
                    Item         INTEGER,
                    Price        INTEGER,
                    Quantity     INTEGER,
                    UnixTime         NUMERIC
                );
                """);
        db.Connection.Execute($"""
                CREATE TABLE IF NOT EXISTS {Tables.GilRecords} (
                    Cid       NUMERIC,
                    GilPlayer         NUMERIC,
                    GilRetainer        NUMERIC,
                    UnixTime         NUMERIC
                );
                """);
    }

    public override Sender? GetPlayerInfo(ulong CID)
    {
        using var db = new GilsightQueryFactory();
        var longCid = *(long*)&CID;
        var result = db.Query(Tables.CidMap).Where("Cid", longCid).Get();
        if(result.Any())
        {
            var f = result.First();
            return new(f.Name, (uint)f.HomeWorld);
        }
        return null;
    }

    public override void PurgeAllRecords(ulong CID)
    {
        var cidLong = *(long*)&CID;
        S.WorkerThread.ClearAndEnqueue(() =>
        {
            using var db = new GilsightQueryFactory();
            foreach(var table in (string[])[Tables.Trades, Tables.GilRecords, Tables.NpcSales, Tables.NpcPurchases, Tables.RetainerSales, Tables.ShopPurchases])
            {
                var result = db.Query(table).Where("Cid", "=", cidLong).Delete();
                PluginLog.Information($"Removed {result} entries from {table}");
            }
        });
    }

    public override List<TradeDescriptor> GetTrades(long unixTimeMsMin = 0, long unixTimeMsMax = 0)
    {
        var ret = new List<TradeDescriptor>();
        using var db = new GilsightQueryFactory();
        var result = db.Query(Tables.Trades);
        if(unixTimeMsMin > 0) result = result.Where("UnixTime", ">=", unixTimeMsMin);
        if(unixTimeMsMax > 0) result = result.Where("UnixTime", "<=", unixTimeMsMax);
        foreach(var x in result.Get())
        {
            try
            {
                long cid = x.Cid;
                long self = x.Self;
                ret.Add(new()
                {
                    TradePartnerCID = *(ulong*)&cid,
                    CidUlong = *(ulong*)&self,
                    ReceivedGil = (int)x.Gil,
                    ReceivedItems = x.Items == null ? null : MessagePackSerializer.Deserialize<ItemWithQuantity[]>(x.Items),
                    UnixTime = x.UnixTime,
                });
            }
            catch(Exception e)
            {
                PluginLog.Error($"Error processing record:");
                e.Log();
            }
        }
        return ret;
    }

    public override void RecordIncomingTrade(TradeDescriptor descriptor)
    {
        var self = Player.CID;
        if(C.Blacklist.Contains(self)) return;
        var longSelf = *(long*)&self;
        S.WorkerThread.Enqueue(() =>
        {
            using var db = new GilsightQueryFactory();
            var cid = descriptor.TradePartnerCID;
            db.Query(Tables.Trades).Insert([
                new("Cid", *(long*)&cid),
                new("Self", longSelf),
                new("UnixTime", DateTimeOffset.Now.ToUnixTimeMilliseconds()),
                new("Gil", descriptor.ReceivedGil),
                new("Items", descriptor.ReceivedItems.Length == 0?null:MessagePackSerializer.Serialize(descriptor.ReceivedItems))
                ]);
            S.MainWindow.TabTradeLog.NeedsUpdate = true;
        });
    }

    public override void RecordPlayerCID(string playerName, uint homeWorld, ulong CID)
    {
        var longCid = *(long*)&CID;
        S.WorkerThread.Enqueue(() =>
        {
            using var db = new GilsightQueryFactory();
            if(db.Query(Tables.CidMap).Where("Cid", longCid).Exists())
            {
                db.Query(Tables.CidMap).Where("Cid", longCid).Update([new("Name", playerName), new("HomeWorld", (int)homeWorld)]);
            }
            else
            {
                db.Query(Tables.CidMap).Insert([new("Cid", longCid), new("Name", playerName), new("HomeWorld", (int)homeWorld)]);
            }
        });
    }

    public override Dictionary<ulong, Sender> GetRegisteredPlayers()
    {
        var ret = new Dictionary<ulong, Sender>();
        using var db = new GilsightQueryFactory();
        foreach(var x in db.Query(Tables.CidMap).Get())
        {
            var cid = (long)x.Cid;
            var name = (string)x.Name;
            var homeWorld = (uint)x.HomeWorld;
            ret[*(ulong*)&cid] = new(name, homeWorld);
        }
        return ret;
    }

    public override void RecordRetainerHistory(List<RetainerHistoryData> trades, ulong CID, string retainerName)
    {
        if(C.Blacklist.Contains(Player.CID)) return;
        S.WorkerThread.Enqueue(() =>
        {
            var cid = CID;
            var ret = new Dictionary<ulong, Sender>();
            using var db = new GilsightQueryFactory();
            var minTime = trades.Min(x => x.UnixTimeSeconds);
            var existing = GetRetainerHistory(minTime);
            //PluginLog.Information($"ExistingCnt: {existing}");
            foreach(var x in trades)
            {
                var desc = new RetainerSaleDescriptor()
                {
                    BuyerName = x.BuyerName,
                    CidUlong = cid,
                    IsMannequinn = x.IsMannequinn,
                    ItemID = (int)x.ItemID + (x.IsHQ ? 1000000 : 0),
                    Price = (int)x.Price,
                    Quantity = (int)x.Quantity,
                    RetainerName = retainerName,
                    UnixTime = x.UnixTimeSeconds * 1000L,
                };
                if(!existing.Any(s => s.Equals(desc)))
                {
                    /*Cid       NUMERIC,
                        RetainerName      TEXT,
                        Item         INTEGER,
                        Price        INTEGER,
                        Quantity     INTEGER,
                        IsMannequinn INTEGER,
                        UnixTime         NUMERIC,
                        Buyer        TEXT*/
                    db.Query(Tables.RetainerSales).Insert([
                        new("Cid", *(long*)&cid),
                        new("RetainerName", retainerName),
                        new("Item", desc.ItemID),
                        new("Price", desc.Price),
                        new("Quantity", desc.Quantity),
                        new("IsMannequinn", desc.IsMannequinn),
                        new("UnixTime", desc.UnixTime),
                        new("Buyer", desc.BuyerName),
                    ]);
                }
            }
        });
    }

    public override List<RetainerSaleDescriptor> GetRetainerHistory(long unixTimeMsMin = 0, long unixTimeMsMax = 0)
    {
        var ret = new List<RetainerSaleDescriptor>();
        using var db = new GilsightQueryFactory();
        var result = db.Query(Tables.RetainerSales);
        if(unixTimeMsMin > 0) result = result.Where("UnixTime", ">=", unixTimeMsMin);
        if(unixTimeMsMax > 0) result = result.Where("UnixTime", "<=", unixTimeMsMax);
        var r = result.Get();
        foreach(var x in r)
        {
            try
            {
                long cid = x.Cid;
                /*Cid       NUMERIC,
                    RetainerName      TEXT,
                    Item         INTEGER,
                    Price        INTEGER,
                    Quantity     INTEGER,
                    IsMannequinn INTEGER,
                    UnixTime         NUMERIC,
                    Buyer        TEXT*/
                ret.Add(new()
                {
                    BuyerName = x.Buyer,
                    CidUlong = *(ulong*)&cid,
                    IsMannequinn = x.IsMannequinn == 1,
                    ItemID = (int)x.Item,
                    Price = (int)x.Price,
                    Quantity = (int)x.Quantity,
                    RetainerName = x.RetainerName,
                    UnixTime = x.UnixTime,
                });
            }
            catch(Exception e)
            {
                PluginLog.Error($"Error processing record:");
                e.Log();
            }
        }
        return ret;
    }

    public override void RecordShopPurchase(ShopPurchaseSqlDescriptor shopPurchaseDescriptor)
    {
        if(C.Blacklist.Contains(Player.CID)) return;
        S.WorkerThread.Enqueue(() =>
        {
            using var db = new GilsightQueryFactory();
            db.Query(Tables.ShopPurchases).Insert(shopPurchaseDescriptor);
        });
    }

    public override List<ShopPurchaseSqlDescriptor> GetShopPurchases(long unixTimeMsMin = 0, long unixTimeMsMax = 0)
    {
        using var db = new GilsightQueryFactory();
        var result = db.Query(Tables.ShopPurchases);
        if(unixTimeMsMin > 0) result = result.Where("UnixTime", ">=", unixTimeMsMin);
        if(unixTimeMsMax > 0) result = result.Where("UnixTime", "<=", unixTimeMsMax);
        return [.. result.Get<ShopPurchaseSqlDescriptor>()];
    }

    public override void RecordNpcPurchase(NpcPurchaseSqlDescriptor npcPurchaseSqlDescriptor)
    {
        if(C.Blacklist.Contains(Player.CID)) return;
        S.WorkerThread.Enqueue(() =>
        {
            using var db = new GilsightQueryFactory();
            db.Query(Tables.NpcPurchases).Insert(npcPurchaseSqlDescriptor);
        });
    }

    public override List<NpcPurchaseSqlDescriptor> GetNpcPurchases(long unixTimeMsMin = 0, long unixTimeMsMax = 0)
    {
        using var db = new GilsightQueryFactory();
        var result = db.Query(Tables.NpcPurchases);
        if(unixTimeMsMin > 0) result = result.Where("UnixTime", ">=", unixTimeMsMin);
        if(unixTimeMsMax > 0) result = result.Where("UnixTime", "<=", unixTimeMsMax);
        return [.. result.Get<NpcPurchaseSqlDescriptor>()];
    }

    public override void RecordNpcSale(NpcSaleSqlDescriptor npcSaleSqlDescriptor)
    {
        if(C.Blacklist.Contains(Player.CID)) return;
        S.WorkerThread.Enqueue(() =>
        {
            using var db = new GilsightQueryFactory();
            db.Query(Tables.NpcSales).Insert(npcSaleSqlDescriptor);
        });
    }

    public override List<NpcSaleSqlDescriptor> GetNpcSales(long unixTimeMsMin = 0, long unixTimeMsMax = 0)
    {
        using var db = new GilsightQueryFactory();
        var result = db.Query(Tables.NpcSales);
        if(unixTimeMsMin > 0) result = result.Where("UnixTime", ">=", unixTimeMsMin);
        if(unixTimeMsMax > 0) result = result.Where("UnixTime", "<=", unixTimeMsMax);
        return [.. result.Get<NpcSaleSqlDescriptor>()];
    }

    public override void RecordPlayerGil(GilRecordSqlDescriptor gilRecordSqlDescriptor)
    {
        if(C.Blacklist.Contains(Player.CID)) return;
        S.WorkerThread.Enqueue(() =>
        {
            var ret = new Dictionary<ulong, Sender>();
            using var db = new GilsightQueryFactory();
            var now = DateTimeOffset.Now.ToLocalTime();
            var purgeAfter = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, now.Offset);
            db.Query(Tables.GilRecords)
            .Where("UnixTime", ">=", purgeAfter.ToUnixTimeMilliseconds())
            .Where("Cid", "=", Player.CID)
            .Delete();
            db.Query(Tables.GilRecords).Insert(gilRecordSqlDescriptor);
            S.MainWindow.UpdateData(false);
        });
    }

    public override List<GilRecordSqlDescriptor> GetGilRecords(long unixTimeMsMin = 0, long unixTimeMsMax = 0)
    {
        using var db = new GilsightQueryFactory();
        var result = db.Query(Tables.GilRecords);
        if(unixTimeMsMin > 0) result = result.Where("UnixTime", ">=", unixTimeMsMin);
        if(unixTimeMsMax > 0) result = result.Where("UnixTime", "<=", unixTimeMsMax);
        return [.. result.Get<GilRecordSqlDescriptor>()];
    }
}
