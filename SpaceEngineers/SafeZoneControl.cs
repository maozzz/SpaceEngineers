using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using VRageMath;
using VRage.Game;
using Sandbox.ModAPI.Interfaces;
using Sandbox.ModAPI.Ingame;
using VRage.Game.Components;
using VRage.Collections;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using VRage;
using VRage.Library;

public sealed class SafeZoneControl : MyGridProgram {
    private List<IMyLargeTurretBase> turrets = new List<IMyLargeTurretBase>();
    private IMyTextSurface lcd;
    private IMySafeZoneBlock safeZone;
    private Util util;
    private DateTime alarmTime;

    private int alarmMinutes = 10;
    private IMyRadioAntenna antenna;

    public Program() {
        antenna.
        util = new Util(GridTerminalSystem);
        util.findBlockByType(ref safeZone);
        findTurrets();
        lcd = Me.GetSurface(0);
        alarmTime = DateTime.Now.AddMinutes(-1 - alarmMinutes);
    }

    public void findTurrets() {
        GridTerminalSystem.GetBlocksOfType(turrets);
    }

    public void Main(string argument, UpdateType updateSource) {
        Runtime.UpdateFrequency = UpdateFrequency.Update100;
        checkTurrets();
    }

    public void checkTurrets() {
        turrets.ForEach(turret => {
            var entity = turret.GetTargetedEntity();
            if (entity.IsEmpty()) {
                checkAlarmExpires();
                return;
            }
            var length = (entity.Position - Me.GetPosition()).Length();
            lcd.WriteText("Distance: " + length.ToString());
            if (length > 500) return;
            alarm();
        });
    }

    public Boolean checkAlarmExpires() {
        if (alarmTime < DateTime.Now.AddMinutes(1 - alarmMinutes)) // за минуту до прохождения тревоги включаем турели
            enableTurrets(true);
        bool b = alarmTime < DateTime.Now.AddMinutes(0 - alarmMinutes);
        if (b) safeZone.Enabled = false;
        return b;
    }

    public void alarm() {
        alarmTime = System.DateTime.Now;
        safeZone.Enabled = true;
        enableTurrets(false);
    }

    public void enableTurrets(bool val) => turrets.ForEach(t => t.Enabled = val);

    /**
     * UTILS
     */
    public class Util : MyGridProgram {
        private IMyGridTerminalSystem gts;

        public Util(IMyGridTerminalSystem gts) {
            this.gts = gts;
        }

        public void findBlockByType<T>(ref T t) where T : class {
            List<T> list = new List<T>();
            gts.GetBlocksOfType(list, arg => true);
            t = list.FirstOrDefault();
        }
    }
}