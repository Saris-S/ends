#if UNITY_EDITOR
using System;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace ALTUSHKA.EasyNodeDialogSystem.Editor
{
    internal static partial class EndsMcpHostED
    {
        #region FIELDS

        private const int SETTINGS_ROW_HEIGHT = 20;
        private const int SETTINGS_ACTION_WIDTH = 50;
        private const int SETTINGS_CLEAR_WIDTH = 90;
        private const int CLIENT_SESSION_GROUP_COUNT = 2;
        private const string CLIENT_STATUS_TOOLTIP = "Initializing - the client is completing its connection.\n\nIdle - no request or Unity operation is currently active.\n\n" +
            "Request in progress - the server is handling a request, which may be waiting for Unity.\n\nExecuting - Unity is running an ENDS operation for this session.\n\nAn open session does not guarantee that the client is still online.";
        private const string CLIENT_REQUESTS_TOOLTIP = "Requests tracked for this session, including reads, edits and service calls. This counts requests, not successful edits or changed nodes. A batch of graph operations counts as one request.";
        private static readonly Color _openSessionColor = new Color(0.4f, 0.85f, 0.5f);
        private static readonly Color _closedSessionColor = new Color(0.95f, 0.4f, 0.4f);
        private static readonly GUIContent _settingsHeader = new GUIContent();
        private static readonly GUIContent _statusLabel = new GUIContent("Status", "Current state of the local server listener, independent of whether any client has connected.");
        private static readonly GUIContent _statusValue = new GUIContent();
        private static readonly GUIContent _copyAddress = new GUIContent("Copy", "Copy the server address into your MCP client's HTTP connection settings.");
        private static readonly GUIContent _startServer = new GUIContent("Start", "Start the local ENDS MCP listener.");
        private static readonly GUIContent _stopServer = new GUIContent("Stop", "Stop accepting requests and cancel requests that have not started. This also disables automatic startup.");
        private static readonly GUIContent _restartServer = new GUIContent("Restart", "Restart the listener. Clients may need to reconnect, unfinished queued requests are cancelled.");
        private static readonly GUIContent _portLabel = new GUIContent("Preferred port", "ENDS tries this port first and can use a nearby free port. Always copy the actual address shown above.");
        private static readonly GUIContent _applyConnection = new GUIContent("Apply", "Save the preferred port and restart the listener if it is enabled.");
        private static readonly GUIContent _clientsLabel = new GUIContent("Clients", "Separate MCP sessions with request activity. Open means the session exists, it does not guarantee the client is online. Recently closed sessions remain visible.");
        private static readonly GUIContent _openSessionsHeader = new GUIContent("Open sessions", "Sessions that have not closed, including clients completing initialization. An open session does not guarantee that the client is still online.");
        private static readonly GUIContent _closedSessionsHeader = new GUIContent("Closed sessions", "Recently closed sessions. Clearing these records does not cancel accepted operations.");
        private static readonly GUIContent _clearClosedClients = new GUIContent("Clear closed", "Remove all closed session records from this list. Open sessions and accepted operations are unaffected.");
        private static readonly GUIContent _forgetClosedClient = new GUIContent("x", "Remove this closed session record from the list. Accepted operations are unaffected.");
        private static bool _detailsOpen;
        private static bool _clientsOpen;
        private static int _displayedClientRevision = -1;
        private static int _displayedClosedClients;
        private static GUIContent[] _clientNames = Array.Empty<GUIContent>();
        private static string[] _clientIds = Array.Empty<string>();
        private static bool[] _clientClosed = Array.Empty<bool>();
        private static string[] _clientDates = Array.Empty<string>();
        private static GUIContent[] _clientStates = Array.Empty<GUIContent>();
        private static GUIContent[] _clientRequests = Array.Empty<GUIContent>();

        #endregion FIELDS

        #region SETTINGS PANEL

        internal static void DrawInSettings()
        {
            _settingsHeader.text = StatusLabel();
            _settingsHeader.tooltip = "Open the ENDS MCP address, client list and connection controls.";

            if (!string.IsNullOrEmpty(_error)) _settingsHeader.tooltip = _error;

            EditorGUILayout.BeginVertical(GUILayout.ExpandHeight(false));

            if (GUILayout.Button(_settingsHeader, GUILayout.Height(SETTINGS_ROW_HEIGHT)))
            {
                _detailsOpen = !_detailsOpen;

                if (_detailsOpen) _portDraft = _port;
            }

            if (_detailsOpen)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandHeight(false));
                string endpoint = BridgeUrl(_host, _port);

                if (_running) endpoint = BridgeUrl(_boundHost, _boundPort);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("URL: " + endpoint, GUILayout.Height(SETTINGS_ROW_HEIGHT));

                if (GUILayout.Button(_copyAddress, GUILayout.Width(SETTINGS_ACTION_WIDTH), GUILayout.Height(SETTINGS_ROW_HEIGHT))) EditorGUIUtility.systemCopyBuffer = endpoint;

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                _portDraft = EditorGUILayout.IntField(_portLabel, _portDraft, GUILayout.Height(SETTINGS_ROW_HEIGHT));

                if (GUILayout.Button(_applyConnection, GUILayout.Width(SETTINGS_ACTION_WIDTH), GUILayout.Height(SETTINGS_ROW_HEIGHT)))
                {
                    if (_portDraft < 1 || _portDraft > 65535) _error = "Port must be from 1 to 65535.";
                    else
                    {
                        if (!TryLoopbackHost(_host, out string host)) host = DEFAULT_HOST;

                        _host = host;
                        _port = _portDraft;
                        EditorPrefs.SetString(PREF_HOST, _host);
                        EditorPrefs.SetInt(PREF_PORT, _port);
                        Restart();
                    }
                }

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                GUIContent startStop = _startServer;

                if (_enabled) startStop = _stopServer;
                if (GUILayout.Button(startStop, GUILayout.Height(SETTINGS_ROW_HEIGHT)))
                {
                    _enabled = !_enabled;
                    EditorPrefs.SetBool(PREF_ENABLED, _enabled);
                    Restart();
                }

                using (new EditorGUI.DisabledScope(!_enabled))
                {
                    if (GUILayout.Button(_restartServer, GUILayout.Height(SETTINGS_ROW_HEIGHT))) Restart();
                }

                EditorGUILayout.EndHorizontal();
                _statusValue.text = ListenerStatus();
                EndsSessionED.ExclusiveEditing = EditorGUILayout.Toggle(new GUIContent("Exclusive editing", "Require agents to claim dialogs before writing. Disabling this keeps revision checks but permits competing edits."), EndsSessionED.ExclusiveEditing);
                EditorGUILayout.LabelField(_statusLabel, _statusValue, GUILayout.Height(SETTINGS_ROW_HEIGHT));

                if (!string.IsNullOrEmpty(_error)) EditorGUILayout.HelpBox(_error, MessageType.Error);

                _operations.ReadActivity(out int pending, out int running);
                EditorGUILayout.LabelField("Requests", running.ToString(CultureInfo.InvariantCulture) + " running, " + pending.ToString(CultureInfo.InvariantCulture) + " queued", GUILayout.Height(SETTINGS_ROW_HEIGHT));

                int clientRevision = _clients.Revision;

                if (_displayedClientRevision != clientRevision && Event.current.type == EventType.Layout)
                {
                    EndsMcpClientsED.ClientInfo[] clients = _clients.Snapshot();
                    _clientNames = new GUIContent[clients.Length];
                    _clientIds = new string[clients.Length];
                    _clientClosed = new bool[clients.Length];
                    _clientDates = new string[clients.Length];
                    _clientStates = new GUIContent[clients.Length];
                    _clientRequests = new GUIContent[clients.Length];
                    int activeSessions = 0;

                    for (int i = 0; i < clients.Length; i++)
                    {
                        string identity = clients[i].Name;

                        if (!string.IsNullOrEmpty(clients[i].Version)) identity += " " + clients[i].Version;

                        identity += " [" + clients[i].Id.Substring(0, 6) + "]";
                        string initialized = clients[i].InitializedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                        _clientNames[i] = new GUIContent(identity, "Session: " + clients[i].Id + "\nState: " + clients[i].State + "\nInitialized: " + initialized + "\nLast request: " + clients[i].LastMethod + ", HTTP " + clients[i].LastHttpStatus + "\n" + clients[i].CloseReason);
                        _clientDates[i] = "Last activity: " + clients[i].LastActivityUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                        string activity = "Idle";

                        if (clients[i].State == "Closed") activity = "Closed";
                        else if (clients[i].RunningOperations > 0) activity = "Executing";
                        else if (clients[i].InFlightRequests > 0) activity = "Request in progress";
                        else if (clients[i].State == "Initializing") activity = "Initializing";

                        string statusTooltip = CLIENT_STATUS_TOOLTIP;

                        if (clients[i].State == "Closed") statusTooltip = "This MCP session has closed. Its request count and last activity remain visible until this record is cleared.";

                        _clientStates[i] = new GUIContent("Current status: " + activity, statusTooltip);
                        _clientRequests[i] = new GUIContent("Requests total: " + clients[i].TotalRequests.ToString(CultureInfo.InvariantCulture), CLIENT_REQUESTS_TOOLTIP);
                        _clientIds[i] = clients[i].Id;
                        _clientClosed[i] = clients[i].State == "Closed";

                        if (clients[i].State != "Closed") activeSessions++;
                    }

                    _displayedClosedClients = clients.Length - activeSessions;
                    _clientsLabel.text = "Clients (" + activeSessions.ToString(CultureInfo.InvariantCulture) + " active, " + _displayedClosedClients.ToString(CultureInfo.InvariantCulture) + " closed)";
                    _openSessionsHeader.text = "Open sessions (" + activeSessions.ToString(CultureInfo.InvariantCulture) + ")";
                    _closedSessionsHeader.text = "Closed sessions (" + _displayedClosedClients.ToString(CultureInfo.InvariantCulture) + ")";
                    _displayedClientRevision = clientRevision;
                }

                _clientsOpen = EditorGUILayout.Foldout(_clientsOpen, _clientsLabel, true);

                if (_clientsOpen)
                {
                    for (int group = 0; group < CLIENT_SESSION_GROUP_COUNT; group++)
                    {
                        bool closedGroup = group == 1;
                        GUIContent header = _openSessionsHeader;
                        Color headerColor = _openSessionColor;

                        if (closedGroup)
                        {
                            header = _closedSessionsHeader;
                            headerColor = _closedSessionColor;
                        }

                        EditorGUILayout.BeginHorizontal();
                        Color contentColor = GUI.contentColor;
                        GUI.contentColor = headerColor;
                        GUILayout.Label(header, EditorStyles.boldLabel, GUILayout.Height(SETTINGS_ROW_HEIGHT));
                        GUI.contentColor = contentColor;

                        if (closedGroup)
                        {
                            using (new EditorGUI.DisabledScope(_displayedClosedClients == 0))
                            {
                                if (GUILayout.Button(_clearClosedClients, GUILayout.Width(SETTINGS_CLEAR_WIDTH), GUILayout.Height(SETTINGS_ROW_HEIGHT))) _clients.ClearClosed();
                            }
                        }

                        EditorGUILayout.EndHorizontal();

                        for (int i = 0; i < _clientNames.Length; i++)
                        {
                            if (_clientClosed[i] != closedGroup) continue;

                            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandHeight(false));
                            EditorGUILayout.BeginHorizontal();
                            GUILayout.Label(_clientNames[i], EditorStyles.boldLabel, GUILayout.Height(SETTINGS_ROW_HEIGHT));

                            if (_clientClosed[i] && GUILayout.Button(_forgetClosedClient, GUILayout.Width(SETTINGS_ROW_HEIGHT), GUILayout.Height(SETTINGS_ROW_HEIGHT))) _clients.ForgetClosed(_clientIds[i]);

                            EditorGUILayout.EndHorizontal();
                            GUILayout.Label(_clientStates[i], EditorStyles.miniLabel, GUILayout.Height(SETTINGS_ROW_HEIGHT));
                            GUILayout.Label(_clientRequests[i], EditorStyles.miniLabel, GUILayout.Height(SETTINGS_ROW_HEIGHT));
                            GUILayout.Label(_clientDates[i], EditorStyles.miniLabel, GUILayout.Height(SETTINGS_ROW_HEIGHT));
                            EditorGUILayout.EndVertical();
                        }
                    }
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndVertical();

        }

        #endregion SETTINGS PANEL
    }
}
#endif
