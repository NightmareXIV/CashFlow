﻿using CashFlow.Gui.BaseTabs;
using CashFlow.Gui.Components;
using ECommons.ChatMethods;
using ECommons.Funding;
using ECommons.SimpleGui;
using ECommons.Throttlers;
using NightmareUI;

namespace CashFlow.Gui;
public unsafe class MainWindow : ConfigWindow
{
    public volatile Dictionary<ulong, Sender> CIDMap = [];
    public TabTradeLog TabTradeLog = new();
    public TabRetainerSales TabRetainerSales = new();
    public TabShopPurchases TabShopPurchases = new();
    public TabNpcPurchases TabNpcPurchases = new();
    public TabNpcSales TabNpcSales = new();
    public TabGilHistory TabGilHistory = new();
    public DateTime DateGraphStart = DateTimeOffset.FromUnixTimeSeconds(C.GraphStartDate).ToLocalTime().DateTime;
    public string DateGraphStartStr = DateTimeOffset.FromUnixTimeSeconds(C.GraphStartDate).ToLocalTime().DateTime.ToString(DateWidget.DateFormat);

    public void UpdateData(bool forced)
    {
        if(FrameThrottler.Check("UpdateBlocked") || forced)
        {
            TabTradeLog.NeedsUpdate = true;
            TabRetainerSales.NeedsUpdate = true;
            TabShopPurchases.NeedsUpdate = true;
            TabNpcPurchases.NeedsUpdate = true;
            TabNpcSales.NeedsUpdate = true;
            TabGilHistory.NeedsUpdate = true;
        }
    }

    private MainWindow()
    {
        EzConfigGui.Init(this);
    }

    public override void Draw()
    {
        PatreonBanner.DrawRight();
        ImGuiEx.EzTabBar("tabs", PatreonBanner.Text, [
            ("Trade Log", TabTradeLog.Draw, null, true),
            ("Purchase Log", TabShopPurchases.Draw, null, true),
            ("Sale Log", TabRetainerSales.Draw, null, true),
            ("NPC Sale Log", TabNpcSales.Draw, null, true),
            ("NPC Purchase Log", TabNpcPurchases.Draw, null, true),
            ("Gil History", TabGilHistory.Draw, null, true),
            ("Settings", DrawSettings, null, true),
            ("Debug", TabDebug.Draw, ImGuiColors.DalamudGrey3, true),
            ]);
    }

    void DrawSettings()
    {
        NuiTools.ButtonTabs([[new("General", DrawSettingsGeneral), new("Exclusions", TabExclusions.Draw)]]);
    }

    private void DrawSettingsGeneral()
    {
        ImGui.SetNextItemWidth(150);
        ImGuiEx.SliderInt("Records per page", ref C.PerPage, 1000, 10000);
        ImGui.Checkbox("Merge sequential gil-only trades with the same player into one", ref C.MergeTrades);
        ImGuiEx.HelpMarker("Does not affects how trades are stored in database, only affects view");
        ImGui.Indent();
        ImGui.SetNextItemWidth(100);
        ImGui.SliderInt($"Time limit for merging trades, minutes", ref C.MergeTradeTreshold, 1, 10);
        ImGui.Unindent();
        ImGui.Checkbox("Change arrows directions", ref C.ReverseArrows);
        ImGuiEx.TextV("Date format:");
        ImGui.SameLine();
        ImGuiEx.RadioButtonBool("Month/Day", "Day.Month", ref C.ReverseDayMonth, sameLine: true, inverted: true);

        ImGui.Separator();
        ImGui.Checkbox("Show history when trading", ref C.ShowTradeOverlay);
        ImGui.Indent();
        ImGui.SetNextItemWidth(150f);
        ImGui.InputInt("Time, minutes", ref C.LastGilTradesMin);
        ImGui.Unindent();

        ImGui.Separator();
        ImGui.Checkbox("Censor Names", ref C.CensorConfig.Enabled);
        ImGui.Indent();
        ImGui.Checkbox("Lesser Censor Mode", ref C.CensorConfig.LesserCensor);
        if(ImGuiEx.IconButtonWithText(FontAwesomeIcon.Undo, "Reset Censor Seed")) C.CensorConfig.Seed = Guid.NewGuid().ToString();
        ImGui.Unindent();

        ImGui.Separator();
        ImGui.Checkbox($"Display UTC time", ref C.UseUTCTime);
        ImGuiEx.HelpMarker($"Your local time will still be used for internal calculations");
        ImGui.Checkbox($"Use custom time format", ref C.UseCustomTimeFormat);
        if(C.UseCustomTimeFormat)
        {
            ImGui.Indent();
            ImGui.SetNextItemWidth(200f);
            ImGui.InputText($"Custom time format", ref C.CustomTimeFormat, 100);
            ImGuiEx.HelpMarker("Click to open help");
            if(ImGuiEx.HoveredAndClicked())
            {
                ShellStart("https://learn.microsoft.com/en-us/dotnet/standard/base-types/custom-date-and-time-format-strings");
            }
            ImGui.Unindent();
        }

        ImGui.Checkbox("Set fixed graph start date", ref C.UseGraphStartDate);
        if(C.UseGraphStartDate)
        {
            ImGui.Indent();
            if(DateWidget.DatePickerWithInput("##min", 1, ref DateGraphStartStr, ref DateGraphStart, out var isOpen))
            {
                C.GraphStartDate = DateGraphStart.ToUniversalTime().ToUnixTimeMilliseconds() / 1000;
            }
            ImGui.Unindent();
        }
    }
}
