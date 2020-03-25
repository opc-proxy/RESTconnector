using System.Text;
using System.Threading.Tasks;
using EmbedIO;
using System;
using System.Net;
using System.Security.Cryptography;
using System.Collections.Generic;
using EmbedIO.Routing;
using EmbedIO.Utilities;
using EmbedIO.WebApi;
using Newtonsoft.Json.Linq;
using opcRESTconnector.Session;
using NLog;
using System.Net.Http.Headers ;
using System.Net.Http;

namespace opcRESTconnector {

    public enum AuthRoles{
        Reader,
        Writer,
        Admin,
        Undefined
    }

    
    public class EnsureActiveUser : WebModuleBase
    {
        public static NLog.Logger logger = null;
        public EnsureActiveUser() : base("/") {
            logger = LogManager.GetLogger(this.GetType().Name);
        }
        public override bool IsFinalHandler => false;

        protected override Task OnRequestAsync(IHttpContext context)
        {
            return AuthUtils.EnsureActiveUser(context) ;
        }
    }

    public class AuthUtils {
        
        public static Task EnsureActiveUser(IHttpContext context){
            if( string.IsNullOrEmpty(context.Session.Id) ) return AuthUtils.sendForbiddenTemplate(context);
            // if Id is not empty then "session" is also filled (no need to tryGet)
            var session = (sessionData) context.Session["session"];
            var user = session?.user;
            if(user == null) return AuthUtils.sendForbiddenTemplate(context);
            if( !user.isActive() ) return AuthUtils.sendForbiddenTemplate(context);
            if( !user.password.isActive() ) return Utils.HttpRedirect(context, Routes.update_pw);
            return Task.CompletedTask;
        }
        public static Task sendForbiddenTemplate(IHttpContext context, string redirectURL = Routes.login){
            context.Response.StatusCode = 403;
            context.SetHandled();
            return context.SendStringAsync(HTMLtemplates.forbidden(redirectURL),"text/html",Encoding.UTF8);
        }

        public static async Task<bool> reCAPTCHA_isValid(string recaptcha, string serverKey){
            var query = new Dictionary<string, string>
            {
                { "secret", serverKey },
                { "response", recaptcha}
            };
            var http = new HttpClient();
            var Req = new FormUrlEncodedContent(query);
            var response = await http.PostAsync("https://www.google.com/recaptcha/api/siteverify", Req);
            var body = await response.Content.ReadAsStringAsync() ;
            try{
                var result = JObject.Parse(await response.Content.ReadAsStringAsync()).ToObject<reCATPCHAresp>();
                return result.success;
            }
            catch{
                return false;
            }
        }
    }

    /// <summary>
    /// Class to handle the generation and validation of CSRF tokens.
    /// The secret key used to sign tokens is randomly generated at startup.
    /// </summary>
    public class CSRF_utils {
        AntiCSRF.AntiCSRF csrf_gen;
        string salt;
        string secret;
        public static NLog.Logger logger = null;

        public CSRF_utils(){
            
            logger = LogManager.GetLogger(this.GetType().Name);
            var csrf_conf = new AntiCSRF.Config.AntiCSRFConfig(){ expiryInSeconds = 180 };
            csrf_gen = new AntiCSRF.AntiCSRF(csrf_conf);
            salt = "XYfday567D";
            byte[] token = new byte[32];
            var rnd = new RNGCryptoServiceProvider();
            rnd.GetBytes(token);
            secret = Convert.ToBase64String(token);
        }

        /// <summary>
        /// Generate a signed token from a random number (not encrypted), note that all users have same "salt".
        /// </summary>
        /// <param name="context"></param>
        public string setCSRFcookie(IHttpContext context){
            var token = csrf_gen.GenerateToken(salt,secret);
            logger.Debug("Set CSRF: " + token);
            var cookie = new System.Net.Cookie("_csrf",token + "; SameSite=Strict");
            cookie.HttpOnly = true;
            cookie.Expires = DateTime.Now.AddMinutes(3) ;
            context.Response.SetCookie(cookie);
            return token;
        }

        public bool validateCSRFtoken(IHttpContext context){
            
            var _data = context.GetRequestFormDataAsync();
            _data.Wait();
            var data = _data.Result;
            System.Net.Cookie req_cookie = null;
            foreach (var c in context.Request.Cookies)
            {
                if(c.Name == "_csrf") {
                    req_cookie = c;
                    logger.Debug("found CSRF "+ c.Value);
                }
            } 
            System.Net.Cookie resp_cookie = new System.Net.Cookie();
            if(req_cookie == null) return false;
            if(!csrf_gen.ValidateToken(req_cookie.Value,secret,salt)) return false;
            logger.Debug("CSRF token Valid");

            if( !data.ContainsKey("_csrf") ) return false;
            logger.Debug("CSRF token present in data");
            
            if( req_cookie.Value != data.Get("_csrf") ) return false;
            logger.Debug("CSRF token and Cookie are same");

            return true;
        }
    }
}
