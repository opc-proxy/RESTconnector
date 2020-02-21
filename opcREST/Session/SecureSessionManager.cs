using System;
using System.Linq;
using System.Net;
using EmbedIO.Utilities;
using EmbedIO.Sessions;
using EmbedIO;
using Jose;
using System.Security.Cryptography;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using NLog;

namespace opcRESTconnector.Session
{
    public class SecureSessionManager : LSManagerCopy {
        private byte[] secret;
        public static NLog.Logger logger = null;
        
        public SecureSessionManager(){
            secret = new byte[32];
            var rnd = new RNGCryptoServiceProvider();
            rnd.GetNonZeroBytes(secret);

            CookieHttpOnly = true;
            CookieName = "_opcSession";
            CookiePath = "/" ;
            CookieDuration = TimeSpan.FromDays(30);

            logger = LogManager.GetLogger(this.GetType().Name);

        }
        
        /// <summary>
        /// It does not actually CREATE any session, it retrieves the session if cookie is 
        /// present and validates it, add a dummy Anonimous session otherwise. It does not add any cookie.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public override ISession Create(IHttpContext context){
            logger.Info("entered in create");
            var id = GetSessionId(context);

            SimpleSession session;
            lock (_sessions)
            {
                if (!string.IsNullOrEmpty(id) && _sessions.TryGetValue(id, out session))
                {
                    session.BeginUse();
                    logger.Info("Session Found");
                    logger.Info("There are in total sessions : " + _sessions.Count);

                }
                else session = new SimpleSession();
            }
            return session;
        }

        public override string GetSessionId(IHttpContext context){
        
            string cookieValue =  context.Request.Cookies.FirstOrDefault(IsSessionCookie)?.Value.Trim() ?? string.Empty;    
            return AuthenticateCookie(cookieValue);
        }

        public SimpleSession RegisterSession(IHttpContext context){
            logger.Info("Registering Session");

            string id = UniqueIdGenerator.GetNext();
            SimpleSession session;
            lock (_sessions) {    
                session = new SimpleSession(id, SessionDuration);
                _sessions.TryAdd(id, session);
            }
            var cookie = createSecureCookie(id);
            context.Request.Cookies.Add(cookie);
            context.Response.Cookies.Add(cookie);
            return session;
        }
        public string AuthenticateCookie(string cookieValue){
            logger.Info("Authenticating cookie value " + cookieValue);
            jwtPayload j = null;
            try{
                string jwt = JWT.Decode(cookieValue,secret,JweAlgorithm.A256KW, JweEncryption.A256CBC_HS512);
                j = JObject.Parse(jwt).ToObject<jwtPayload>();
                if(j.exp < DateTime.UtcNow.Ticks ) throw new Exception("token expired");
            }
            catch(Exception e) {
                logger.Error("Auth failed: "+ e.Message);
                j = new jwtPayload();
            }
            return j.sub;
        }
        public Cookie createSecureCookie(string id){
            logger.Info("Creating cookie");
            Cookie c = BuildSessionCookie(id);
            var payload = new jwtPayload();
            payload.exp = c.Expires.Ticks;
            payload.sub = c.Value;
            string token = Jose.JWT.Encode(payload.ToString(), secret, JweAlgorithm.A256KW, JweEncryption.A256CBC_HS512);
            logger.Info("jwt created : " + token);
            c.Value = token + "; SameSite=Strict";
            
            return c;
        }

        public override void Delete(IHttpContext context)
        {
            var id = GetSessionId(context);

            if (string.IsNullOrEmpty(id))
                return;

            lock (_sessions)
            {
                if (_sessions.TryGetValue(id, out var session)){
                    session.EndUse(() => { });
                    _sessions.TryRemove(id, out var x); 
                    logger.Info("Session Removed : " + x.Id );
                }
            }
            
            context.Request.Cookies.Add(BuildSessionCookie(string.Empty));
            context.Response.Cookies.Add(BuildSessionCookie(string.Empty));
        }

    }

    public class jwtPayload{
        public string sub {get;set;}
        public long exp {get;set;}

        public new string ToString(){
            return JObject.FromObject(this).ToString();
        }
        public jwtPayload(){
            sub = String.Empty;
            exp = DateTime.UtcNow.Ticks - 10;
        }
    }
}