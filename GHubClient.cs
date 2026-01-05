using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AudioSwitcher
{
    /// <summary>
    /// Event args for headset connection state changes
    /// </summary>
    public class HeadsetConnectionEventArgs : EventArgs
    {
        public bool IsConnected { get; }
        public string DeviceName { get; }
        public string RawMessage { get; }

        public HeadsetConnectionEventArgs(bool isConnected, string deviceName, string rawMessage)
        {
            IsConnected = isConnected;
            DeviceName = deviceName;
            RawMessage = rawMessage;
        }
    }

    /// <summary>
    /// WebSocket client for Logitech G HUB
    /// Monitors headset connection status via G HUB's local WebSocket API
    /// </summary>
    public class GHubClient : IDisposable
    {
        private const string DEFAULT_WS_URL = "ws://localhost:9010";
        private const int RECONNECT_DELAY_MS = 5000;
        private const int BUFFER_SIZE = 8192;

        private ClientWebSocket _webSocket;
        private CancellationTokenSource _cts;
        private readonly string _wsUrl;
        private readonly string _targetDeviceKeyword;
        private bool _disposed;
        private bool _isRunning;

        /// <summary>
        /// Fired when connection to G HUB is established
        /// </summary>
        public event EventHandler OnConnected;

        /// <summary>
        /// Fired when connection to G HUB is lost
        /// </summary>
        public event EventHandler OnDisconnected;

        /// <summary>
        /// Fired when headset connection state changes
        /// </summary>
        public event EventHandler<HeadsetConnectionEventArgs> OnHeadsetConnectionChanged;

        /// <summary>
        /// Fired when any message is received (for debugging)
        /// </summary>
        public event EventHandler<string> OnMessageReceived;

        /// <summary>
        /// Fired when an error occurs
        /// </summary>
        public event EventHandler<Exception> OnError;

        /// <summary>
        /// Whether currently connected to G HUB
        /// </summary>
        public bool IsConnected => _webSocket?.State == WebSocketState.Open;

        /// <summary>
        /// Create a G HUB WebSocket client
        /// </summary>
        /// <param name="targetDeviceKeyword">Keyword to match device name (e.g., "PRO X 2", "G PRO")</param>
        /// <param name="wsUrl">WebSocket URL (default: ws://localhost:9010)</param>
        public GHubClient(string targetDeviceKeyword = "PRO X 2", string wsUrl = DEFAULT_WS_URL)
        {
            _targetDeviceKeyword = targetDeviceKeyword;
            _wsUrl = wsUrl;
        }

        /// <summary>
        /// Start the connection and monitoring loop
        /// </summary>
        public async Task StartAsync()
        {
            if (_isRunning) return;
            _isRunning = true;
            _cts = new CancellationTokenSource();

            // Run connection loop in background
            _ = ConnectionLoopAsync();
            await Task.CompletedTask;
        }

        /// <summary>
        /// Stop the connection
        /// </summary>
        public void Stop()
        {
            _isRunning = false;
            _cts?.Cancel();
        }

        private async Task ConnectionLoopAsync()
        {
            while (_isRunning && !_cts.IsCancellationRequested)
            {
                try
                {
                    await ConnectAndReceiveAsync();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (WebSocketException wsEx)
                {
                    OnError?.Invoke(this, new Exception($"WebSocket error: {wsEx.WebSocketErrorCode} - {wsEx.Message}", wsEx));
                }
                catch (Exception ex)
                {
                    OnError?.Invoke(this, ex);
                }

                if (_isRunning && !_cts.IsCancellationRequested)
                {
                    OnDisconnected?.Invoke(this, EventArgs.Empty);
                    try
                    {
                        await Task.Delay(RECONNECT_DELAY_MS, _cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
        }

        private async Task ConnectAndReceiveAsync()
        {
            _webSocket = new ClientWebSocket();

            try
            {
                // CRITICAL: G HUB requires this exact Origin header (2026 verified)
                _webSocket.Options.SetRequestHeader("Origin", "http://localhost:9010");
                
                // Connect to G HUB with timeout
                using (var connectCts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, connectCts.Token))
                {
                    await _webSocket.ConnectAsync(new Uri(_wsUrl), linkedCts.Token);
                }
                
                OnConnected?.Invoke(this, EventArgs.Empty);

                // Subscribe to device events
                await SubscribeToDevicesAsync();

                // Receive messages
                await ReceiveLoopAsync();
            }
            finally
            {
                if (_webSocket != null)
                {
                    if (_webSocket.State == WebSocketState.Open)
                    {
                        try
                        {
                            await _webSocket.CloseAsync(
                                WebSocketCloseStatus.NormalClosure,
                                string.Empty,
                                CancellationToken.None);
                        }
                        catch { }
                    }
                    _webSocket.Dispose();
                    _webSocket = null;
                }
            }
        }

        private async Task SubscribeToDevicesAsync()
        {
            // Subscribe to device list and state changes
            // Note: The exact G HUB protocol is not officially documented
            // These subscription messages are based on reverse engineering
            // Try multiple possible paths and message formats
            var subscribeMessages = new[]
            {
                // Standard subscription format
                @"{""msgId"":""1"",""verb"":""SUBSCRIBE"",""path"":""/devices/list""}",
                @"{""msgId"":""2"",""verb"":""SUBSCRIBE"",""path"":""/devices/state""}",
                @"{""msgId"":""3"",""verb"":""GET"",""path"":""/devices/list""}",
                
                // Alternative paths that G HUB might use
                @"{""msgId"":""4"",""verb"":""SUBSCRIBE"",""path"":""/device_list""}",
                @"{""msgId"":""5"",""verb"":""SUBSCRIBE"",""path"":""/connected_devices""}",
                @"{""msgId"":""6"",""verb"":""GET"",""path"":""/devices""}"
            };

            foreach (var msg in subscribeMessages)
            {
                await SendAsync(msg);
                await Task.Delay(100); // Small delay between messages
            }
        }

        private async Task ReceiveLoopAsync()
        {
            var buffer = new byte[BUFFER_SIZE];
            var messageBuilder = new StringBuilder();

            while (_webSocket.State == WebSocketState.Open && !_cts.IsCancellationRequested)
            {
                try
                {
                    var result = await _webSocket.ReceiveAsync(
                        new ArraySegment<byte>(buffer),
                        _cts.Token);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        break;
                    }

                    messageBuilder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));

                    if (result.EndOfMessage)
                    {
                        string message = messageBuilder.ToString();
                        messageBuilder.Clear();

                        ProcessMessage(message);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (WebSocketException)
                {
                    break;
                }
            }
        }

        private async Task SendAsync(string message)
        {
            if (_webSocket?.State != WebSocketState.Open) return;

            var bytes = Encoding.UTF8.GetBytes(message);
            await _webSocket.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                true,
                _cts.Token);
        }

        private void ProcessMessage(string message)
        {
            // Fire raw message event for debugging
            OnMessageReceived?.Invoke(this, message);

            // Skip if no target device configured
            if (string.IsNullOrEmpty(_targetDeviceKeyword)) return;

            // Check if message contains our target device
            if (message.IndexOf(_targetDeviceKeyword, StringComparison.OrdinalIgnoreCase) < 0)
            {
                return;
            }

            // Parse connection state
            // G HUB messages may contain various formats like:
            // "connected": true/false
            // "isConnected": true/false
            // "status": "connected"/"disconnected"
            // "connectionState": "connected"

            bool? isConnected = null;

            // Try various patterns
            if (ContainsPattern(message, "\"connected\"", "true") ||
                ContainsPattern(message, "\"isConnected\"", "true") ||
                ContainsPattern(message, "\"status\"", "\"connected\"") ||
                ContainsPattern(message, "\"connectionState\"", "\"connected\""))
            {
                isConnected = true;
            }
            else if (ContainsPattern(message, "\"connected\"", "false") ||
                     ContainsPattern(message, "\"isConnected\"", "false") ||
                     ContainsPattern(message, "\"status\"", "\"disconnected\"") ||
                     ContainsPattern(message, "\"connectionState\"", "\"disconnected\""))
            {
                isConnected = false;
            }

            if (isConnected.HasValue)
            {
                OnHeadsetConnectionChanged?.Invoke(this,
                    new HeadsetConnectionEventArgs(isConnected.Value, _targetDeviceKeyword, message));
            }
        }

        private bool ContainsPattern(string message, string key, string value)
        {
            int keyIndex = message.IndexOf(key, StringComparison.OrdinalIgnoreCase);
            if (keyIndex < 0) return false;

            // Look for the value within a reasonable distance after the key
            int searchStart = keyIndex + key.Length;
            int searchEnd = Math.Min(searchStart + 50, message.Length);
            string segment = message.Substring(searchStart, searchEnd - searchStart);

            return segment.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            Stop();
            _webSocket?.Dispose();
            _cts?.Dispose();
        }
    }
}
