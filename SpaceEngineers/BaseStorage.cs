using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using VRageMath;
using VRage.Game;
using Sandbox.ModAPI.Interfaces;
using Sandbox.ModAPI.Ingame;
using Sandbox.Game.EntityComponents;
using VRage.Game.Components;
using VRage.Collections;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using VRage;
using VRage.Library;

public sealed class BaseStorage : MyGridProgram {


    /**
     * Название метода для обновления данных
     */
    const string refreshStorageInfoName = "refreshStorageInfo";
    
    /**
     * Название метода для очистки экрана
     */
    const string clearDisplayName = "clearDisplay";
    
    /**
     * Название дисплея(1х2) для вывода
     */
    const string baseInfoLcdName = "baseInfoLcd";
    
    
    
    private List<IMyTerminalBlock> containers; // контейнеры
    private IMyTextPanel lcd;
    StringBuilder sb = new StringBuilder();


    Dictionary<string, string> types = new Dictionary<string, string>();
    Dictionary<string, Decimal> ingots = new Dictionary<string, Decimal>();
    Dictionary<string, Decimal> ores = new Dictionary<string, Decimal>();

    public Program() {
        lcd = GridTerminalSystem.GetBlockWithName(baseInfoLcdName) as IMyTextPanel;
        lcd.ShowPublicTextOnScreen();
        Runtime.UpdateFrequency = UpdateFrequency.Update100;

        types.Add("Uranium", "Ur");
        types.Add("Iron", "Fe");
        types.Add("Silver", "Ag");
        types.Add("Silicon", "Si");
        types.Add("Nickel", "Ni");
        types.Add("Platinum", "Pt");
        types.Add("Gold", "Au");
        types.Add("Stone", "St");
        types.Add("Magnesium", "Mg");
        types.Add("Cobalt", "Co");
        types.Add("Ice", "Ice");
    }

    public List<IMyTerminalBlock> refreshContainers() {
        containers = new List<IMyTerminalBlock>();
        GridTerminalSystem.GetBlocksOfType(containers, block => block.HasInventory);
        return containers;
    }

    public void Save() {
        // Вызывается, когда программе требуется сохранить своё состояние.
        // Используйте этот метод, чтобы сохранить состояние программы в поле Storage,
        // или в другое место.
        // 
        // Этот метод опционален и может быть удалён,
        // если не требуется.
    }

    public void Main(string argument, UpdateType updateSource) {
        switch (argument) {
            case refreshStorageInfoName:
                refreshStorageInfo();
                break;
            case clearDisplayName:
                clearDisplay();
                break;
            default:
                refreshStorageInfo();
                break;
        }
    }

    public void refreshStorageInfo() {
        refreshContainers();
        sb.Clear();
        resetStorageResourceCount();

        // Пробегаем все инвентари и суммируем ресурсы
        foreach (IMyTerminalBlock container in containers) {
            for (int i = 0; i < container.InventoryCount; i++) {
                List<IMyInventoryItem> items = container.GetInventory(i).GetItems();
                foreach (IMyInventoryItem item in items) {
                    if (item.Content.TypeId.ToString().Equals("MyObjectBuilder_Ingot")) {
                        string ingotType = types[item.Content.SubtypeId.ToString()];
                        if (ingotType == null) {
                            sb.Append("Exception!!! Add ").Append(item.Content.SubtypeId.ToString())
                                .Append(" to ore list\n");
                        }
                        else {
                            ingots[ingotType] += (Decimal) item.Amount;
                        }
                    }
                    if (item.Content.TypeId.ToString().Equals("MyObjectBuilder_Ore")) {
                        string oreType = types[item.Content.SubtypeId.ToString()];
                        if (oreType == null) {
                            sb.Append("Exception!!! Add ").Append(item.Content.SubtypeId.ToString())
                                .Append(" to ore list\n");
                        }
                        else {
                            ores[oreType] += (Decimal) item.Amount;
                        }
                    }
                }
            }
        }

        // Выводим на дислей
        sb.Append($"{"     INGOTS",-23} | {"       ORE",-20}\n");
        foreach (KeyValuePair<string, string> type in types) {
            sb.Append($" {type.Value,-3}:{ingots[type.Value],15:F2}    |   ");
            sb.Append($" {type.Value,-3}:{ores[type.Value],15:F2} |\n");
        }

        lcd.WritePublicText(sb.ToString());
    }

    public void clearDisplay() {
        lcd.WritePublicText("");
    }

    private void resetStorageResourceCount() {
        foreach (KeyValuePair<string, string> type in types) {
            ingots[type.Value] = Decimal.Zero;
            ores[type.Value] = Decimal.Zero;
        }
    }
}