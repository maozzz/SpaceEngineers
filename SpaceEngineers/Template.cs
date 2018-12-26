﻿using System;
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

public sealed class Program : MyGridProgram {
    public Program()
    {
        // Конструктор, вызванный единожды в каждой сессии и
        //  всегда перед вызовом других методов. Используйте его,
        // чтобы инициализировать ваш скрипт.
        //  
        // Конструктор опционален и может быть удалён, 
        // если в нём нет необходимости.
        // 
        // Рекомендуется использовать его, чтобы установить RuntimeInfo.UpdateFrequency
        // , что позволит перезапускать ваш скрипт
        // автоматически, без нужды в таймере.
    }

    public void Save()
    {
        // Вызывается, когда программе требуется сохранить своё состояние.
        // Используйте этот метод, чтобы сохранить состояние программы в поле Storage,
        // или в другое место.
        // 
        // Этот метод опционален и может быть удалён,
        // если не требуется.
    }

    public void Main(string argument, UpdateType updateSource)
    {
        // Главная точка входа в скрипт вызывается каждый раз,
        // когда действие Запуск программного блока активируется,
        // или скрипт самозапускается. Аргумент updateSource описывает,
        // откуда поступило обновление.
        // 
        // Метод необходим сам по себе, но аргументы
        // ниже могут быть удалены, если не требуются.
    }

}