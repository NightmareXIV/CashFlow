using ECommons.GameFunctions;

namespace CashFlow.Services;
public unsafe class TradeDetectionManager : IDisposable
{
    private Dictionary<uint, uint> Snapshot;
    private ulong PartnerCID;
    private TradeDetectionManager()
    {
        Svc.Condition.ConditionChange += Condition_ConditionChange;
    }

    private void Condition_ConditionChange(ConditionFlag flag, bool value)
    {
        if(flag == ConditionFlag.TradeOpen)
        {
            if(value)
            {
                //trade starts
                var tradePartner = Utils.GetTradePartner();
                if(tradePartner != null)
                {
                    PartnerCID = tradePartner.Struct()->ContentId;
                    P.DataProvider.RecordPlayerCID(tradePartner);
                }
                Snapshot = Utils.GetInventorySnapshot(Utils.ValidInventories);
            }
            else
            {
                //trade ends
                var tradePartner = Utils.GetTradePartner();
                if(tradePartner != null)
                {
                    PartnerCID = tradePartner.Struct()->ContentId;
                    P.DataProvider.RecordPlayerCID(tradePartner);
                }
                if(Snapshot != null && PartnerCID != 0)
                {
                    var result = Utils.GetTradeResult(PartnerCID, Snapshot, Utils.GetInventorySnapshot(Utils.ValidInventories));
                    PluginLog.Information($"Trade result with {tradePartner}:\n{result}");
                    if(result.ReceivedItems.Length != 0 || result.ReceivedGil != 0)
                    {
                        P.DataProvider.RecordIncomingTrade(result);
                    }
                }
                PartnerCID = 0;
                Snapshot = null;
            }
        }
    }

    public void Dispose()
    {
        Svc.Condition.ConditionChange -= Condition_ConditionChange;
    }
}
