using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EmbedIO.Utilities;
using EmbedIO.Sessions;
using EmbedIO;
using System.Collections.Generic;
using opcRESTconnector.Data;

namespace opcRESTconnector.Session {

    public class SimpleSession : ISession
        {
            private readonly Dictionary<string, object> _data = new Dictionary<string, object>(EmbedIO.Sessions.Session.KeyComparer);

            private int _usageCount;
            public bool isAnonymous;

            public SimpleSession(string id, TimeSpan duration)
            {
                Id = Validate.NotNullOrEmpty(nameof(id), id);
                Duration = duration;
                LastActivity = DateTime.UtcNow;
                _usageCount = 1;
                isAnonymous = false;
            }

            public SimpleSession(){
                Id = "";
                Duration = TimeSpan.Zero;
                LastActivity = DateTime.UtcNow;
                _usageCount = 1;
                isAnonymous = true;
            }
            public SimpleSession(sessionData data){
                Id = data.Id.ToString();
                long ticks = data.expiryUTC.Ticks - DateTime.UtcNow.Ticks;
                Duration = TimeSpan.FromTicks( ticks > 0 ? ticks : 0 );
                LastActivity = data.last_seen;
                _usageCount = 1;
                this["session"] = data;
            }

            public string Id { get; }

            public TimeSpan Duration { get; }

            public DateTime LastActivity { get; private set; }

            public int Count
            {
                get
                {
                    lock (_data)
                    {
                        return _data.Count;
                    }
                }
            }

            public bool IsEmpty
            {
                get
                {
                    lock (_data)
                    {
                        return _data.Count == 0;
                    }
                }
            }

            public object this[string key]
            {
                get
                {
                    lock (_data)
                    {
                        return _data[key];
                    }
                }
                set
                {
                    lock (_data)
                    {
                        _data[key] = value;
                    }
                }
            }

            public void Clear()
            {
                lock (_data)
                {
                    _data.Clear();
                }
            }

            public bool ContainsKey(string key)
            {
                lock (_data)
                {
                    return _data.ContainsKey(key);
                }
            }

            public bool TryRemove(string key, out object value)
            {
                lock (_data)
                {
                    if (!_data.TryGetValue(key, out value))
                        return false;

                    _data.Remove(key);
                    return true;
                }
            }

            public IReadOnlyList<KeyValuePair<string, object>> TakeSnapshot()
            {
                lock (_data)
                {
                    return _data.ToArray();
                }
            }

            public bool TryGetValue(string key, out object value)
            {
                lock (_data)
                {
                    return _data.TryGetValue(key, out value);
                }
            }

            internal void BeginUse()
            {
                lock (_data)
                {
                    _usageCount++;
                    LastActivity = DateTime.UtcNow;
                }
            }

            internal void EndUse(Action unregister)
            {
                lock (_data)
                {
                    --_usageCount;
                    UnregisterIfNeededCore(unregister);
                }
            }

            internal void UnregisterIfNeeded(Action unregister)
            {
                lock (_data)
                {
                    UnregisterIfNeededCore(unregister);
                }
            }

            private void UnregisterIfNeededCore(Action unregister)
            {
                if (_usageCount < 1 && (IsEmpty || DateTime.UtcNow > LastActivity + Duration))
                    unregister();
            }
        }
}
