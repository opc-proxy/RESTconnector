using System;
using System.Threading;
using EmbedIO;
using EmbedIO.Sessions;
using opcRESTconnector.Data;

namespace opcRESTconnector.Session{

    /// <summary>
    /// Dummy Session that allows the user to read and write at all times
    /// </summary>
    public class DummySessionManager : ISessionManager
    {
        DataStore users;
        public DummySessionManager(RESTconfigs conf){
            users = new DataStore(conf);
        }
        public ISession Create(IHttpContext context)
        {
            SimpleSession return_session = new SimpleSession();
            // Hack the system! building dummy user that can Read and Write, always.
            UserData _user = new UserData("Anonymous","none",AuthRoles.Writer,1);
            _user.password.expiry = DateTime.UtcNow.AddMinutes(1);
            sessionData _session = new sessionData(_user, 1,"","");
            _session.write_expiry = DateTime.UtcNow.AddMinutes(1);
            return_session["session"] = _session;
            return_session.BeginUse();
            return return_session;        
        }

        public void Delete(IHttpContext context, String x)
        {
            //throw new NotImplementedException();
        }

        public void OnContextClose(IHttpContext context)
        {
            //throw new NotImplementedException();
        }

        public void Start(CancellationToken cancellationToken)
        {
            //throw new NotImplementedException();
        }
    }
}