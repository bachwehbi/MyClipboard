using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using Beebotte.API.Client.Net;

namespace MyClipboard
{

    public class ClipboadMonitor : IDisposable
    {
        readonly Thread _formThread;
        bool _disposed;

        public ClipboadMonitor()
        {
            _formThread = new Thread(() => { new ClipboardMonitorForm(this); })
                          {
                              IsBackground = true
                          };

            _formThread.SetApartmentState(ApartmentState.STA);
            _formThread.Start();
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            Disposed();
            if (_formThread != null && _formThread.IsAlive)
                _formThread.Abort();
            _disposed = true;
            GC.SuppressFinalize(this);
        }

        ~ClipboadMonitor()
        {
            Dispose();
        }

        public event Action<string> ClipboardTextChanged = delegate { };
        public event Action Disposed = delegate { };

        public void OnClipboardTextChanged(string text)
        {
            ClipboardTextChanged(text);
        }
    }

    public class ClipboardMonitorForm : Form
    {
        public const int WM_CLIPBOARDUPDATE = 0x031D;

        public const string User32Dll = "User32.dll";
        [DllImport(User32Dll, CharSet = CharSet.Auto)]
        public static extern bool AddClipboardFormatListener(IntPtr hWndObserver);

        [DllImport(User32Dll, CharSet = CharSet.Auto)]
        public static extern bool RemoveClipboardFormatListener(IntPtr hWndObserver);

        public ClipboardMonitorForm(ClipboadMonitor clipboardWatcher)
        {
            HideForm();
            RegisterWin32();
            ClipboardTextChanged += clipboardWatcher.OnClipboardTextChanged;
            clipboardWatcher.Disposed += () => InvokeIfRequired(Dispose);
            Disposed += (sender, args) => UnregisterWin32();
            Application.Run(this);
        }

        void InvokeIfRequired(Action action)
        {
            if (InvokeRequired)
                Invoke(action);
            else
                action();
        }

        public event Action<string> ClipboardTextChanged = delegate { };

        void HideForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
        }

        void RegisterWin32()
        {
            AddClipboardFormatListener(Handle);
        }

        void UnregisterWin32()
        {
            if (IsHandleCreated)
                RemoveClipboardFormatListener(Handle);
        }

        protected override void WndProc(ref System.Windows.Forms.Message m)
        {
            switch (m.Msg)
            {
                case WM_CLIPBOARDUPDATE:
                    Console.WriteLine(m.ToString());
                    ClipboardChanged();
                    break;

                default:
                    base.WndProc(ref m);
                    break;
            }
        }

        void ClipboardChanged()
        {
            if (Clipboard.ContainsText())
                ClipboardTextChanged(Clipboard.GetText());
        }
    }

    public class Program
    {
        static string accesskey = "YOUR_API_KEY";
        static string secretkey = "YOUR_SECRET_KEY";

        /* hostname */
        static string hostname = "http://ws.beebotte.com";
        static string channelName = "demo"; //the channel name to subscribe to
        static string resourceName = "clipboard"; //the resource to subscribe to
        static bool isPrivateChannel = true; //Boolean indicating if the channel is private
        static bool readAccess = true; //Boolean indicating if the connection has read access on the channel/resource
        static bool writeAccess = true; //Boolean indicating if the connection has write access on the channel/resource
        static Connector connector;

        static bool doPublish = true;

        static void setClipboard(String text)
        {
            doPublish = false; // Disable clipbaord publishing as this is not a local change
            Thread thread = new Thread(() => Clipboard.SetText(text));
            thread.SetApartmentState(ApartmentState.STA); //Set the thread to STA
            thread.Start();
            thread.Join();
            doPublish = true; // Are we sure at this point clipbaord watchers have been called??? 
        }

        [STAThread]
        static void Main(string[] args)
        {
            connector = new Connector(accesskey, secretkey, hostname);
            var clipboardWatcher = new ClipboadMonitor();

            connector.Connect();

            Console.WriteLine("Press Enter to quit!");

            connector.OnConnected += (u, m) =>
            {
                var subscription = connector.Subscribe(channelName, resourceName, isPrivateChannel, readAccess, writeAccess);
                subscription.OnMessage += (i, n) =>
                {
                    var received = Convert.ToString(n.Message.data);
                    setClipboard(received);
                    Console.WriteLine(received);
                };
            };

            clipboardWatcher.ClipboardTextChanged += text =>
            {
                Console.WriteLine(string.Format("Text arrived @ clipboard: {0}", text));
                if (doPublish)
                {
                    connector.Publish(channelName, resourceName, true, text);
                }
            };
            Console.ReadLine();
            
        }
    }
}
