﻿// Copyright (c) 2014 George Kimionis
// Distributed under the GPLv3 software license, see the accompanying file LICENSE or http://opensource.org/licenses/GPL-3.0

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using BitcoinLib.Services.Coins.Base;

namespace BitcoinLib.RPC.Connector
{
    //  This class is disconnected from the core logic and its sole purpose is to serve as a quick and dirty means of debugging
    public static class RawRpcConnector
    {
        //  Usage example:  String networkDifficultyJsonResult = RawRpcConnector.MakeRequest("{\"method\":\"getdifficulty\",\"params\":[],\"id\":1}", "http://127.0.0.1:8332/", "MyRpcUsername", "MyRpcPassword");
        public static String MakeRequest(String jsonRequest, String daemonUrl, String rpcUsername, String rpcPassword)
        {
            try
            {
                //CookieContainer tempCookies = new CookieContainer();
                //ASCIIEncoding encoding = new ASCIIEncoding();
                //Byte[] byteData = encoding.GetBytes(jsonRequest);
                //HttpWebRequest postReq = (HttpWebRequest) WebRequest.Create(daemonUrl);
                //postReq.Credentials = new NetworkCredential(rpcUsername, rpcPassword);
                //postReq.Method = "POST";
                //postReq.KeepAlive = true;
                //postReq.CookieContainer = tempCookies;
                //postReq.ContentType = "application/json";
                //postReq.ContentLength = byteData.Length;
                //Stream postreqstream = postReq.GetRequestStream();
                //postreqstream.Write(byteData, 0, byteData.Length);
                //postreqstream.Close();
                //HttpWebResponse postresponse = (HttpWebResponse) postReq.GetResponse();
                //StreamReader postreqreader = new StreamReader(postresponse.GetResponseStream());
                //return postreqreader.ReadToEnd();
                return MakeRequestInner(jsonRequest, daemonUrl, rpcUsername, rpcPassword).Result;
            }
            catch (Exception exception)
            {
                return exception.ToString();
            }
        }

        public static async Task<string> MakeRequestInner(string jsonRequest, string daemonUrl, string rpcUsername,
            string rpcPassword)
        {
            var tempCookies = new CookieContainer();
            var handler = new HttpClientHandler();
            handler.Credentials = new NetworkCredential(rpcUsername, rpcPassword);
            handler.CookieContainer = tempCookies;

            using (var client = new HttpClient(handler))
            {              
                HttpContent contentPost = new StringContent(jsonRequest, Encoding.ASCII,
                    "application/json");
                contentPost.Headers.Add("Keep-Alive", "true");
                contentPost.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                using (HttpResponseMessage response = await client.PostAsync(daemonUrl, contentPost))
                {
                    return await response.Content.ReadAsStringAsync();
                }
            }
        }

        //  Usage example:  String networkDifficultyJsonResult = RawRpcConnector.MakeRequest("{\"method\":\"getdifficulty\",\"params\":[],\"id\":1}", new BitcoinService());
        public static String MakeRequest(String jsonRequest, ICoinService coinService)
        {
            return MakeRequest(jsonRequest, coinService.Parameters.SelectedDaemonUrl, coinService.Parameters.RpcUsername, coinService.Parameters.RpcPassword);
        }
    }
}