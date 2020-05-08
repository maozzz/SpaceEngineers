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
using VRageRender;

namespace IngameScript
{
    partial class Program
    {
        /// <summary>
        /// Для работы с аргументами надо добавить соответствующие действия через <code>addArgumentAction</code> и передавать аргумент в <code>tick()</code>
        /// </summary>
        /// <example>Context ctx = new Context(new Dictionary<string, object> {{"lcdName", "lcd"}});</example>
        public class Context : StackProc, Tickable
        {
            public static int ticks;
            private Dictionary<String, object> data;
            private Dictionary<String, Action> argumenResolver = new Dictionary<string, Action>();
            private List<Tickable> tickables = new List<Tickable>();

            public Context(Dictionary<string, object> initData) : base("context")
            {
                data = initData;
            }

            public T put<T>(String key, T val)
            {
                if (data.ContainsKey(key)) throw new Exception("Attempt to rewrite context value");
                data[key] = val;
                return val;
            }

            public T putForce<T>(String key, T val)
            {
                rm(key);
                data[key] = val;
                return val;
            }

            /// <summary>
            /// Добавляет действие для аргумента.
            /// </summary>
            /// <param name="arg"></param>
            /// <param name="action"></param>
            public void addArgumentAction(string arg, Action action)
            {
                this.argumenResolver[arg] = action;
            }

            /// <summary>
            /// По аргуенту возвращает действие, которое надо выполнить.
            /// </summary>
            /// <param name="arg"></param>
            /// <returns>действие (Action)</returns>
            public Action resolveArgument(string arg)
            {
                return argumenResolver.ContainsKey(arg) ? argumenResolver[arg] : null;
            }

            public void tick(string arg)
            {
                if (arg != null && argumenResolver.ContainsKey(arg)) { resolveArgument(arg).Invoke(); }
                ticks++;
                tickables.ForEach(el => {
                    el.tick(arg);
                    if ((el is Timer) && ((Timer)el).isOutdated()) tickables.Remove(el);
                });
            }

            /// <summary>
            /// Количество тиков, отработавших в контексте
            /// </summary>
            /// <returns></returns>
            public int getTicks()
            {
                return ticks;
            }

            public void registerTickable(Tickable item)
            {
                this.tickables.Add(item);
            }

            public bool contains(string key) => data.ContainsKey(key);
            public bool rm(string key) => data.ContainsKey(key) && data.Remove(key);
            public object get(string key) => data.GetValueOrDefault(key);
            public IEnumerable<T> get<T>(ref IEnumerable<T> t) => t = data.Values.OfType<T>();
            public T get<T>(ref T t) where T : class => t = data.Values.OfType<T>().FirstOrDefault();
        }

        /* ==============================================================
         * =================== ОБЩЕГО НАЗНАЧЕНИЯ ========================
         * ============================================================*/

        public class Timer : Tickable
        {
            private bool cyclic;
            private int est, timeout;
            private Action action;

            public Timer(int est, bool cyclic, Action action)
            {
                this.timeout = this.est = est;
                this.cyclic = cyclic;
                this.action = action;
            }

            public void tick(string arg)
            {
                if (!isOutdated()) est--;
                if (est == 0)
                {
                    action();
                    est--;
                    if (cyclic) est = timeout;
                }
            }

            public bool isOutdated() => est < 0;
        }

        public interface Tickable
        {
            void tick(string arg);
        }
    }
}
