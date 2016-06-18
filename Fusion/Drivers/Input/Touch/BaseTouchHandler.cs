﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace Fusion.Input.Touch
{
    public abstract class BaseTouchHandler : IDisposable, IInteractionHandler
    {
        IntPtr _context;
        int _lastFrameID = -1;

        HashSet<int> _activePointers = new HashSet<int>();

        public BaseTouchHandler()
        {
            _context = Win32TouchFunctions.CreateInteractionContext(this, SynchronizationContext.Current);

            //Win32.SetPropertyInteractionContext(Context, Win32.INTERACTION_CONTEXT_PROPERTY.FILTER_POINTERS, Win32.ICP_FILTER_POINTERS_ON);
        }

        public void Dispose()
        {
            Win32TouchFunctions.DisposeInteractionContext(_context);
            _context = IntPtr.Zero;
        }

        public IntPtr Context
        {
            get { return _context; }
        }

        void IInteractionHandler.ProcessInteractionEvent(InteractionOutput output)
        {
            ProcessEvent(output);
        }

        internal abstract void ProcessEvent(InteractionOutput output);


        public void ProcessPointerFrames(int pointerID, int frameID)
        {
            if (_lastFrameID != frameID)
            {
                _lastFrameID = frameID;
                int entriesCount = 0;
                int pointerCount = 0;
                if (!Win32TouchFunctions.GetPointerFrameInfoHistory(pointerID, ref entriesCount, ref pointerCount, IntPtr.Zero))
                {
                    Win32TouchFunctions.CheckLastError();
                }
                Win32TouchFunctions.POINTER_INFO[] piArr = new Win32TouchFunctions.POINTER_INFO[entriesCount * pointerCount];
                if (!Win32TouchFunctions.GetPointerFrameInfoHistory(pointerID, ref entriesCount, ref pointerCount, piArr))
                {
                    Win32TouchFunctions.CheckLastError();
                }
                IntPtr hr = Win32TouchFunctions.ProcessPointerFramesInteractionContext(_context, entriesCount, pointerCount, piArr);
                if (Win32TouchFunctions.FAILED(hr))
                {
                    Debug.WriteLine("ProcessPointerFrames failed: " + Win32TouchFunctions.GetMessageForHR(hr));
                }
            }
        }

        public HashSet<int> ActivePointers
        {
            get { return _activePointers; }
        }

        public void AddPointer(int pointerID)
        {
            Win32TouchFunctions.AddPointerInteractionContext(_context, pointerID);
            _activePointers.Add(pointerID);
        }

        public void RemovePointer(int pointerID)
        {
            Win32TouchFunctions.RemovePointerInteractionContext(_context, pointerID);
            _activePointers.Remove(pointerID);
        }

        public void StopProcessing()
        {
            foreach (int pointerID in _activePointers)
            {
                Win32TouchFunctions.RemovePointerInteractionContext(_context, pointerID);
            }
            _activePointers.Clear();
            Win32TouchFunctions.StopInteractionContext(_context);
        }
    }
}
