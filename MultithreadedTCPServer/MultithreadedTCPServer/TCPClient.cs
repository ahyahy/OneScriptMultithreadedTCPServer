using ScriptEngine.HostedScript.Library;
using ScriptEngine.HostedScript.Library.Binary;
using ScriptEngine.Machine.Contexts;
using ScriptEngine.Machine;
using System.Threading.Tasks;
using System;

namespace mtcps
{
    public class TCPClient
    {
        public MsTCPClient dll_obj;
        public System.Text.Encoding Encoding { get; set; } = System.Text.Encoding.UTF8;
        public System.Net.Sockets.TcpClient M_TcpClient;
        public string MessageReceived;

        public TCPClient()
        {
            M_TcpClient = new System.Net.Sockets.TcpClient();
            this.ClientReceived += TCPClient_ClientReceived;
        }

        public TCPClient(string HostName, int port)
        {
            M_TcpClient = new System.Net.Sockets.TcpClient(HostName, port);
            this.ClientReceived += TCPClient_ClientReceived;
        }

        private void TCPClient_ClientReceived(object sender, MsEventArgs e)
        {
            if (dll_obj?.ClientReceived != null)
            {
                var args = new MsEventArgs
                {
                    eventAction = dll_obj.ClientReceived,
                    parameter = Utils.GetEventParameter(dll_obj.ClientReceived),
                    data = e.Data
                };
                OneScriptMultithreadedTCPServer.Event = args;
                OneScriptMultithreadedTCPServer.ExecuteEvent(dll_obj.ClientReceived);
            }
        }

        public event EventHandler<MsEventArgs> ClientReceived;
        public void OnClientReceived(BinaryDataBuffer p1)
        {
            var handler = ClientReceived;
            if (handler != null)
            {
                handler(this, new MsEventArgs(p1));
            }
        }

        public bool Connected
        {
            get { return M_TcpClient.Connected; }
        }

        public void Close()
        {
            M_TcpClient.Close();
        }

        public void Connect(string hostname, int portNo)
        {
            M_TcpClient.Connect(hostname, portNo);
        }

        public System.Net.Sockets.NetworkStream GetStream()
        {
            return M_TcpClient.GetStream();
        }

        public async void Send(BinaryDataBuffer message)
        {
            if (!M_TcpClient.Connected)
            {
                Utils.GlobalContext().Echo("Ошибка отправки текста: Клиентотключен.");
                return;
            }

            try
            {
                var stream = M_TcpClient.GetStream();
                var data = message.Bytes;
                await stream.WriteAsync(data, 0, data.Length).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Utils.GlobalContext().Echo("Ошибка отправки буфера двоичных данных: " + ex.Message);
            }
        }

        public async void Send(string message)
        {
            if (!M_TcpClient.Connected)
            {
                Utils.GlobalContext().Echo("Ошибка отправки текста: Клиентотключен.");
                return;
            }

            try
            {
                var stream = M_TcpClient.GetStream();
                var data = Encoding.GetBytes(message.EndsWith("\n") ? message : message + "\n");
                await stream.WriteAsync(data, 0, data.Length).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Utils.GlobalContext().Echo("Ошибка отправки текста: " + ex.Message);
            }
        }
    }

    [ContextClass("МсTCPКлиент", "MsTCPClient")]
    public class MsTCPClient : AutoContext<MsTCPClient>
    {
        public MsTCPClient()
        {
            TCPClient TCPClient1 = new TCPClient();
            TCPClient1.dll_obj = this;
            Base_obj = TCPClient1;
        }

        public MsTCPClient(string HostName, int port)
        {
            TCPClient TCPClient1 = new TCPClient(HostName, port);
            TCPClient1.dll_obj = this;
            Base_obj = TCPClient1;
        }

        public TCPClient Base_obj;

        [ContextProperty("КлиентПолучилДанные", "ClientReceived")]
        public MsAction ClientReceived { get; set; }

        [ContextMethod("ОбработатьКлиентПолучилДанные", "ProcessingClientReceived")]
        public void ProcessingClientReceived(BinaryDataBuffer p1)
        {
            Base_obj.OnClientReceived(p1);
        }

        [ContextProperty("Кодировка", "Encoding")]
        public MsEncoding Encoding
        {
            get
            {
                mtcps.Encoding Encoding1 = new mtcps.Encoding();
                Encoding1.M_Encoding = Base_obj.Encoding;
                return new MsEncoding(Encoding1);
            }
            set { Base_obj.Encoding = value.Base_obj.M_Encoding; }
        }

        [ContextProperty("Подключен", "Connected")]
        public bool Connected
        {
            get { return Base_obj.Connected; }
        }

        [ContextMethod("Закрыть", "Close")]
        public void Close()
        {
            Base_obj.Close();
        }

        [ContextMethod("Отправить", "Send")]
        public void Send(IValue p1)
        {
            if (Utils.IsType<BinaryDataBuffer>(p1))
            {
                _ = Task.Run(() =>
                {
                    Base_obj.Send((BinaryDataBuffer)p1);
                });
            }
            else
            {
                _ = Task.Run(() =>
                {
                    Base_obj.Send(p1.AsString());
                });
            }
        }

        [ContextMethod("Подключить", "Connect")]
        public void Connect(string p1, int p2)
        {
            Base_obj.Connect(p1, p2);
        }

        [ContextMethod("ПолучитьПоток", "GetStream")]
        public MsNetworkStream GetStream()
        {
            try
            {
                return new MsNetworkStream(Base_obj.GetStream());
            }
            catch
            {
                return null;
            }
        }
    }
}
