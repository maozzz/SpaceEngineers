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
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game;
using VRageMath;
using System.IO;
using VRageRender;
using Sandbox.Game.Screens.Helpers.RadialMenuActions;

namespace IngameScript
{
    partial class Program
    {
        /// <summary>
        /// Параметры, берущиеся из контекта:
        /// gyroMult - множитель для вращения гироскопа. {"gyroMult", 2f},
        /// weakThrusters - enum из <code>ThrustDirection</code> определяющий на каком направлении самые слабые двигатели  {"weakThrusters", ThrustDirection.L}
        /// </summary>

        public class Rider
        {
            private float gyroMult;
            private IMyGridTerminalSystem gts;
            private IMyShipController ctrl;
            private IMyTextSurface lcd;
            private float mass;

            private List<IMyThrust> thrsAll = new List<IMyThrust>(); // все ускорители
            private Dictionary<ThrustDirection, PowerStat> thrsByDir; // Ускорители рассортированные по направлениям относительно кокпита

            private List<IMyGyro> gyrosAll = new List<IMyGyro>(); // Все гироскопы
            private float weakA = 0.1f; // Ускорение по самым слабым двигателям для рассчета безопасных скоростей.

            public Rider(IMyGridTerminalSystem gts, IMyShipController ctrl, IMyTextSurface lcd)
            {
                this.gts = gts;
                this.ctrl = ctrl;
                this.lcd = lcd;

                this.gyroMult = ctx.contains("gyroMult") ? ((float)ctx.get("gyroMult")) : 1f;

                recalcLTH();
            }

            /// <summary>
            /// Перерассчет ЛТХ ЛА
            /// </summary>
            public void recalcLTH()
            {
                this.mass = ctrl.CalculateShipMass().PhysicalMass;
                initThrusters();
                gts.GetBlocksOfType(gyrosAll); // инициализация гироскопов
            }


            /// <summary>
            /// Направляет нос корабля на точку. Направление вниз совмещает с orientation.
            /// Если сигналы по  осям меньше dW - возвращает true.
            /// </summary>
            /// <param name="direction"></param>
            public bool orient(Vector3D direction, Vector3D orientation, float dW = 0.1f)
            {
                Vector3D dir = Vector3D.Normalize(Vector3D.ProjectOnPlane(ref direction, ref orientation));
                Vector3D orient = Vector3D.Normalize(orientation);
                float yaw = (float)dir.Dot(ctrl.WorldMatrix.Right) * gyroMult;
                float pitch = (float)orient.Dot(ctrl.WorldMatrix.Backward) * gyroMult;
                float roll = (float)orient.Dot(ctrl.WorldMatrix.Left) * gyroMult;
                gyrosAll.ForEach(g =>
                {
                    g.GyroOverride = true;
                    g.Yaw = yaw;
                    g.Pitch = pitch;
                    g.Roll = roll;
                });
                return (Math.Abs(yaw) < dW) && (Math.Abs(roll) < dW) && (Math.Abs(pitch) < dW);
            }

            /// <summary>
            /// Распределяет трастеры по направлениям и заранее подсчитывает для них тягу и ускорение
            /// </summary>
            public void initThrusters()
            {
                gts.GetBlocksOfType(thrsAll);
                this.thrsByDir = new Dictionary<ThrustDirection, PowerStat>();
                foreach (ThrustDirection dir in Enum.GetValues(typeof(ThrustDirection)))
                {
                    thrsByDir.Add(dir, new PowerStat(mass));
                }
                var wm = ctrl.WorldMatrix;
                thrsAll.ForEach(th =>
                {
                    if (th.WorldMatrix.Backward.Dot(wm.Forward) > 0.9) thrsByDir[ThrustDirection.F].Add(th);
                    if (th.WorldMatrix.Backward.Dot(wm.Backward) > 0.9) thrsByDir[ThrustDirection.B].Add(th);
                    if (th.WorldMatrix.Backward.Dot(wm.Up) > 0.9) thrsByDir[ThrustDirection.U].Add(th);
                    if (th.WorldMatrix.Backward.Dot(wm.Down) > 0.9) thrsByDir[ThrustDirection.D].Add(th);
                    if (th.WorldMatrix.Backward.Dot(wm.Left) > 0.9) thrsByDir[ThrustDirection.L].Add(th);
                    if (th.WorldMatrix.Backward.Dot(wm.Right) > 0.9) thrsByDir[ThrustDirection.R].Add(th);
                });
                if (ctx.contains("weakThrusters")) this.weakA = thrsByDir[(ThrustDirection)ctx.get("weakThrusters")].getA();
            }

            /// <summary>
            /// Перемещение к точке. safeK - коэффициент запаса самого опасного ускорения (weakA).
            /// Когда расстояние меньше lenOk - цель достигнута - возвращаем true;
            /// </summary>
            /// <param name="point"></param>
            /// <param name="safeK">Коэффициент запаса по направлению самых слабых двигателей</param>
            /// <param name="logDist">Дистанция перехода на логарифмическое торможение</param>
            /// <param name="logE">Основание логарифма торможения</param>
            /// <param name="logK">Множитель логарифма</param>
            /// <param name="aMult">Мультипликатор ускорения</param>
            public bool toPoint(Vector3D point, float velocity = 0, float safeK = 0.8f, float lenOk = 0.1f, double logDist = 5, double logE = Math.E, float logK = 1, float aMult = 2)
            {
                ctrl.DampenersOverride = false;
                var path = point - ctrl.GetPosition(); // вектор до цели
                var len = path.Length();
                if (len < lenOk) return true;
                var vAbs = velocity == 0
                        ? len > logDist ? Math.Sqrt(2 * len * weakA * safeK) : Math.Log(len + 1, logE) * logK
                        : velocity;
                var desiredV = Vector3D.Normalize(path) * vAbs; // Желаемый вектор скорости
                var desiredA = desiredV - ctrl.GetShipVelocities().LinearVelocity;
                thrustA(desiredA * aMult - ctrl.GetNaturalGravity());
                return false;
            }

            /// <summary>
            /// Перемещение к точке вдоль вектора. safeK - коэффициент запаса самого опасного ускорения (weakA).
            /// Когда расстояние меньше lenOk - цель достигнута - возвращаем true;
            /// </summary>
            /// <param name="point"></param>
            /// <param name="pathVec">Вектор движения, вдоль которого надо двигаться.</param>
            /// <param name="pathVecK">Коэффициент притяжения к вектору пути.</param>
            /// <param name="safeK">Коэффициент запаса по направлению самых слабых двигателей</param>
            /// <param name="logDist">Дистанция перехода на логарифмическое торможение</param>
            /// <param name="logK">Основание логарифма торможения</param>
            /// <param name="aMult">Мультипликатор ускорения</param>
            public bool toPoint(Vector3D point, Vector3D pathVec, float velocity = 0, float pathVecK = 1, float safeK = 0.8f, float lenOk = 0.1f, double logDist = 5, double logE = Math.E, float logK = 1, float aMult = 2)
            {
                var path = point - ctrl.GetPosition(); // вектор до цели
                var newPoint = point + Vector3D.ProjectOnPlane(ref path, ref pathVec) * pathVecK;
                return toPoint(newPoint, velocity, safeK, lenOk, logDist, logE, logK, aMult);
            }

            public void compensation(bool damp = true, float dampK = 1)
            {
                ctrl.DampenersOverride = false;
                Vector3D desired = -ctrl.GetNaturalGravity();
                if (damp) desired -= dampK * ctrl.GetShipVelocities().LinearVelocity;
                thrustA(desired);
            }

            /// <summary>
            /// Выставляет ускорители на заданное ускорение
            /// </summary>
            /// <param name="a"></param>
            public void thrustA(Vector3D a)
            {
                var pF = (float)a.Dot(ctrl.WorldMatrix.Forward);
                var pL = (float)a.Dot(ctrl.WorldMatrix.Left);
                var pU = (float)a.Dot(ctrl.WorldMatrix.Up);

                thrsByDir[ThrustDirection.F].overrideA(pF);
                thrsByDir[ThrustDirection.B].overrideA(-pF);
                thrsByDir[ThrustDirection.L].overrideA(pL);
                thrsByDir[ThrustDirection.R].overrideA(-pL);
                thrsByDir[ThrustDirection.U].overrideA(pU);
                thrsByDir[ThrustDirection.D].overrideA(-pU);
            }

            public void free()
            {
                releaseEngines();
                releaseGyros();
            }

            public void releaseGyros()
            {
                gyrosAll.ForEach(g => g.GyroOverride = false);
            }

            /// <summary>
            /// Отпускает управление двигателями
            /// </summary>
            public void releaseEngines()
            {
                ctrl.DampenersOverride = true;
                thrsAll.ForEach(th => th.ThrustOverride = 0);
            }

            public float getMass() => mass;
        }
        // END OF RIDER

        public enum ThrustDirection
        {
            F, B, U, D, L, R
        }

        /// <summary>
        /// Класс для агрегирования характеристик двигателей по направолениям корабля.
        /// </summary>
        public class PowerStat
        {
            private List<IMyThrust> thrusters = new List<IMyThrust>();
            private float f = 0;
            private float a = 0;
            private float mass;

            public PowerStat(float mass) { this.mass = mass; }
            public void Add(IMyThrust th)
            {
                thrusters.Add(th);
                f += th.MaxEffectiveThrust;
                a = f / mass;
            }

            public void overrideA(float val)
            {
                var perc = val / a;
                thrusters.ForEach(t => t.ThrustOverridePercentage = perc);
            }

            public List<IMyThrust> getThrusters() => thrusters;
            public float getF() => f;
            public float getA() => a;
            public float getMass => mass;
        }
    }
}
