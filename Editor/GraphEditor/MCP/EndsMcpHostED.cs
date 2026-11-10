#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace ALTUSHKA.EasyNodeDialogSystem.Editor
{
    [InitializeOnLoad]
    internal static partial class EndsMcpHostED
    {
        #region FIELDS

        private const int DEFAULT_PORT = 3847;
        private const int MAX_BODY_BYTES = 1024 * 1024;
        private const int MAIN_WAIT_MS = 8000;
        private const int RETRY_SAME_PORT = 3;
        private const int MAX_PORT_OFFSET = 30;
        private const double RETRY_SECONDS = 1.0;
        private const string DEFAULT_HOST = "127.0.0.1";
        private const string PREF_ENABLED = "EndsMcp.Enabled";
        private const string PREF_HOST = "EndsMcp.Host";
        private const string PREF_PORT = "EndsMcp.Port";
        private const string SERVER_INSTRUCTIONS = "ENDS edits an existing Dialog Database asset. Specify its asset path with database to avoid targeting another open database. "
            + "Read ends_contract_schema for current fields and constraints, and ends_contract_read for complete data and revision. ends_read_dialog is a filtered projection. "
            + "Use exact full node ids such as LNN.5, CHN.2 or STN.1 as shown in the editor. Omit new ids to allocate them, never abbreviate or invent existing references. "
            + "When building or rearranging a graph, read ends_describe with topic layout. Review the resulting bounds and links, then refine positions for readability. "
            + "Database writes, Save and Discard require expectedRevision from the latest read. Apply an atomic ends_contract_apply batch. UI and MCP share the same rules and working session. "
            + "Acquire dialogs with ends_edit_lease before writing, shared=true also claims common catalogs and dialog ordering. Reread revision after acquiring. Leases renew on activity and expire after 120 seconds. "
            + "Edits remain pending. save=true saves the affected scopes. ends_session_save/discard accept dialogs and shared selectors, omitting both targets the entire database and requires ownership of every target. Unsaved dependencies reject saving. "
            + "On conflict reread. On rejection inspect errorDetails and validation. On save_failed inspect changed and dirty before retrying. "
            + "For mutations choose a globally unique requestKey before sending. Keys belong to this Unity domain and survive transport reconnection. "
            + "After timeout query ends_operation_status with that key, do not repeat with a new key. The same key and arguments replay the result without another write. "
            + "Queued or running means accepted but unfinished. Results expire after 10 minutes or Unity domain reload, unknown requires rereading state. ends_transport_status works while the editor queue is busy. "
            + "Reference diagnostics are read-only, preserve unchanged values when replacing lists and change subject fields to repair references. "
            + "Use ends_describe for relevant runtime semantics or limitations when uncertain, reuse by topic and contractVersion. Do not assume arithmetic consequences, nested condition groups or asset creation are supported.";

        internal const string PROTOCOL_VERSION = "2025-06-18";
        private static readonly EndsMcpOperationsED _operations = new EndsMcpOperationsED();
        private static readonly EndsMcpClientsED _clients = new EndsMcpClientsED();
        private static readonly SemaphoreSlim _httpClients = new SemaphoreSlim(EndsMcpOperationsED.MAX_PENDING + 16);
        private static EndsMcpJsonED _toolDefinitions;

        private static HttpListener _listener;
        private static Thread _thread;
        private static bool _enabled = true;
        private static bool _running;
        private static bool _giveUp;
        private static int _port = DEFAULT_PORT;
        private static int _portOffset;
        private static int _samePortFails;
        private static int _boundPort;
        private static int _portDraft = DEFAULT_PORT;
        private static double _nextRetryAt;
        private static string _host = DEFAULT_HOST;
        private static string _boundHost = "";
        private static string _error = "";
        private static string _lastRequestedProtocolVersion = "";
        private static string _paintedLabel = "";
        private static int _paintedClientRevision = -1;
        private static int _paintedPending = -1;
        private static int _paintedRunning = -1;

        internal static event Action SettingsChanged;

        #endregion FIELDS

        #region START

        static EndsMcpHostED()
        {
            _enabled = EditorPrefs.GetBool(PREF_ENABLED, true);
            _host = EditorPrefs.GetString(PREF_HOST, DEFAULT_HOST);
            _port = EditorPrefs.GetInt(PREF_PORT, DEFAULT_PORT);
            _portDraft = _port;
            AssemblyReloadEvents.beforeAssemblyReload += Shutdown;
            EditorApplication.quitting += Shutdown;
            AppDomain.CurrentDomain.DomainUnload += StopDomainListener;
            AppDomain.CurrentDomain.ProcessExit += StopDomainListener;
            EditorApplication.update += MaintainListener;
        }

        private static void Shutdown()
        {
            EditorApplication.update -= MaintainListener;
            StopListener();
        }

        private static void StopDomainListener(object sender, EventArgs arguments)
        {
            StopListener();
        }

        private static void MaintainListener()
        {
            _operations.Drain();

            if (_enabled && !_running && !_giveUp && EditorApplication.timeSinceStartup >= _nextRetryAt) TryStart();

            string label = StatusLabel();
            int clientRevision = _clients.Revision;
            _operations.ReadActivity(out int pending, out int running);

            if (label == _paintedLabel && clientRevision == _paintedClientRevision && pending == _paintedPending && running == _paintedRunning) return;

            _paintedLabel = label;
            _paintedClientRevision = clientRevision;
            _paintedPending = pending;
            _paintedRunning = running;
            RepaintEndsWindows();
            SettingsChanged?.Invoke();
        }

        private static void Restart()
        {
            _giveUp = false;
            _portOffset = 0;
            _samePortFails = 0;
            _nextRetryAt = 0;
            _error = "";
            StopListener();

            if (_enabled) TryStart();
        }

        private static void TryStart()
        {
            if (_running) return;

            if (!TryLoopbackHost(_host, out string host))
            {
                _error = "Host must be localhost or a loopback IP address.";
                _giveUp = true;
                return;
            }

            int port = CandidatePort();

            if (port < 1 || port > 65535)
            {
                _error = "No free port near " + _port.ToString(CultureInfo.InvariantCulture) + ".";
                _giveUp = true;
                return;
            }

            HttpListener listener = new HttpListener();

            try
            {
                if (_toolDefinitions == null) _toolDefinitions = EndsDialogAiReadED.ToolList();

                listener.Prefixes.Add("http://" + host + ":" + port.ToString(CultureInfo.InvariantCulture) + "/");
                listener.IgnoreWriteExceptions = true;
                listener.Start();
            }
            catch (HttpListenerException ex)
            {
                try { listener.Close(); }
                catch (Exception) { }

                RetryStart(port, ex.ErrorCode);

                return;
            }
            catch (SocketException ex)
            {
                try { listener.Close(); }
                catch (Exception) { }

                RetryStart(port, ex.ErrorCode);

                return;
            }
            catch (Exception ex)
            {
                try { listener.Close(); }
                catch (Exception) { }

                _error = "Could not start ENDS at " + BridgeUrl(_host.Trim(), port) + ". Error type: " + ex.GetType().Name + ".";
                _giveUp = true;
                return;
            }

            _listener = listener;
            _running = true;
            _error = "";
            _boundHost = host;
            _boundPort = port;
            _clients.Open();
            _operations.Open();
            _thread = new Thread(() => ListenLoop(listener));
            _thread.IsBackground = true;
            _thread.Name = "ENDS MCP";
            _thread.Start();
        }

        private static void RetryStart(int port, int errorCode)
        {
            string address = _host.Trim() + ":" + port.ToString(CultureInfo.InvariantCulture);
            _error = "Could not listen at " + address + ". Error code: " + errorCode.ToString(CultureInfo.InvariantCulture) + ".";

            if (errorCode == (int)SocketError.AddressAlreadyInUse || errorCode == 183) _error = "Address " + address + " is already in use. ENDS will try a nearby port. Servers on different ports can run together.";

            if (errorCode == 5 || errorCode == (int)SocketError.AccessDenied)
            {
                _error = "Access denied while opening " + address + ". Choose another local address or port.";
                _giveUp = true;

                return;
            }

            _samePortFails++;

            if (_samePortFails >= RETRY_SAME_PORT)
            {
                _samePortFails = 0;
                _portOffset++;
            }

            if (_portOffset > MAX_PORT_OFFSET)
            {
                _giveUp = true;
                _error = "Could not open a listener near " + _port.ToString(CultureInfo.InvariantCulture) + ". Last error code: " + errorCode.ToString(CultureInfo.InvariantCulture) + ".";

                return;
            }

            _nextRetryAt = EditorApplication.timeSinceStartup + RETRY_SECONDS;
        }

        private static int CandidatePort()
        {
            return _port + _portOffset;
        }

        private static void StopListener()
        {
            _running = false;
            _clients.CloseAll("Server stopped.");
            _operations.Close();
            HttpListener listener = _listener;
            Thread thread = _thread;
            _listener = null;
            _thread = null;
            _boundHost = "";
            _boundPort = 0;

            if (listener != null)
            {
                try { listener.Stop(); }
                catch (Exception) { }

                try { listener.Close(); }
                catch (Exception) { }
            }

            if (thread != null && thread.IsAlive && thread != Thread.CurrentThread) thread.Join(1000);
        }

        private static void ListenLoop(HttpListener listener)
        {
            while (listener.IsListening)
            {
                HttpListenerContext context;

                try
                {
                    context = listener.GetContext();
                }
                catch (Exception)
                {
                    if (!listener.IsListening) return;

                    continue;
                }

                if (!_httpClients.Wait(0))
                {
                    try { WriteResponse(context, 503, "application/json", ProtocolError(null, -32000, "HTTP capacity reached. Request was not accepted and made no changes.")); }
                    catch (Exception) { context.Response.Abort(); }

                    continue;
                }

                ThreadPool.QueueUserWorkItem(HandleClient, context);
            }
        }

        #endregion START

        #region HTTP

        private static void HandleClient(object state)
        {
            HttpListenerContext context = (HttpListenerContext)state;
            EndsMcpClientsED.SessionED session = null;
            bool requestStarted = false;
            bool createdSession = false;
            int requestStatus = 500;

            try
            {
                string method = context.Request.HttpMethod;
                string requestedProtocol = context.Request.Headers["MCP-Protocol-Version"];
                string sessionId = context.Request.Headers["Mcp-Session-Id"];

                if (!IsLocalRequest(context.Request.Url, context.Request.Headers["Origin"], _boundPort))
                {
                    WriteResponse(context, 403, "application/json", ProtocolError(null, -32000, "Only local desktop clients without Origin are allowed. Request was not accepted."));
                    return;
                }

                if (!string.IsNullOrEmpty(requestedProtocol))
                {
                    Volatile.Write(ref _lastRequestedProtocolVersion, requestedProtocol);

                    if (requestedProtocol != PROTOCOL_VERSION)
                    {
                        WriteResponse(context, 400, "application/json", ProtocolError(null, -32600, "Unsupported MCP-Protocol-Version. Supported version: " + PROTOCOL_VERSION + "."));
                        return;
                    }
                }

                if (string.Equals(method, "OPTIONS", StringComparison.OrdinalIgnoreCase))
                {
                    WriteOptions(context);
                    return;
                }

                string path = context.Request.Url.AbsolutePath;

                if (!string.Equals(path, "/mcp", StringComparison.OrdinalIgnoreCase) && !string.Equals(path, "/mcp/", StringComparison.OrdinalIgnoreCase))
                {
                    WriteResponse(context, 404, "application/json", ProtocolError(null, -32601, "Not found."));
                    return;
                }

                if (method == "DELETE" || method == "GET")
                {
                    if (string.IsNullOrEmpty(requestedProtocol))
                    {
                        WriteResponse(context, 400, "application/json", ProtocolError(null, -32600, "MCP-Protocol-Version is required after initialize."));
                        return;
                    }

                    if (!_clients.TryBeginRequest(sessionId, method, out session, out requestStatus))
                    {
                        WriteResponse(context, requestStatus, "application/json", ProtocolError(null, -32000, "Missing or expired MCP session. Initialize a new session without Mcp-Session-Id."));
                        return;
                    }

                    requestStarted = true;

                    if (method == "DELETE")
                    {
                        _clients.Close(session.Id, "Client ended the session.");
                        requestStatus = 200;
                        WriteResponse(context, requestStatus, "application/json", "");
                    }
                    else
                    {
                        requestStatus = 405;
                        context.Response.Headers["Allow"] = "POST, DELETE";
                        WriteResponse(context, requestStatus, "application/json", ProtocolError(null, -32601, "SSE listening is not offered. Use POST for MCP messages."));
                    }

                    return;
                }

                if (!string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase))
                {
                    WriteResponse(context, 405, "application/json", ProtocolError(null, -32601, "POST only."));
                    return;
                }

                string contentType = context.Request.ContentType ?? "";

                if (!string.Equals(contentType.Split(';')[0].Trim(), "application/json", StringComparison.OrdinalIgnoreCase))
                {
                    WriteResponse(context, 415, "application/json", ProtocolError(null, -32600, "Content-Type must be application/json."));
                    return;
                }

                string body;

                try { body = ReadBody(context.Request); }
                catch (DecoderFallbackException)
                {
                    WriteResponse(context, 400, "application/json", ProtocolError(null, -32700, "Request body must contain valid UTF-8."));
                    return;
                }

                if (body == null)
                {
                    WriteResponse(context, 413, "application/json", ProtocolError(null, -32600, "Request is too large."));
                    return;
                }

                EndsMcpJsonED message;

                try
                {
                    message = EndsMcpJsonED.Parse(body);
                }
                catch (FormatException ex)
                {
                    WriteResponse(context, 400, "application/json", ProtocolError(null, -32700, ex.Message));
                    return;
                }

                if (message.Type != EndsMcpJsonED.Kind.Object)
                {
                    WriteResponse(context, 400, "application/json", ProtocolError(null, -32600, "Request must be a JSON object."));
                    return;
                }

                if (!IsValidRequest(message))
                {
                    WriteResponse(context, 400, "application/json", ProtocolError(null, -32600, "Expected jsonrpc 2.0, a method string and a string or numeric id for requests."));
                    return;
                }

                if (string.IsNullOrEmpty(requestedProtocol) && message.Get("method").Text != "initialize")
                {
                    WriteResponse(context, 400, "application/json", ProtocolError(message, -32600, "MCP-Protocol-Version is required after initialize. Supported version: " + PROTOCOL_VERSION + "."));
                    return;
                }

                string rpcMethod = message.Get("method").Text;

                if (rpcMethod == "initialize")
                {
                    if (!string.IsNullOrEmpty(sessionId))
                    {
                        WriteResponse(context, 400, "application/json", ProtocolError(message, -32600, "Initialize must not include Mcp-Session-Id."));
                        return;
                    }

                    if (!_clients.TryCreate(message.Get("params")?.Get("clientInfo"), out session, out requestStatus))
                    {
                        WriteResponse(context, requestStatus, "application/json", ProtocolError(message, -32602, "Valid clientInfo is required and the server must have free session capacity."));
                        return;
                    }

                    createdSession = true;
                    sessionId = session.Id;
                    context.Response.Headers["Mcp-Session-Id"] = sessionId;
                }

                if (!_clients.TryBeginRequest(sessionId, rpcMethod, out session, out requestStatus))
                {
                    WriteResponse(context, requestStatus, "application/json", ProtocolError(message, -32000, "Missing or expired MCP session. Initialize a new session without Mcp-Session-Id."));
                    return;
                }

                requestStarted = true;

                if (!_clients.AcceptMethod(session, rpcMethod))
                {
                    requestStatus = 400;
                    WriteResponse(context, requestStatus, "application/json", ProtocolError(message, -32600, "Complete initialization with notifications/initialized before using this session."));
                    return;
                }

                string reply = HandleMessage(message, session);

                if (reply == null)
                {
                    requestStatus = 202;
                    WriteResponse(context, requestStatus, "application/json", "");
                    return;
                }

                string accept = context.Request.Headers["Accept"] ?? "";
                bool jsonOk = accept.IndexOf("application/json", StringComparison.OrdinalIgnoreCase) >= 0;
                bool sseOk = accept.IndexOf("text/event-stream", StringComparison.OrdinalIgnoreCase) >= 0;
                requestStatus = 200;

                if (!jsonOk && sseOk)
                {
                    WriteResponse(context, 200, "text/event-stream", "event: message\ndata: " + reply + "\n\n");
                    return;
                }

                WriteResponse(context, 200, "application/json", reply);
            }
            catch (Exception)
            {
                requestStatus = 500;

                if (createdSession && session != null) _clients.Close(session.Id, "Initialization response could not be delivered.");

                try { context.Response.Abort(); }
                catch (Exception) { }
            }
            finally
            {
                if (requestStarted) _clients.FinishRequest(session, requestStatus);

                _httpClients.Release();
            }
        }

        private static string ReadBody(HttpListenerRequest request)
        {
            if (request.ContentLength64 > MAX_BODY_BYTES) return null;

            using (MemoryStream ms = new MemoryStream())
            {
                byte[] buffer = new byte[8192];

                while (true)
                {
                    int read = request.InputStream.Read(buffer, 0, buffer.Length);

                    if (read <= 0) break;
                    if (ms.Length + read > MAX_BODY_BYTES) return null;

                    ms.Write(buffer, 0, read);
                }

                return new UTF8Encoding(false, true).GetString(ms.ToArray());
            }
        }

        private static void WriteOptions(HttpListenerContext context)
        {
            HttpListenerResponse response = context.Response;
            response.StatusCode = 204;
            response.ContentLength64 = 0;
            response.OutputStream.Close();
        }

        private static void WriteResponse(HttpListenerContext context, int status, string contentType, string body)
        {
            HttpListenerResponse response = context.Response;
            response.StatusCode = status;
            response.Headers["MCP-Protocol-Version"] = PROTOCOL_VERSION;

            if (body == null) body = "";

            byte[] bytes = Encoding.UTF8.GetBytes(body);
            response.ContentType = contentType;
            response.ContentLength64 = bytes.Length;
            response.OutputStream.Write(bytes, 0, bytes.Length);
            response.OutputStream.Close();
        }

        internal static bool TryLoopbackHost(string value, out string host)
        {
            host = "";

            if (string.IsNullOrWhiteSpace(value)) return false;

            string trimmed = value.Trim();

            if (string.Equals(trimmed, "localhost", StringComparison.OrdinalIgnoreCase))
            {
                host = DEFAULT_HOST;
                return true;
            }

            if (!IPAddress.TryParse(trimmed.Trim('[', ']'), out IPAddress address) || !IPAddress.IsLoopback(address)) return false;

            host = address.ToString();

            if (address.AddressFamily == AddressFamily.InterNetworkV6) host = "[" + host + "]";

            return true;
        }

        internal static bool IsLocalRequest(Uri url, string origin, int port)
        {
            if (origin != null || url == null || url.Port != port || url.Scheme != "http") return false;

            return TryLoopbackHost(url.Host, out _);
        }

        private static bool IsValidRequest(EndsMcpJsonED message)
        {
            EndsMcpJsonED version = message.Get("jsonrpc");
            EndsMcpJsonED method = message.Get("method");
            EndsMcpJsonED id = message.Get("id");
            EndsMcpJsonED parameters = message.Get("params");

            if (version?.Type != EndsMcpJsonED.Kind.String || version.Text != "2.0" || method?.Type != EndsMcpJsonED.Kind.String || string.IsNullOrEmpty(method.Text)) return false;
            if (id != null && id.Type != EndsMcpJsonED.Kind.String && id.Type != EndsMcpJsonED.Kind.Number) return false;
            if (id?.Type == EndsMcpJsonED.Kind.Number && (!decimal.TryParse(id.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal number) || decimal.Truncate(number) != number)) return false;
            if (method.Text == "initialize" && id == null) return false;
            if (parameters != null && parameters.Type != EndsMcpJsonED.Kind.Object) return false;

            return true;
        }

        private static string HandleMessage(EndsMcpJsonED message, EndsMcpClientsED.SessionED session)
        {
            string method = message.Get("method").Text;

            if (method != "tools/call") return Dispatch(message);
            if (IsNotification(message)) return null;

            EndsMcpJsonED parameters = message.Get("params");
            EndsMcpJsonED name = parameters?.Get("name");
            EndsMcpJsonED arguments = parameters?.Get("arguments");

            if (name?.Type != EndsMcpJsonED.Kind.String || (arguments != null && arguments.Type != EndsMcpJsonED.Kind.Object)) return ProtocolError(message, -32602, "Expected a tool name and an arguments object.");

            if (arguments == null)
            {
                arguments = EndsMcpJsonED.Obj();
                parameters.Set("arguments", arguments);
            }

            if (name.Text == "ends_transport_status" || name.Text == "ends_operation_status") return TransportTool(message, name.Text, arguments, session);

            string key = Guid.NewGuid().ToString("N");
            EndsMcpJsonED requestKey = arguments.Get("requestKey");

            if (requestKey != null)
            {
                if (!IsMutation(name.Text, arguments) || requestKey.Type != EndsMcpJsonED.Kind.String || string.IsNullOrWhiteSpace(requestKey.Text) || requestKey.Text.Length > 128)
                    return AdmissionError(message, key, "invalid_request_key", "requestKey requires a mutation and a nonempty string of at most 128 characters.");

                key = requestKey.Text;
                arguments.Remove("requestKey");
            }

            return QueueMessage(message, key, IsMutation(name.Text, arguments), session);
        }

        private static string QueueMessage(EndsMcpJsonED message, string key, bool mutation, EndsMcpClientsED.SessionED session)
        {
            string fingerprint = EndsMcpOperationsED.Fingerprint(message);

            if (!_operations.TrySubmit(key, fingerprint, () =>
            {
                _clients.StartOperation(session);

                try
                {
                    using (EndsSessionED.UseActor(session.Id, session.Name)) return Dispatch(message);
                }
                finally { _clients.FinishOperation(session); }
            }, out EndsMcpOperationsED.Operation operation, out string error)) return AdmissionError(message, key, error, "Request was not accepted. No change was applied by this attempt.");

            EndsMcpJsonED status = _operations.Wait(operation, MAIN_WAIT_MS, out string reply);

            if (reply != null)
            {
                EndsMcpJsonED response = EndsMcpJsonED.Parse(reply);
                response.Set("id", message.Get("id"));

                if (!mutation) _operations.Forget(key);

                return response.Write();
            }

            return ToolResult(message, status.Write(), status.Get("status").Text == "cancelled" || status.Get("error") != null);
        }

        private static bool IsMutation(string name, EndsMcpJsonED arguments)
        {
            return name.StartsWith("ends_write_", StringComparison.Ordinal) || name == "ends_contract_apply" || name == "ends_session_save" || name == "ends_session_discard" || name == "ends_edit_lease" || arguments.Get("save")?.Bool == true;
        }

        private static string AdmissionError(EndsMcpJsonED request, string key, string code, string message)
        {
            EndsMcpJsonED result = EndsMcpJsonED.Obj();
            result.Set("status", EndsMcpJsonED.Str("rejected"));
            result.Set("code", EndsMcpJsonED.Str(code));
            result.Set("requestKey", EndsMcpJsonED.Str(key));
            result.Set("accepted", EndsMcpJsonED.Boolean(false));
            result.Set("changed", EndsMcpJsonED.Boolean(false));
            result.Set("error", EndsMcpJsonED.Str(message));
            return ToolResult(request, result.Write(), true);
        }

        private static string TransportTool(EndsMcpJsonED message, string name, EndsMcpJsonED arguments, EndsMcpClientsED.SessionED session)
        {
            foreach (string key in arguments.Keys)
            {
                if (name != "ends_operation_status" || key != "requestKey") return AdmissionError(message, "", "invalid_arguments", "Unexpected transport argument " + key + ".");
            }

            if (name == "ends_operation_status")
            {
                EndsMcpJsonED key = arguments.Get("requestKey");

                if (key?.Type != EndsMcpJsonED.Kind.String || string.IsNullOrWhiteSpace(key.Text) || key.Text.Length > 128) return AdmissionError(message, "", "invalid_request_key", "requestKey must be a nonempty string of at most 128 characters.");

                return ToolResult(message, _operations.Read(key.Text).Write(), false);
            }

            EndsMcpJsonED transport = TransportStatus();
            transport.Set("currentSessionId", EndsMcpJsonED.Str(session.Id));
            int otherOpen = 0;

            foreach (EndsMcpClientsED.ClientInfo client in _clients.Snapshot())
            {
                if (client.State != "Closed" && client.Id != session.Id) otherOpen++;
            }

            transport.Set("otherOpenSessions", EndsMcpJsonED.Num(otherOpen));

            return ToolResult(message, transport.Write(), false);
        }

        private static EndsMcpJsonED TransportStatus()
        {
            EndsMcpJsonED status = _operations.Status();
            status.Set("protocolVersion", EndsMcpJsonED.Str(PROTOCOL_VERSION));
            status.Set("lastRequestedProtocolVersion", EndsMcpJsonED.Str(Volatile.Read(ref _lastRequestedProtocolVersion)));
            status.Set("sessionMode", EndsMcpJsonED.Str("stateful"));
            status.Set("endpoint", EndsMcpJsonED.Str(BridgeUrl(_boundHost, _boundPort)));
            status.Set("maxPending", EndsMcpJsonED.Num(EndsMcpOperationsED.MAX_PENDING));
            status.Set("updateBudgetMilliseconds", EndsMcpJsonED.Num(EndsMcpOperationsED.UPDATE_BUDGET_MS));
            status.Set("retentionSeconds", EndsMcpJsonED.Num(600));
            status.Set("clientTracking", EndsMcpJsonED.Str("mcp_sessions"));
            status.Set("maxClientSessions", EndsMcpJsonED.Num(EndsMcpClientsED.MAX_SESSIONS));
            status.Set("clientSessions", _clients.Describe());

            return status;
        }

        internal static void AddTransportTools(EndsMcpJsonED tools)
        {
            foreach (EndsMcpJsonED tool in tools.Items)
            {
                EndsMcpJsonED schema = tool.Get("inputSchema");
                EndsMcpJsonED properties = schema.Get("properties");

                if (!IsMutation(tool.Get("name").Text, EndsMcpJsonED.Obj()) && properties.Get("save") == null) continue;

                properties.Set("requestKey", TransportString("Client-chosen unique mutation key, at most 128 characters. Choose it before sending, reuse it only with identical arguments. Results are retained for 10 minutes in the current Unity domain."));

                if (properties.Get("expectedRevision") == null || !IsMutation(tool.Get("name").Text, EndsMcpJsonED.Obj())) continue;

                EndsMcpJsonED required = schema.Get("required");

                if (required == null)
                {
                    required = EndsMcpJsonED.Arr();
                    schema.Set("required", required);
                }

                if (!required.Items.Exists(field => field.Text == "expectedRevision")) required.Add(EndsMcpJsonED.Str("expectedRevision"));

                properties.Get("expectedRevision").Set("description", EndsMcpJsonED.Str("Required working database revision from the latest read. A mismatch returns conflict without changes. Reread and reconsider the request before retrying."));
            }

            EndsMcpJsonED operationProperties = EndsMcpJsonED.Obj();
            operationProperties.Set("requestKey", TransportString("Key supplied to a mutation or returned in a pending response."));
            tools.Add(TransportDefinition("ends_operation_status", "Reads a retained operation outcome without waiting for the Unity queue. Unknown after expiry or domain reload does not mean the write failed, reread state before retrying.", operationProperties, true));
            tools.Add(TransportDefinition("ends_transport_status", "Reads the supported protocol, client sessions, request activity and queue limits without waiting for the Unity queue.", EndsMcpJsonED.Obj(), false));
        }

        private static EndsMcpJsonED TransportString(string description)
        {
            EndsMcpJsonED result = EndsMcpJsonED.Obj();
            result.Set("type", EndsMcpJsonED.Str("string"));
            result.Set("minLength", EndsMcpJsonED.Num(1));
            result.Set("maxLength", EndsMcpJsonED.Num(128));
            result.Set("description", EndsMcpJsonED.Str(description));
            return result;
        }

        private static EndsMcpJsonED TransportDefinition(string name, string description, EndsMcpJsonED properties, bool keyRequired)
        {
            EndsMcpJsonED schema = EndsMcpJsonED.Obj();
            schema.Set("type", EndsMcpJsonED.Str("object"));
            schema.Set("properties", properties);
            schema.Set("additionalProperties", EndsMcpJsonED.Boolean(false));

            if (keyRequired)
            {
                EndsMcpJsonED required = EndsMcpJsonED.Arr();
                required.Add(EndsMcpJsonED.Str("requestKey"));
                schema.Set("required", required);
            }

            EndsMcpJsonED tool = EndsMcpJsonED.Obj();
            tool.Set("name", EndsMcpJsonED.Str(name));
            tool.Set("description", EndsMcpJsonED.Str(description));
            tool.Set("inputSchema", schema);
            return tool;
        }

        #endregion HTTP

        #region PROTOCOL

        private static string Dispatch(EndsMcpJsonED message)
        {
            EndsMcpJsonED methodNode = message.Get("method");

            if (methodNode == null || methodNode.Type != EndsMcpJsonED.Kind.String || string.IsNullOrEmpty(methodNode.Text)) return ProtocolError(message, -32600, "Method is missing.");

            string method = methodNode.Text;
            bool notification = IsNotification(message);

            if (method == "initialize")
            {
                return Envelope(message, InitializeResult(message));
            }

            if (notification || method.StartsWith("notifications/", StringComparison.Ordinal)) return null;

            if (method == "ping" || method == "logging/setLevel") return Envelope(message, EndsMcpJsonED.Obj());

            if (method == "tools/list")
            {
                EndsMcpJsonED result = EndsMcpJsonED.Obj();
                result.Set("tools", _toolDefinitions);
                return Envelope(message, result);
            }

            if (method == "tools/call") return CallTool(message);

            if (method == "resources/list" || method == "resources/templates/list")
            {
                EndsMcpJsonED result = EndsMcpJsonED.Obj();
                if (method == "resources/list") result.Set("resources", EndsMcpJsonED.Arr());
                else result.Set("resourceTemplates", EndsMcpJsonED.Arr());
                return Envelope(message, result);
            }

            if (method == "prompts/list")
            {
                EndsMcpJsonED result = EndsMcpJsonED.Obj();
                result.Set("prompts", EndsMcpJsonED.Arr());
                return Envelope(message, result);
            }

            return ProtocolError(message, -32601, "Method not found.");
        }

        private static string CallTool(EndsMcpJsonED message)
        {
            EndsMcpJsonED parameters = message.Get("params");

            if (parameters == null || parameters.Type != EndsMcpJsonED.Kind.Object) return ProtocolError(message, -32602, "params must be an object.");

            EndsMcpJsonED nameNode = parameters.Get("name");

            if (nameNode == null || nameNode.Type != EndsMcpJsonED.Kind.String || string.IsNullOrEmpty(nameNode.Text)) return ProtocolError(message, -32602, "Tool name is missing.");

            EndsMcpJsonED arguments = parameters.Get("arguments");

            if (arguments == null) arguments = EndsMcpJsonED.Obj();

            if (arguments.Type != EndsMcpJsonED.Kind.Object) return ProtocolError(message, -32602, "arguments must be an object.");

            string name = nameNode.Text;
            bool ok = false;
            string payload = null;
            string error = null;

            try
            {
                ValidateToolArguments(name, arguments);
            }
            catch (Exception exception)
            {
                EndsMcpJsonED rejected = EndsMcpJsonED.Obj();
                rejected.Set("status", EndsMcpJsonED.Str("rejected"));
                rejected.Set("changed", EndsMcpJsonED.Boolean(false));
                rejected.Set("error", EndsMcpJsonED.Str(exception.Message));

                return ToolResult(message, rejected.Write(), true);
            }

            if (name == "ends_describe") ok = EndsMcpDocumentationED.TryDescribe(arguments, out payload, out error);
            else if (name == "ends_read_catalog") ok = EndsDialogAiReadED.TryCatalog(arguments, out payload, out error);
            else if (name == "ends_read_dialog") ok = EndsDialogAiReadED.TryRead(arguments, out payload, out error);
            else if (name == "ends_write_text") ok = EndsDialogAiReadED.TryWriteText(arguments, out payload, out error);
            else if (EndsDialogAiWriteED.TryDispatch(name, arguments, out payload, out error) || error != null) ok = error == null;
            else return ToolResult(message, "Unknown tool " + name + ".", true);

            if (!ok && !string.IsNullOrEmpty(payload)) return ToolResult(message, payload, true);
            if (!ok) return ToolResult(message, error, true);

            return ToolResult(message, payload, false);
        }

        private static void ValidateToolArguments(string name, EndsMcpJsonED arguments)
        {
            if (_toolDefinitions == null) _toolDefinitions = EndsDialogAiReadED.ToolList();

            foreach (EndsMcpJsonED tool in _toolDefinitions.Items)
            {
                if (tool.Get("name").Text != name) continue;

                EndsMcpJsonED schema = tool.Get("inputSchema");
                EndsMcpJsonED properties = schema.Get("properties");

                foreach (string key in arguments.Keys)
                {
                    if (properties?.Get(key) == null) throw new InvalidOperationException(name + " does not accept argument " + key + ". Read its input schema.");

                    EndsMcpJsonED type = properties.Get(key).Get("type");
                    EndsMcpJsonED value = arguments.Get(key);

                    if (type == null) continue;

                    switch (type.Text)
                    {
                        case "string": EndsDialogGraphContractED.String(value); break;
                        case "boolean": EndsDialogGraphContractED.Boolean(value); break;
                        case "integer": EndsDialogGraphContractED.Integer(value); break;
                        case "array":
                            if (value.Type != EndsMcpJsonED.Kind.Array) throw new InvalidOperationException(key + " must be an array.");

                            break;
                        case "object":
                            if (value.Type != EndsMcpJsonED.Kind.Object) throw new InvalidOperationException(key + " must be an object.");

                            break;
                    }
                }

                if (schema.Get("required")?.Items != null)
                {
                    foreach (EndsMcpJsonED required in schema.Get("required").Items)
                    {
                        if (required.Text == "expectedRevision" && arguments.Get(required.Text) == null) throw new InvalidOperationException(name + " requires expectedRevision from the latest database read. Read the current state before writing.");
                        if (arguments.Get(required.Text) == null) throw new InvalidOperationException(name + " requires argument " + required.Text + ".");
                    }
                }

                return;
            }
        }

        private static EndsMcpJsonED InitializeResult(EndsMcpJsonED message)
        {
            EndsMcpJsonED parameters = message.Get("params");

            if (parameters != null)
            {
                EndsMcpJsonED clientVersion = parameters.Get("protocolVersion");

                if (clientVersion != null && clientVersion.Type == EndsMcpJsonED.Kind.String && !string.IsNullOrEmpty(clientVersion.Text)) Volatile.Write(ref _lastRequestedProtocolVersion, clientVersion.Text);

            }

            EndsMcpJsonED tools = EndsMcpJsonED.Obj();
            EndsMcpJsonED capabilities = EndsMcpJsonED.Obj();
            capabilities.Set("tools", tools);
            EndsMcpJsonED info = EndsMcpJsonED.Obj();
            info.Set("name", EndsMcpJsonED.Str("ends"));
            info.Set("version", EndsMcpJsonED.Str("1.0.0"));
            EndsMcpJsonED result = EndsMcpJsonED.Obj();
            result.Set("protocolVersion", EndsMcpJsonED.Str(PROTOCOL_VERSION));
            result.Set("capabilities", capabilities);
            result.Set("serverInfo", info);
            result.Set("instructions", EndsMcpJsonED.Str(SERVER_INSTRUCTIONS));
            return result;
        }

        private static bool IsNotification(EndsMcpJsonED message)
        {
            EndsMcpJsonED id = message.Get("id");

            if (id == null) return true;
            if (id.Type == EndsMcpJsonED.Kind.Null) return true;

            return false;
        }

        private static string ToolResult(EndsMcpJsonED request, string text, bool isError)
        {
            EndsMcpJsonED block = EndsMcpJsonED.Obj();
            block.Set("type", EndsMcpJsonED.Str("text"));

            if (text == null) block.Set("text", EndsMcpJsonED.Str(""));
            else block.Set("text", EndsMcpJsonED.Str(text));

            EndsMcpJsonED content = EndsMcpJsonED.Arr();
            content.Add(block);
            EndsMcpJsonED result = EndsMcpJsonED.Obj();
            result.Set("content", content);
            result.Set("isError", EndsMcpJsonED.Boolean(isError));

            return Envelope(request, result);
        }

        private static string Envelope(EndsMcpJsonED request, EndsMcpJsonED result)
        {
            EndsMcpJsonED reply = EndsMcpJsonED.Obj();
            reply.Set("jsonrpc", EndsMcpJsonED.Str("2.0"));
            reply.Set("id", EndsMcpJsonED.Null());

            if (request != null)
            {
                EndsMcpJsonED id = request.Get("id");

                if (id != null) reply.Set("id", id);
            }

            reply.Set("result", result);

            return reply.Write();
        }

        private static string ProtocolError(EndsMcpJsonED request, int code, string message)
        {
            EndsMcpJsonED error = EndsMcpJsonED.Obj();
            error.Set("code", EndsMcpJsonED.Num(code));

            if (message == null) error.Set("message", EndsMcpJsonED.Str(""));
            else error.Set("message", EndsMcpJsonED.Str(message));

            EndsMcpJsonED reply = EndsMcpJsonED.Obj();
            reply.Set("jsonrpc", EndsMcpJsonED.Str("2.0"));
            reply.Set("id", EndsMcpJsonED.Null());

            if (request != null)
            {
                EndsMcpJsonED id = request.Get("id");

                if (id != null) reply.Set("id", id);
            }

            reply.Set("error", error);

            return reply.Write();
        }

        #endregion PROTOCOL

        #region CONNECTION ADDRESS

        private static string BridgeUrl(string host, int port)
        {
            return "http://" + host + ":" + port.ToString(CultureInfo.InvariantCulture) + "/mcp";
        }

        #endregion CONNECTION ADDRESS

        #region STATUS

        private static string StatusLabel()
        {
            return "ENDS MCP - " + ListenerStatus();
        }

        private static string ListenerStatus()
        {
            if (!_enabled) return "Stopped";
            if (_running) return "Running";
            if (_giveUp) return "Error";
            if (!string.IsNullOrEmpty(_error)) return "Retrying";

            return "Starting";
        }

        private static void RepaintEndsWindows()
        {
            DialogEditorWindowED[] windows = Resources.FindObjectsOfTypeAll<DialogEditorWindowED>();

            for (int i = 0; i < windows.Length; i++)
            {
                if (windows[i] == null) continue;

                windows[i].Repaint();
            }
        }

        #endregion STATUS
    }
}
#endif