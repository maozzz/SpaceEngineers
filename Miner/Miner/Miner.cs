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
        public enum Stage
        {
            toDock,
            toMine,
            pause,
            mine,
            docked,
            undock
        }

        public static Context ctx = new Context(new Dictionary<string, object>
        {
            {"gts", null },
            {"lcdMenuName", "lcd" }, // Основной дисплей под меню
            {"lcdProcName", "lcdL" }, // Дисплей для отображения процесса
            {"weakThrusters", ThrustDirection.L},
            {"gyroMult", 1f},
            {"process", null },
            {"menu", null },
            {"waypoints", new List<Vector3D>() },
            {"dockDirection", null }, // ориентация стыковки
            {"dockConnector", null }, // коннектор стыковки
            {"dockPoint", null }, // точка стыковки
            {"permitMass", 100000},
            {"shaftN", 0},
            {"shaftW", 6f},
            {"shaftL", 6f},
            {"tunnelDepth", 30},
            {"mineVelocity", 0.5f},
            {"dockTimer",30},
        });

        IMyGridTerminalSystem gts;
        Util util;
        Rider rider;
        IMyShipController ctrl;
        IMyTextSurface lcd; // системный дисплей
        Menu menu;
        IMyTextSurface lcdMenu;
        QueuedProc proc;
        List<IMyBatteryBlock> battaries = new List<IMyBatteryBlock>();
        Stage stage = Stage.pause;
        private int stageStatus;
        private Vector3D currentTunnelPoint;
        private bool systemOverload = false;
        private int unloadTimer = 0;

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
            gts = ctx.putForce("gts", GridTerminalSystem);

            util = new Util(GridTerminalSystem);
            // Cockpit
            util.findBlockByType(ref ctrl);
            // System lcd
            lcd = (IMyTextSurface)((IMyGridTerminalSystem)ctx.get("gts")).GetBlockWithName(ctx.get("lcdProcName").ToString());
            lcd.ContentType = ContentType.TEXT_AND_IMAGE;
            // Rider
            rider = new Rider(GridTerminalSystem, ctrl, lcd);

            initMenu();
            initHandlers();
            initProcess();

            load();
        }

        public void Main(string argument, UpdateType updateSource)
        {
            ctx.putForce("arg", argument);
            ctx.tick(argument);

            if (ctx.getTicks() % 10 == 0)
            {
                rider.recalcLTH();
                systemOverload =(rider.getMass() > (int)ctx.get("permitMass"));
                lcdMenu.WriteText(menu.exec());
                return;
            }

            proc.exec();
            if (argument != "") lcdMenu.WriteText(menu.exec());
        }

        public void initMenu()
        {
            lcdMenu = (IMyTextSurface)gts.GetBlockWithName(ctx.get("lcdMenuName").ToString());
            menu = ctx.putForce("menu", new SimpleMenu("menu", null));

            menu.add(new ChangeVarMenuItem(new SimpleReactMsg(
                      "free",
                      null, () => "free OK"), free));
            menu.add(new ChangeVarMenuItem(new SimpleReactMsg(
                      "set dock",
                      null, () => "sdock OK"), setDock));
            menu.add(new ChangeVarMenuItem(new SimpleReactMsg(
                      "set mine area",
                      null, () => "sma OK"), setMineArea));
            menu.add(new ChangeVarMenuItem(new SimpleReactMsg(
                      "save",
                      null, () => "save OK"), save));
            menu.add(new ChangeVarMenuItem(new SimpleReactMsg(
                      "load",
                      null, () => "load OK"), load));
            menu.add(new ChangeVarMenuItem(new SimpleReactMsg(
                      "add waypnt",
                      null, () => "aWaypnt OK"), addWaypoint));

            Menu actions = new SimpleMenu("actions", menu);
            actions.add(new ChangeVarMenuItem(new SimpleReactMsg(
                      "mine",
                      null, () => "mine OK"), startMine));
            actions.add(new ChangeVarMenuItem(new SimpleReactMsg(
                      "toMine",
                      null, () => "to mine OK"), toMine));
            actions.add(new ChangeVarMenuItem(new SimpleReactMsg(
                      "toDock",
                      null, () => "to dock OK"), toDock));
            actions.add(new ChangeVarMenuItem(new SimpleReactMsg(
                      "dock",
                      null, () => "dock OK"), () => {
                          proc.add(dockJob());
                      }));
            actions.add(new ChangeVarMenuItem(new SimpleReactMsg(
                      "undock",
                      null, () => "undock OK"), () => {
                          proc.add(undockJob());
                      }));

            menu.add(actions);

            // Параметры майнинга
            Menu submenu = new SimpleMenu("variables", menu);
            Menu shaftN = new SimpleMenu("shaftN", submenu);
            shaftN.add(new ChangeVarMenuItem("shaftN", 1, "shNum+", "={0}"));
            shaftN.add(new ChangeVarMenuItem("shaftN", -1, "shNum-", "={0}"));
            submenu.add(shaftN);

            Menu shaftW = new SimpleMenu("shaftW", submenu);
            shaftW.add(new ChangeVarMenuItem("shaftW", 0.2f, "shftW+", "={0}"));
            shaftW.add(new ChangeVarMenuItem("shaftW", -0.2f, "shftW-", "={0}"));
            submenu.add(shaftW);

            Menu shaftL = new SimpleMenu("shaftL", submenu);
            shaftL.add(new ChangeVarMenuItem("shaftL", 0.2f, "shftL+", "={0}"));
            shaftL.add(new ChangeVarMenuItem("shaftL", -0.2f, "shftL-", "={0}"));
            submenu.add(shaftL);

            Menu tunDep = new SimpleMenu("tunn depth", submenu);
            tunDep.add(new ChangeVarMenuItem("tunnelDepth", 1, "td+", "={0}"));
            tunDep.add(new ChangeVarMenuItem("tunnelDepth", -1, "td-", "={0}"));
            submenu.add(tunDep);

            Menu dockTimer = new SimpleMenu("dock timer", submenu);
            dockTimer.add(new ChangeVarMenuItem("dockTimer", 1, "td+", "={0}"));
            dockTimer.add(new ChangeVarMenuItem("dockTimer", -1, "td-", "={0}"));
            submenu.add(dockTimer);

            Menu permMass = new SimpleMenu("permit mass", submenu);
            permMass.add(new ChangeVarMenuItem("permitMass", 1000, "pm+", "={0}"));
            permMass.add(new ChangeVarMenuItem("permitMass", -1000, "pm-", "={0}"));
            submenu.add(permMass);

            Menu mineVel = new SimpleMenu("mine speed", submenu);
            mineVel.add(new ChangeVarMenuItem("mineVelocity", 0.1, "speed+", "={0}"));
            mineVel.add(new ChangeVarMenuItem("mineVelocity", -0.1, "speed-", "={0}"));
            submenu.add(mineVel);
            menu.add(submenu);
        }

        public void initHandlers()
        {
            ctx.addArgumentAction("free", free);
        }

        public void initProcess()
        {
            proc = ctx.putForce("process", new MinerProc());
        }

        public void free()
        {
            drillsOn(false);
            stage = Stage.pause;
            proc.clear();
            rider.free();
        }

        private bool mine()
        {
            if (stage != Stage.mine)
            {
                stageStatus = 1;
                stage = Stage.mine;
                return false;
            }
            switch (stageStatus)
            {
                //case 0: // подходим к глиссаде
                //    if (rider.moveTo(mineGlissade)) stageStatus++;
                //    break;
                case 1: // подходим к месту майнинга
                    if (toMineStartArea()) stageStatus++;
                    break;
                case 2: // добыча
                    if (doMine()) stageStatus++;
                    break;
                case 3: // на поверхность
                    if (upToHoriz()) stageStatus = 4;
                    break;
                case 4:
                    toDock();
                    return true;
            }
            return false;
        }
        private Boolean upToHoriz()
        {
            drillsOn(false);
            Vector3D mineDirection = (Vector3D)ctx.get("mineDirection");
            Vector3D mineOrientation = (Vector3D)ctx.get("mineOrientation");
            Vector3D path = -mineDirection;
            if (rider.orient(mineOrientation, mineDirection) && rider.toPoint(currentTunnelPoint, path, (float)ctx.get("mineVelocity") * 3))
            {
                stageStatus = 1;
            }
            lcd.WriteText("shft Num: " + (int)ctx.get("shaftN") + "\n", true);
            lcd.WriteText("Mass: " + rider.getMass() + "\n", true);
            return false;
        }
        private Boolean doMine()
        {
            if (systemOverload)
            {
                return true;
            }
            drillsOn(true);
            int tunnelDepth = (int)ctx.get("tunnelDepth");
            Vector3D mineDirection = (Vector3D)ctx.get("mineDirection");
            Vector3D mineOrientation = (Vector3D)ctx.get("mineOrientation");
            Vector3D floor = currentTunnelPoint + mineDirection * tunnelDepth;
            var d = (floor - ctrl.GetPosition()).Length();
            if (rider.orient(mineOrientation, mineDirection, 0.4f) && rider.toPoint(floor, mineDirection, (float)ctx.get("mineVelocity"), 10, 0.8f, 1))
            {
                ctx.putForce("shaftN", (int)ctx.get("shaftN") + 1);
                return true;
            }
            Vector3D donePath = (ctrl.GetPosition() - currentTunnelPoint);
            var curDepth = donePath.Dot(mineDirection);
            var deviation = Vector3D.ProjectOnPlane(ref donePath, ref mineDirection).Length();
            lcd.WriteText($"Depth: {curDepth:F2}\n", true);
            lcd.WriteText($"Remain: {d:F2}\n", true);
            lcd.WriteText($"Deviation: {deviation:F2}\n", true);
            lcd.WriteText("Shft Num: " + (int)ctx.get("shaftN") + "\n", true);
            lcd.WriteText("Mass: " + rider.getMass() + "\n", true);
            return false;
        }
        private void drillsOn(Boolean on)
        {
            GridTerminalSystem.GetBlocksOfType(new List<IMyShipDrill>(), drill => {
                drill.Enabled = on;
                return false;
            });
        }

        private Boolean toMineStartArea()
        {
            Vector3D mineArea = (Vector3D)ctx.get("mineArea");
            Vector3D mineDirection = (Vector3D)ctx.get("mineDirection");
            Vector3D mineOrientation = (Vector3D)ctx.get("mineOrientation");

            if (systemOverload)
            {
                if (rider.orient(mineOrientation, mineDirection) && rider.toPoint(mineArea))
                {
                    stageStatus = 4;
                }

                return false;
            }

            Vector3D spiralXy = GetSpiralXY((int)ctx.get("shaftN"), (float)ctx.get("shaftW"), (float)ctx.get("shaftL"));
            currentTunnelPoint = mineArea + spiralXy.X * mineOrientation +
                                 spiralXy.Z * Vector3D.Cross(mineDirection, mineOrientation);
            return rider.orient(mineOrientation, mineDirection) && rider.toPoint(currentTunnelPoint);
        }

        /**
         * Смещение точки бурения
         */
        private Vector3D GetSpiralXY(int p, float W, float L, int n = 20)
        {
            int positionX = 0, positionY = 0, direction = 0, stepsCount = 1, stepPosition = 0, stepChange = 0;
            int X = 0;
            int Y = 0;
            for (int i = 0; i < n * n; i++)
            {
                if (i == p)
                {
                    X = positionX;
                    Y = positionY;
                    break;
                }

                if (stepPosition < stepsCount)
                {
                    stepPosition++;
                }
                else
                {
                    stepPosition = 1;
                    if (stepChange == 1)
                    {
                        stepsCount++;
                    }

                    stepChange = (stepChange + 1) % 2;
                    direction = (direction + 1) % 4;
                }

                if (direction == 0)
                {
                    positionY++;
                }
                else if (direction == 1)
                {
                    positionX--;
                }
                else if (direction == 2)
                {
                    positionY--;
                }
                else if (direction == 3)
                {
                    positionX++;
                }
            }

            return new Vector3D(X * L, 0, Y * W);
        }

        public void startMine()
        {
            proc.add(mineJob());
        }

        public Job mineJob()
        {
            return new TheJob("mining", () =>
            {
                return mine();
            });
        }

        public void toMine()
        {
            stage = Stage.toMine;
            Vector3D[] waypoints = (ctx.get("waypoints") as List<Vector3D>).ToArray();
            Vector3D prev = ctrl.GetPosition();
            for (int i = 0; i < waypoints.Length; i++)
            {
                proc.add(moveJob(waypoints[i], waypoints[i] - prev, i.ToString()));
                prev = waypoints[i];
            }
            proc.add(mineJob());
        }

        public void toDock()
        {
            stage = Stage.toMine;
            proc.clear();
            Vector3D[] waypoints = (ctx.get("waypoints") as List<Vector3D>).ToArray();
            Vector3D prev = ctrl.GetPosition();
            for (int i = waypoints.Length - 1; i >= 0; i--)
            {
                proc.add(moveJob(waypoints[i], waypoints[i] - prev, i.ToString()));
                prev = waypoints[i];
            }
            proc.add(dockJob());
        }

        public Job moveJob(Vector3D point, Vector3D path, string num = "") => new TheJob("movind", () =>
        {
            if (num != "") lcd.WriteText($"WPNum: {num}\n");
            var d = (ctrl.GetPosition() - point).Length();
            lcd.WriteText($"Distance: {d:F2}\n", true);
            lcd.WriteText($"Mass: {rider.getMass():F2}\n", true);
            if (rider.orient(path, ctrl.GetNaturalGravity()))
                return rider.toPoint(point, path);
            else rider.compensation();
            return false;
        });

        public Job unloadJob() {
            unloadTimer = (int)ctx.get("dockTimer") * 6;
            return new TheJob("unloading", () =>
            {
                lcd.WriteText("Unloading: " + unloadTimer.ToString() + "\n");
                lcd.WriteText("Mass: " + ctrl.CalculateShipMass().TotalMass + "\n", true);
                unloadTimer--;
                if (unloadTimer <= 0)
                {
                    proc.add(undockJob());
                    return true;
                }
                return false;
            });
        }

        public Job freeJob() => new TheJob("free", () =>
        {
            free();
            return true;
        });

        /// <summary>
        /// Задача по стыковке
        /// </summary>
        /// <returns></returns>
        public Job dockJob()
        {
            return new TheJob("docking", () =>
            {
                Vector3D p = (Vector3D)ctx.get("dockPoint");
                Vector3D dir = (Vector3D)ctx.get("dockDirection");
                IMyShipConnector conn = (IMyShipConnector)ctx.get("connector");
                if (conn == null)
                {
                    lcd.WriteText("Connector undefined", true);
                    return true; // закончить задачу
                }
                var d = (ctrl.GetPosition() - p).Length();
                lcd.WriteText($"Distance: {d:F2}\n", true);
                lcd.WriteText($"Mass: {rider.getMass():F2}\n", true);
                rider.orient(dir, ctrl.GetNaturalGravity());
                if (rider.toPoint(p, dir, d >2 ? 2f : 0.5f))
                { // Приблизились к коннектору
                    if (conn.Status == MyShipConnectorStatus.Connectable)
                    {
                        rider.free();
                        conn.Connect();
                        proc.add(unloadJob());
                        return true;
                    }
                }
                return false;
            });
        }

        /// <summary>
        /// Задача по расстыковке
        /// </summary>
        /// <returns></returns>
        public Job undockJob()
        {
            ((IMyShipConnector)ctx.get("connector")).Disconnect();
            return new TheJob("undock", () =>
            {
                Vector3D dir = ctrl.WorldMatrix.Forward;
                Vector3D p = (Vector3D)ctx.get("dockPoint") - 10 * dir;
                IMyShipConnector conn = (IMyShipConnector)ctx.get("connector");
                if (conn == null)
                {
                    lcd.WriteText("Connector undefined", true);
                    return true; // закончить задачу
                }
                rider.orient(dir, ctrl.GetNaturalGravity());
                if (rider.toPoint(p))
                {
                    toMine();
                    return true;
                }
                return false;
            });
        }

        public void addWaypoint()
        {
            ((List<Vector3D>)ctx.get("waypoints")).Add(ctrl.GetPosition());
        }

        public void setDock()
        {
            ctx.putForce("dockDirection", ctrl.WorldMatrix.Forward);
            List<IMyShipConnector> connectors = new List<IMyShipConnector>();
            gts.GetBlocksOfType(connectors, c => ((IMyShipConnector) c).Status == MyShipConnectorStatus.Connected && c.IsSameConstructAs(ctrl));
            connectors.ForEach(connector => {
                {
                    IMyShipConnector dockConnector = ctx.putForce("connector", connector);
                    Vector3D dockPoint = ctx.putForce("dockPoint", ctrl.GetPosition());
                    lcd.WriteText("Connector: " + dockConnector.CustomName + "\n");
                }
            });
        }
        private void setMineArea()
        {
            lcd.WriteText("MINING AREA SETTED");
            var mineArea = ctx.putForce("mineArea", ctrl.GetPosition());
            Vector3D mineDirection;
            Vector3D mineOrientation;
            if (ctrl.GetTotalGravity().Length() > 0.1)
            {
                mineDirection = Vector3D.Normalize(ctrl.GetTotalGravity());
                mineOrientation = Vector3D.Normalize(ctrl.GetTotalGravity().Cross(ctrl.WorldMatrix.Left));
            }
            else
            {
                mineDirection = ctrl.WorldMatrix.Down;
                mineOrientation = ctrl.WorldMatrix.Forward;
            }
            ctx.putForce("mineOrientation", mineOrientation);
            ctx.putForce("mineDirection", mineDirection);

            //mineGlissade = mineArea + rc.WorldMatrix.Up * 20 + rc.WorldMatrix.Backward * 20;
            lcd.WriteText("MINING AREA SETTED");
        }

        public void save()
        {
            MyIni ini = new MyIni();
            // dock
            ini.Set("dock", "dockPoint", util.vectorToGps((Vector3D)ctx.get("dockPoint"), "dockPoint"));
            ini.Set("dock", "dockDirection", util.vectorToGps((Vector3D)ctx.get("dockDirection"), "dockDirection"));
            ini.Set("dock", "connector", ((IMyShipConnector) ctx.get("connector")).CustomName);

            // waypoints
            Vector3D[] points = ((List<Vector3D>)ctx.get("waypoints")).ToArray();
            for(int i=0; i<points.Length; i++)
            {
                ini.Set("waypoints", i.ToString(), util.vectorToGps(points[i], "wp" + i));
            }

            // mining area
            ini.Set("mine", "mineArea", util.vectorToGps((Vector3D)ctx.get("mineArea"), "mineArea"));
            ini.Set("mine", "mineOrientation", util.vectorToGps((Vector3D)ctx.get("mineOrientation"), "mineOrientation"));
            ini.Set("mine", "mineDirection", util.vectorToGps((Vector3D)ctx.get("mineDirection"), "mineDirection"));

            // params
            ini.Set("params", "tunnelDepth", ctx.get("tunnelDepth").ToString());
            ini.Set("params", "dockTimer", ctx.get("dockTimer").ToString());
            ini.Set("params", "shaftL", ctx.get("shaftL").ToString());
            ini.Set("params", "shaftW", ctx.get("shaftW").ToString());
            ini.Set("params", "shaftN", ctx.get("shaftN").ToString());
            ini.Set("params", "permitMass", ctx.get("permitMass").ToString());
            ini.Set("params", "mineVelocity", ctx.get("mineVelocity").ToString());
            Me.CustomData = ini.ToString();
        }

        public void load()
        {
            MyIni ini = new MyIni();
            MyIniParseResult result;
            if (!ini.TryParse(Me.CustomData, out result))
                throw new Exception(result.ToString());
            if (ini.ContainsKey("dock", "dockPoint")) ctx.putForce("dockPoint", util.vectorFromGps(ini.Get("dock", "dockPoint").ToString()));
            if (ini.ContainsKey("dock", "dockDirection"))
            {
                ctx.putForce("dockDirection", util.vectorFromGps(ini.Get("dock", "dockDirection").ToString()));
                IMyShipConnector dConn = ctx.putForce("connector", gts.GetBlockWithName(ini.Get("dock", "connector").ToString()) as IMyShipConnector);
                if (dConn == null) lcd.WriteText("connector undefined\n"); else lcd.WriteText("dock loaded\n");
                dConn.Enabled = true;
            }

            // waypoints
            List<MyIniKey> keys = new List<MyIniKey>();
            List<Vector3D> wpts = ctx.putForce("waypoints", new List<Vector3D>());
            ini.GetKeys("waypoints", keys);
            keys.ForEach(k =>
            {
                string gps = ini.Get(k).ToString();
                wpts.Add(util.vectorFromGps(gps));
            });
            lcd.WriteText("waypoints loaded\n", true);

            //mine area
            ini.GetKeys("mine", keys);
            keys.ForEach(k =>
            {
                string gps = ini.Get(k).ToString();
                ctx.putForce(k.Name, util.vectorFromGps(gps));
                lcd.WriteText("loaded: " + k.Name + "\n", true);
            });

            if (ini.ContainsKey("params", "tunnelDepth")) ctx.putForce("tunnelDepth", ini.Get("params", "tunnelDepth").ToInt32());
            if (ini.ContainsKey("params", "dockTimer")) ctx.putForce("dockTimer", ini.Get("params", "dockTimer").ToInt32());
            if (ini.ContainsKey("params", "shaftL")) ctx.putForce("shaftL", float.Parse(ini.Get("params", "shaftL").ToString()));
            if (ini.ContainsKey("params", "shaftW")) ctx.putForce("shaftW", float.Parse(ini.Get("params", "shaftW").ToString()));
            if (ini.ContainsKey("params", "shaftN")) ctx.putForce("shaftN", ini.Get("params", "shaftN").ToInt32());
            if (ini.ContainsKey("params", "permitMass")) ctx.putForce("permitMass", ini.Get("params", "permitMass").ToInt32());
            if (ini.ContainsKey("params", "mineVelocity")) ctx.putForce("mineVelocity", float.Parse(ini.Get("params", "mineVelocity").ToString()));
        }

        class MinerProc : QueuedProc
        {
            private IMyTextSurface lcd;
            public MinerProc() : base("miner") {
                lcd = (IMyTextSurface)((IMyGridTerminalSystem)ctx.get("gts")).GetBlockWithName(ctx.get("lcdProcName").ToString());
            }

            public override Job exec()
            {
                lcd.WriteText("Action: " + (current()!= null ? current().name : "null") + "\n");
                return base.exec();
            }
        }
    }
}
