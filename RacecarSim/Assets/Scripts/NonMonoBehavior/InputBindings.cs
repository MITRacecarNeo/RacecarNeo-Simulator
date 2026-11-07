using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// What the Controls screen lists: each simulator action with its keyboard keys and controller
/// input, read from Controller's keyboard map and Hotkeys so the screen matches the code.
/// </summary>
public static class InputBindings
{
    /// <summary>
    /// One action: keyboard key groups (keys in a group are pressed together) and the controller
    /// input, or null when there is none.
    /// </summary>
    public class Binding
    {
        public string Action;
        public KeyCode[][] Keys;
        public string Controller;
    }

    /// <summary>
    /// A titled group of bindings.
    /// </summary>
    public class Section
    {
        public string Title;
        public Binding[] Bindings;
    }

    /// <summary>
    /// Every section, in screen order.
    /// </summary>
    public static Section[] Sections => new[]
    {
        new Section
        {
            Title = "Drive",
            Bindings = new[]
            {
                InputBindings.Row("Throttle", "RT", InputBindings.One(Controller.KeyFor(Controller.Trigger.RIGHT))),
                InputBindings.Row("Reverse", "LT", InputBindings.One(Controller.KeyFor(Controller.Trigger.LEFT))),
                InputBindings.Row("Steer", "Left stick", InputBindings.Each(Controller.KeysFor(Controller.Joystick.LEFT).Take(2))),
                InputBindings.Row("Max speed", "LB  RB", InputBindings.Each(new[] { Controller.KeyFor(Controller.Button.LB), Controller.KeyFor(Controller.Button.RB) })),
            },
        },
        new Section
        {
            Title = "Level",
            Bindings = new[]
            {
                InputBindings.Row("Start program", "Start", InputBindings.One(Controller.KeyFor(Controller.Button.START))),
                InputBindings.Row("Stop or pause", "Back", InputBindings.One(Controller.KeyFor(Controller.Button.BACK))),
                InputBindings.Row("Restart", "Start + Back", new[] { new[] { Controller.KeyFor(Controller.Button.START), Controller.KeyFor(Controller.Button.BACK) } }),
                InputBindings.Row("Main menu", null, InputBindings.One(Hotkeys.Exit)),
                InputBindings.Row("Slower, faster", null, InputBindings.Each(new[] { Hotkeys.SlowDown, Hotkeys.SpeedUp })),
                InputBindings.Row("Next checkpoint", null, InputBindings.One(Hotkeys.SkipCheckpoint)),
                InputBindings.Row("Back to checkpoint", null, InputBindings.One(Hotkeys.ResetToCheckpoint)),
            },
        },
        new Section
        {
            Title = "Camera",
            Bindings = new[]
            {
                InputBindings.Row("Next view", null, InputBindings.One(Hotkeys.NextView)),
                InputBindings.Row("Default view", null, InputBindings.One(Hotkeys.ResetView)),
                InputBindings.Row("Zoom", null, InputBindings.One(KeyCode.Mouse2)),
                InputBindings.Row("Sensor panels", null, InputBindings.One(Hotkeys.ToggleSensors)),
                InputBindings.Row("Map and speed", null, InputBindings.One(Hotkeys.ToggleMap)),
            },
        },
        new Section
        {
            Title = "Program buttons",
            Bindings = new[]
            {
                InputBindings.Row("A  B  X  Y", "A  B  X  Y", InputBindings.Each(new[]
                {
                    Controller.KeyFor(Controller.Button.A), Controller.KeyFor(Controller.Button.B),
                    Controller.KeyFor(Controller.Button.X), Controller.KeyFor(Controller.Button.Y),
                })),
                InputBindings.Row("Stick clicks", "LS  RS", InputBindings.Each(new[] { Controller.KeyFor(Controller.Button.LJOY), Controller.KeyFor(Controller.Button.RJOY) })),
                InputBindings.Row("Left stick", "Left stick", InputBindings.Each(Controller.KeysFor(Controller.Joystick.LEFT))),
                InputBindings.Row("Right stick", "Right stick", InputBindings.Each(Controller.KeysFor(Controller.Joystick.RIGHT))),
            },
        },
        new Section
        {
            Title = "Race",
            Bindings = new[]
            {
                InputBindings.Row("Reset car", null, new[] { new[] { Hotkeys.ResetCar, KeyCode.Alpha1 } }),
                InputBindings.Row("Race cameras", null, InputBindings.Each(new[] { KeyCode.LeftArrow, KeyCode.RightArrow, KeyCode.Alpha1 })),
            },
        },
    };

    /// <summary>
    /// The short label printed on a key cap.
    /// </summary>
    public static string KeyLabel(KeyCode key)
    {
        switch (key)
        {
            case KeyCode.LeftShift: return "L Shift";
            case KeyCode.RightShift: return "R Shift";
            case KeyCode.LeftAlt: return "L Alt";
            case KeyCode.RightAlt: return "R Alt";
            case KeyCode.Return: return "Enter";
            case KeyCode.Escape: return "Esc";
            case KeyCode.CapsLock: return "Caps";
            case KeyCode.Slash: return "/";
            case KeyCode.LeftArrow: return "\u2190";
            case KeyCode.RightArrow: return "\u2192";
            case KeyCode.UpArrow: return "\u2191";
            case KeyCode.DownArrow: return "\u2193";
            case KeyCode.Mouse2: return "Scroll";
            default:
                return key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9 ? ((int)(key - KeyCode.Alpha0)).ToString() : key.ToString();
        }
    }

    private static Binding Row(string action, string controller, KeyCode[][] keys)
    {
        return new Binding { Action = action, Controller = controller, Keys = keys };
    }

    private static KeyCode[][] One(KeyCode key)
    {
        return new[] { new[] { key } };
    }

    private static KeyCode[][] Each(IEnumerable<KeyCode> keys)
    {
        return keys.Select(key => new[] { key }).ToArray();
    }
}
