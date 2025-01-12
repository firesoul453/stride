// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.
#if STRIDE_UI_SDL
using System;
using System.Collections.Generic;
using Stride.Core.Mathematics;
using Silk.NET.SDL;
using Point = Stride.Core.Mathematics.Point;

namespace Stride.Graphics.SDL
{
    public unsafe static class Application
    {
        private static Sdl SDL => Window.SDL;

        /// <summary>
        /// Initialize Application for handling events and available windows.
        /// </summary>
        static Application()
        {
            InternalWindows = new Dictionary<IntPtr, WeakReference<Window>>(10);
        }

        /// <summary>
        /// Register <paramref name="c"/> to the list of available windows.
        /// </summary>
        /// <param name="c">Window to register</param>
        public static void RegisterWindow(Window c)
        {
            lock (InternalWindows)
            {
                InternalWindows.Add(c.SdlHandle, new WeakReference<Window>(c));
            }
        }

        /// <summary>
        /// Unregister <paramref name="c"/> from the list of available windows.
        /// </summary>
        /// <param name="c">Window to unregister</param> 
        public static void UnregisterWindow(Window c)
        {
            lock (InternalWindows)
            {
                InternalWindows.Remove(c.SdlHandle);
            }
        }

        /// <summary>
        /// Window that currently has the focus.
        /// </summary>
        public static Window WindowWithFocus { get; private set; }

        /// <summary>
        /// Screen coordinate of the mouse.
        /// </summary>
        public static Point MousePosition
        {
            get
            {
                int x, y;
                SDL.GetGlobalMouseState(&x, &y);
                return new Point(x, y);
            }
            set
            {
                int err = SDL.WarpMouseGlobal(value.X, value.Y);
                if (err != 0)
                    throw new NotSupportedException("Current platform doesn't let you set the position of the mouse cursor.");
            }
        }

        /// <summary>
        /// List of windows managed by the application.
        /// </summary>
        public static List<Window> Windows
        {
            get
            {
                lock (InternalWindows)
                {
                    var res = new List<Window>(InternalWindows.Count);
                    List<IntPtr> toRemove = null;
                    foreach (var weakRef in InternalWindows)
                    {
                        Window ctrl;
                        if (weakRef.Value.TryGetTarget(out ctrl))
                        {
                            res.Add(ctrl);
                        }
                        else
                        {
                                // Window was reclaimed without being unregistered first.
                                // We add it to `toRemove' to remove it from InternalWindows later.
                            if (toRemove == null)
                            {
                                toRemove = new List<IntPtr>(5);
                            }
                            toRemove.Add(weakRef.Key);
                        }
                    }
                        // Clean InternalWindows from windows that have been collected.
                    if (toRemove != null)
                    {
                        foreach (var w in toRemove)
                        {
                            InternalWindows.Remove(w);
                        }
                    }
                    return res;
                }
            }
        }

        /// <summary>
        /// Process all available events.
        /// </summary>
        public static void ProcessEvents()
        {
            Event e;
            while (SDL.PollEvent(&e) > 0)
            {
                // Handy for debugging
                //if (e.type == EventType.WINDOWEVENT)
                //    Debug.WriteLine(e.window.windowEvent);

                Application.ProcessEvent(e);
            }
        }

        /// <summary>
        /// Process a single event and dispatch it to the right window.
        /// </summary>
        public static void ProcessEvent(Event e)
        {
            Window ctrl = null;

                // Code below is to extract the associated `Window' instance and to find out the window
                // with focus. In the future, we could even add events handled at the application level.
            switch ((EventType)e.Type)
            {
                case EventType.Mousebuttondown:
                case EventType.Mousebuttonup:
                    ctrl = WindowFromSdlHandle(SDL.GetWindowFromID(e.Button.WindowID));
                    break;

                case EventType.Mousemotion:
                    ctrl = WindowFromSdlHandle(SDL.GetWindowFromID(e.Motion.WindowID));
                    break;

                case EventType.Mousewheel:
                    ctrl = WindowFromSdlHandle(SDL.GetWindowFromID(e.Wheel.WindowID));
                    break;
                    
                case EventType.Keydown:
                case EventType.Keyup:
                    ctrl = WindowFromSdlHandle(SDL.GetWindowFromID(e.Key.WindowID));
                    break;

                case EventType.Textediting:
                    ctrl = WindowFromSdlHandle(SDL.GetWindowFromID(e.Edit.WindowID));
                    break;

                case EventType.Textinput:
                    ctrl = WindowFromSdlHandle(SDL.GetWindowFromID(e.Text.WindowID));
                    break;

                case EventType.Fingermotion:
                case EventType.Fingerdown:
                case EventType.Fingerup:
                    ctrl = WindowWithFocus;
                    break;

                case EventType.Windowevent:
                {
                    ctrl = WindowFromSdlHandle(SDL.GetWindowFromID(e.Window.WindowID));
                    switch ((WindowEventID)e.Window.Event)
                    {
                        case WindowEventID.WindoweventFocusGained:
                            WindowWithFocus = ctrl;
                            break;

                        case WindowEventID.WindoweventFocusLost:
                            WindowWithFocus = null;
                            break;
                    }
                    break;
                }
                case EventType.Joydeviceadded:
                case EventType.Joydeviceremoved:
                    // Send these events to all the windows
                    Windows.ForEach(x => x.ProcessEvent(e));
                    break;
                
                case EventType.Droptext:
                case EventType.Dropfile:
                    ctrl = WindowFromSdlHandle(SDL.GetWindowFromID(e.Drop.WindowID));
                    break;
            }
            ctrl?.ProcessEvent(e);
        }

        /// <summary>
        /// Given a SDL Handle of a SDL window, retrieve the corresponding managed object. If object
        /// was already garbage collected, we will also clean up <see cref="InternalWindows"/>.
        /// </summary>
        /// <param name="w">SDL Handle of the window we are looking for</param>
        /// <returns></returns>
        private static Window WindowFromSdlHandle(Silk.NET.SDL.Window* w)
        {
            lock (InternalWindows)
            {
                WeakReference<Window> weakRef;
                if (InternalWindows.TryGetValue((IntPtr)w, out weakRef))
                {
                    Window ctrl;
                    if (weakRef.TryGetTarget(out ctrl))
                    {
                        return ctrl;
                    } 
                    else
                    {
                            // Window does not exist anymore in our code. Clean `InternalWindows'.
                        InternalWindows.Remove((IntPtr)w);
                        return null;
                    }
                }
                else
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Backup storage for windows of current application.
        /// </summary>
        private static readonly Dictionary<IntPtr, WeakReference<Window>> InternalWindows;
    
        public static string Clipboard
        {
            get => SDL.GetClipboardTextS();
            set => SDL.SetClipboardText(value);
        }
    }
}
#endif

