using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game;
using VRage;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        public enum FlightMode
        {
            free, hover
        }

        public static Context ctx = new Context(new Dictionary<string, object>
        {
            {"gts", null },
            {"lcdName", "lcd" }, // Основной дисплей под меню
            {"cockpitName", "cockpit" }, // Название блока кокпита
            {"gyroMult", 1f},
            {"desiredH", 10f},
            {"desiredSpeed", 0f},
            {"maxVSpeed", 2f},
        });

        IMyShipController ctrl;
        IMyTextSurface lcd;
        FlightMode mode = FlightMode.free;

        HoverRider hRider;
        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
            //lcd = GridTerminalSystem.GetBlockWithName(ctx.get("lcdName").ToString()) as IMyTextSurface;
            //lcd = Me.GetSurface(0);
            ctrl = GridTerminalSystem.GetBlockWithName(ctx.get("cockpitName").ToString()) as IMyShipController;
            lcd = ((IMyCockpit) ctrl).GetSurface(0);
            hRider = new HoverRider(GridTerminalSystem, lcd, ctrl);

            ctx.addArgumentAction("free", () =>
            {
                mode = FlightMode.free;
                hRider.free();
            });
            ctx.addArgumentAction("hover", () => mode = FlightMode.hover);
            ctx.addArgumentAction("incVSpeed", () =>
            {
                if (mode == FlightMode.free) return;
                var vSpeed = (float)ctx.get("maxVSpeed");
                ctx.putForce("maxVSpeed", vSpeed += 1f);
            });
            ctx.addArgumentAction("decVSpeed", () =>
            {
                if (mode == FlightMode.free) return;
                var vSpeed = (float)ctx.get("maxVSpeed");
                ctx.putForce("maxVSpeed", vSpeed -= 1f);
            });
            ctx.addArgumentAction("incHeight10", () =>
            {
                if (mode == FlightMode.free) return;
                var height = (float)ctx.get("desiredH");
                ctx.putForce("desiredH", height += 10f);
            });
            ctx.addArgumentAction("decHeight10", () =>
            {
                if (mode == FlightMode.free) return;
                var height = (float)ctx.get("desiredH");
                ctx.putForce("desiredH", height -= 10f);
            });
            ctx.addArgumentAction("incHeight100", () =>
            {
                if (mode == FlightMode.free) return;
                var height = (float)ctx.get("desiredH");
                ctx.putForce("desiredH", height += 100f);
            });
            ctx.addArgumentAction("decHeight100", () =>
            {
                if (mode == FlightMode.free) return;
                var height = (float)ctx.get("desiredH");
                ctx.putForce("desiredH", height -= 100f);
            });
            ctx.addArgumentAction("stop", () => {
                if (mode == FlightMode.free) return;
                ctx.putForce("desiredSpeed", 0f);
                    });
            ctx.addArgumentAction("fullSpeed", () => {
                if (mode == FlightMode.free) return;
                ctx.putForce("desiredSpeed", 100f);
            });
            ctx.addArgumentAction("halfSpeed", () => {
                if (mode == FlightMode.free) return;
                ctx.putForce("desiredSpeed", 50f);
            });
            ctx.addArgumentAction("hoverLight", () => {
                hRider.setMode(HoverMode.light);
            });
            ctx.addArgumentAction("hoverAgressive", () => {
                hRider.setMode(HoverMode.agressive);
            });
            hRider.free();
        }

        public void Main(string argument, UpdateType updateSource)
        {
            if (ctrl.MoveIndicator.Y != 0)
            {
                if (mode == FlightMode.free) return;
                var H = (float)ctx.get("desiredH");
                H += ctrl.MoveIndicator.Y > 0 ? 1 : -1;
                ctx.putForce("desiredH", H);
            }
            if (ctrl.MoveIndicator.Z != 0)
            {
                if (mode == FlightMode.free) return;
                var V = (float)ctx.get("desiredSpeed");
                V += ctrl.MoveIndicator.Z < 0 ? 1 : -1;
                ctx.putForce("desiredSpeed", V);
            }
            ctx.tick(argument);
            if (ctx.getTicks() % 10 == 0)
            {
                hRider.recalcLTH();
            }

            lcd.WriteText($"Режим: {mode}\n");
            lcd.WriteText($"Макс верт. скорость: {ctx.get("maxVSpeed")}\n", true);
            switch (mode)
            {
                case FlightMode.hover:
                    var v = (float)ctx.get("desiredSpeed");
                    var maxV = (float)ctx.get("maxVSpeed");
                    hRider.tunePower(v, maxV);
                    break;
            }
        }
    }
}
