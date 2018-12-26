using System;
using System.Collections;

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
        Console.WriteLine($"Job: {job.GetType().Name}");
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
        Console.WriteLine($"Job: {job.GetType().Name}");
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