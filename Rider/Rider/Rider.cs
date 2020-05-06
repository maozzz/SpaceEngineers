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

namespace IngameScript
{
    partial class Program
    {

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

                this.gyroMult = ctx.contains("gyroMult") ? ((float) ctx.get("gyroMult")) : 1f;

                // Предрассчет ЛТХ
                recalcLTH();
            }

            public void recalcLTH()
            {
                this.mass = ctrl.CalculateShipMass().PhysicalMass;
                initThrusters();
                initGyros();
            }

            public void initGyros()
            {
                gts.GetBlocksOfType(gyrosAll);
            }

            /// <summary>
            /// Направляет нос корабля на точку. Вертикальную ось совмещает с orientation.
            /// Если сигналы по  осям меньше dW - возвращает true.
            /// </summary>
            /// <param name="direction"></param>
            public bool orient(Vector3D direction, Vector3D orientation, float dW)
            {
                Vector3D dir = Vector3D.Normalize(direction);
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
                lcd.WriteText(yaw.ToString() + " " + pitch.ToString() + " " + roll.ToString());
                return (yaw < dW && roll < dW && pitch < dW) ? true : false;
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

            public void toPoint(Vector3D point, float safeK = 0.8f)
            {
                ctrl.DampenersOverride = false;
                var path = point - ctrl.GetPosition(); // вектор до цели
                var len = path.Length();
                var vAbs = len > 10 ? Math.Sqrt(2 * len * weakA * safeK) : Math.Min(len, 2);
                var desiredV = Vector3D.Normalize(path) * vAbs; // Желаемый вектор скорости
                var desiredA = desiredV - ctrl.GetShipVelocities().LinearVelocity;
                thrustA(desiredA * 2 - ctrl.GetNaturalGravity());
            }

            public void compensation()
            {
                ctrl.DampenersOverride = false;
                thrustA(-ctrl.GetNaturalGravity());
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
        }

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
