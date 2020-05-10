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
        public interface Process : Job
        {
            void add(Job j);
            Job current();
            Job[] all();
        }

        public interface Job
        {
            string name { get; }
            Job exec();
        }

        /// <summary>
        /// Конструктор Func<Job> func заменяет exec
        /// Конструктор Func<bool> bFunc если возвращает true - задача закончена, false - продолжить выполнение
        /// </summary>
        public class TheJob : Job
        {
            private Func<Job> func;
            private Func<bool> bFunc = null; // Если true - задача закончена, false - вернуть себя, чтобы выполняласт дальше
            public string name { get; }

            public TheJob(string name, Func<Job> func)
            {
                this.name = name;
                this.func = func;
            }

            public TheJob(string name, Func<bool> bFunc)
            {
                this.name = name;
                this.bFunc = bFunc;
            }

            public Job exec() => bFunc == null
                ? func.Invoke()
                : bFunc.Invoke() ? null : this;
        }

        public class StackProc : Process
        {
            public string name { get; }
            public StackProc(string name)
            {
                this.name = name;
            }
            private Stack stack = new Stack();

            public Job exec()
            {
                if (stack.Count == 0) return null;
                var job = stack.Peek() as Job;
                var res = job.exec();
                if (res == null) stack.Pop(); // задача закончилась
                else if (res != job) stack.Push(res); // новая подзадача
                // else; // иначе - задача не закончена
                return stack.Count > 0 ? this : null;
            }

            public void add(Job j) => stack.Push(j);
            public Job current() => (Job)stack.Peek();
            public Job[] all() => (Job[])stack.ToArray();
            public void clear() => stack.Clear();
        }

        public class QueuedProc : Process
        {
            private Queue q = new Queue();
            public string name { get; }

            public QueuedProc(string name)
            {
                this.name = name;
            }
            public virtual Job exec()
            {
                if (q.Count == 0) return null;
                var job = q.Peek() as Job;
                var res = job != null ? job.exec() : null;
                if (res == null && q.Count>0) q.Dequeue(); // задача закончилась
                else if (res != job) q.Enqueue(res); // новая подзадача
                //else; // иначе - задача не закончена
                return q.Count > 0 ? this : null;
            }

            public void add(Job j)
            {
                q.Enqueue(j);
            }

            public Job current() => (q.Count>0 ? (Job)q.Peek() : null);

            public Job[] all() => (Job[])q.ToArray();
            public void clear() => q.Clear();
        }
    }
}
