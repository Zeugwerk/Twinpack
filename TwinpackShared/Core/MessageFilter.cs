using NLog.Filters;
using System;
using System.Runtime.InteropServices;


namespace Twinpack.Core
{
    // Interface for an OleMessageFilter that is used to wait for COM event. Without implementing
    // this interface, most commands the use the Beckhoff Automation Interface will fail due to
    // timing issues.
    [ComImport(), Guid("00000016-0000-0000-C000-000000000046"), InterfaceTypeAttribute(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IOleMessageFilter
    {
        [PreserveSig]
        int HandleInComingCall(int dwCallType, IntPtr hTaskCaller, int dwTickCount, IntPtr lpInterfaceInfo);
        [PreserveSig]
        int RetryRejectedCall(IntPtr hTaskCallee, int dwTickCount, int dwRejectType);
        [PreserveSig]
        int MessagePending(IntPtr hTaskCallee, int dwTickCount, int dwPendingType);
    }

    // This class is suggested by Beckhoff in order to use the Automation Interface. It is used to
    // synchronize the Visualstudio instance that is started within the application.
    public class MessageFilter : IOleMessageFilter, IDisposable
    {
        IOleMessageFilter _oldFilter;
        public MessageFilter()
        {
            CoRegisterMessageFilter(this, out _oldFilter);
        }

        public void Dispose()
        {
            if (_oldFilter != null)
            {
                CoRegisterMessageFilter(_oldFilter, out _);
                _oldFilter = null;
            }
        }

        int IOleMessageFilter.HandleInComingCall(int dwCallType, System.IntPtr hTaskCaller, int dwTickCount, System.IntPtr lpInterfaceInfo)
        {
            return 0;
        }

        int IOleMessageFilter.RetryRejectedCall(System.IntPtr hTaskCallee, int dwTickCount, int dwRejectType)
        {
            // Thread call was refused, try again. 
            if (dwRejectType == 2) // flag = SERVERCALL_RETRYLATER. 
            {
                // retry thread call at once, if return value >=0 & // <100. 
                return 99;
            }

            return -1;
        }

        int IOleMessageFilter.MessagePending(System.IntPtr hTaskCallee, int dwTickCount, int dwPendingType)
        {
            //return flag PENDINGMSG_WAITDEFPROCESS. 
            return 2;
        } // implement IOleMessageFilter interface

        [DllImport("Ole32.dll")]
        private static extern int CoRegisterMessageFilter(IOleMessageFilter newFilter, out IOleMessageFilter oldFilter);
    }
}
