using System;
using System.Collections.Generic;

public class Context : Dictionary<String, object> {
    private Job tickJob;

    public Context(Job tickJob) {
        this.tickJob = tickJob;
    }

    public void tick() {
        tickJob.exec();
    }
}