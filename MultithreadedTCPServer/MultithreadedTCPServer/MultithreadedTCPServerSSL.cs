using ScriptEngine.HostedScript.Library.Binary;
using ScriptEngine.HostedScript.Library;
using ScriptEngine.Machine.Contexts;
using ScriptEngine.Machine;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Net;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;
using System.Net.Security;

namespace mtcps
{
    public class MultithreadedTCPServerSSL : IDisposable
    {
        public MsMultithreadedTCPServerSSL dll_obj;
        private static long _lastClientId;
        private volatile ServerState _state = ServerState.Stopped;
        private readonly object _stateLock = new object();
        public ServerState State => _state;
        private int maxMessageSize = 1024 * 1024 * 128; // 128 MB Ограничение на размер принимаемого сообщения.
        private readonly ByteBufferPool _bufferPool = new ByteBufferPool(1024);
        private int maxClients = 1500; // Ограничение на количество подключаемых клиентов.
        public int ActiveClients => _clients.Count; // Количество активных клиентов.
        private TcpListener _listener;
        private X509Certificate2 certificate = null;
        private ConcurrentDictionary<string, ClientInfo> _clients = new ConcurrentDictionary<string, ClientInfo>();
        private CancellationTokenSource _cts;
        private bool _isRunning;
        public int Port { get; set; } = 8080;
        public System.Text.Encoding Encoding { get; set; } = System.Text.Encoding.UTF8;
        public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromSeconds(10); // Интервал проверки активности клиента.

        public event Action<string> OnClientConnected;
        public event Action<string> OnClientDisconnected;
        public event Action<string, BinaryDataBuffer> OnMessageReceived;
        public event Action<string> OnServerError;

        private static string pathCert;
        private static string password;

        private class ClientInfo
        {
            public TcpClient Client { get; set; }
            public SslStream SslStream { get; set; }
            public CancellationTokenSource Cts { get; set; }
            public string Id { get; set; }
            public DateTime LastActivity { get; set; }
            public SemaphoreSlim WriteLock { get; } = new SemaphoreSlim(1, 1); // Семафор для синхронизации записи
        }

        public MultithreadedTCPServerSSL(int port, string path_cert, string pas)
        {
            Port = port;
            pathCert = path_cert;
            password = pas;

            OnClientConnected += MultithreadedTCPServer_OnClientConnected;
            OnClientDisconnected += MultithreadedTCPServer_OnClientDisconnected;
            OnMessageReceived += MultithreadedTCPServer_OnMessageReceived;
            OnServerError += MultithreadedTCPServer_OnServerError;
        }

        private void MultithreadedTCPServer_OnServerError(string obj)
        {
            if (dll_obj?.ServerError != null)
            {
                var args = new MsEventArgs
                {
                    eventAction = dll_obj.ServerError,
                    parameter = GetEventParameter(dll_obj.ServerError),
                    serverError = obj
                };
                OneScriptMultithreadedTCPServer.EventQueue.Enqueue(args);
            }
        }

        private void MultithreadedTCPServer_OnMessageReceived(string arg1, BinaryDataBuffer arg2)
        {
            if (dll_obj?.MessageReceived != null)
            {
                var args = new MsEventArgs
                {
                    eventAction = dll_obj.MessageReceived,
                    parameter = GetEventParameter(dll_obj.MessageReceived),
                    clientId = arg1,
                    data = arg2
                };
                OneScriptMultithreadedTCPServer.EventQueue.Enqueue(args);
            }
        }

        private void MultithreadedTCPServer_OnClientDisconnected(string obj)
        {
            if (dll_obj?.ClientDisconnected != null)
            {
                var args = new MsEventArgs
                {
                    eventAction = dll_obj.ClientDisconnected,
                    parameter = GetEventParameter(dll_obj.ClientDisconnected),
                    clientId = obj
                };
                OneScriptMultithreadedTCPServer.EventQueue.Enqueue(args);
            }
        }

        private void MultithreadedTCPServer_OnClientConnected(string obj)
        {
            if (dll_obj?.ClientConnected != null)
            {
                var args = new MsEventArgs
                {
                    eventAction = dll_obj.ClientConnected,
                    parameter = GetEventParameter(dll_obj.ClientConnected),
                    clientId = obj
                };
                OneScriptMultithreadedTCPServer.EventQueue.Enqueue(args);
            }
        }

        public async Task StartAsync(CancellationToken externalToken = default)
        {
            lock (_stateLock)
            {
                if (_state != ServerState.Stopped && _state != ServerState.StoppedWithError)
                {
                    throw new InvalidOperationException($"Сервер уже работает. Текущее состояние: {_state}");
                }
                _state = ServerState.Starting;
            }

            _isRunning = true;
            _cts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);

            try
            {
                // Загрузка сертификата с приватным ключом (если объединены).
                certificate = new X509Certificate2(pathCert, password);
            }
            catch (Exception ex)
            {
                OnServerError?.Invoke("Ошибка сервера: " + ex.Message);
                return;
            }

            _listener = new TcpListener(IPAddress.Any, Port);
            _listener.Start(100); // backlog для ожидающих подключений.

            _state = ServerState.Running;
            Utils.GlobalContext().Echo("SSL сервер запущен на порт " + Port);
            OneScriptMultithreadedTCPServer.multiServerUploadedSSL = true;

            try
            {
                var acceptTasks = new List<Task>();
                // Запускаем несколько задач для приема подключений.
                int acceptLoops = Math.Max(1, System.Environment.ProcessorCount / 2); // Оптимальное количество.
                for (int i = 0; i < acceptLoops; i++)
                {
                    acceptTasks.Add(AcceptClientsLoopAsync(_cts.Token));
                }

                // Ожидаем завершения всех задач или отмены.
                await Task.WhenAll(acceptTasks).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                OnServerError?.Invoke("Сервер остановлен по запросу");
            }
            catch (Exception ex)
            {
                _state = ServerState.StoppedWithError;
                OnServerError?.Invoke("Ошибка сервера: " + ex.Message);
            }
            finally
            {
                _state = ServerState.Stopped;
                CleanupResources();
            }
        }

        private async Task AcceptClientsLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested && _isRunning)
            {
                try
                {
                    var client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);

                    if (token.IsCancellationRequested)
                    {
                        client.Close();
                        break;
                    }

                    // Ограничение количества одновременных подключений.
                    if (_clients.Count >= MaxClients)
                    {
                        await SendRejectionAndClose(client, "Сервер перегружен");
                        continue;
                    }

                    // Настраиваем таймауты клиента.
                    client.ReceiveTimeout = 30000;
                    client.SendTimeout = 10000;

                    // Запустим задачу взаимодействия с клиентом так, чтобы она не блокировала подключение новых клиентов.
                    _ = HandleClientAsync(client, token).ContinueWith(t =>
                    {
                        if (t.IsFaulted)
                        {
                            OnServerError?.Invoke("Ошибка обработки клиента: " + t.Exception?.InnerException?.Message);
                        }
                    }, TaskContinuationOptions.OnlyOnFaulted);
                }
                catch (ObjectDisposedException)
                {
                    // Сервер прослушивания был остановлен.
                    break;
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.Interrupted)
                {
                    // Прервано вызовом Stop().
                    break;
                }
                catch (Exception ex)
                {
                    if (!token.IsCancellationRequested)
                    {
                        OnServerError?.Invoke("Ошибка приёма клиента: " + ex.Message);
                    }
                }
            }
        }

        private async Task SendRejectionAndClose(TcpClient client, string message)
        {
            try
            {
                var data = Encoding.GetBytes($"ERROR: {message}\n");
                await client.GetStream().WriteAsync(data, 0, data.Length);
                await Task.Delay(100); // Даем время на отправку.
            }
            catch { }
            finally
            {
                client.Close();
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken token)
        {
            var clientId = Convert.ToString(Interlocked.Increment(ref _lastClientId));
            SslStream sslStream = null;

            try
            {
                // Создаем отдельный SslStream для этого клиента
                sslStream = new SslStream(client.GetStream(), false);
                await sslStream.AuthenticateAsServerAsync(certificate,
                    clientCertificateRequired: false,
                    checkCertificateRevocation: false);

                var clientInfo = new ClientInfo
                {
                    Client = client,
                    SslStream = sslStream,
                    Cts = CancellationTokenSource.CreateLinkedTokenSource(token),
                    Id = clientId,
                    LastActivity = DateTime.UtcNow
                };

                if (!_clients.TryAdd(clientId, clientInfo))
                {
                    client.Close();
                    sslStream?.Close();
                    return;
                }

                OnClientConnected?.Invoke(clientId);

                if (CheckClientActivity)
                {
                    // Запускаем пингование клиента (heartbeat) в фоне.
                    var heartbeatTask = HeartbeatLoopAsync(clientInfo, clientInfo.Cts.Token);
                    // Основной цикл чтения.
                    await ReadLoopAsync(clientInfo, clientInfo.Cts.Token).ConfigureAwait(false);
                    // Отменяем пингование клиента (heartbeat).
                    clientInfo.Cts.Cancel();
                    // Ожидаем завершения пингования клиента (heartbeat) с обработкой исключений.
                    await heartbeatTask.ContinueWith(t =>
                    {
                        if (t.IsFaulted)
                        {
                            OnServerError?.Invoke("Ошибка heartbeat для клиента " + clientId + ": " + t.Exception?.Message);
                        }
                    }, TaskContinuationOptions.OnlyOnFaulted);
                }
                else
                {
                    // Основной цикл чтения.
                    await ReadLoopAsync(clientInfo, clientInfo.Cts.Token).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                OnServerError?.Invoke("Ошибка в обработке клиента " + clientId + ": " + ex.Message);
            }
            finally
            {
                // Удаляем клиента из словаря
                if (_clients.TryRemove(clientId, out var removedClient))
                {
                    removedClient.Cts?.Dispose();
                    removedClient.SslStream?.Close();
                    removedClient.Client?.Close();
                }

                OnClientDisconnected?.Invoke(clientId);
            }
        }

        private async Task ReadLoopAsync(ClientInfo clientInfo, CancellationToken token)
        {
            var buffer = _bufferPool.Rent();

            try
            {
                while (!token.IsCancellationRequested && clientInfo.Client.Connected)
                {
                    try
                    {
                        // Читаем входящие данные.
                        BinaryDataBuffer bdb = new BinaryDataBuffer(new byte[0]);
                        int bytesRead = 0;

                        do
                        {
                            bytesRead = await clientInfo.SslStream.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);
                            bdb = bdb.Concat((new BinaryDataBuffer(buffer)).Read(0, bytesRead));
                            // Ограничение на размер сообщения.
                            if (bdb.Size > maxMessageSize)
                            {
                                await SendRejectionAndClose(clientInfo.Client, "Превышен размер сообщения");
                                bdb = new BinaryDataBuffer(new byte[0]);
                                OnServerError?.Invoke("Превышен размер сообщения для клиента " + clientInfo.Id + ". Клиент отключен.");
                                break;
                            }
                        } while (bytesRead == buffer.Length);

                        if (bdb?.Count() > 0)
                        {
                            clientInfo.LastActivity = DateTime.UtcNow;
                            // Вызываем обработчик данных.
                            OnMessageReceived?.Invoke(clientInfo.Id, bdb);
                        }
                    }
                    catch (IOException ex) when (ex.InnerException is SocketException)
                    {
                        break; // Соединение разорвано.
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        OnServerError?.Invoke("Ошибка чтения от клиента " + clientInfo.Id + ": " + ex.Message);
                        break;
                    }
                }
            }
            finally
            {
                _bufferPool.Return(buffer);
            }
        }

        private async Task HeartbeatLoopAsync(ClientInfo clientInfo, CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested && clientInfo.Client.Connected)
                {
                    await Task.Delay(HeartbeatInterval, token).ConfigureAwait(false);

                    if (token.IsCancellationRequested)
                    {
                        break;
                    }

                    // Проверяем активность клиента.
                    if ((DateTime.UtcNow - clientInfo.LastActivity) > TimeSpan.FromSeconds(Convert.ToInt32(HeartbeatInterval.TotalSeconds * 3)))
                    {
                        // Клиент неактивен слишком долго.
                        OnServerError?.Invoke("Клиент " + clientInfo.Id + " неактивен");
                        break;
                    }

                    // Отправляем PING с использованием lock.
                    await clientInfo.WriteLock.WaitAsync();
                    try
                    {
                        if (clientInfo.Client.Connected && clientInfo.SslStream != null && clientInfo.SslStream.CanWrite)
                        {
                            var pingData = Encoding.GetBytes($"PING|{DateTime.Now:o}\n");
                            await clientInfo.SslStream.WriteAsync(pingData, 0, pingData.Length).ConfigureAwait(false);
                            clientInfo.LastActivity = DateTime.UtcNow;
                        }
                    }
                    catch (Exception ex)
                    {
                        OnServerError?.Invoke("Клиент " + clientInfo.Id + " не ответил на отправленный PING: " + ex.Message);
                        break;
                    }
                    finally
                    {
                        clientInfo.WriteLock.Release();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Нормальное завершение по токену.
            }
            catch (Exception ex)
            {
                OnServerError?.Invoke("Ошибка на отправленный PING для клиента " + clientInfo.Id + ": " + ex.Message);
            }
        }

        // Отправить строку в сообщении конкретному клиенту.
        public async Task<bool> SendAsync(string clientId, string message)
        {
            if (!_clients.TryGetValue(clientId, out var clientInfo) || !clientInfo.Client.Connected)
            {
                return false;
            }

            await clientInfo.WriteLock.WaitAsync();
            try
            {
                var data = Encoding.GetBytes(message.EndsWith("\n") ? message : message + "\n");
                await clientInfo.SslStream.WriteAsync(data, 0, data.Length).ConfigureAwait(false);
                clientInfo.LastActivity = DateTime.UtcNow;
                return true;
            }
            catch (Exception ex)
            {
                OnServerError?.Invoke("Ошибка отправки клиенту " + clientId + ": " + ex.Message);
                // При ошибке отправки удаляем клиента.
                _clients.TryRemove(clientId, out _);
                return false;
            }
            finally
            {
                clientInfo.WriteLock.Release();
            }
        }

        // Отправить байты в сообщении конкретному клиенту.
        public async Task<bool> SendAsync(string clientId, BinaryDataBuffer message)
        {
            if (!_clients.TryGetValue(clientId, out var clientInfo) || !clientInfo.Client.Connected)
            {
                return false;
            }

            await clientInfo.WriteLock.WaitAsync();
            try
            {
                var data = message.Bytes;
                await clientInfo.SslStream.WriteAsync(data, 0, data.Length).ConfigureAwait(false);
                clientInfo.LastActivity = DateTime.UtcNow;
                return true;
            }
            catch (Exception ex)
            {
                OnServerError?.Invoke("Ошибка отправки клиенту " + clientId + ": " + ex.Message);
                // При ошибке отправки удаляем клиента.
                _clients.TryRemove(clientId, out _);
                return false;
            }
            finally
            {
                clientInfo.WriteLock.Release();
            }
        }

        // Отправить строку всем клиентам.
        public async Task BroadcastAsync(string message)
        {
            var data = Encoding.GetBytes(message.EndsWith("\n") ? message : message + "\n");
            var failedClients = new List<string>();
            var tasks = new List<Task>();

            // Создаем копию для безопасной итерации
            var clientsSnapshot = _clients.ToArray();

            //Utils.GlobalContext().Echo($"Начинаю рассылку для {clientsSnapshot.Length} клиентов");

            foreach (var kvp in clientsSnapshot)
            {
                // Для каждого клиента создаем задачу
                tasks.Add(SendToClientWithLock(kvp.Value, data));
            }

            // Ждем завершения всех задач
            if (tasks.Count > 0)
            {
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }

            // Удаляем отвалившихся клиентов
            foreach (var clientId in failedClients)
            {
                _clients.TryRemove(clientId, out _);
            }

            //Utils.GlobalContext().Echo($"Рассылка завершена");
        }

        private async Task SendToClientWithLock(ClientInfo clientInfo, byte[] data)
        {
            await clientInfo.WriteLock.WaitAsync();
            try
            {
                if (clientInfo.Client.Connected && clientInfo.SslStream != null && clientInfo.SslStream.CanWrite)
                {
                    await clientInfo.SslStream.WriteAsync(data, 0, data.Length).ConfigureAwait(false);
                    clientInfo.LastActivity = DateTime.UtcNow;
                }
            }
            catch (Exception ex)
            {
                OnServerError?.Invoke($"Ошибка отправки клиенту {clientInfo.Id}: {ex.Message}");
                // Не удаляем клиента здесь, это сделает основной поток
            }
            finally
            {
                clientInfo.WriteLock.Release();
            }
        }

        // Отправить байты всем клиентам.
        public async Task BroadcastAsync(BinaryDataBuffer message)
        {
            var data = message.Bytes;
            var failedClients = new List<string>();
            var tasks = new List<Task>();

            var clientsSnapshot = _clients.ToArray();

            foreach (var kvp in clientsSnapshot)
            {
                tasks.Add(SendToClientWithLock(kvp.Value, data));
            }

            if (tasks.Count > 0)
            {
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }

            foreach (var clientId in failedClients)
            {
                _clients.TryRemove(clientId, out _);
            }
        }

        public async Task StopAsync(TimeSpan? gracefulTimeout = null)
        {
            var timeout = gracefulTimeout ?? TimeSpan.FromSeconds(30);

            lock (_stateLock)
            {
                if (_state != ServerState.Running && _state != ServerState.Starting)
                {
                    return;
                }

                _state = ServerState.Stopping;
            }

            Utils.GlobalContext().Echo("Начало выключения сервера...");
            // 1. Останавливаем прием новых подключений.
            _isRunning = false;
            // 2. Останавливаем сервер прослушивания.
            try
            {
                _listener?.Stop();
            }
            catch { }
            // 3. Уведомляем все задачи о необходимости завершения.
            _cts?.Cancel();
            // 4. Мягкое (Graceful) закрытие существующих подключений.
            await GracefulDisconnectClientsAsync(timeout).ConfigureAwait(false);
            // 5. Принудительное завершение оставшихся подключений.
            ForceDisconnectAllClients();
            // 6. Очистка ресурсов.
            CleanupResources();

            _state = ServerState.Stopped;
            Utils.GlobalContext().Echo("Сервер полностью остановлен");
        }

        private async Task GracefulDisconnectClientsAsync(TimeSpan timeout)
        {
            if (_clients.IsEmpty)
            {
                return;
            }

            var gracefulTasks = new List<Task>();
            var disconnectMessage = Encoding.GetBytes("SERVER_SHUTDOWN\n");
            // Отправляем уведомление о завершении всем клиентам.
            foreach (var kvp in _clients)
            {
                var task = Task.Run(async () =>
                {
                    try
                    {
                        await kvp.Value.SslStream.WriteAsync(disconnectMessage, 0, disconnectMessage.Length);
                        // Даем время клиенту обработать сообщение.
                        await Task.Delay(1000);
                    }
                    catch { }
                });

                gracefulTasks.Add(task);
            }
            // Ожидаем завершения отправки уведомлений или таймаута.
            try
            {
                var timeoutTask = Task.Delay(timeout);
                var completedTask = await Task.WhenAny(Task.WhenAll(gracefulTasks), timeoutTask);

                if (completedTask == timeoutTask)
                {
                    Utils.GlobalContext().Echo("Время задержки выключения закончилось (" + timeout.TotalSeconds + " сек). Отключаем принудительно.");
                }
            }
            catch { }
        }

        private void ForceDisconnectAllClients()
        {
            foreach (var clientInfo in _clients.Values)
            {
                try
                {
                    clientInfo.SslStream?.Close();
                    // Отправляем TCP RST вместо мягкого (graceful) закрытия.
                    clientInfo.Client.Client.LingerState = new LingerOption(true, 0);
                    clientInfo.Client.Close();
                    clientInfo.Cts?.Dispose();
                }
                catch { }
            }

            _clients.Clear();
        }

        private void CleanupResources()
        {
            try
            {
                _listener?.Stop();
                // Очищаем токены отмены.
                _cts?.Dispose();
            }
            catch (Exception ex)
            {
                OnServerError?.Invoke("Ошибка при очистке ресурсов: " + ex.Message);
            }
        }

        private bool _disposed = false;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                try
                {
                    // Если сервер работает, останавливаем его.
                    if (_state == ServerState.Running || _state == ServerState.Starting)
                    {
                        var stopTask = StopAsync(TimeSpan.FromSeconds(5));
                        if (!stopTask.Wait(TimeSpan.FromSeconds(10)))
                        {
                            OnServerError?.Invoke("Таймаут при остановке сервера");
                        }
                    }

                    _cts?.Dispose();
                    _listener?.Stop();
                    // Очищаем клиентов.
                    foreach (var clientInfo in _clients.Values)
                    {
                        try
                        {
                            clientInfo.SslStream?.Close();
                            clientInfo.Client?.Close();
                            clientInfo.Cts?.Dispose();
                        }
                        catch { }
                    }
                    _clients.Clear();
                }
                catch (Exception ex)
                {
                    OnServerError?.Invoke("Ошибка при освобождении ресурсов: " + ex.Message);
                }
            }

            _disposed = true;
        }

        ~MultithreadedTCPServerSSL()
        {
            Dispose(false);
        }

        public int MaxMessageSize
        {
            get { return maxMessageSize; }
            set { maxMessageSize = value; }
        }

        public int MaxClients
        {
            get { return maxClients; }
            set { maxClients = value; }
        }

        private bool checkClientActivity = false;
        public bool CheckClientActivity
        {
            get { return checkClientActivity; }
            set { checkClientActivity = value; }
        }

        public static dynamic GetEventParameter(dynamic dll_objEvent)
        {
            if (dll_objEvent != null)
            {
                dynamic eventType = dll_objEvent.GetType();
                if (eventType == typeof(DelegateAction))
                {
                    return null;
                }
                else if (eventType == typeof(MsAction))
                {
                    return ((MsAction)dll_objEvent).Parameter;
                }
                else
                {
                    return null;
                }
            }
            else
            {
                return null;
            }
        }
    }

    [ContextClass("МсМногопоточныйTCPСерверSSL", "MsMultithreadedTCPServerSSL")]
    public class MsMultithreadedTCPServerSSL : AutoContext<MsMultithreadedTCPServerSSL>
    {
        public MsMultithreadedTCPServerSSL(int port, string path_cert, string pas)
        {
            MultithreadedTCPServerSSL MultithreadedTCPServerSSL1 = new MultithreadedTCPServerSSL(port, path_cert, pas);
            MultithreadedTCPServerSSL1.dll_obj = this;
            Base_obj = MultithreadedTCPServerSSL1;
        }

        public MultithreadedTCPServerSSL Base_obj;

        [ContextMethod("ОтправитьКлиенту", "SendToClient")]
        public void SendToClient(string p1, IValue p2)
        {
            if (Utils.IsType<BinaryDataBuffer>(p2))
            {
                _ = Task.Run(() =>
                {
                    Base_obj.SendAsync(p1, (BinaryDataBuffer)p2).ConfigureAwait(false);
                });
            }
            else
            {
                _ = Task.Run(() =>
                {
                    Base_obj.SendAsync(p1, p2.AsString()).ConfigureAwait(false);
                });
            }
        }

        [ContextMethod("ОтправитьВсем", "SendToAll")]
        public void SendToAll(IValue p1)
        {
            if (Utils.IsType<BinaryDataBuffer>(p1))
            {
                _ = Task.Run(() =>
                {
                    Base_obj.BroadcastAsync((BinaryDataBuffer)p1).ConfigureAwait(false);
                });
            }
            else
            {
                _ = Task.Run(() =>
                {
                    Base_obj.BroadcastAsync(p1.AsString()).ConfigureAwait(false);
                });
            }
        }

        [ContextProperty("ПроверятьАктивностьКлиента", "CheckClientActivity")]
        public bool CheckClientActivity
        {
            get { return Base_obj.CheckClientActivity; }
            set { Base_obj.CheckClientActivity = value; }
        }

        [ContextMethod("Начать", "Start")]
        public void Start()
        {
            StartMethod().ConfigureAwait(false);
        }

        public async Task StartMethod()
        {
            try
            {
                var cts = new CancellationTokenSource();

                try
                {
                    // Запуск сервера.
                    var serverTask = Base_obj.StartAsync(cts.Token);

                    // Мониторинг состояния.
                    _ = Task.Run(async () =>
                    {
                        while (!cts.Token.IsCancellationRequested)
                        {
                            await Task.Delay(5000);
                        }
                    });

                    // Ожидаем завершения сервера.
                    await serverTask;
                }
                catch (TaskCanceledException)
                {
                    Utils.GlobalContext().Echo("Сервер остановлен по запросу пользователя");
                }
                catch (Exception ex)
                {
                    Utils.GlobalContext().Echo("Критическая ошибка: " + ex.Message);
                }
            }
            catch (Exception e)
            {
                Utils.GlobalContext().Echo("Ошибка. На порт " + Base_obj.Port + " сервер уже запущен. " +
                    System.Environment.NewLine + e.Message);
                OneScriptMultithreadedTCPServer.multiServerErrorSSL = true;
            }
        }

        [ContextMethod("Остановить", "Stop")]
        public async void Stop()
        {
            await Base_obj.StopAsync();
        }

        [ContextProperty("ПриПодключенииКлиента", "ClientConnected")]
        public MsAction ClientConnected { get; set; }

        [ContextProperty("ПриОтключенииКлиента", "ClientDisconnected")]
        public MsAction ClientDisconnected { get; set; }

        [ContextProperty("СерверПолучилДанные", "MessageReceived")]
        public MsAction MessageReceived { get; set; }

        [ContextProperty("ПриОшибкеСервера", "ServerError")]
        public MsAction ServerError { get; set; }

        [ContextProperty("КоличествоАктивныхКлиентов", "ActiveClientsNumber")]
        public int ActiveClientsNumber
        {
            get { return Base_obj.ActiveClients; }
        }

        [ContextProperty("СостояниеСервера", "ServerState")]
        public int ServerState
        {
            get { return (int)Base_obj.State; }
        }

        [ContextProperty("МаксимальныйРазмерСообщения", "MaxMessageSize")]
        public int MaxMessageSize
        {
            get { return Base_obj.MaxMessageSize; }
            set { Base_obj.MaxMessageSize = value; }
        }

        [ContextProperty("МаксимальноеКоличествоПодключений", "MaxClients")]
        public int MaxClients
        {
            get { return Base_obj.MaxClients; }
            set { Base_obj.MaxClients = value; }
        }

        [ContextProperty("Кодировка", "Encoding")]
        public MsEncoding Encoding
        {
            get
            {
                Encoding Encoding1 = new Encoding();
                Encoding1.M_Encoding = Base_obj.Encoding;
                return new MsEncoding(Encoding1);
            }
            set { Base_obj.Encoding = value.Base_obj.M_Encoding; }
        }

        private int heartbeatInterval = 10;
        [ContextProperty("ИнтервалПроверки", "HeartbeatInterval")]
        public int HeartbeatInterval
        {
            get { return heartbeatInterval; }
            set
            {
                heartbeatInterval = value;
                Base_obj.HeartbeatInterval = TimeSpan.FromSeconds(heartbeatInterval);
            }
        }

        public static X509Certificate2 CreateSelfSignedCertificate(string path, string subjectName, string pas)
        {
            var rsa = RSA.Create(2048);
            var request = new CertificateRequest(
                new X500DistinguishedName($"CN={subjectName}"),
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            // Добавляем расширения
            request.CertificateExtensions.Add(
                new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature |
                                          X509KeyUsageFlags.KeyEncipherment, true));

            var certificate = request.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddDays(-1),
                DateTimeOffset.UtcNow.AddYears(1));

            // Экспорт в .pfx
            byte[] pfxData = certificate.Export(X509ContentType.Pfx, pas);
            File.WriteAllBytes(path, pfxData);

            Utils.GlobalContext().Echo($"Сертификат сохранен в: {path}");

            return certificate;
        }

        public static void ExtractCrtFromPfx(string pfxPath, string password, string crtPath)
        {
            // Загружаем PFX
            X509Certificate2 cert = new X509Certificate2(pfxPath, password);

            // Извлекаем только сертификат (публичную часть) в DER формате
            byte[] crtData = cert.Export(X509ContentType.Cert);

            // Сохраняем как .crt (бинарный DER формат)
            File.WriteAllBytes(crtPath, crtData);

            Utils.GlobalContext().Echo($"Сертификат сохранен в: {crtPath}");
        }
    }
}
