using CashFlow.Data.ExplicitStructs;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Memory;
using ECommons.Automation;
using ECommons.ExcelServices;
using ECommons.EzHookManager;
using ECommons.GameHelpers;
using ECommons.MathHelpers;
using FFXIVClientStructs.FFXIV.Client.Network;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Callback = ECommons.Automation.Callback;

namespace CashFlow.Services;
public unsafe class MemoryManager
{
    private MemoryManager()
    {
        EzSignatureHelper.Initialize(this);
        HandleMarketBoardPurchasePacketHook = new((nint)PacketDispatcher.MemberFunctionPointers.HandleMarketBoardPurchasePacket, HandleMarketBoardPurchasePacketDetour);
        FireCallbackHook = new((nint)AtkUnitBase.MemberFunctionPointers.FireCallback, FireCallbackDetour, false);
    }

    public delegate nint ProcessEventLogMessage(nint a1, int a2, nint a3, nint a4);
    [EzHook("40 55 53 56 57 41 56 48 8D AC 24 ?? ?? ?? ?? 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 85 ?? ?? ?? ?? 8B DA", false)]
    public EzHook<ProcessEventLogMessage> ProcessEventLogMessageHook;

    private nint ProcessEventLogMessageDetour(nint a1, int a2, nint a3, nint a4)
    {
        try
        {
            PluginLog.Information($"{a1},{a2},{a3},{a4}");
            PluginLog.Information($"{(nint)AgentMerchantSettingInfo.Instance()}");
            if(a2 == 6079 || a2 == 6080)
            {
                for(var i = 0; i < S.EventWatcher.LastMerchantInfo.Data.ItemsSpan.Length; i++)
                {
                    if(Bitmask.IsBitSet(S.EventWatcher.LastMerchantInfo.Data.SelectedItems, i))
                    {
                        var d = S.EventWatcher.LastMerchantInfo.Data.ItemsSpan[i];
                        P.DataProvider.RecordShopPurchase(new()
                        {
                            CidUlong = Player.CID,
                            IsMannequinnBool = true,
                            Item = (int)d.ItemID,
                            Price = (int)d.Price,
                            Quantity = 1,
                            RetainerName = S.EventWatcher.LastMerchantInfo.Name,
                            UnixTime = DateTimeOffset.Now.ToUnixTimeMilliseconds(),
                        });
                    }
                }
                S.EventWatcher.LastMerchantInfo = null;
                S.MainWindow.UpdateData(false);
            }
        }
        catch(Exception e)
        {
            e.Log();
        }
        return ProcessEventLogMessageHook.Original(a1, a2, a3, a4);
    }

    public EzHook<AtkUnitBase.Delegates.FireCallback> FireCallbackHook;
    private bool FireCallbackDetour(AtkUnitBase* addon, uint valueCount, AtkValue* values, bool updateState)
    {
        try
        {
            if(addon->IsReady())
            {
                var name = addon->NameString;
                if(name == "ItemSearchResult" && valueCount == 2 && values[0].Int == 2)
                {
                    var clicked = values[1].Int;
                    if(Utils.GetLoadedShopItems().TryGetFirst(x => x.Index == clicked, out var item))
                    {
                        S.EventWatcher.LastClickedItem = new()
                        {
                            CidUlong = Player.CID,
                            IsMannequinnBool = false,
                            Item = addon->AtkValues[0].Int % 1000000 + (item.HQ ? 1000000 : 0),
                            Price = item.Price,
                            Quantity = item.Amount,
                            RetainerName = item.Retainer,
                            UnixTime = DateTimeOffset.Now.ToUnixTimeMilliseconds(),
                        };
                    }
                }
            }
        }
        catch(Exception e)
        {
            e.Log();
        }
        return FireCallbackHook.Original(addon, valueCount, values, updateState);
    }


    private EzHook<PacketDispatcher.Delegates.HandleMarketBoardPurchasePacket> HandleMarketBoardPurchasePacketHook;
    private void HandleMarketBoardPurchasePacketDetour(uint targetId, nint packet)
    {
        try
        {
            if(S.EventWatcher.LastClickedItem != null)
            {
                P.DataProvider.RecordShopPurchase(S.EventWatcher.LastClickedItem);
                S.EventWatcher.LastClickedItem = null;
                S.MainWindow.UpdateData(false);
            }
            //PluginLog.Information(MemoryHelper.ReadRaw(packet, 100).ToHexString());
        }
        catch(Exception e)
        {
            e.Log();
        }
        HandleMarketBoardPurchasePacketHook.Original(targetId, packet);
    }

    private delegate nint ProcessPacketSystemLogMessage(int a1, int a2, ShopLogData* a3, byte a4);
    [EzHook("48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 48 83 EC 30 41 0F B6 D9 49 8B F8 8B F2")]
    private EzHook<ProcessPacketSystemLogMessage> ProcessPacketSystemLogMessageHook;

    private nint ProcessPacketSystemLogMessageDetour(int a1, int a2, ShopLogData* a3, byte a4)
    {
        try
        {
            //PluginLog.Debug($"PPSLMD: {a1}, {a2}, {(nint)a3:X16}, {a4}\n{MemoryHelper.ReadRaw((nint)a3, 50).ToHexString()}");
            if(a2 == 1687)
            {
                //PluginLog.Debug($"Purchase detected: {a3->Item.ValueNullable?.GetName()}/x{a3->Quantity} for {a3->Price} gil, hq={a3->IsHQ}");
                P.DataProvider.RecordNpcPurchase(new()
                {
                    CidUlong = Player.CID,
                    IsBuybackBool = false,
                    Item = (int)a3->Item.RowId,
                    Quantity = (int)a3->Quantity,
                    Price = (int)a3->Price,
                    UnixTime = DateTimeOffset.Now.ToUnixTimeMilliseconds(),
                });
                S.MainWindow.UpdateData(false);
            }
            else if(a2 == 1688)
            {
                //PluginLog.Debug($"Sale detected: {a3->Item.ValueNullable?.GetName()}/x{a3->Quantity} for {a3->Price} gil, hq={a3->IsHQ}");

                P.DataProvider.RecordNpcSale(new()
                {
                    CidUlong = Player.CID,
                    Item = (int)a3->Item.RowId,
                    Quantity = (int)a3->Quantity,
                    Price = (int)a3->Price,
                    UnixTime = DateTimeOffset.Now.ToUnixTimeMilliseconds(),
                });
                S.MainWindow.UpdateData(false);
            }
            else if(a2 == 1689)
            {
                //PluginLog.Debug($"Buyback detected: {a3->Item.ValueNullable?.GetName()}/x{a3->Quantity} for {a3->Price} gil, hq={a3->IsHQ}");

                P.DataProvider.RecordNpcPurchase(new()
                {
                    CidUlong = Player.CID,
                    IsBuybackBool = true,
                    Item = (int)a3->Item.RowId,
                    Quantity = (int)a3->Quantity,
                    Price = (int)a3->Price,
                    UnixTime = DateTimeOffset.Now.ToUnixTimeMilliseconds(),
                });
                S.MainWindow.UpdateData(false);
            }
        }
        catch(Exception e)
        {
            e.Log();
        }
        return ProcessPacketSystemLogMessageHook.Original(a1, a2, a3, a4);
    }


    private delegate nint ProcessRetainerHistoryDelegate(nint a1, nint data);
    [EzHook("40 53 56 57 41 57 48 83 EC 38 48 8B F1")]
    private EzHook<ProcessRetainerHistoryDelegate> ProcessRetainerHistoryHook;

    private nint ProcessRetainerHistoryDetour(nint a1, nint data)
    {
        try
        {
            PluginLog.Debug(MemoryHelper.ReadRaw(data, 8).ToHexString());
            List<RetainerHistoryData> list = [];
            for(var i = 0; i < 20; i++)
            {
                var rec = *(RetainerHistoryData*)(data + 8 + sizeof(RetainerHistoryData) * i);
                if(rec.ItemID == 0) break;
                list.Add(rec);
                PluginLog.Information($"""
                    ====== ProcessRetainerHistoryDetour ({i}) ======
                    {nameof(rec.ItemID)} = {ExcelItemHelper.GetName(rec.ItemID)}
                    {nameof(rec.Price)} = {rec.Price:N0}
                    {nameof(rec.BuyerName)} = {rec.BuyerName}
                    {nameof(rec.IsMannequinn)} = {rec.IsMannequinn}
                    {nameof(rec.IsHQ)} = {rec.IsHQ}
                    {nameof(rec.Quantity)} = {rec.Quantity}
                    {nameof(rec.Unk17)} = {rec.Unk17}
                    {nameof(rec.UnixTimeSeconds)} = {rec.UnixTimeSeconds} / {DateTimeOffset.FromUnixTimeSeconds(rec.UnixTimeSeconds)}
                    ============
                    """);
            }
            if(list.Count > 0)
            {
                var retainer = Svc.Objects.OrderBy(Player.DistanceTo).FirstOrDefault(x => x.ObjectKind == ObjectKind.Retainer);
                if(retainer != null)
                {
                    P.DataProvider.RecordRetainerHistory(list, Player.CID, retainer.Name.ToString());
                    S.MainWindow.UpdateData(false);
                }
            }
        }
        catch(Exception e)
        {
            e.Log();
        }
        return ProcessRetainerHistoryHook.Original(a1, data);
    }
}
