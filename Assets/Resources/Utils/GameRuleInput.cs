using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

/// <summary>
/// Bridges render-rate Input System updates into deterministic fixed ticks.
/// Update only captures device state; actor rules consume one immutable snapshot
/// for the entire fixed scheduler pass.
/// </summary>
public static class GameRuleInput
{
    private static readonly HashSet<Key> heldKeys = new HashSet<Key>();
    private static readonly HashSet<Key> pendingKeyDown = new HashSet<Key>();
    private static readonly HashSet<Key> pendingKeyUp = new HashSet<Key>();

    private static readonly HashSet<Key> tickHeldKeys = new HashSet<Key>();
    private static readonly HashSet<Key> tickKeyDown = new HashSet<Key>();
    private static readonly HashSet<Key> tickKeyUp = new HashSet<Key>();

    private static bool mouseHeld;
    private static bool pendingMouseDown;
    private static bool pendingMouseUp;
    private static bool tickMouseHeld;
    private static bool tickMouseDown;
    private static bool tickMouseUp;

    private static Vector2 pointerPosition;
    private static Vector2 pendingMouseDownPosition;
    private static Vector2 pendingMouseUpPosition;
    private static Vector2 tickPointerPosition;
    private static Vector2 tickMouseDownPosition;
    private static Vector2 tickMouseUpPosition;
    private static bool hasCapturedFrame;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        heldKeys.Clear();
        pendingKeyDown.Clear();
        pendingKeyUp.Clear();
        tickHeldKeys.Clear();
        tickKeyDown.Clear();
        tickKeyUp.Clear();

        mouseHeld = false;
        pendingMouseDown = false;
        pendingMouseUp = false;
        tickMouseHeld = false;
        tickMouseDown = false;
        tickMouseUp = false;

        pointerPosition = Vector2.zero;
        pendingMouseDownPosition = Vector2.zero;
        pendingMouseUpPosition = Vector2.zero;
        tickPointerPosition = Vector2.zero;
        tickMouseDownPosition = Vector2.zero;
        tickMouseUpPosition = Vector2.zero;
        hasCapturedFrame = false;
    }

    /// <summary>
    /// Called once from GameManager.Update after the dynamic Input System update.
    /// It never mutates actors or game variables.
    /// </summary>
    public static void CaptureFrame()
    {
        Keyboard keyboard = Keyboard.current;
        heldKeys.Clear();

        if (keyboard != null)
        {
            foreach (KeyControl keyControl in keyboard.allKeys)
            {
                Key key = keyControl.keyCode;
                if (key == Key.None) continue;

                if (keyControl.isPressed)
                    heldKeys.Add(key);
                if (keyControl.wasPressedThisFrame)
                    pendingKeyDown.Add(key);
                if (keyControl.wasReleasedThisFrame)
                    pendingKeyUp.Add(key);
            }
        }

        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            pointerPosition = mouse.position.ReadValue();
            mouseHeld = mouse.leftButton.isPressed;

            if (mouse.leftButton.wasPressedThisFrame)
            {
                pendingMouseDown = true;
                pendingMouseDownPosition = pointerPosition;
            }

            if (mouse.leftButton.wasReleasedThisFrame)
            {
                pendingMouseUp = true;
                pendingMouseUpPosition = pointerPosition;
            }
        }
        else
        {
            mouseHeld = false;
        }

        hasCapturedFrame = true;
    }

    /// <summary>
    /// Freezes all accumulated input for one fixed scheduler pass. Edge events
    /// remain visible to every actor and are consumed only after the pass ends.
    /// </summary>
    public static void BeginFixedTick()
    {
        if (!hasCapturedFrame)
            CaptureFrame();

        CopySet(heldKeys, tickHeldKeys);
        CopySet(pendingKeyDown, tickKeyDown);
        CopySet(pendingKeyUp, tickKeyUp);

        tickMouseHeld = mouseHeld;
        tickMouseDown = pendingMouseDown;
        tickMouseUp = pendingMouseUp;
        tickPointerPosition = pointerPosition;
        tickMouseDownPosition = pendingMouseDown ? pendingMouseDownPosition : pointerPosition;
        tickMouseUpPosition = pendingMouseUp ? pendingMouseUpPosition : pointerPosition;

        pendingKeyDown.Clear();
        pendingKeyUp.Clear();
        pendingMouseDown = false;
        pendingMouseUp = false;
    }

    public static void EndFixedTick()
    {
        tickKeyDown.Clear();
        tickKeyUp.Clear();
        tickMouseDown = false;
        tickMouseUp = false;
    }

    public static bool KeyboardState(string keyName, string mode)
    {
        if (!Enum.TryParse(keyName?.Trim(), true, out Key key))
            return false;

        switch (mode?.Trim().ToLowerInvariant())
        {
            // Include a down edge so a complete short tap between two fixed
            // ticks is still observable for one deterministic tick.
            case "press": return tickHeldKeys.Contains(key) || tickKeyDown.Contains(key);
            case "down": return tickKeyDown.Contains(key);
            case "up": return tickKeyUp.Contains(key);
            default: return false;
        }
    }

    public static bool TouchState(string mode)
    {
        switch (mode?.Trim().ToLowerInvariant())
        {
            case "press": return tickMouseHeld || tickMouseDown;
            case "down": return tickMouseDown;
            case "up": return tickMouseUp;
            case "tap": return tickMouseUp && !tickMouseHeld;
            case "isover": return true;
            default: return false;
        }
    }

    public static Vector2 PointerPosition(string mode = null)
    {
        switch (mode?.Trim().ToLowerInvariant())
        {
            case "down": return tickMouseDownPosition;
            case "up":
            case "tap": return tickMouseUpPosition;
            case "press": return tickMouseDown ? tickMouseDownPosition : tickPointerPosition;
            default: return tickPointerPosition;
        }
    }

    private static void CopySet(HashSet<Key> source, HashSet<Key> destination)
    {
        destination.Clear();
        destination.UnionWith(source);
    }
}
