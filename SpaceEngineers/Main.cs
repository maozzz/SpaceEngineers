using System;
using System.Threading;
using Sandbox.Game.GameSystems;

public static class MainStarter {
    public static String Args = "";
    public static String Args2 = "next";
    private static int k = 1;

    static void Main(string[] args) {
        Menu menu = new SimpleMenu("MainMenu", null, 10);
        Menu submenu = new SimpleMenu("MainMenu", menu, 10);
        submenu.add(new SimpleMenuItem(new SimpleMsg("item-1", "text of item 1\nsecond line of 1\n3-d line of 1\n4-th line", null)));
        submenu.add(new SimpleMenuItem(new SimpleMsg("item-2", "text of item 2", null)));
        submenu.add(new SimpleMenuItem(new SimpleMsg("item-3", "text of item 3", null)));
        submenu.add(new ChangeVarMenuItem(new SimpleReactMsg("changeK", null, () => string.Format("k = {0}", k)), 
            () => k++));
        
        menu.add(new SimpleMenuItem(new SimpleMsg("item-01", "text of item 01", null)));
        menu.add(submenu);
        menu.add(new SimpleMenuItem(new SimpleMsg("item-02", "text of item 02", null)));
        
        
        

        while (true) {
            Console.Write("\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n");
            Console.Write(k);
            bool b = new Random().Next(10) >5;
            Args = b ? "exec" : "";
            Console.Write($"Activate: {b}\n");
            string exec = menu.exec();
            Console.WriteLine(exec);
            Thread.Sleep(2000);
        }
    }
}