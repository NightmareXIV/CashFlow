using CashFlow.Data;
using CashFlow.Data.LegacyDescriptors;
using CsvHelper;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Memory;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using MessagePack.Resolvers;

namespace CashFlow;
public static unsafe class Utils
{
    public static void WriteFields(this CsvWriter writer, params string[] fields)
    {
        foreach(var s in fields)
        {
            writer.WriteField(s);
        }
        writer.NextRecord();
    }

    public static string GetBriefDate(this DateOnly date, bool? reverse = null)
    {
        reverse ??= C.ReverseDayMonth;
        if(reverse == true)
        {
            return $"{date.Month:D2}/{date.Day:D2}";
        }
        else
        {
            return $"{date.Day:D2}.{date.Month:D2}";
        }
    }

    public static void DrawColoredGilText(long t)
    {
        Vector4? col = null;
        if(t > 0)
        {
            col = EColor.Green;
            Utils.DrawGilIncrease();
        }
        else if(t < 0)
        {
            col = EColor.Red;
            Utils.DrawGilDecrease();
        }
        ImGuiEx.Text(col, $" {t:N0}");
    }

    public static long GetTotalDays(this DateOnly date)
    {
        return new DateTimeOffset(date, new TimeOnly(0, 0), TimeSpan.Zero).ToUnixTimeMilliseconds() / (24L * 60L * 60L * 1000L);
    }

    public static DateOnly GetLocalDateFromUnixTime(long unixtime)
    {
        unixtime += (long)DateTimeOffset.Now.Offset.TotalMilliseconds;
        var date = DateTimeOffset.FromUnixTimeMilliseconds(unixtime).Date;
        return new(date.Year, date.Month, date.Day);
    }

    public static long GetCurrentOrCachedPlayerRetainerGil()
    {
        var rm = RetainerManager.Instance();
        if(rm->IsReady)
        {
            return rm->Retainers.ToArray().Sum(x => x.Gil);
        }
        else
        {
            return GetCachedRetainerGil(Player.CID);
        }
    }

    public static long GetCurrentPlayerRetainerGil()
    {
        var rm = RetainerManager.Instance();
        if(rm->IsReady)
        {
            return rm->Retainers.ToArray().Sum(x => x.Gil);
        }
        return 0;
    }

    public static long GetCachedRetainerGil(ulong cid)
    {
        if(C.CachedRetainerGil.TryGetValue(cid, out var value))
        {
            return value;
        }
        return 0;
    }

    public static void DrawGilDecrease(bool sameLine = true)
    {
        ImGuiEx.Text(EColor.Red, UiBuilder.DefaultFont, C.ReverseArrows ? "↘" : $"↗");
        if(sameLine) ImGui.SameLine(0, 0);
    }
    public static void DrawGilIncrease(bool sameLine = true)
    {
        ImGuiEx.Text(EColor.Green, UiBuilder.DefaultFont, C.ReverseArrows ? "↗" : $"↘");
        if(sameLine) ImGui.SameLine(0, 0);
    }

    public static long ToUnixTimeMilliseconds(this DateTime dt) => new DateTimeOffset(dt).ToUnixTimeMilliseconds();

    public static string ToPreferredTimeString(this DateTimeOffset dto)
    {
        if(C.UseCustomTimeFormat)
        {
            try
            {
                return C.UseUTCTime ? dto.ToString(C.CustomTimeFormat) : dto.ToLocalTime().ToString(C.CustomTimeFormat);
            }
            catch(Exception)
            {
                return $"Time format error";
            }
        }
        else
        {
            return C.UseUTCTime ? dto.ToString() : dto.ToLocalTime().ToString();
        }
    }

    public static int ShopLoadedItems => AtkStage.Instance()->GetNumberArrayData()[(int)NumberArrayType.ItemSearch]->IntArray[402];
    public static List<ItemPrice> GetLoadedShopItems(bool onlyHq = false)
    {
        var ret = new List<ItemPrice>();
        for(var i = 0; i < ShopLoadedItems; i++)
        {
            var item = new ItemPrice()
            {
                Price = AtkStage.Instance()->GetNumberArrayData()[(int)NumberArrayType.ItemSearch]->IntArray[403 + i * 6],
                Amount = AtkStage.Instance()->GetNumberArrayData()[(int)NumberArrayType.ItemSearch]->IntArray[404 + i * 6],
                HQ = AtkStage.Instance()->GetNumberArrayData()[(int)NumberArrayType.ItemSearch]->IntArray[405 + i * 6] == 1,
                Retainer = MemoryHelper.ReadStringNullTerminated((nint)AtkStage.Instance()->GetStringArrayData()[(int)StringArrayType.ItemSearch]->StringArray[208 + i * 6].Value),
                Index = i,
            };
            ret.Add(item);
        }
        return ret;
    }

    public static IPlayerCharacter GetTradePartner()
    {
        //Client::Game::InventoryManager_SendTradeRequest
        var id = *(uint*)(((nint)InventoryManager.Instance()) + 8612);
        return Svc.Objects.OfType<IPlayerCharacter>().FirstOrDefault(x => x.EntityId == id);
    }

    public static readonly InventoryType[] ValidInventories = [
        InventoryType.Inventory1,
        InventoryType.Inventory2,
        InventoryType.Inventory3,
        InventoryType.Inventory4,
        InventoryType.ArmoryBody,
        InventoryType.ArmoryEar,
        InventoryType.ArmoryFeets,
        InventoryType.ArmoryHands,
        InventoryType.ArmoryHead,
        InventoryType.ArmoryLegs,
        InventoryType.ArmoryMainHand,
        InventoryType.ArmoryNeck,
        InventoryType.ArmoryOffHand,
        InventoryType.ArmoryRings,
        InventoryType.ArmoryWaist,
        InventoryType.ArmoryWrist,
        InventoryType.Crystals,
        ];

    public static Dictionary<uint, uint> GetInventorySnapshot(IEnumerable<InventoryType> validInventories)
    {
        var im = InventoryManager.Instance();
        var ret = new Dictionary<uint, uint>
        {
            [1] = (uint)im->GetInventoryItemCount(1)
        };
        foreach(var type in validInventories)
        {
            var inv = im->GetInventoryContainer(type);
            for(var i = 0; i < inv->GetSize(); i++)
            {
                var slot = inv->GetInventorySlot(i);
                var id = slot->GetItemId();
                if(id != 0)
                {
                    if(!ret.ContainsKey(id))
                    {
                        ret[id] = slot->GetQuantity();
                    }
                    else
                    {
                        ret[id] += slot->GetQuantity();
                    }
                }
            }
        }
        return ret;
    }

    public static TradeDescriptor GetTradeResult(ulong cid, Dictionary<uint, uint> invStart, Dictionary<uint, uint> invEnd)
    {
        var gilDiff = (int)invEnd[1] - (int)invStart[1];
        Dictionary<uint, int> diff = [];
        foreach(var x in invStart.Keys.Concat(invEnd.Keys))
        {
            if(x == 1) continue;
            diff[x] = (int)invEnd.SafeSelect(x, 0u) - (int)invStart.SafeSelect(x, 0u);
        }
        return new()
        {
            TradePartnerCID = cid,
            ReceivedGil = gilDiff,
            ReceivedItems = [.. diff.Where(x => x.Value != 0).Select(x => new ItemWithQuantity(x.Key, x.Value))],
        };
    }
}
