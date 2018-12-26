using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI.Ingame;
using VRageMath;

    public class RCMovement {
        private IMyRemoteControl rc;
        private List<IMyGyro> gyro;
        private IMyTextPanel lcd;
        private IMyGridTerminalSystem gts;

        private int ticks = 0;
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

        public RCMovement(IMyGridTerminalSystem gts, IMyRemoteControl rc, List<IMyGyro> gyro, IMyTextPanel lcd,
            IMyGridProgramRuntimeInfo runtime) {
            rc.ControlThrusters = true;
            this.rc = rc;
            this.gyro = gyro;
            this.lcd = lcd;
            this.gts = gts;
            this.runtime = runtime;
            gts.GetBlocksOfType(thAll, thrust => true);
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
                }
                else {
                    move(path);
                }

                return false;
            }
            else {
                freeControl();
                return true;
            }
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
            else {
                freeControl();
                return true;
            }
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
            if (stat.thrusters.Count() == 0) // рассчет на гравитацию
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
            }
            else {
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