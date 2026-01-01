using ScriptEngine.HostedScript.Library.Binary;
using ScriptEngine.HostedScript.Library;
using ScriptEngine.Machine.Contexts;
using ScriptEngine.Machine;
using System.Collections.Concurrent;
using System;

namespace mtcps
{
    [ContextClass("МногопоточныйTCPСерверДляОдноСкрипта", "OneScriptMultithreadedTCPServer")]
    public class OneScriptMultithreadedTCPServer : AutoContext<OneScriptMultithreadedTCPServer>
    {
        public static IValue EventAction = null;
        public static ConcurrentQueue<dynamic> EventQueue = new ConcurrentQueue<dynamic>();
        public static OneScriptMultithreadedTCPServer instance;

        [ScriptConstructor]
        public static IRuntimeContextInstance Constructor()
        {
            instance = new OneScriptMultithreadedTCPServer();

            instance.ClientConnected += instance.Instance_ClientConnected;
            instance.ClientDisconnected += instance.Instance_ClientDisconnected;
            instance.ErrorServer += instance.Instance_ErrorServer;
            instance.ServerReceived += instance.Instance_ServerReceived;

            return instance;
        }

        private void Instance_ServerReceived(object sender, MsEventArgs e)
        {
            if (ServerReceivedProp != null)
            {
                var args = new MsEventArgs
                {
                    eventAction = ServerReceivedProp,
                    parameter = Utils.GetEventParameter(ServerReceivedProp),
                    data = e.Data,
                    clientId = e.ClientId
                };
                OneScriptMultithreadedTCPServer.Event = args;
                OneScriptMultithreadedTCPServer.ExecuteEvent(ServerReceivedProp);
            }
        }

        private void Instance_ErrorServer(object sender, MsEventArgs e)
        {
            if (ErrorServerProp != null)
            {
                var args = new MsEventArgs
                {
                    eventAction = ErrorServerProp,
                    parameter = Utils.GetEventParameter(ErrorServerProp),
                    serverError = e.ServerError
                };
                OneScriptMultithreadedTCPServer.Event = args;
                OneScriptMultithreadedTCPServer.ExecuteEvent(ErrorServerProp);
            }
        }

        private void Instance_ClientDisconnected(object sender, MsEventArgs e)
        {
            if (ClientDisconnectedProp != null)
            {
                var args = new MsEventArgs
                {
                    eventAction = ClientDisconnectedProp,
                    parameter = Utils.GetEventParameter(ClientDisconnectedProp),
                    clientId = e.ClientId
                };
                OneScriptMultithreadedTCPServer.Event = args;
                OneScriptMultithreadedTCPServer.ExecuteEvent(ClientDisconnectedProp);
            }
        }

        private void Instance_ClientConnected(object sender, MsEventArgs e)
        {
            if (ClientConnectedProp != null)
            {
                var args = new MsEventArgs
                {
                    eventAction = ClientConnectedProp,
                    parameter = Utils.GetEventParameter(ClientConnectedProp),
                    clientId = e.ClientId
                };
                OneScriptMultithreadedTCPServer.Event = args;
                OneScriptMultithreadedTCPServer.ExecuteEvent(ClientConnectedProp);
            }
        }

        public event EventHandler<MsEventArgs> ClientConnected;
        public void OnClientConnected(MsEventArgs args)
        {
            var handler = ClientConnected;
            if (handler != null)
            {
                handler(this, new MsEventArgs(args));
            }
        }

        public event EventHandler<MsEventArgs> ClientDisconnected;
        public void OnClientDisconnected(MsEventArgs args)
        {
            var handler = ClientDisconnected;
            if (handler != null)
            {
                handler(this, new MsEventArgs(args));
            }
        }

        public event EventHandler<MsEventArgs> ServerReceived;
        public void OnServerReceived(MsEventArgs args)
        {
            var handler = ServerReceived;
            if (handler != null)
            {
                handler(this, new MsEventArgs(args));
            }
        }

        public event EventHandler<MsEventArgs> ErrorServer;
        public void OnErrorServer(MsEventArgs args)
        {
            var handler = ErrorServer;
            if (handler != null)
            {
                handler(this, new MsEventArgs(args));
            }
        }

        [ContextProperty("ПриОтключенииКлиента", "ClientDisconnected")]
        public MsAction ClientDisconnectedProp { get; set; }

        [ContextProperty("ПриОшибкеСервера", "ErrorServer")]
        public MsAction ErrorServerProp { get; set; }

        [ContextProperty("ПриПодключенииКлиента", "ClientConnected")]
        public MsAction ClientConnectedProp { get; set; }

        [ContextProperty("СерверПолучилДанные", "ServerReceived")]
        public MsAction ServerReceivedProp { get; set; }

        [ContextProperty("СостояниеСервера", "ServerState")]
        public MsMultithreadedTCPServerState ServerState
        {
            get { return new MsMultithreadedTCPServerState(); }
        }

        [ContextMethod("ОбработатьПриОтключенииКлиента", "ProcessingClientDisconnected")]
        public void ProcessingClientDisconnected(MsEventArgs args)
        {
            instance.OnClientDisconnected(args);
        }

        [ContextMethod("ОбработатьПриОшибкеСервера", "ProcessingErrorServer")]
        public void ProcessingErrorServer(MsEventArgs args)
        {
            instance.OnErrorServer(args);
        }

        [ContextMethod("ОбработатьПриПодключенииКлиента", "ProcessingClientConnected")]
        public void ProcessingClientConnected(MsEventArgs args)
        {
            instance.OnClientConnected(args);
        }

        [ContextMethod("ОбработатьСерверПолучилДанные", "ProcessingServerReceived")]
        public void ProcessingServerReceived(MsEventArgs args)
        {
            instance.OnServerReceived(args);
        }

        //БезопасныйПоток", "MsSslStream














        [ContextMethod("Кодировка", "Encoding")]
        public MsEncoding Encoding()
        {
            return new MsEncoding();
        }

        [ContextMethod("Действие", "Action")]
        public MsAction Action(IRuntimeContextInstance script, string methodName, IValue param = null)
        {
            return new MsAction(script, methodName, param);
        }

        public static IValue Event = null;
        [ContextProperty("АргументыСобытия", "EventArgs")]
        public IValue EventArgs
        {
            get { return Event; }
        }

        public bool goOn = true;
        [ContextProperty("Продолжать", "GoOn")]
        public bool GoOn
        {
            get { return goOn; }
            set { goOn = value; }
        }

        [ContextMethod("ПолучитьСобытие", "DoEvents")]
        public DelegateAction DoEvents()
        {
            while (EventQueue.Count == 0)
            {
                System.Threading.Thread.Sleep(7);
            }

            IValue Action1 = EventHandling();
            if (Action1.GetType() == typeof(MsAction))
            {
                return DelegateAction.Create(((MsAction)Action1).Script, ((MsAction)Action1).MethodName);
            }
            return (DelegateAction)Action1;
        }

        public IValue EventHandling()
        {
            dynamic EventArgs1;
            EventQueue.TryDequeue(out EventArgs1);
            Event = EventArgs1;
            EventAction = EventArgs1.EventAction;
            return EventAction;
        }

        [ContextProperty("НоваяСтрока", "NewLine")]
        public string NewLine
        {
            get { return Utils.NewLine; }
        }

        [ContextMethod("TCPКлиент", "TCPClient")]
        public MsTCPClient TCPClient(IValue HostName = null, IValue port = null)
        {
            if (Utils.AllNotNull(HostName, port))
            {
                return new MsTCPClient(HostName.AsString(), Utils.ToInt32(port));
            }
            return new MsTCPClient();
        }

        [ContextMethod("TCPКлиентSSL", "TCPClientSSL")]
        public MsTCPClientSSL TCPClientSSL(IValue HostName = null, IValue port = null, IValue path_certificate_crt = null)
        {
            if (Utils.AllNotNull(HostName, port, path_certificate_crt))
            {
                return new MsTCPClientSSL(HostName.AsString(), Utils.ToInt32(port), path_certificate_crt.AsString());
            }
            return new MsTCPClientSSL();
        }

        [ContextMethod("МногопоточныйСервер", "MultithreadedServer")]
        public MsMultithreadedTCPServer MultithreadedTCPServer(int port)
        {
            return new MsMultithreadedTCPServer(port);
        }
        [ContextMethod("МногопоточныйСерверSSL", "MultithreadedServerSSL")]
        public MsMultithreadedTCPServerSSL MultithreadedTCPServerSSL(int port, string path_certificate_crt = null, string pas = null)
        {
            return new MsMultithreadedTCPServerSSL(port, path_certificate_crt, pas);
        }






        public static bool multiServerUploaded = false;
        public static bool multiServerError = false;
        [ContextMethod("ФоновыйМногопоточныйСервер", "BackgroundMultithreadedServer")]
        public MsMultithreadedTCPServer BackgroundMultithreadedServer(int p1)
        {
            string backgroundTasksMultiTCPServer = @"
Процедура ЗапускМногопоточногоСервера(параметр1, параметр2) Экспорт
    Контекст = Новый Структура();
    Контекст.Вставить(""МС"", параметр1);
    Контекст.Вставить(""Сервер"", параметр2);
	Стр = ""
	|
	|Процедура Сервер_ПриПодключенииКлиента() Экспорт
	|    МС.ОбработатьПриПодключенииКлиента(МС.АргументыСобытия);
	|КонецПроцедуры
	|
	|Процедура Сервер_ПриОтключенииКлиента() Экспорт
	|    МС.ОбработатьПриОтключенииКлиента(МС.АргументыСобытия);
	|КонецПроцедуры
	|
	|Процедура Сервер_СерверПолучилДанные() Экспорт
	|    МС.ОбработатьСерверПолучилДанные(МС.АргументыСобытия);
	|КонецПроцедуры
	|
	|Процедура Сервер_ПриОшибкеСервера() Экспорт
	|    МС.ОбработатьПриОшибкеСервера(МС.АргументыСобытия);
	|КонецПроцедуры
	|
	|Сервер.ПриПодключенииКлиента = МС.Действие(ЭтотОбъект, """"Сервер_ПриПодключенииКлиента"""");
	|Сервер.ПриОтключенииКлиента = МС.Действие(ЭтотОбъект, """"Сервер_ПриОтключенииКлиента"""");
	|Сервер.СерверПолучилДанные = МС.Действие(ЭтотОбъект, """"Сервер_СерверПолучилДанные"""");
	|Сервер.ПриОшибкеСервера = МС.Действие(ЭтотОбъект, """"Сервер_ПриОшибкеСервера"""");
	|Сервер.Начать();
	|
	|Пока МС.Продолжать Цикл
	|   МС.ПолучитьСобытие().Выполнить();
	|КонецЦикла;
	|"";
	ЗагрузитьСценарийИзСтроки(Стр, Контекст);
КонецПроцедуры

МассивПараметров = Новый Массив(2);
МассивПараметров[0] = МС;
МассивПараметров[1] = Сервер;
Задание = ФоновыеЗадания.Выполнить(ЭтотОбъект, ""ЗапускМногопоточногоСервера"", МассивПараметров);
";

            MsMultithreadedTCPServer MultiTCPServer = new MsMultithreadedTCPServer(p1);
            StructureImpl extContext = new StructureImpl();
            extContext.Insert("МС", instance);
            extContext.Insert("Сервер", MultiTCPServer);
            Utils.GlobalContext().LoadScriptFromString(backgroundTasksMultiTCPServer, extContext);
            while (!multiServerUploaded)
            {
                System.Threading.Thread.Sleep(300);
                if (multiServerError)
                {
                    break;
                }
            }
            if (multiServerError)
            {
                return null;
            }
            return MultiTCPServer;
        }

        [ContextMethod("ФоновыйTCPКлиент", "BackgroundTCPClient")]
        public MsTCPClient LaunchTCPConnection(string HostName = null, int port = 0)
        {
            return LoadTCPClient(HostName, port);
        }

        public static MsTCPClient LoadTCPClient(string HostName = null, int port = 0)
        {
            string backgroundTasksTCPConnection = @"
Процедура ЗапускКлиента(параметр1) Экспорт
    Контекст = Новый Структура();
    Контекст.Вставить(""Клиент"", параметр1);
	Стр = ""
	|Перем ПотокСети1;
	|
	|Процедура ПроверитьСообщение()
	|    Клиент.ОбработатьКлиентПолучилДанные(ПотокСети1.ПрочитатьВБуферДвоичныхДанных());
	|КонецПроцедуры
	|
	|ПотокСети1 = Клиент.ПолучитьПоток();
	|
	|Пока Клиент.Подключен Цикл
	|    Если Не ПотокСети1.ДанныеДоступны Тогда
	|        Приостановить(100);
	|    Иначе
	|        ПроверитьСообщение();
	|    КонецЕсли;
	|КонецЦикла;
	|"";
	ЗагрузитьСценарийИзСтроки(Стр, Контекст);
КонецПроцедуры

МассивПараметров = Новый Массив(1);
МассивПараметров[0] = Клиент;
Задание = ФоновыеЗадания.Выполнить(ЭтотОбъект, ""ЗапускКлиента"", МассивПараметров);
";
            MsTCPClient clientTCP = new MsTCPClient(HostName, port);
            StructureImpl extContext = new StructureImpl();
            extContext.Insert("Клиент", clientTCP);
            Utils.GlobalContext().LoadScriptFromString(backgroundTasksTCPConnection, extContext);

            if (clientTCP.Connected)
            {
                return clientTCP;
            }
            else
            {
                return null;
            }
        }

        public static void ExecuteEvent(dynamic dll_objEvent)
        {
            if (dll_objEvent == null)
            {
                return;
            }
            if (dll_objEvent.GetType() == typeof(DelegateAction))
            {
                try
                {
                    ((DelegateAction)dll_objEvent).CallAsProcedure(0, null);
                }
                catch { }
            }
            else if (dll_objEvent.GetType() == typeof(MsAction))
            {
                MsAction Action1 = ((MsAction)dll_objEvent);
                IRuntimeContextInstance script = Action1.Script;
                string method = Action1.MethodName;
                ReflectorContext reflector = new ReflectorContext();
                try
                {
                    reflector.CallMethod(script, method, null);
                }
                catch { }
            }
            else
            {
                //System.Windows.Forms.MessageBox.Show("Обработчик события " + dll_objEvent.ToString() + " задан неверным типом.", "Обработчик события контрола", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning, System.Windows.Forms.MessageBoxDefaultButton.Button1);
            }
            Event = null;
        }

        [ContextMethod("СоздатьСамоподписанныйСертификат", "CreateSelfSignedCertificate")]
        public void CreateSelfSignedCertificate(string path, string subjectName, string pas)
        {
            MsMultithreadedTCPServerSSL.CreateSelfSignedCertificate(path, subjectName, pas);
        }
    }
}
