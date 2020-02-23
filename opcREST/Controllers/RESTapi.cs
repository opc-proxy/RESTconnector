using System.Collections.Generic;
using System.Threading.Tasks;
using EmbedIO.Routing;
using EmbedIO.Utilities;
using EmbedIO.WebApi;
using EmbedIO;
using System;
using OpcProxyCore;

namespace opcRESTconnector
{
    public class Utils{
        public static ReadResponse packReadNodes(dbVariableValue[] values, ReadStatusCode status){

            ReadResponse r = new ReadResponse();

            foreach(var variable in values){
                NodeValue val = new NodeValue();
                val.Name = variable.name;
                val.Type = variable.systemType.Substring(7).ToLower();
                val.Value = variable.value;
                val.Timestamp = variable.timestamp.ToUniversalTime().ToString("o");
                r.Nodes.Add(val);
            }
            r.ErrorMessage = ( status == ReadStatusCode.Ok) ? "none" : "Not Found";
            r.IsError = ( status != ReadStatusCode.Ok);

            return r;
        }
    }

    public sealed class nodeRESTController : WebApiController
    {
        serviceManager _service;
        RESTconfigs _conf;
        public nodeRESTController(serviceManager manager, RESTconfigs conf){
            _service = manager;
            _conf = conf;
        }



        [Route(HttpVerb.Get, "/{node_name}")]
        public  ReadResponse GetNode(string node_name){
           
            // Check if Authorized
            var role = (AuthRoles)(HttpContext.Items["Role"] ?? AuthRoles.Undefined);
            if(_conf.enableBasicAuth && role == AuthRoles.Undefined) 
                throw HttpException.Forbidden();
            
           try{
                List<string> names = new List<string>{ node_name };
                ReadStatusCode status;
                var values =  _service.readValueFromCache(names.ToArray(),out status);
                if(status != ReadStatusCode.Ok) HttpContext.Response.StatusCode = 404;
                return Utils.packReadNodes(values,status);
           }
            catch(Exception ex){
                Console.WriteLine(ex.Message);
                throw HttpException.BadRequest();
            }
        }

        
        [Route(HttpVerb.Post, "/{node_name}")]
         public async Task<WriteResponse> PostData(string node_name) 
        {
              // Check if Authorized
            var role = (AuthRoles)(HttpContext.Items["Role"] ?? AuthRoles.Undefined);
            if(_conf.enableBasicAuth && role != AuthRoles.Writer && role != AuthRoles.Admin ) 
                throw HttpException.Forbidden();
            
            var data = await HttpContext.GetRequestFormDataAsync();
            // validity check
            if(_conf.enableAPIkey && ( !data.ContainsKey("apiKey") || data.Get("apiKey") != _conf.apyKey ))  
                throw HttpException.Forbidden();

            if(!data.ContainsKey("value")) 
                throw HttpException.BadRequest();
                    
            var value = data.Get("value");
            var status = await _service.writeToOPCserver(node_name, value);
            
            WriteResponse r = new WriteResponse();
            r.IsError = (Opc.Ua.StatusCode.IsBad(status[0]));
            r.ErrorMessage = (r.IsError) ? "Not Found" : "none";

            if(r.IsError) HttpContext.Response.StatusCode = 404;

            return r;
        }


    }

   
}