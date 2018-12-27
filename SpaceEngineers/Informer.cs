using System;
using System.Collections.Generic;
using System.Linq;

/* ==============================================================
 * =================== КЛАССЫ ДЛЯ МЕНЮ ==========================
 * ==============================================================
 * Если идти снизу вверх: то понарастающей видна реализация.   ==
 * Msg - простой интерфейс для хранения необходимого           ==
 *                                                             ==
 * ReactMsg = Msg                                              ==
 *     + exec()                                                ==
 * exec - должен возвращать инфу сообщения/пунска меню         ==
 *                                                             ==
 * MenuItem = ReactMsg добавляется                             ==
 *     + Activable                                             ==
 *     + Focusable                                             ==
 * для хранения информации внутри элемента. Меню хранит        ==
 * эту же логику в себе.                                       ==
 *                                                             ==
 * Menu = MenuItem                                             ==
 *     + add - добавление пункта меню                          ==
 *     + deactivateSubmenu - для возврата из подменю           ==
 *                                                             ==
 * ChangeVarMenuItem : SimpleMenuItem                          ==
 *     реализация для изменения переменных.                    ==
 *     Пришлось сломать логику Activable, т.к. по ним          ==
 *     надо увеличивать/уменьшать переменную                   ==
 *     Само действие с переменной передается при создании      ==
 *     экземпляра. А там должен быть доступ к переменной,      ==
 *     которую собираемся менять.                              ==
 * ============================================================*/
    public class SimpleMenu : SimpleMenuItem, Menu {
        private List<MenuItem> items = new List<MenuItem>();
        private Menu parent;
        private MenuItem activeItem, focusedItem;
        private const int width = 10;

        public SimpleMenu(string title, Menu parent) : base(new SimpleMsg(title, null, parent)) {
            this.parent = parent;
            if (parent != null)
                add(new SimpleMenuItem(new SimpleReactMsg("..", this, () => {
                    if (activeItem != null) deactivateSubmenu();
                    parent.deactivateSubmenu();
                    return "";
                })));
        }

        public void add(MenuItem item) => items.Add(item);
        public void deactivateSubmenu() => changeActiveStatus(ref activeItem);

        public void next() => items.ForEach(i => {
            if (i.isFocused()) {
                changeFocusStatus(ref i);
            } else if (focusedItem == null) {
                changeFocusStatus(ref i);
            }
        });

        public override string exec() {
            if (!(activeItem is Menu)) {
                if (ctx.get("arg").ToString() == "next") next();
                if (focusedItem == null && items.Count > 0) // Если не заполнено focusedItem - ищем выбранный
                {
                    var a = items.FirstOrDefault(i => i.isFocused());
                    if (a == null) { // если выбранного нет - выбираем первый попавшийся
                        focusedItem = items.First();
                        changeFocusStatus(ref focusedItem);
                    }
                }
                if (ctx.get("arg").ToString() == "exec") { // если команда на выполнение - активируем/деактивируем
                    changeActiveStatus(ref focusedItem);
                    ctx.putForce("arg", "");
                }
            }

            // Получаем результат выполнения активного
            Queue<string> q = activeItem != null
                ? new Queue<string>(activeItem.exec().Split('\n'))
                : new Queue<string>();

            var concat = string.Concat(items.Select(i => {
                var str = i.getTitle();
                return
                    $"{(i.isFocused() ? ">" : " ")}{(i.isActive() ? "|" : " ")}{str.Substring(0, str.Length < width ? str.Length : width),-width}|{(q.Count > 0 ? q.Dequeue() : "")}\n";
            }));
            while (q.Count > 1) { // 1 - потому что у него последняя - пустая строка
                var str = $"  {"",-width}|{(q.Count > 0 ? q.Dequeue() : "")}\n";
                if (str != "") concat += str;
            }
            return concat;
        }

        private void changeActiveStatus(ref MenuItem i) {
            if (i.isActive()) {
                i.deactivate();
                activeItem = null;
            } else {
                i.activate();
                if (activeItem != null && activeItem != i) activeItem.deactivate();
                activeItem = i;
            }
            if (activeItem != null && activeItem is Menu) {
                items.ForEach(it => {
                    if (it.isFocused()) changeFocusStatus(ref it);
                });
            }
        }

        private void changeFocusStatus(ref MenuItem i) {
            if (i.isFocused()) {
                i.focusOff();
                focusedItem = null;
            } else {
                i.focusOn();
                focusedItem = i;
            }
        }
    }


/**
 * Реализация простого пункта меню
 */
    public class SimpleMenuItem : SimpleReactMsg, MenuItem {
        Msg msg;
        private bool active;
        private bool focused;

        public SimpleMenuItem(ReactMsg msg) : base(msg.getTitle(), msg.getSrc(), msg.exec) {
            this.msg = msg;
        }

        public SimpleMenuItem(Msg msg) : base(msg.getTitle(), msg.getSrc(), msg.getText) {
            this.msg = msg;
        }

        public virtual void activate() => active = true;
        public virtual void deactivate() => active = false;
        public virtual bool isActive() => active;
        public void focusOn() => focused = true;
        public void focusOff() => focused = false;
        public bool isFocused() => focused;
    }

    public class ChangeVarMenuItem : SimpleMenuItem {
        private Action action;

        public ChangeVarMenuItem(ReactMsg msg, Action action) : base(msg) {
            this.action = action;
        }

        public ChangeVarMenuItem(string ctxVarName, object dT, string title, string text) : base(new SimpleReactMsg(
            title,
            null, () => string.Format(text, ctx.get(ctxVarName)))) {
            action = () => {
                var o = ctx.get(ctxVarName);
                if (o is int) {
                    ctx.putForce(ctxVarName, (int) o + int.Parse(dT.ToString()));
                } else if (o is float) {
                    ctx.putForce(ctxVarName, (float) o + float.Parse(dT.ToString()));
                } else {
                    ctx.putForce(ctxVarName, (double) o + double.Parse(dT.ToString()));
                }
            };
        }

        public override void activate() => action();

        public override void deactivate() {
            if (isFocused()) action();
        }

        public override bool isActive() => false;
    }

/**
 * Сообщение: которое может выполнять действие
 */
    public class SimpleReactMsg : SimpleMsg, ReactMsg {
        private Func<string> func;

        public SimpleReactMsg(string title, object srcObject, Func<string> func)
            : base(title, null, srcObject) {
            this.func = func;
        }

        public virtual string exec() => func();
    }

/**
 * Сообщение для вывода простого текста
 */
    public class SimpleMsg : Msg {
        private object src;
        private string title;
        private string text;

        public SimpleMsg(string title, string text, object srcObject) {
            this.src = srcObject;
            this.title = title;
            this.text = text;
        }

        public object getSrc() => src;
        public string getTitle() => title;
        public string getText() => text;
    }

    public interface Menu : MenuItem {
        void add(MenuItem item);
        void deactivateSubmenu();
    }

    public interface MenuItem : ReactMsg, Activable, Focusable { }

    public interface ReactMsg : Msg {
        string exec();
    }

    public interface Msg {
        object getSrc();
        string getTitle();
        string getText();
    }

    public interface Activable {
        void activate();
        void deactivate();
        bool isActive();
    }

    public interface Focusable {
        void focusOn();
        void focusOff();
        bool isFocused();
    }

/**
 *^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
 */