﻿using Sandbox.Game.EntityComponents;
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
        public static Context ctx = new Context(new Dictionary<string, object> {
                {"lcdIndex", 0},
                {"flightMode", null },
                {"gyroMult", 2f},
                {"weakThrusters", ThrustDirection.L},
            });

        Util util;
        Rider rider;
        Vector3D point;
        IMyShipController ctrl;
        IMyTextSurface lcd;
        public Program()
        {
            ctx.addArgumentAction("gps", () =>
            {
                IMyLightingBlock lamp = null;
                util.findBlockByType(ref lamp);
                lamp.Enabled = !lamp.Enabled;
            });

            util = new Util(GridTerminalSystem);

            // Rider 
            util.findBlockByType(ref ctrl);
            lcd = ((IMyCockpit)ctrl).GetSurface(0);
            lcd.ContentType = ContentType.TEXT_AND_IMAGE;
            rider = new Rider(GridTerminalSystem, ctrl, lcd);

            Runtime.UpdateFrequency = UpdateFrequency.Update10;


            // Обрабочики аргументов
            ctx.addArgumentAction("compensate", () => ctx.putForce("flightMode", FlightMode.compensation));
            ctx.addArgumentAction("point", () => ctx.putForce("flightMode", FlightMode.toPoint));
            ctx.addArgumentAction("free", () =>
            {
                rider.free();
                ctx.putForce("flightMode", FlightMode.free);
            });

            ctx.putForce("flightMode", FlightMode.free);
            point = util.vectorFromGps("GPS:mao_z #2:53605.95:-26613.65:12022.53:");
        }

        public void Save()
        {
        }

        public void Main(string argument, UpdateType updateSource)
        {
            ctx.tick(argument);

            if (ctx.getTicks() % 100 == 0)
            {
                rider.recalcLTH();
                return;
            }

            switch ((FlightMode)ctx.get("flightMode"))
            {
                case FlightMode.compensation:
                    rider.compensation();
                    break;
                case FlightMode.toPoint:
                    Vector3D pathVec = util.vectorFromGps("GPS:mao_z #3:53564.2:-26578.3:12264:") - util.vectorFromGps("GPS:mao_z #4:53560.38:-26660.58:12079.39:");
                    //rider.toPoint(point, 2);
                    if (rider.orient(pathVec, ctrl.GetNaturalGravity()))
                    {
                        if (rider.toPoint(point, pathVec, 3f, 10))
                        {
                            rider.compensation();
                        }
                    }
                    else
                    {
                        rider.compensation();
                    }
                    break;
                default:
                    break;
            }
        }

        public enum FlightMode
        {
            compensation,
            free,
            toPoint
        }
    }
}
