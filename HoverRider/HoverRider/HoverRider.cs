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

namespace IngameScript
{
    partial class Program
    {
        public enum HoverMode
        {
            light, agressive
        }
        public class HoverRider
        {
            private float gyroMult;
            IMyGridTerminalSystem gts;
            IMyTextSurface lcd;
            List<IMyThrust> thrusters = new List<IMyThrust>();
            List<IMyGyro> gyros = new List<IMyGyro>();
            IMyShipController ctrl;
            float sumA = 0;
            float sumF = 0;
            float mass;
            float maxAHor = 0; // Макмимальное возможное горизонтальное ускорение (от гравитации)
            Vector3D grav; // вектор гравитации
            HoverMode mode = HoverMode.light;
            public HoverRider(IMyGridTerminalSystem gts, IMyTextSurface lcd, IMyShipController ctrl)
            {
                this.gts = gts;
                this.lcd = lcd;
                this.ctrl = ctrl;
                this.gyroMult = ctx.contains("gyroMult") ? ((float)ctx.get("gyroMult")) : 1f;
                recalcLTH();
            }

            public void recalcLTH()
            { 
                grav = ctrl.GetNaturalGravity();
                double gravLen = grav.Length();
                mass = ctrl.CalculateShipMass().PhysicalMass;
                sumF = 0;
                sumA = 0;
                gts.GetBlocksOfType(thrusters);
                thrusters.ForEach(t => sumF += t.MaxEffectiveThrust);
                sumA = sumF / mass;
                maxAHor = (float) Math.Sqrt(sumA * sumA - gravLen * gravLen);

                gts.GetBlocksOfType(gyros);
            }

            public float getMass()
            {
                return mass;
            }


            /// <summary>
            /// Направляет нос корабля на точку. Направление вниз совмещает с orientation.
            /// Если сигналы по  осям меньше dW - возвращает true.
            /// </summary>
            /// <param name="direction"></param>
            public bool orient(Vector3D orientation, float dW = 0.1f)
            {
                Vector3D orient = Vector3D.Normalize(orientation);
                float yaw = ctrl.RotationIndicator.Y / 20;
                float pitch = (float)orient.Dot(ctrl.WorldMatrix.Backward) * gyroMult;
                float roll = (float)orient.Dot(ctrl.WorldMatrix.Left) * gyroMult;
                gyros.ForEach(g =>
                {
                    g.GyroOverride = true;
                    g.Pitch = pitch;
                    g.Roll = roll;
                    g.Yaw = yaw;
                });
                return (Math.Abs(roll) < dW) && (Math.Abs(pitch) < dW);
            }

            public void setThrustPerc(double percantage)
            {
                thrusters.ForEach(t => t.ThrustOverridePercentage = (float)percantage);
            }

            public void free()
            {
                ctrl.DampenersOverride = true;
                thrusters.ForEach(t => t.ThrustOverride = 0);
                gyros.ForEach(g => g.GyroOverride = false);
            }

            public float getDesiredH()
            {
                return (float)ctx.get("desiredH");
            }

            public void tunePower(float desiredSpeed, float maxVSpeed= 2f)
            {
                ctrl.DampenersOverride = false;
                var gravNorm = Vector3D.Normalize(grav);
                double H = 0;
                var up1 = ctrl.WorldMatrix.Up;
                var f1 = ctrl.WorldMatrix.Forward;
                ctrl.TryGetPlanetElevation(MyPlanetElevation.Surface, out H);
                Vector3D desiredV = gravNorm * (Math.Min(maxVSpeed, H - getDesiredH())) + Vector3D.Normalize(Vector3D.ProjectOnPlane(ref f1, ref gravNorm)) * desiredSpeed;
                Vector3D dV = desiredV - ctrl.GetShipVelocities().LinearVelocity;
                Vector3D desiredA = dV - grav;
                lcd.WriteText($"Заданная высота: {getDesiredH():F1}\n", true);
                lcd.WriteText($"Заданная скорость: {desiredSpeed:F1}\n", true);
                lcd.WriteText($"Текущ. высота: {H:F1}\n", true);

                var aVertVal = desiredA.Dot(gravNorm); // вертикальное ускорение (длинна)
                var aHor = Vector3D.ProjectOnPlane(ref desiredA, ref grav); // горизонтальное ускорение
                var aHorAbs = aHor.Length(); // Длинна горизонтального значения
                Vector3D aVert;
                 if (aVertVal > 0)
                { // двигателей вниз нет - отсекаем тягу вниз
                     aVert = Vector3D.Zero;
                     aVertVal = 0.1;
                } else { 
                     aVert = gravNorm * aVertVal;
                }
                Vector3D aHorOrient;
                if (mode == HoverMode.agressive)
                    aHorOrient = ((aHorAbs > 0.1) && aHorAbs > maxAHor) ? maxAHor / aHorAbs * aHor : aHor;
                else
                    aHorOrient = ((aHorAbs > 0.1) && aHorAbs > Math.Abs(aVertVal)) ? Math.Abs(aVertVal) / aHorAbs * aHor : aHor;
                //lcd.WriteText(aHorOrient.Length().ToString(), true);
                //lcd.WriteText(aVertVal.ToString(), true);
                orient(-(aVert + aHorOrient));

                // максимальный наклон, но сейчас гироскоп мог еще не успеть наклонить - тягу надо дать меньше
                var trustPerc = aVertVal / up1.Dot(gravNorm) / sumA;
                setThrustPerc(trustPerc);
            }

            public void setMode(HoverMode mode)
            {
                this.mode = mode;
            }
        }
    }
}
