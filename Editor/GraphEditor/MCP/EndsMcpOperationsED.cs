#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace ALTUSHKA.EasyNodeDialogSystem.Editor
{
    internal sealed class EndsMcpOperationsED
    {
        #region FIELDS

        internal const int MAX_PENDING = 256;
        internal const int UPDATE_BUDGET_MS = 8;
        private const int MAX_RECORDS = 1024;
        private const int MAX_RESULT_BYTES = 16 * 1024 * 1024;
        private static readonly TimeSpan _retention = TimeSpan.FromMinutes(10);
        private readonly object _sync = new object();
        private readonly Queue<Operation> _pending = new Queue<Operation>();
        private readonly Dictionary<string, Operation> _records = new Dictionary<string, Operation>(StringComparer.Ordinal);
        private readonly int _capacity;
        private int _resultBytes;
        private int _runningCount;
        private bool _accepting;

        internal sealed class Operation
        {
            internal string Key;
            internal string Fingerprint;
            internal string State = "queued";
            internal string Reply;
            internal string Failure;
            internal Func<string> Execute;
            internal DateTime CompletedAt;
            internal int ResultBytes;
            internal int Waiters;
            internal bool Delivered;
        }

        #endregion FIELDS

        #region ADMISSION

        internal EndsMcpOperationsED(int capacity = MAX_PENDING)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));

            _capacity = capacity;
        }

        internal void Open()
        {
            lock (_sync) _accepting = true;
        }

        internal bool TrySubmit(string key, string fingerprint, Func<string> execute, out Operation operation, out string error)
        {
            lock (_sync)
            {
                TrimExpired();

                if (_records.TryGetValue(key, out operation))
                {
                    error = null;

                    if (operation.Fingerprint == fingerprint) return true;

                    error = "request_key_conflict";
                    return false;
                }

                error = "not_accepted";

                if (!_accepting) return false;

                error = "busy";

                if (_pending.Count >= _capacity || _records.Count >= MAX_RECORDS || _resultBytes >= MAX_RESULT_BYTES) return false;

                operation = new Operation { Key = key, Fingerprint = fingerprint, Execute = execute };
                _records.Add(key, operation);
                _pending.Enqueue(operation);
                error = null;
                return true;
            }
        }

        internal void Close()
        {
            lock (_sync)
            {
                _accepting = false;

                while (_pending.Count > 0)
                {
                    Operation operation = _pending.Dequeue();
                    operation.State = "cancelled";
                    operation.Failure = "Listener stopped before execution. No change was applied by this request.";
                    operation.Execute = null;
                    operation.CompletedAt = DateTime.UtcNow;
                }

                Monitor.PulseAll(_sync);
            }
        }

        #endregion ADMISSION

        #region EXECUTION

        internal void Drain(int budgetMilliseconds = UPDATE_BUDGET_MS)
        {
            Stopwatch elapsed = Stopwatch.StartNew();

            do
            {
                Operation operation;

                lock (_sync)
                {
                    if (_pending.Count == 0) return;

                    operation = _pending.Dequeue();
                    operation.State = "running";
                    _runningCount++;
                }

                string reply = null;
                string failure = null;

                try { reply = operation.Execute(); }
                catch (Exception exception) { failure = exception.Message; }

                lock (_sync)
                {
                    _runningCount--;
                    operation.Reply = reply;
                    operation.Failure = failure;
                    operation.State = "completed";
                    operation.CompletedAt = DateTime.UtcNow;
                    operation.Execute = null;

                    if (reply != null) operation.ResultBytes = Encoding.UTF8.GetByteCount(reply);

                    _resultBytes += operation.ResultBytes;
                    TrimResults();
                    Monitor.PulseAll(_sync);
                }
            }
            while (elapsed.ElapsedMilliseconds < budgetMilliseconds);
        }

        internal EndsMcpJsonED Wait(Operation operation, int timeoutMilliseconds, out string reply)
        {
            Stopwatch elapsed = Stopwatch.StartNew();

            lock (_sync)
            {
                operation.Waiters++;

                try
                {
                    while (operation.State == "queued" || operation.State == "running")
                    {
                        int remaining = timeoutMilliseconds - (int)elapsed.ElapsedMilliseconds;

                        if (remaining <= 0) break;

                        Monitor.Wait(_sync, remaining);
                    }

                    reply = operation.Reply;
                    operation.Delivered = true;
                    return Describe(operation, false);
                }
                finally
                {
                    operation.Waiters--;
                    TrimResults();
                }
            }
        }

        internal EndsMcpJsonED Read(string key)
        {
            lock (_sync)
            {
                TrimExpired();

                if (!_records.TryGetValue(key, out Operation operation))
                {
                    EndsMcpJsonED missing = EndsMcpJsonED.Obj();
                    missing.Set("requestKey", EndsMcpJsonED.Str(key));
                    missing.Set("status", EndsMcpJsonED.Str("unknown"));
                    missing.Set("message", EndsMcpJsonED.Str("No retained record. Results survive for 10 minutes in this Unity domain only. Reread the graph before deciding to retry."));
                    return missing;
                }

                EndsMcpJsonED result = Describe(operation);
                operation.Delivered = true;
                TrimResults();
                return result;
            }
        }

        internal void ReadActivity(out int pending, out int running)
        {
            lock (_sync)
            {
                pending = _pending.Count;
                running = _runningCount;
            }
        }

        internal EndsMcpJsonED Status()
        {
            lock (_sync)
            {
                TrimExpired();
                EndsMcpJsonED result = EndsMcpJsonED.Obj();
                result.Set("pending", EndsMcpJsonED.Num(_pending.Count));
                result.Set("running", EndsMcpJsonED.Num(_runningCount));
                result.Set("retainedRecords", EndsMcpJsonED.Num(_records.Count));
                result.Set("retainedResultBytes", EndsMcpJsonED.Num(_resultBytes));
                result.Set("accepting", EndsMcpJsonED.Boolean(_accepting));
                return result;
            }
        }

        internal void Forget(string key)
        {
            lock (_sync)
            {
                if (!_records.TryGetValue(key, out Operation operation) || operation.Waiters > 0 || operation.State != "completed") return;

                _resultBytes -= operation.ResultBytes;
                _records.Remove(key);
            }
        }

        #endregion EXECUTION

        #region RESULTS

        private static EndsMcpJsonED Describe(Operation operation, bool includeResult = true)
        {
            EndsMcpJsonED result = EndsMcpJsonED.Obj();
            result.Set("requestKey", EndsMcpJsonED.Str(operation.Key));
            result.Set("status", EndsMcpJsonED.Str(operation.State));
            result.Set("accepted", EndsMcpJsonED.Boolean(true));

            if (operation.State == "queued" || operation.State == "cancelled") result.Set("changed", EndsMcpJsonED.Boolean(false));
            if (operation.Failure != null) result.Set("error", EndsMcpJsonED.Str(operation.Failure));
            if (includeResult && operation.Reply != null)
            {
                EndsMcpJsonED envelope = EndsMcpJsonED.Parse(operation.Reply);
                EndsMcpJsonED toolResult = envelope.Get("result");

                if (toolResult != null) result.Set("result", toolResult);
                if (envelope.Get("error") != null) result.Set("protocolError", envelope.Get("error"));
            }
            else if (operation.State == "completed" && operation.Failure == null && operation.Reply == null)
            {
                result.Set("message", EndsMcpJsonED.Str("Completed. The response body is no longer retained or the request was a notification. It will not execute again with this key. Reread current state."));
            }

            return result;
        }

        private void TrimExpired()
        {
            DateTime cutoff = DateTime.UtcNow - _retention;
            List<string> expired = new List<string>();

            foreach (KeyValuePair<string, Operation> item in _records)
            {
                Operation operation = item.Value;

                if (operation.Waiters == 0 && operation.CompletedAt != default && operation.CompletedAt < cutoff) expired.Add(item.Key);
            }

            foreach (string key in expired)
            {
                _resultBytes -= _records[key].ResultBytes;
                _records.Remove(key);
            }
        }

        private void TrimResults()
        {
            if (_resultBytes <= MAX_RESULT_BYTES) return;

            foreach (Operation operation in _records.Values)
            {
                if (!operation.Delivered || operation.Waiters > 0 || operation.Reply == null) continue;

                _resultBytes -= operation.ResultBytes;
                operation.ResultBytes = 0;
                operation.Reply = null;

                if (_resultBytes <= MAX_RESULT_BYTES) return;
            }
        }

        internal static string Fingerprint(EndsMcpJsonED message)
        {
            EndsMcpJsonED request = EndsMcpJsonED.Obj();
            request.Set("method", message.Get("method"));
            EndsMcpJsonED parameters = message.Get("params");

            if (message.Get("method")?.Text == "tools/call" && parameters != null)
            {
                EndsMcpJsonED invocation = EndsMcpJsonED.Obj();
                invocation.Set("name", parameters.Get("name"));
                invocation.Set("arguments", Canonical(parameters.Get("arguments")));
                request.Set("params", invocation);
            }
            else request.Set("params", Canonical(parameters));

            using (SHA256 hash = SHA256.Create())
            {
                return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(request.Write())));
            }
        }

        private static EndsMcpJsonED Canonical(EndsMcpJsonED value)
        {
            if (value == null) return EndsMcpJsonED.Null();

            if (value.Type == EndsMcpJsonED.Kind.Object)
            {
                EndsMcpJsonED result = EndsMcpJsonED.Obj();
                List<string> keys = new List<string>(value.Keys);
                keys.Sort(StringComparer.Ordinal);

                foreach (string key in keys)
                {
                    result.Set(key, Canonical(value.Get(key)));
                }

                return result;
            }

            if (value.Type == EndsMcpJsonED.Kind.Array)
            {
                EndsMcpJsonED result = EndsMcpJsonED.Arr();

                foreach (EndsMcpJsonED item in value.Items)
                {
                    result.Add(Canonical(item));
                }

                return result;
            }

            return value;
        }

        #endregion RESULTS
    }
}
#endif
