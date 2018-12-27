using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Serialization.Json;
using LitJson;
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
using VRage;
using VRage.Library;
using VRage.Serialization;

public class Dispatcher : MyGridProgram {
    public static Context ctx = new Context(new Dictionary<string, object> {
        {"lcdName", "lcdL"}
    });
    
    

    public Program() {
        ctx.putForce("lcd", GridTerminalSystem.GetBlockWithName(ctx.get("lcdName").ToString()) as IMyTextPanel);
    }

    public void Save() {
    }

    public void Main(string argument, UpdateType updateSource) {
        ((IMyTextPanel) ctx.get("lcd")).WritePublicText("qweqwe");
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

#if RIDER
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
#endif
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

}