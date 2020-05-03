using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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


public class Util : MyGridProgram {
    private IMyGridTerminalSystem gts;

    public Util(IMyGridTerminalSystem gts)
    {
        this.gts = gts;
    }

    public void findBlockByType<T>(ref T t) where T : class
    {
        List<T> list = new List<T>();
        gts.GetBlocksOfType(list, arg => true);
        t = list.First();
    }

    public Vector3D vectorFromGps(String gpsStr) {
        var strings = gpsStr.Split(":");
        return new Vector3D(Double.Parse(strings[1]),Double.Parse(strings[1]),Double.Parse(strings[1]));
    }
}