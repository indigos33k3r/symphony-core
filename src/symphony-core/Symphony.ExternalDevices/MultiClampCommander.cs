using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using Symphony.Core;
using log4net;

namespace Symphony.ExternalDevices
{

    /// <summary>
    /// Event arguments containing a MultiClampInterop.MulticlampData describing
    /// the *new* parameters of the MultiClamp device. The MulticlampData is likely
    /// derived from a MC Commander telegraph Windows event.
    /// </summary>
    public class MultiClampParametersChangedArgs : TimeStampedEventArgs
    {
        public MultiClampParametersChangedArgs(IClock clock, MultiClampInterop.MulticlampData data)
            : base(clock)
        {
            this.Data = data;
        }

        public MultiClampInterop.MulticlampData Data { get; private set; }
    }

    /// <summary>
    /// Implementation of the IMultiClampCommander interface for Windows.
    /// 
    /// Usage:
    /// <example>
    /// <code>
    /// using(var multiClampCommander = new MultiClampCommander(serial, channel, clock)) {
    ///   multiClampCommander.ParametersChanges += (mcc, args) => { //handle device parameters };
    ///   ...
    /// }
    /// </code>
    /// </example>
    /// </summary>
    public sealed class MultiClampCommander : IMultiClampCommander
    {
        private readonly object _eventLock = new object();

        public uint SerialNumber { get; private set; }
        public uint COMPort { get; set; }
        public uint DeviceNumber { get; set; }
        public uint Channel { get; private set; }
        public MultiClampInterop.HardwareType HardwareType { get; private set; }

        public event EventHandler<MultiClampParametersChangedArgs> ParametersChanged;

        private IClock Clock { get; set; }

        private static readonly ILog log = LogManager.GetLogger(typeof(MultiClampCommander));

        public static IEnumerable<uint> AvailableSerialNumbers()
        {
            availableSerialNumbers = new HashSet<uint>();

            RegisterForIdEvents();
            try
            {
                ScanMultiClamps();
                Thread.Sleep(10);
            }
            finally
            {
                UnregisterForIdEvents();
            }

            return availableSerialNumbers;
        }

        private static HashSet<uint> availableSerialNumbers;

        /// <summary>
        /// Constructs a new 700A MultiClampCommander for a given COM port, device number (AKA AxoBus ID), and channel.
        /// </summary>
        /// <param name="comPort"></param>
        /// <param name="deviceNumber"></param>
        /// <param name="channel"></param>
        /// <param name="clock"></param>
        public MultiClampCommander(uint comPort, uint deviceNumber, uint channel, IClock clock)
        {
            COMPort = comPort;
            DeviceNumber = deviceNumber;
            Channel = channel;
            Clock = clock;
            HardwareType = MultiClampInterop.HardwareType.MCTG_HW_TYPE_MC700A;

            Init();
        }

        /// <summary>
        /// Constructs a new 700B MultiClampCommander for a given serial number and channel.
        /// </summary>
        /// <param name="serialNumber">MultiClamp device serial number</param>
        /// <param name="channel">MultiClamp channel</param>
        /// <param name="clock">IClock for generating event time stamps</param>
        public MultiClampCommander(uint serialNumber, uint channel, IClock clock)
        {
            SerialNumber = serialNumber;
            Channel = channel;
            Clock = clock;
            HardwareType = MultiClampInterop.HardwareType.MCTG_HW_TYPE_MC700B;

            Init();
        }

        private void Init()
        {
            var lParam = DeviceLParam();
            OpenMultiClampConversation(lParam);
            RegisterForWmCopyDataEvents();
            RegisterForReconnectEvents();
        }

        private uint DeviceLParam()
        {
            return HardwareType == MultiClampInterop.HardwareType.MCTG_HW_TYPE_MC700A ? 
                MultiClampInterop.MCTG_Pack700ASignalIDs(this.COMPort, this.DeviceNumber, this.Channel) : 
                MultiClampInterop.MCTG_Pack700BSignalIDs(this.SerialNumber, this.Channel);
        }

        private static void RegisterForIdEvents()
        {
            log.Debug("Registering for MCTG_ID_MESSAGE messages...");

            Win32Interop.MessageEvents.WatchMessage(MultiClampInterop.MCTG_ID_MESSAGE, ReceiveIdEvent);
        }

        private static void UnregisterForIdEvents()
        {
            log.Debug("Unregistering for MCTG_ID_MESSAGE messages...");

            Win32Interop.MessageEvents.UnwatchMessage(MultiClampInterop.MCTG_ID_MESSAGE, ReceiveIdEvent);
        }

        private static void ReceiveIdEvent(object sender, Win32Interop.MessageReceivedEventArgs evtArgs)
        {
            log.DebugFormat("Received MCTG_ID_MESSAGE: {0}", evtArgs.Message);

            // Not aware of a good way to determine if this is a 700A or 700B messsage. We'll assume 700B and let
            // the user do some bitwise operators on the serial number to get the COM port and device number if they
            // know the message is coming from a 700A Commander.
            uint serialNumber;
            uint channel;
            MultiClampInterop.MCTG_Unpack700BSignalIDs((uint)evtArgs.Message.LParam, out serialNumber, out channel);

            lock(availableSerialNumbers) availableSerialNumbers.Add(serialNumber);
        }

        private static void ScanMultiClamps()
        {
            log.Debug("Scanning for MultiClamps...");
            int result = Win32Interop.PostMessage(Win32Interop.HWND_BROADCAST, MultiClampInterop.MCTG_BROADCAST_MESSAGE,
                                                  (IntPtr)Win32Interop.MessageEvents.WindowHandle, (IntPtr)0);
            log.DebugFormat("  result = {0}", result);
        }

        private void ReceiveReconnectEvent(object sender, Win32Interop.MessageReceivedEventArgs evtArgs)
        {
            var lParam = DeviceLParam();
            if (evtArgs.Message.LParam != (IntPtr) lParam)
                return;

            log.DebugFormat("Received MCTG_RECONNECT_MESSAGE: {0}", evtArgs.Message);

            UnregisterForWmCopyDataEvents();
            UnregisterForReconnectEvents();

            OpenMultiClampConversation(lParam);
            RegisterForWmCopyDataEvents();
            RegisterForReconnectEvents();
                
            RequestTelegraphValue((uint) lParam);        
        }

        private void RegisterForReconnectEvents()
        {
            log.Debug("Registering for MCTG_RECONNECT_MESSAGE messages...");

            Win32Interop.MessageEvents.WatchMessage(MultiClampInterop.MCTG_RECONNECT_MESSAGE, ReceiveReconnectEvent);
        }

        private void UnregisterForReconnectEvents()
        {
            log.Debug("Unregistering for MCTG_RECONNECT_MESSAGE messages...");

            Win32Interop.MessageEvents.UnwatchMessage(MultiClampInterop.MCTG_RECONNECT_MESSAGE, ReceiveReconnectEvent);
        }

        public void RequestTelegraphValue()
        {
            RequestTelegraphValue(DeviceLParam());
        }

        private static void RequestTelegraphValue(uint lParam)
        {
            log.Debug("Requesting telegraph data...");
            int result = Win32Interop.PostMessage(Win32Interop.HWND_BROADCAST, MultiClampInterop.MCTG_REQUEST_MESSAGE,
                                                  (IntPtr) Win32Interop.MessageEvents.WindowHandle, (IntPtr) lParam);
            log.DebugFormat("  result = {0}", result);
        }

        private static void OpenMultiClampConversation(uint lParam)
        {
            log.Debug("Opening MultiClamp conversation...");
            int result = Win32Interop.PostMessage(Win32Interop.HWND_BROADCAST, MultiClampInterop.MCTG_OPEN_MESSAGE,
                                                  (IntPtr)Win32Interop.MessageEvents.WindowHandle, (IntPtr)lParam);
            log.DebugFormat("  result = {0}", result);
        }

        private void ReceiveWmCopyDataEvent(object sender, Win32Interop.MessageReceivedEventArgs evtArgs)
        {
            // WM_COPYDATA LPARAM is a pointer to a COPYDATASTRUCT structure
            Win32Interop.COPYDATASTRUCT cds = (Win32Interop.COPYDATASTRUCT) Marshal.PtrToStructure(evtArgs.Message.LParam, typeof (Win32Interop.COPYDATASTRUCT));

            // WM_COPYDATA structure (COPYDATASTRUCT)
            // dwData -- RegisterWindowMessage(MCTG_REQUEST_MESSAGE_STR)
            // cbData -- size (in bytes) of the MC_TELEGRAPH_DATA structure being sent
            // lpData -- MC_TELEGRAPH_DATA*
            try
            {
                if (cds.lpData == IntPtr.Zero || (cds.cbData != 128 /* 700A */ && cds.cbData != 256 /* 700B */) || (long)cds.dwData != MultiClampInterop.MCTG_REQUEST_MESSAGE)
                    return;

                var mtd = (MultiClampInterop.MC_TELEGRAPH_DATA)Marshal.PtrToStructure(cds.lpData, typeof(MultiClampInterop.MC_TELEGRAPH_DATA));
                if (mtd.uChannelID != Channel) 
                    return;

                // For some reason the hardware type can come through corrupted. The rest of the structure is intact however.
                if (mtd.uHardwareType != (uint)MultiClampInterop.HardwareType.MCTG_HW_TYPE_MC700A && mtd.uHardwareType != (uint)MultiClampInterop.HardwareType.MCTG_HW_TYPE_MC700B)
                {
                    log.Debug("Unknown hardware type in received message. Using device hardware type instead.");
                    mtd.uHardwareType = (uint)HardwareType;
                }
                if (mtd.uHardwareType != (uint)HardwareType)
                    return;

                var md = new MultiClampInterop.MulticlampData(mtd);

                log.Debug("WM_COPYDATA message received from MCCommander");
                OnParametersChanged(md);
            }
            catch (ArgumentOutOfRangeException)
            {
                log.ErrorFormat("WM_COPYDATA message received from MCCommander, but operating mode is not valid.");
                RequestTelegraphValue((uint) evtArgs.Message.LParam);
            }
        }

        private void RegisterForWmCopyDataEvents()
        {
            log.Debug("Registering for WM_COPYDATA messages");

            Win32Interop.MessageEvents.WatchMessage(Win32Interop.WM_COPYDATA, ReceiveWmCopyDataEvent);
        }

        private void UnregisterForWmCopyDataEvents()
        {
            log.Debug("Unregistering for WM_COPYDATA messages");

            Win32Interop.MessageEvents.UnwatchMessage(Win32Interop.WM_COPYDATA, ReceiveWmCopyDataEvent);
        }

        private void OnParametersChanged(MultiClampInterop.MulticlampData data)
        {
            lock (_eventLock)
            {
                var temp = ParametersChanged;
                if (temp != null)
                {
                    temp(this, new MultiClampParametersChangedArgs(Clock, data));
                }
            }
        }

        // Indirect Dispose method from http://msdn.microsoft.com/en-us/library/system.idisposable.aspx
        private bool _disposed = false;

        void Dispose(bool disposing)
        {
            if (!this._disposed)
            {
                // Remove references from static Win32Interop class or this object will exist indefinitely
                UnregisterForWmCopyDataEvents();
                UnregisterForReconnectEvents();

                int result = Win32Interop.PostMessage(Win32Interop.HWND_BROADCAST, MultiClampInterop.MCTG_CLOSE_MESSAGE, (IntPtr)Win32Interop.MessageEvents.WindowHandle, (IntPtr)DeviceLParam());

                if (disposing)
                {
                    //Handle manage object disposal

                    // This object will be cleaned up by the Dispose method.
                    // Therefore, you should call GC.SupressFinalize to
                    // take this object off the finalization queue
                    // and prevent finalization code for this object
                    // from executing a second time.
                    GC.SuppressFinalize(this);
                }

                // Note disposing has been done.
                _disposed = true;

            }

        }

        public void Dispose()
        {
            Dispose(true);
        }

        ~MultiClampCommander()
        {
            Dispose(false);
        }
    }
}