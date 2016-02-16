// Copyright (c) 2014 George Kimionis
// Distributed under the GPLv3 software license, see the accompanying file LICENSE or http://opensource.org/licenses/GPL-3.0

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BitcoinLib.Auxiliary;
using BitcoinLib.ExceptionHandling.Rpc;
using BitcoinLib.RPC.RequestResponse;
using BitcoinLib.RPC.Specifications;
using BitcoinLib.Services.Coins.Base;
using Newtonsoft.Json;

namespace BitcoinLib.RPC.Connector
{
    public sealed class RpcConnector : IRpcConnector
    {
        private readonly ICoinService _coinService;

        public RpcConnector(ICoinService coinService)
        {
            _coinService = coinService;
        }

        public T MakeRequest<T>(RpcMethods rpcMethod, params object[] parameters)
        {
            return MakeRequest<T>(rpcMethod, 0, parameters);
        }

        private async Task<T> MakeRequestInner<T>(RpcMethods rpcMethod, Int16 timedOutRequestsCount, params object[] parameters)
        {
            JsonRpcRequest jsonRpcRequest = new JsonRpcRequest(1, rpcMethod.ToString(), parameters);

            var handler = new HttpClientHandler();
            handler.Credentials = new NetworkCredential(_coinService.Parameters.RpcUsername, _coinService.Parameters.RpcPassword);

            using (var client = new HttpClient(handler))
            {
                var byteArray = Encoding.ASCII.GetBytes(_coinService.Parameters.RpcUsername + ":" + _coinService.Parameters.RpcPassword);
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
                client.Timeout = new TimeSpan(0, 0, 0, 0, _coinService.Parameters.RpcRequestTimeoutInSeconds * GlobalConstants.MillisecondsInASecond);

                HttpContent contentPost = new StringContent(jsonRpcRequest.GetString(), Encoding.UTF8,
                    "application/json");
                contentPost.Headers.ContentType = new MediaTypeHeaderValue("application/json-rpc");

                using (HttpResponseMessage response = await client.PostAsync(_coinService.Parameters.SelectedDaemonUrl, contentPost))
                {
                    var jsonResult = await response.Content.ReadAsStringAsync();
                    JsonRpcResponse<T> rpcResponse = JsonConvert.DeserializeObject<JsonRpcResponse<T>>(jsonResult);
                    return rpcResponse.Result;
                }
            }
        }

        private T MakeRequest<T>(RpcMethods rpcMethod, Int16 timedOutRequestsCount, params object[] parameters)
        {
            return MakeRequestInner<T>(rpcMethod, timedOutRequestsCount, parameters).Result;
            //JsonRpcRequest jsonRpcRequest = new JsonRpcRequest(1, rpcMethod.ToString(), parameters);

            //HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(_coinService.Parameters.SelectedDaemonUrl);
            //SetBasicAuthHeader(webRequest, _coinService.Parameters.RpcUsername, _coinService.Parameters.RpcPassword);
            //webRequest.Credentials = new NetworkCredential(_coinService.Parameters.RpcUsername, _coinService.Parameters.RpcPassword);
            //webRequest.ContentType = "application/json-rpc";
            //webRequest.Method = "POST";
            //webRequest.Proxy = null;
            //webRequest.Timeout = _coinService.Parameters.RpcRequestTimeoutInSeconds * GlobalConstants.MillisecondsInASecond;
            //Byte[] byteArray = jsonRpcRequest.GetBytes();
            //webRequest.ContentLength = jsonRpcRequest.GetBytes().Length;

            //try
            //{
            //    using (Stream dataStream = webRequest.GetRequestStream())
            //    {
            //        dataStream.Write(byteArray, 0, byteArray.Length);
            //        dataStream.Dispose();
            //    }
            //}
            //catch (Exception exception)
            //{
            //    throw new RpcException("There was a problem sending the request to the wallet", exception);
            //}

            //try
            //{
            //    String json;

            //    using (WebResponse webResponse = webRequest.GetResponse())
            //    {
            //        using (Stream stream = webResponse.GetResponseStream())
            //        {
            //            using (StreamReader reader = new StreamReader(stream))
            //            {
            //                String result = reader.ReadToEnd();
            //                reader.Dispose();
            //                json = result;
            //            }
            //        }
            //    }

            //    JsonRpcResponse<T> rpcResponse = JsonConvert.DeserializeObject<JsonRpcResponse<T>>(json);
            //    return rpcResponse.Result;
            //}
            //catch (WebException webException)
            //{
            //    #region RPC Internal Server Error (with an Error Code)

            //    HttpWebResponse webResponse = webException.Response as HttpWebResponse;

            //    if (webResponse != null)
            //    {
            //        switch (webResponse.StatusCode)
            //        {
            //            case HttpStatusCode.InternalServerError:
            //                {
            //                    using (Stream stream = webResponse.GetResponseStream())
            //                    {
            //                        if (stream == null)
            //                        {
            //                            throw new RpcException("The RPC request was either not understood by the server or there was a problem executing the request", webException);
            //                        }

            //                        using (StreamReader reader = new StreamReader(stream))
            //                        {
            //                            String result = reader.ReadToEnd();
            //                            reader.Dispose();

            //                            try
            //                            {
            //                                JsonRpcResponse<Object> jsonRpcResponseObject = JsonConvert.DeserializeObject<JsonRpcResponse<Object>>(result);

            //                                RpcInternalServerErrorException internalServerErrorException = new RpcInternalServerErrorException(jsonRpcResponseObject.Error.Message, webException)
            //                                {
            //                                    RpcErrorCode = jsonRpcResponseObject.Error.Code
            //                                };

            //                                throw internalServerErrorException;
            //                            }
            //                            catch (JsonException)
            //                            {
            //                                throw new RpcException(result, webException);
            //                            }
            //                        }
            //                    }
            //                }

            //            default:
            //                throw new RpcException("The RPC request was either not understood by the server or there was a problem executing the request", webException);
            //        }
            //    }

            //    #endregion

            //    #region RPC Time-Out

            //    if (webException.Message == "The operation has timed out")
            //    {
            //        throw new RpcRequestTimeoutException(webException.Message);
            //    }

            //    #endregion

            //    throw new RpcException("An unknown web exception occured while trying to read the JSON response", webException);
            //}
            //catch (JsonException jsonException)
            //{
            //    throw new RpcResponseDeserializationException("There was a problem deserializing the response from the wallet", jsonException);
            //}
            //catch (ProtocolViolationException protocolViolationException)
            //{
            //    throw new RpcException("Unable to connect to the server", protocolViolationException);
            //}
            //catch (Exception exception)
            //{
            //    String queryParameters = jsonRpcRequest.Parameters.Cast<String>().Aggregate(String.Empty, (current, parameter) => current + (parameter + " "));
            //    throw new Exception(String.Format("A problem was encountered while calling MakeRpcRequest() for: {0} with parameters: {1}. \nException: {2}", jsonRpcRequest.Method, queryParameters, exception.Message));
            //}
        }

        //private static void SetBasicAuthHeader(WebRequest webRequest, String username, String password)
        //{
        //    String authInfo = username + ":" + password;
        //    authInfo = Convert.ToBase64String(Encoding.Default.GetBytes(authInfo));
        //    webRequest.Headers["Authorization"] = "Basic " + authInfo;
        //}
    }
}