#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ALTUSHKA.EasyNodeDialogSystem.Editor
{
    internal sealed class EndsMcpClientsED
    {
        #region FIELDS

        internal const int MAX_SESSIONS = 128;
        internal const int MAX_CLOSED_SESSIONS = 32;
        private const int MAX_LABEL_LENGTH = 128;
        private readonly object _sync = new object();
        private readonly Dictionary<string, SessionED> _sessions = new Dictionary<string, SessionED>(StringComparer.Ordinal);
        private readonly List<SessionED> _closed = new List<SessionED>();
        private bool _accepting;
        private int _revision;

        internal int Revision
        {
            get { lock (_sync) return _revision; }
        }

        internal sealed class SessionED
        {
            internal readonly string Id = Guid.NewGuid().ToString("N");
            internal readonly string Name;
            internal readonly string Version;
            internal readonly DateTime InitializedUtc = DateTime.UtcNow;
            internal DateTime LastActivityUtc = DateTime.UtcNow;
            internal string State = "Initializing";
            internal string LastMethod = "initialize";
            internal string CloseReason = "";
            internal int InFlightRequests;
            internal int RunningOperations;
            internal int LastHttpStatus;
            internal int TotalRequests;

            internal SessionED(string name, string version)
            {
                Name = name;
                Version = version;
            }
        }

        internal readonly struct ClientInfo
        {
            internal readonly string Id;
            internal readonly string Name;
            internal readonly string Version;
            internal readonly DateTime InitializedUtc;
            internal readonly DateTime LastActivityUtc;
            internal readonly string State;
            internal readonly string LastMethod;
            internal readonly string CloseReason;
            internal readonly int InFlightRequests;
            internal readonly int RunningOperations;
            internal readonly int LastHttpStatus;
            internal readonly int TotalRequests;

            internal ClientInfo(SessionED session)
            {
                Id = session.Id;
                Name = session.Name;
                Version = session.Version;
                InitializedUtc = session.InitializedUtc;
                LastActivityUtc = session.LastActivityUtc;
                State = session.State;
                LastMethod = session.LastMethod;
                CloseReason = session.CloseReason;
                InFlightRequests = session.InFlightRequests;
                RunningOperations = session.RunningOperations;
                LastHttpStatus = session.LastHttpStatus;
                TotalRequests = session.TotalRequests;
            }
        }

        #endregion FIELDS

        #region SESSIONS

        internal void Open()
        {
            lock (_sync) _accepting = true;
        }

        internal bool TryCreate(EndsMcpJsonED clientInfo, out SessionED session, out int status)
        {
            session = null;
            status = 400;

            if (clientInfo?.Type != EndsMcpJsonED.Kind.Object) return false;

            EndsMcpJsonED name = clientInfo.Get("name");
            EndsMcpJsonED version = clientInfo.Get("version");

            if (name?.Type != EndsMcpJsonED.Kind.String || string.IsNullOrWhiteSpace(name.Text)) return false;
            if (version != null && version.Type != EndsMcpJsonED.Kind.String) return false;

            string clientName = NormalizeLabel(name.Text);
            string clientVersion = NormalizeLabel(version?.Text);

            if (clientName.Length == 0) return false;

            lock (_sync)
            {
                status = 503;

                if (!_accepting || _sessions.Count >= MAX_SESSIONS) return false;

                session = new SessionED(clientName, clientVersion);
                _sessions.Add(session.Id, session);
                _revision++;
                status = 200;

                return true;
            }
        }

        internal bool TryBeginRequest(string id, string method, out SessionED session, out int status)
        {
            session = null;
            status = 400;

            if (string.IsNullOrEmpty(id)) return false;

            lock (_sync)
            {
                status = 404;

                if (!_sessions.TryGetValue(id, out session)) return false;

                session.InFlightRequests++;
                session.TotalRequests++;
                session.LastActivityUtc = DateTime.UtcNow;
                session.LastMethod = method;
                _revision++;
                status = 200;

                return true;
            }
        }

        internal bool AcceptMethod(SessionED session, string method)
        {
            lock (_sync)
            {
                if (session.State == "Closed") return false;
                if (method == "notifications/initialized")
                {
                    session.State = "Open";
                    _revision++;

                    return true;
                }

                return session.State == "Open" || method == "initialize" || method == "ping";
            }
        }

        internal void FinishRequest(SessionED session, int status)
        {
            lock (_sync)
            {
                session.InFlightRequests--;
                session.LastHttpStatus = status;
                _revision++;
            }
        }

        internal void StartOperation(SessionED session)
        {
            lock (_sync)
            {
                session.RunningOperations++;
                _revision++;
            }
        }

        internal void FinishOperation(SessionED session)
        {
            lock (_sync)
            {
                session.RunningOperations--;
                _revision++;
            }
        }

        internal bool Close(string id, string reason)
        {
            lock (_sync)
            {
                if (!_sessions.TryGetValue(id, out SessionED session)) return false;

                _sessions.Remove(id);
                session.State = "Closed";
                session.CloseReason = reason;
                EndsSessionED.NotifyClientClosed(session.Id);
                _closed.Insert(0, session);

                if (_closed.Count > MAX_CLOSED_SESSIONS) _closed.RemoveAt(_closed.Count - 1);

                _revision++;

                return true;
            }
        }

        internal bool ForgetClosed(string id)
        {
            lock (_sync)
            {
                for (int i = 0; i < _closed.Count; i++)
                {
                    if (_closed[i].Id != id) continue;

                    _closed.RemoveAt(i);
                    _revision++;

                    return true;
                }

                return false;
            }
        }

        internal void ClearClosed()
        {
            lock (_sync)
            {
                if (_closed.Count == 0) return;

                _closed.Clear();
                _revision++;
            }
        }

        internal void CloseAll(string reason)
        {
            lock (_sync)
            {
                _accepting = false;

                foreach (string id in new List<string>(_sessions.Keys))
                {
                    Close(id, reason);
                }
            }
        }

        private static string NormalizeLabel(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";

            StringBuilder label = new StringBuilder(Math.Min(value.Length, MAX_LABEL_LENGTH));

            for (int i = 0; i < value.Length && label.Length < MAX_LABEL_LENGTH; i++)
            {
                if (char.IsControl(value[i])) label.Append(' ');
                else label.Append(value[i]);
            }

            return label.ToString().Trim();
        }

        internal ClientInfo[] Snapshot()
        {
            lock (_sync)
            {
                List<ClientInfo> result = new List<ClientInfo>(_sessions.Count + _closed.Count);

                foreach (SessionED session in _sessions.Values)
                {
                    result.Add(new ClientInfo(session));
                }

                foreach (SessionED session in _closed)
                {
                    result.Add(new ClientInfo(session));
                }

                return result.ToArray();
            }
        }

        internal EndsMcpJsonED Describe()
        {
            EndsMcpJsonED result = EndsMcpJsonED.Arr();

            foreach (ClientInfo client in Snapshot())
            {
                EndsMcpJsonED entry = EndsMcpJsonED.Obj();
                entry.Set("sessionId", EndsMcpJsonED.Str(client.Id));
                entry.Set("name", EndsMcpJsonED.Str(client.Name));
                entry.Set("version", EndsMcpJsonED.Str(client.Version));
                entry.Set("initializedUtc", EndsMcpJsonED.Str(client.InitializedUtc.ToString("O", CultureInfo.InvariantCulture)));
                entry.Set("lastActivityUtc", EndsMcpJsonED.Str(client.LastActivityUtc.ToString("O", CultureInfo.InvariantCulture)));
                entry.Set("state", EndsMcpJsonED.Str(client.State));
                entry.Set("lastMethod", EndsMcpJsonED.Str(client.LastMethod));
                entry.Set("lastHttpStatus", EndsMcpJsonED.Num(client.LastHttpStatus));
                entry.Set("inFlightRequests", EndsMcpJsonED.Num(client.InFlightRequests));
                entry.Set("runningOperations", EndsMcpJsonED.Num(client.RunningOperations));
                entry.Set("totalRequests", EndsMcpJsonED.Num(client.TotalRequests));
                entry.Set("closeReason", EndsMcpJsonED.Str(client.CloseReason));
                result.Add(entry);
            }

            return result;
        }

        #endregion SESSIONS
    }
}
#endif
