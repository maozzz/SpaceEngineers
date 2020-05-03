using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
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
        init();
    }

    public void init() {
        ctx.put("gts", GridTerminalSystem);
        ctx.put("runtime", Runtime);
        Util util = ctx.put("utils", new Util());
        ctx.putForce("lcd", GridTerminalSystem.GetBlockWithName(ctx.get("lcdName").ToString()) as IMyTextPanel);
    }

    public void Save() { }

    public void Main(string argument, UpdateType updateSource) { }


/* ==============================================================
 * ======================= РАДИООБМЕН ===========================
 * ============================================================*/

    class RadioMessage {
        private const string delim = "###";
        private const string keyValueDelim = "$$$";
        private string @from;
        private string to;
        private string command;
        private string data;

        public RadioMessage(
            string from,
            string to,
            string command,
            string data
        ) {
            this.from = from;
            this.to = to;
            this.command = command;
            this.data = data;
        }
    }

/* ==============================================================
 * ===================  СКРИАЛИЗАЦИЯ  ========================
 * ============================================================*/
    class Serializer {
        private const char keyValDelim = ':';
        private const char start = '{';
        private const char end = '}';
        private const char stringStart = '"';
        private const char stringEnd = '"';
        private const char arrStart = '[';
        private const char arrEnd = ']';
        private const char delim = ',';
        private const char escape = '\\';

        public static object parse(ref String s, ref int index) {
            object o = null;
            while (index < s.Length) {
                char ch = s[index];
                index++;
                if (ch == ' ' || ch == '\n' || ch == '\r' || ch == '\t' || ch == keyValDelim) continue;
                if (ch == arrEnd || ch == end || ch == delim) return o;
                if (ch == arrStart) return parseList(ref s, ref index);
                if (ch == start) return parseDict(ref s, ref index);
                if (ch == stringStart) return parseString(ref s, ref index);
                index--;
                return parseNum(ref s, ref index);
            }
            return null;
        }

        private static string parseString(ref string s, ref int index) {
            List<char> buf = new List<char>();
            Char last = '"';
            for (; index < s.Length; index++) {
                char ch = s[index];
                if (ch == stringEnd && last != escape) {
                    index++;
                    return String.Concat(buf);
                }
                if (last == escape && ch != stringEnd) {
                    buf.Add(last);
                }
                if (ch != escape)
                    buf.Add(ch);
                last = ch;
            }
            return null;
        }

        private static object parseNum(ref string s, ref int index) {
            List<char> buf = new List<char>();
            bool dec = false;
            for (; index < s.Length; index++) {
                char ch = s[index];
                if (ch == keyValDelim || ch == delim || ch == end || ch == arrEnd) {
                    break;
                }
                if (ch == '.') dec = true;
                buf.Add(ch);
            }
            object a = dec
                ? float.Parse(String.Concat(buf), CultureInfo.InvariantCulture)
                : int.Parse(String.Concat(buf).Trim());
            return a;
        }

        private static List<object> parseList(ref string s, ref int index) {
            List<object> list = new List<object>();
            while (s[index] != arrEnd) {
                list.Add(parse(ref s, ref index));
                if (s[index] == delim || s[index] == ' ') index++;
            }
            index++;
            return list;
        }

        private static Dictionary<object, object> parseDict(ref string s, ref int index) {
            Dictionary<object, object> dic = new Dictionary<object, object>();
            while (s[index] != end) {
                object key = parse(ref s, ref index);
                index++;
                while (s[index] == ' ' || s[index] == keyValDelim) index++;
                object val = parse(ref s, ref index);
                dic.Add(key, val);
                while (s[index] == ' ' || s[index] == delim) index++;
            }
            return dic;
        }

        public static string encode(object o) {
            if (o is List<object>) {
                var sb = new StringBuilder();
                sb.Append("[");
                bool first = true;
                ((List<object>) o).ForEach(o1 => {
                    if (!first) sb.Append(",");
                    sb.Append(encode(o1));
                    first = false;
                });
                sb.Append("]");
                return sb.ToString();
            }
            if (o is int || o is float || o is double) {
                return o.ToString().Replace(',', '.');
            }
            if (o is string) {
                return "\"" + ((string) o).Replace("\"", "\\\"") + "\"";
            }
            if (o is Dictionary<object, object>) {
                var sb = new StringBuilder();
                sb.Append("{");
                bool first = true;
                foreach (var (key, value) in (Dictionary<object, object>) o) {
                    string k = encode(key);
                    string v = encode(value);
                    if (k == null || v == null) continue;
                    if (!first) sb.Append(",");
                    first = false;
                    sb.Append(k).Append(":").Append(v);
                }
                sb.Append("}");
                return sb.ToString();
            }
            return null;
        }
    }

/* ==============================================================
 * ================  ОРГАНИЗАЦИЯ ПРОЦЕССОВ  ====================
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

/* ==============================================================
 * ===================  ОБЩЕГО НАЗНАЧЕНИЯ  ======================
 * ============================================================*/
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