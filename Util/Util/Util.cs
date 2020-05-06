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
        /**
         * Создание: Util util = new Util();
         */
        public class Util : MyGridProgram
        {
            private IMyGridTerminalSystem gts = null;
            public Util(IMyGridTerminalSystem gts)
            {
                this.gts = gts;
            }

            /// <summary>
            /// Ищет 1 блок по типу.
            /// </summary>
            /// <typeparam name="T"></typeparam>
            /// <param name="t"></param>
            /// <returns></returns>
            public T findBlockByType<T>(ref T t) where T : class
            {
                List<T> list = new List<T>();
                gts.GetBlocksOfType(list, arg => true);
                return t = list.First();
            }


            /**
             * <summary>Преобразует строку в формате GPS в вектор.</summary>
             */
            public Vector3D vectorFromGps(String gpsStr)
            {
                var strings = gpsStr.Split(':');
                return new Vector3D(Double.Parse(strings[2]), Double.Parse(strings[3]), Double.Parse(strings[4]));
            }

            /**
             * <summary>Преобразует вектор в строку в формате GPS с заданным именем метки.</summary>
             */
            public string vectorToGps(Vector3D vec, string name)
            {
                return "GPS:" + name + ":" + vec.X + ":" + vec.Y + ":" + vec.Z + ":";
            }
        }
    }
}
