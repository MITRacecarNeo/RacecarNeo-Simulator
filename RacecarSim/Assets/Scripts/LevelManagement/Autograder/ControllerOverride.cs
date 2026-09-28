using UnityEngine;

/// <summary>
/// Predefined controller inputs which are used instead of the physical controller.
/// </summary>
public class ControllerOverride : MonoBehaviour
{
    #region Set in Unity Editor
    /// <summary>
    /// The series of buttons which are pressed and released in the level.
    /// </summary>
    [SerializeField]
    private Controller.Button[] buttonPresses = new Controller.Button[0];

    /// <summary>
    /// The button held for the duration of the level.
    /// </summary>
    /// <remarks>Since the Unity Editor does not support nullable enums, we use START to indicate no button.</remarks>
    [SerializeField]
    private Controller.Button heldButtonUnity = Controller.Button.START;

    /// <summary>
    /// The value at which each trigger is held for the duration of the level.
    /// </summary>
    [SerializeField]
    private Vector2 triggerValues = Vector2.zero;

    /// <summary>
    /// The value at which each joystick is held for the duration of the level.
    /// </summary>
    [SerializeField]
    private Vector2[] joystickValues = { Vector2.zero, Vector2.zero };
    #endregion

    #region Constants
    /// <summary>
    /// The time (in seconds) for which each button in buttonPresses is held down when pressed.
    /// </summary>
    private const float buttonPressTime = 0.5f;
    #endregion

    #region Public Interface
    /// <summary>
    /// Returns true if the provided button is pressed.
    /// </summary>
    /// <param name="button">A button on an Xbox controller.</param>
    /// <returns>True if the provided button is currently pressed.</returns>
    public bool IsDown(Controller.Button button)
    {
        this.Advance();
        return button == this.HeldButton || (button == this.pressedButton && this.pressedButtonState != ButtonState.Released);
    }

    /// <summary>
    /// Returns true if the provided button was pressed this frame.
    /// </summary>
    /// <param name="button">A button on an Xbox controller.</param>
    /// <returns>True if the provided button was pressed this frame.</returns>
    public bool WasPressed(Controller.Button button)
    {
        this.Advance();

        // This button press was already registered earlier this frame
        if (this.pressedButton == button && this.pressedButtonState == ButtonState.Pressed)
        {
            return true;
        }

        // No button currently pressed and this is the next button to be pressed
        if (!this.pressedButton.HasValue && this.buttonPressIndex < this.buttonPresses.Length && this.buttonPresses[this.buttonPressIndex] == button)
        {
            this.pressedButton = this.buttonPresses[this.buttonPressIndex];
            this.pressedButtonState = ButtonState.Pressed;
            this.pressedButtonTime = Time.time;
            this.buttonPressIndex++;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Returns true if the provided button was released this frame.
    /// </summary>
    /// <param name="button">A button on an Xbox controller.</param>
    /// <returns>True if the provided button was released this frame.</returns>
    public bool WasReleased(Controller.Button button)
    {
        this.Advance();
        return button == this.pressedButton && this.pressedButtonState == ButtonState.Released;
    }

    /// <summary>
    /// Returns the value of a trigger.
    /// </summary>
    /// <param name="trigger">A trigger on an Xbox controller.</param>
    /// <returns>The value of the provided trigger, ranging from 0 (unpressed) to 1 (fully pressed).</returns>
    public float GetTrigger(Controller.Trigger trigger)
    {
        return this.triggerValues[(int)trigger];
    }

    /// <summary>
    /// Returns the coordinates of a joystick.
    /// </summary>
    /// <param name="joystick">A joystick on an Xbox controller.</param>
    /// <returns>The x and y coordinates of the provided joystick, ranging from (-1, -1) (bottom left) to (1, 1) (top right)</returns>
    public Vector2 GetJoystick(Controller.Joystick joystick)
    {
        return this.joystickValues[(int)joystick];
    }
    #endregion

    /// <summary>
    /// The three stages of a button press.
    /// </summary>
    private enum ButtonState
    {
        Pressed,
        Held,
        Released
    }

    /// <summary>
    /// The index of the button in buttonPresses which is next to be pressed.
    /// </summary>
    private int buttonPressIndex = 0;

    /// <summary>
    /// The button which is currently pressed, or null if no button is pressed.
    /// </summary>
    private Controller.Button? pressedButton = null;

    /// <summary>
    /// The Time.time at which the current button was pressed.
    /// </summary>
    private float pressedButtonTime;

    /// <summary>
    /// The state of the current pressed button.
    /// </summary>
    private ButtonState pressedButtonState;

    /// <summary>
    /// The Time.frameCount at which Advance last ran.
    /// </summary>
    private int lastAdvanceFrame = -1;

    /// <summary>
    /// The button held for the duration of the level, or null if no button is held for the duration of the level.
    /// </summary>
    private Controller.Button? HeldButton
    {
        get
        {
            return this.heldButtonUnity == Controller.Button.START ? (Controller.Button?)null : this.heldButtonUnity;
        }
    }

    private void Awake()
    {
        Controller.Override = this;
    }

    /// <summary>
    /// Moves the current button press through Pressed, Held, and Released, once per frame on the
    /// first query. Each of Pressed and Released lasts exactly one frame regardless of the order in
    /// which Unity runs Update on this component and on the LevelManager.
    /// </summary>
    private void Advance()
    {
        if (this.lastAdvanceFrame == Time.frameCount)
        {
            return;
        }
        this.lastAdvanceFrame = Time.frameCount;

        if (!this.pressedButton.HasValue)
        {
            return;
        }

        if (this.pressedButtonState == ButtonState.Released)
        {
            this.pressedButton = null;
        }
        else if (Time.time - this.pressedButtonTime > ControllerOverride.buttonPressTime)
        {
            this.pressedButtonState = ButtonState.Released;
        }
        else
        {
            this.pressedButtonState = ButtonState.Held;
        }
    }
}
