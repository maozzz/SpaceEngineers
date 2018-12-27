using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Sandbox.Game.Entities.Blocks;
using Sandbox.ModAPI.Ingame;
using VRage.Game.VisualScripting.Utils;
using VRageMath;

public sealed class Miner2 : MyGridProgram {
    public static Context ctx = new Context(new Dictionary<string, object> {
        {"lcdName", "lcd"},
        {"lcdLName", "lcdL"},
        {"lcdRName", "lcdR"},
        {"permitMass", 50000},
        {"shaftN", 0},
        {"shaftW", 6f},
        {"shaftL", 6f},
        {"tunnelDepth", 20}
    });

    /**
     * Названия
     */
    public Util util;
    public Menu menu;

    public enum Stage {
        toDock,
        pause,
        mine,
        docked,
        undock
    }

    private int stageStatus;

    private Stage stage = Stage.pause;

    public Rider rider;
    public IMyTextPanel lcd;
    public IMyTextPanel lcdL;
    public IMyTextPanel lcdR;
    public IMyRemoteControl rc;

    private List<IMyShipConnector> connectors = new List<IMyShipConnector>();

    private Vector3D dockPoint;
    private Vector3D dockDirection;
    private Vector3D dockGlissade;
    private IMyShipConnector dockConnector;

    private Vector3D mineArea;
    private Vector3D mineDirection;
    private Vector3D mineGlissade;
    private Vector3D mineOrientation;
    private Vector3D currentTunnelPoint;
    
    private int shaftsLimit = 400;
    private float mineSpeedK = 0.6f;
    private bool systemOverload;

    private int timerTicks;
    private StackProc process = new StackProc();

    public Program() {
        init();
    }

    private void init() {
        ctx.put("gts", GridTerminalSystem);
        ctx.put("runtime", Runtime);
        util = ctx.put("utils", new Util());
        GridTerminalSystem.GetBlocksOfType(connectors, connector => true);
        ctx.put("rc", util.findBlockByType(ref rc));
        lcd = ctx.put("lcd", GridTerminalSystem.GetBlockWithName(ctx.get("lcdName") as string) as IMyTextPanel);
        lcdL = ctx.put("lcdL", GridTerminalSystem.GetBlockWithName(ctx.get("lcdLName") as string) as IMyTextPanel);
        lcdR = ctx.put("lcdR", GridTerminalSystem.GetBlockWithName(ctx.get("lcdRName") as string) as IMyTextPanel);
        ctx.put("rider", new Rider(lcdR));
        ctx.get(ref rider);
        Runtime.UpdateFrequency = UpdateFrequency.Update10;

        initMenu(lcd);
    }

    public void Main(string argument, UpdateType updateSource) {
        ctx.putForce("arg", argument);
        ctx.tick();

        if (timerTicks > 0) timerTicks--;
        rider.tick();
        if (Context.ticks % 60 == 0 || stage == Stage.docked) {
            checkSystem();
        }

        if (systemOverload) {
            lcdL.WritePublicText("SYSTEM OVERLOADED!!!\n", true);
        }

        switch (argument) {
            case "saveDock":
                dockDirection = rc.WorldMatrix.Forward;
                connectors.ForEach(connector => {
                    if (connector.Status == MyShipConnectorStatus.Connected) {
                        dockConnector = connector;
                        dockPoint = rc.GetPosition() + dockConnector.WorldMatrix.Forward * 0.3;
                        dockGlissade = dockPoint + dockConnector.WorldMatrix.Backward * 10 + rc.WorldMatrix.Up * 5;
                    }
                });
                break;
            case "toDock":
                toDock();
                break;
            case "miningArea":
                setMiningArea();
                break;
            case "mine":
                mine();
                break;
            case "pause":
                pause();
                break;
        }

        switch (stage) {
            case Stage.toDock:
                toDock();
                break;
            case Stage.mine:
                mine();
                break;
            case Stage.docked:
                docked();
                break;
            case Stage.undock:
                undock();
                break;
        }

        if (stage != Stage.pause)
            process.exec();
        if (Context.ticks % 60 == 0 || ctx.get("arg").ToString() != null)
            lcd.WritePublicText(menu.exec());
    }

    private void initMenu(IMyTextPanel lcd) {
        menu = ctx.put("menu", new SimpleMenu("menu", null));
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

        Menu permMass = new SimpleMenu("permit mass", submenu);
        permMass.add(new ChangeVarMenuItem("permitMass", 1000, "pm+", "={0}"));
        permMass.add(new ChangeVarMenuItem("permitMass", -1000, "pm-", "={0}"));
        submenu.add(permMass);

        menu.add(new SimpleMenuItem(new SimpleMsg("item-01", "text of item 01", null)));
        menu.add(submenu);
        menu.add(new SimpleMenuItem(new SimpleMsg("item-02", "text of item 02", null)));
    }

    private void setMiningArea() {
        lcdL.WritePublicText("MINING AREA SETTED");
        mineArea = rc.GetPosition();
        if (rc.GetTotalGravity().Length() > 0.1) {
            mineDirection = Vector3D.Normalize(rc.GetTotalGravity());
            mineOrientation = Vector3D.Normalize(rc.GetTotalGravity().Cross(rc.WorldMatrix.Left));
        } else {
            mineDirection = rc.WorldMatrix.Down;
            mineOrientation = rc.WorldMatrix.Forward;
        }

        mineGlissade = mineArea + rc.WorldMatrix.Up * 30 + rc.WorldMatrix.Backward * 10;
        lcdL.WritePublicText("MINING AREA SETTED");
    }

    /**
     * Смещение точки бурения
     */
    private Vector3D GetSpiralXY(int p, float W, float L, int n = 20) {
        int positionX = 0, positionY = 0, direction = 0, stepsCount = 1, stepPosition = 0, stepChange = 0;
        int X = 0;
        int Y = 0;
        for (int i = 0; i < n * n; i++) {
            if (i == p) {
                X = positionX;
                Y = positionY;
                break;
            }

            if (stepPosition < stepsCount) {
                stepPosition++;
            } else {
                stepPosition = 1;
                if (stepChange == 1) {
                    stepsCount++;
                }

                stepChange = (stepChange + 1) % 2;
                direction = (direction + 1) % 4;
            }

            if (direction == 0) {
                positionY++;
            } else if (direction == 1) {
                positionX--;
            } else if (direction == 2) {
                positionY--;
            } else if (direction == 3) {
                positionX++;
            }
        }

        return new Vector3D(X * L, 0, Y * W);
    }

    private void checkSystem() {
        rider.resetMass();
        int permitMass = (int) ctx.get("permitMass");
        systemOverload = rider.totalMass >
                         permitMass + (stage == Stage.mine && stageStatus == 2 ? 0.1 * permitMass : 0);
    }

    private void pause() {
        stage = Stage.pause;
        rider.freeControl();
        drillsOn(false);
        Runtime.UpdateFrequency = UpdateFrequency.Update100;
    }

    private void docked() {
        if (stage != Stage.docked) {
            stage = Stage.docked;
            timerTicks = 6;
        }

        lcdL.WritePublicText($"TIMER: {timerTicks}\n", true);
        if (timerTicks > 0) return;
        if (!systemOverload) {
            stage = Stage.undock;
        }
    }

    private void undock() {
        dockConnector.Disconnect();
        Runtime.UpdateFrequency = UpdateFrequency.Update10;
        if (rider.moveTo(dockGlissade, dockDirection, dockDirection)) {
            mine();
        }
    }

    private void mine() {
        if (stage != Stage.mine) {
            stageStatus = 0;
            stage = Stage.mine;
            return;
        }

        switch (stageStatus) {
            case 0: // подходим к глиссаде
                if (rider.moveTo(mineGlissade)) stageStatus++;
                break;
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
                break;
        }
    }

    private Boolean upToHoriz() {
        drillsOn(false);
        Vector3D path = currentTunnelPoint - rc.GetPosition();
        if (path.Normalize() < 1) {
            stageStatus = 1;
        }

        path *= mineSpeedK;
        rider.moveTo(rc.GetPosition() + path, mineDirection, mineOrientation);
        return false;
    }

    private Boolean doMine() {
        if (systemOverload) {
            return true;
        }

        drillsOn(true);
        int tunnelDepth = (int) ctx.get("tunnelDepth");
        Vector3D floor = currentTunnelPoint + mineDirection * tunnelDepth;
        Vector3D path = floor - rc.GetPosition();
        if (path.Length() < 1) {
            ctx.putForce("shaftN", (int) ctx.get("shaftN") + 1);
            return true;
        }

        path = mineSpeedK * Vector3D.Normalize(path);
        rider.moveTo(rc.GetPosition() + path, mineDirection, mineOrientation);
        return false;
    }

    private void drillsOn(Boolean on) {
        GridTerminalSystem.GetBlocksOfType(new List<IMyShipDrill>(), drill => {
            drill.Enabled = on;
            return false;
        });
    }

    private Boolean toMineStartArea() {
        if (systemOverload) {
            if (rider.moveTo(mineGlissade, -mineOrientation)) {
                stageStatus = 4;
            }

            return false;
        }

        Vector3D spiralXy = GetSpiralXY((int) ctx.get("shaftN"),(float) ctx.get("shaftW"), (float) ctx.get("shaftL"), shaftsLimit);
//        lcd.WritePublicText(spiralXy.ToString(), true);
        currentTunnelPoint = mineArea + spiralXy.X * mineOrientation +
                             spiralXy.Z * Vector3D.Cross(mineDirection, mineOrientation);
        return rider.moveTo(currentTunnelPoint, mineOrientation);
    }

    private void toDock() {
        if (stage != Stage.toDock) {
            stage = Stage.toDock;
            stageStatus = 1;
            return;
        }

        if (dockConnector == null) {
//            lcd.WritePublicText("connection undefined\n");
            docked();
            return;
        }

        switch (stageStatus) {
            case 0: // конектимся
                rider.moveTo(dockPoint, dockDirection);
                if (dockConnector.Status == MyShipConnectorStatus.Connectable) {
                    dockConnector.Connect();
                    Runtime.UpdateFrequency = UpdateFrequency.Update100;
                    docked();
                }

                break;
            case 1: // подходим к точке захода
                if (rider.moveTo(dockGlissade)) {
                    stageStatus = 0;
                }

                break;
        }
    }


/* ==============================================================
 * =============== КЛАСС RIDER ДЛЯ ПЕРЕМЕЩЕНИЯ ==================
 * ============================================================*/
    public class Rider {
        private IMyRemoteControl rc;
        private List<IMyGyro> gyro = new List<IMyGyro>();
        private IMyTextPanel lcd;
        private IMyGridTerminalSystem gts;

        private int ticks;
        private float gyroK = 1.5f;
        private float powerK = 0.1f;
        private float nonLinearPowerK = 0.2f;

        public float totalMass;
        private IMyGridProgramRuntimeInfo runtime;

        public enum Direction {
            F,
            B,
            U,
            D,
            L,
            R
        }

        public struct PowerStat {
            public List<IMyThrust> thrusters;
            public float f;
            public float a;
            public Direction opposite;

            public PowerStat(List<IMyThrust> thrusters, float a, float f, Direction opposite) {
                this.a = a;
                this.f = f;
                this.thrusters = thrusters;
                this.opposite = opposite;
            }
        }

        public Dictionary<Direction, PowerStat> stats = new Dictionary<Direction, PowerStat>();
        private List<IMyThrust> thAll = new List<IMyThrust>();

        public Rider(IMyTextPanel lcd) {
            ctx.get(ref gts);
            ctx.get(ref rc);
            ctx.get(ref runtime);
            gts.GetBlocksOfType(gyro, g => true);
            this.lcd = lcd;
            gts.GetBlocksOfType(thAll, thrust => true);
            rc.ControlThrusters = true;
            resetMass();
            resetForceStats(Direction.U, Direction.D);
            resetForceStats(Direction.D, Direction.U);
            resetForceStats(Direction.F, Direction.B);
            resetForceStats(Direction.B, Direction.F);
            resetForceStats(Direction.L, Direction.R);
            resetForceStats(Direction.R, Direction.L);
        }

        public void tick() {
            ticks++;
            if (ticks % 500 != 0) return;
            resetMass();
            resetForceStats(Direction.U, Direction.D, true);
            resetForceStats(Direction.D, Direction.U, true);
            resetForceStats(Direction.F, Direction.B, true);
            resetForceStats(Direction.B, Direction.F, true);
            resetForceStats(Direction.L, Direction.R, true);
            resetForceStats(Direction.R, Direction.L, true);
        }

        /**
         * Освобождение управления
         */
        public void freeControl() {
            freeEngines();
            gyro.ForEach(myGyro => myGyro.GyroOverride = false);
        }

        public void freeEngines() {
            rc.ControlThrusters = false;
            rc.DampenersOverride = true;
            thAll.ForEach(thrust => thrust.ThrustOverride = 0);
        }

        /**
         * Просчитываем силовые характеристики ЛА
         * a_only = пересчет только ускорения
         */
        private void resetForceStats(Direction dir, Direction oppDir, Boolean a_only = false) {
            if (a_only) {
                stats[dir] = new PowerStat(stats[dir].thrusters, stats[dir].a, stats[dir].f, oppDir);
                return;
            }

            var ths = new List<IMyThrust>();
            PowerStat stat;
            switch (dir) {
                case Direction.U:
                    gts.GetBlocksOfType(ths, thrust => thrust.GridThrustDirection.Y < 0);
                    break;
                case Direction.D:
                    gts.GetBlocksOfType(ths, thrust => thrust.GridThrustDirection.Y > 0);
                    break;
                case Direction.F:
                    gts.GetBlocksOfType(ths, thrust => thrust.GridThrustDirection.Z > 0);
                    break;
                case Direction.B:
                    gts.GetBlocksOfType(ths, thrust => thrust.GridThrustDirection.Z < 0);
                    break;
                case Direction.L:
                    gts.GetBlocksOfType(ths, thrust => thrust.GridThrustDirection.X > 0);
                    break;
                case Direction.R:
                    gts.GetBlocksOfType(ths, thrust => thrust.GridThrustDirection.X < 0);
                    break;
            }

            stat.thrusters = ths;
            stat.a = stat.f = 0;
            ths.ForEach(thrust => stat.f += thrust.MaxEffectiveThrust);
            stat.a = stat.f / totalMass;
            stat.opposite = oppDir;
            stats[dir] = stat;
        }

        /**
         * Перемещение к точке: true - точка достигнута
         */
        public bool moveTo(Vector3D point) {
            Vector3D path = new Vector3D(point.X, point.Y, point.Z) - rc.GetPosition();
            Vector3D dir = path;
            dir.Normalize();
            var length = path.Length();
            runtime.UpdateFrequency = length > 5 ? UpdateFrequency.Update10 : UpdateFrequency.Update1;
            if (length > 0.3) {
                if (length > 2) {
                    if (setDirection(path))
                        move(path);
                    else
                        freeEngines();
                } else {
                    move(path);
                }

                return false;
            }

            freeControl();
            return true;
        }

        public bool moveTo(Vector3D point, Vector3D direction) {
            return moveTo(point, direction, direction);
        }

        public bool moveTo(Vector3D point, Vector3D direction, Vector3D orintation) {
            Vector3D path = new Vector3D(point.X, point.Y, point.Z) - rc.GetPosition();
            var length = path.Length();
            path += path - direction * length;
            runtime.UpdateFrequency = length > 5 ? UpdateFrequency.Update10 : UpdateFrequency.Update1;
            if (length > 0.2) {
                if (setDirection(orintation))
                    move(path);
                else
                    freeEngines();
                return false;
            }

            freeControl();
            return true;
        }

        /**
         * Рассчет необходимого ускорения.
         */
        private void move(Vector3D path) {
            lcd.WritePublicText($"DISTANCE: {path.Length(),8:F2}");
            thAll.ForEach(thrust => thrust.ThrustOverridePercentage = 0);
            powerDirection(path, rc.WorldMatrix.Forward, Direction.F, Direction.B);
            powerDirection(path, rc.WorldMatrix.Left, Direction.L, Direction.R);
            powerDirection(path, rc.WorldMatrix.Up, Direction.U, Direction.D);
        }

        /**
         * Управление трастерами по конкретной оси
         */
        public void powerDirection(Vector3D path, Vector3D matrixDirection, Direction direction, Direction opp) {
            var s = path.Dot(matrixDirection);
            var grav = -rc.GetTotalGravity().Dot(matrixDirection);
            var currVel = rc.GetShipVelocities().LinearVelocity.Dot(matrixDirection) * (s > 0 ? 1 : -1);
            var t0 = Math.Abs(s / currVel) * (currVel < 0 ? 3 : 1); // время прибытия
            float a = s >= 0 ? stats[direction].a : stats[opp].a;
            if (a == 0) a = (float) rc.GetTotalGravity().Length();
            var tt = currVel / a;
            tt += tt < 0 ? -2 : +2; // время остановки
            PowerStat stat; // выбираем нужные двигатели
            if (tt < t0) // Надо ускориться по path
                stat = s > 0 ? stats[direction] : stats[opp];
            else // надо ускориться против path
                stat = s > 0 ? stats[opp] : stats[direction];
            if (Math.Abs(grav) > 0.1) grav = (float) (stat.a / grav);
            power(stat.thrusters, (float) Math.Abs(Math.Pow(Math.Abs(s * powerK), nonLinearPowerK) + grav));
            if (!stat.thrusters.Any()) // рассчет на гравитацию
                power(stats[stat.opposite].thrusters, 0.01f);
        }

        /**
         * Установка ориентации корабля. При гравитации сохраняется вертикальное положение.
         */
        public Boolean setDirection(Vector3D direct) {
            gyro.ForEach(g => g.GyroOverride = true);
            float roll = 0, pitch = 0, yaw = 0;
            yaw = gyroK * (float) rc.WorldMatrix.Right.Dot(direct / direct.Length());

            if (rc.GetTotalGravity().Length() > 0.1) {
                roll = gyroK * (float) (rc.WorldMatrix.Left.Dot(rc.GetTotalGravity()) /
                                        rc.GetTotalGravity().Length());
                pitch = gyroK * (float) (rc.WorldMatrix.Backward.Dot(rc.GetTotalGravity()) /
                                         rc.GetTotalGravity().Length());
            } else {
                pitch = gyroK * (float) rc.WorldMatrix.Down.Dot(direct / direct.Length());
            }

            gyro.ForEach(g => {
                g.Yaw = yaw;
                g.Roll = roll;
                g.Pitch = pitch;
            });
            return Math.Abs(yaw) < 0.01 && Math.Abs(pitch) < 0.01 && Math.Abs(roll) < 0.01;
        }

        /**
         * Пересчет массы ЛА
         */
        public float resetMass() {
            totalMass = rc.CalculateShipMass().PhysicalMass;
            return totalMass;
        }

        /**
         * установка тяги power = 0 - 1
         */
        private void power(List<IMyThrust> ths, float power) {
            ths.ForEach(thrust => { thrust.ThrustOverride = thrust.MaxThrust * power * power; });
        }
    }

/* ==============================================================
 * =================== ОБЩЕГО НАЗНАЧЕНИЯ ========================
 * ============================================================*/
    public interface Process : Job {
        void add(Job j);
        Job current();
        Job[] all();
    }

    public interface Job {
        Job exec();
    }

    public class TheJob : Job {
        private Func<Job> func;

        public TheJob(Func<Job> func) {
            this.func = func;
        }

        public Job exec() => func.Invoke();
    }

    public class StackProc : Process {
        private Stack stack = new Stack();

        public Job exec() {
            if (stack.Count == 0) return null;
            var job = stack.Peek() as Job;
            var res = job.exec();
            if (res == null) stack.Pop(); // задача закончилась
            else if (res != job) stack.Push(res); // новая подзадача
            else ; // иначе - задача не закончена
            return stack.Count > 0 ? this : null;
        }

        public void add(Job j) => stack.Push(j);
        public Job current() => (Job) stack.Peek();
        public Job[] all() => (Job[]) stack.ToArray();
    }

    public class QueuedProc : Process {
        private Queue q = new Queue();

        public Job exec() {
            if (q.Count == 0) return null;
            var job = q.Peek() as Job;
            var res = job.exec();
            if (res == null) q.Dequeue(); // задача закончилась
            else if (res != job) q.Enqueue(res); // новая подзадача
            else ; // иначе - задача не закончена
            return q.Count > 0 ? this : null;
        }

        public void add(Job j) {
            q.Enqueue(j);
        }

        public Job current() => (Job) q.Peek();

        public Job[] all() => (Job[]) q.ToArray();
    }

    public class TransferJob : Job {
        private Rider rider;
        private Vector3D pos1, pos2;
        private bool reverse;

        public TransferJob(Vector3D pos1, Vector3D pos2) {
            ctx.get(ref rider);
            this.pos2 = pos2;
            this.pos1 = pos1;
        }

        public Job exec() {
            if (rider.moveTo(reverse ? pos1 : pos2)) {
                reverse = !reverse;
            }
            return this;
        }
    }

    public class Context : StackProc, Tickable {
        public static int ticks;
        private Dictionary<String, object> data;
        private List<Timer> timers = new List<Timer>();

        public Context(Dictionary<string, object> initData) {
            data = initData;
        }

        public T put<T>(String key, T val) {
            if (data.ContainsKey(key)) throw new Exception("Attempt to rewrite context value");
            data[key] = val;
            return val;
        }

        public T putForce<T>(String key, T val) {
            rm(key);
            data[key] = val;
            return val;
        }

        public void tick() {
            ticks++;
            timers.ForEach(timer => {
                timer.tick();
                if (timer.isOutdated()) timers.Remove(timer);
            });
        }

        public bool rm(string key) => data.ContainsKey(key) && data.Remove(key);
        public object get(string key) => data.GetValueOrDefault(key);
        public IEnumerable<T> get<T>(ref IEnumerable<T> t) => t = data.Values.OfType<T>();
        public T get<T>(ref T t) where T : class => t = data.Values.OfType<T>().FirstOrDefault();

        public void setTimer(Timer timer) => timers.Add(timer);
    }

    public class Timer : Tickable {
        private bool cyclic;
        private int est, timeout;
        private Action action;

        public Timer(int est, bool cyclic, Action action) {
            this.timeout = this.est = est;
            this.cyclic = cyclic;
            this.action = action;
        }

        public void tick() {
            if (!isOutdated()) est--;
            if (est == 0) {
                action();
                est--;
                if (cyclic) est = timeout;
            }
        }

        public bool isOutdated() => est < 0;
    }

    public class Util : MyGridProgram {
        private IMyGridTerminalSystem gts;

        public Util() {
            ctx.get(ref gts);
        }

        public T findBlockByType<T>(ref T t) where T : class {
            List<T> list = new List<T>();
            gts.GetBlocksOfType(list, arg => true);
            return t = list.First();
        }
    }

    public interface Tickable {
        void tick();
    }

/* ==============================================================
 * =================== КЛАССЫ ДЛЯ МЕНЮ ==========================
 * ==============================================================
 * Если идти снизу вверх: то понарастающей видна реализация.   ==
 * Msg - простой интерфейс для хранения необходимого           ==
 *                                                             ==
 * ReactMsg = Msg                                              ==
 *     + exec()                                                ==
 * exec - должен возвращать инфу сообщения/пунска меню         ==
 *                                                             ==
 * MenuItem = ReactMsg добавляется                             ==
 *     + Activable                                             ==
 *     + Focusable                                             ==
 * для хранения информации внутри элемента. Меню хранит        ==
 * эту же логику в себе.                                       ==
 *                                                             ==
 * Menu = MenuItem                                             ==
 *     + add - добавление пункта меню                          ==
 *     + deactivateSubmenu - для возврата из подменю           ==
 *                                                             ==
 * ChangeVarMenuItem : SimpleMenuItem                          ==
 *     реализация для изменения переменных.                    ==
 *     Пришлось сломать логику Activable, т.к. по ним          ==
 *     надо увеличивать/уменьшать переменную                   ==
 *     Само действие с переменной передается при создании      ==
 *     экземпляра. А там должен быть доступ к переменной,      ==
 *     которую собираемся менять.                              ==
 * ============================================================*/
    public class SimpleMenu : SimpleMenuItem, Menu {
        private List<MenuItem> items = new List<MenuItem>();
        private Menu parent;
        private MenuItem activeItem, focusedItem;
        private const int width = 10;

        public SimpleMenu(string title, Menu parent) : base(new SimpleMsg(title, null, parent)) {
            this.parent = parent;
            if (parent != null)
                add(new SimpleMenuItem(new SimpleReactMsg("..", this, () => {
                    if (activeItem != null) deactivateSubmenu();
                    parent.deactivateSubmenu();
                    return "";
                })));
        }

        public void add(MenuItem item) => items.Add(item);
        public void deactivateSubmenu() => changeActiveStatus(ref activeItem);

        public void next() => items.ForEach(i => {
            if (i.isFocused()) {
                changeFocusStatus(ref i);
            } else if (focusedItem == null) {
                changeFocusStatus(ref i);
            }
        });

        public override string exec() {
            if (!(activeItem is Menu)) {
                if (ctx.get("arg").ToString() == "next") next();
                if (focusedItem == null && items.Count > 0) // Если не заполнено focusedItem - ищем выбранный
                {
                    var a = items.FirstOrDefault(i => i.isFocused());
                    if (a == null) { // если выбранного нет - выбираем первый попавшийся
                        focusedItem = items.First();
                        changeFocusStatus(ref focusedItem);
                    }
                }
                if (ctx.get("arg").ToString() == "exec") { // если команда на выполнение - активируем/деактивируем
                    changeActiveStatus(ref focusedItem);
                    ctx.putForce("arg", "");
                }
            }

            // Получаем результат выполнения активного
            Queue<string> q = activeItem != null
                ? new Queue<string>(activeItem.exec().Split('\n'))
                : new Queue<string>();

            var concat = string.Concat(items.Select(i => {
                var str = i.getTitle();
                return
                    $"{(i.isFocused() ? ">" : " ")}{(i.isActive() ? "|" : " ")}{str.Substring(0, str.Length < width ? str.Length : width),-width}|{(q.Count > 0 ? q.Dequeue() : "")}\n";
            }));
            while (q.Count > 1) { // 1 - потому что у него последняя - пустая строка
                var str = $"  {"",-width}|{(q.Count > 0 ? q.Dequeue() : "")}\n";
                if (str != "") concat += str;
            }
            return concat;
        }

        private void changeActiveStatus(ref MenuItem i) {
            if (i.isActive()) {
                i.deactivate();
                activeItem = null;
            } else {
                i.activate();
                if (activeItem != null && activeItem != i) activeItem.deactivate();
                activeItem = i;
            }
            if (activeItem != null && activeItem is Menu) {
                items.ForEach(it => {
                    if (it.isFocused()) changeFocusStatus(ref it);
                });
            }
        }

        private void changeFocusStatus(ref MenuItem i) {
            if (i.isFocused()) {
                i.focusOff();
                focusedItem = null;
            } else {
                i.focusOn();
                focusedItem = i;
            }
        }
    }


/**
 * Реализация простого пункта меню
 */
    public class SimpleMenuItem : SimpleReactMsg, MenuItem {
        Msg msg;
        private bool active;
        private bool focused;

        public SimpleMenuItem(ReactMsg msg) : base(msg.getTitle(), msg.getSrc(), msg.exec) {
            this.msg = msg;
        }

        public SimpleMenuItem(Msg msg) : base(msg.getTitle(), msg.getSrc(), msg.getText) {
            this.msg = msg;
        }

        public virtual void activate() => active = true;
        public virtual void deactivate() => active = false;
        public virtual bool isActive() => active;
        public void focusOn() => focused = true;
        public void focusOff() => focused = false;
        public bool isFocused() => focused;
    }

    public class ChangeVarMenuItem : SimpleMenuItem {
        private Action action;

        public ChangeVarMenuItem(ReactMsg msg, Action action) : base(msg) {
            this.action = action;
        }

        public ChangeVarMenuItem(string ctxVarName, object dT, string title, string text) : base(new SimpleReactMsg(
            title,
            null, () => string.Format(text, ctx.get(ctxVarName)))) {
            action = () => {
                var o = ctx.get(ctxVarName);
                if (o is int) {
                    ctx.putForce(ctxVarName, (int) o + int.Parse(dT.ToString()));
                } else if (o is float) {
                    ctx.putForce(ctxVarName, (float) o + float.Parse(dT.ToString()));
                } else {
                    ctx.putForce(ctxVarName, (double) o + double.Parse(dT.ToString()));
                }
            };
        }

        public override void activate() => action();

        public override void deactivate() {
            if (isFocused()) action();
        }

        public override bool isActive() => false;
    }

/**
 * Сообщение: которое может выполнять действие
 */
    public class SimpleReactMsg : SimpleMsg, ReactMsg {
        private Func<string> func;

        public SimpleReactMsg(string title, object srcObject, Func<string> func)
            : base(title, null, srcObject) {
            this.func = func;
        }

        public virtual string exec() => func();
    }

/**
 * Сообщение для вывода простого текста
 */
    public class SimpleMsg : Msg {
        private object src;
        private string title;
        private string text;

        public SimpleMsg(string title, string text, object srcObject) {
            this.src = srcObject;
            this.title = title;
            this.text = text;
        }

        public object getSrc() => src;
        public string getTitle() => title;
        public string getText() => text;
    }

    public interface Menu : MenuItem {
        void add(MenuItem item);
        void deactivateSubmenu();
    }

    public interface MenuItem : ReactMsg, Activable, Focusable { }

    public interface ReactMsg : Msg {
        string exec();
    }

    public interface Msg {
        object getSrc();
        string getTitle();
        string getText();
    }

    public interface Activable {
        void activate();
        void deactivate();
        bool isActive();
    }

    public interface Focusable {
        void focusOn();
        void focusOff();
        bool isFocused();
    }

/**
 *^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
 */
}