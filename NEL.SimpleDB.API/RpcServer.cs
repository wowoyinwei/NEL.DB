﻿using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.DependencyInjection;
using NEL.Pipeline;
using NEL.Simple.SDK;
using Neo;
using Neo.IO;
using Neo.IO.Json;
using Neo.Ledger;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Neo.Network.P2P.Payloads;
using System.Collections.Generic;
using Neo.Persistence;
using Neo.Persistence.SimpleDB;
using NEL.SimpleDB.API.SmartContract;
using Neo.VM;

namespace NEL.SimpleDB.API
{
    public sealed class RpcServer : IDisposable
    {
        private IWebHost host;

        private Store store;

        private Fixed8 maxGasInvoke = default(Fixed8);

        public RpcServer(Setting setting)
        {
            //开启rpc服务
            Start(IPAddress.Parse(setting.BindAddress), setting.Port);
            store = new SimpleDBStore(setting);
            UpdateHeaderHashList.CreateIns(store);
        }

        private async Task ProcessAsync(HttpContext context)
        {
            context.Response.Headers["Access-Control-Allow-Origin"] = "*";
            context.Response.Headers["Access-Control-Allow-Methods"] = "GET, POST";
            context.Response.Headers["Access-Control-Allow-Headers"] = "Content-Type";
            context.Response.Headers["Access-Control-Max-Age"] = "31536000";
            if (context.Request.Method != "GET" && context.Request.Method != "POST") return;
            var session = context.Response.Cookies.ToString();
            JObject request = null;
            if (context.Request.Method == "GET")
            {
                string jsonrpc = context.Request.Query["jsonrpc"];
                string id = context.Request.Query["id"];
                string method = context.Request.Query["method"];
                string _params = context.Request.Query["params"];
                if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(method) && !string.IsNullOrEmpty(_params))
                {
                    try
                    {
                        _params = Encoding.UTF8.GetString(Convert.FromBase64String(_params));
                    }
                    catch (FormatException) { }
                    request = new JObject();
                    if (!string.IsNullOrEmpty(jsonrpc))
                        request["jsonrpc"] = jsonrpc;
                    request["id"] = id;
                    request["method"] = method;
                    request["params"] = JObject.Parse(_params);
                }
            }
            else if (context.Request.Method == "POST")
            {
                using (StreamReader reader = new StreamReader(context.Request.Body))
                {
                    try
                    {
                        request = JObject.Parse(reader);
                    }
                    catch (FormatException) { }
                }
            }
            JObject response = CreateErrorResponse(null, -32800, "time out");
            request["host"] = context.Request.Host.Value;
            if (request == null)
            {
                response = CreateErrorResponse(null, -32700, "Parse error");
            }
            else if (request is JArray array)
            {
                if (array.Count == 0)
                {
                    response = CreateErrorResponse(request["id"], -32600, "Invalid Request");
                }
                else
                {
                    response = ProcessSend(request);
                }
            }
            else
            {
                response = ProcessSend(request);
            }
            context.Response.ContentType = "application/json-rpc";
            await context.Response.WriteAsync(response.ToString(), Encoding.UTF8);
        }

        public void Start(IPAddress bindAddress, int port, string sslCert = null, string password = null, string[] trustedAuthorities = null)
        {
            host = new WebHostBuilder().UseKestrel(options => options.Listen(bindAddress, port, listenOptions =>
            {
                if (string.IsNullOrEmpty(sslCert)) return;
                listenOptions.UseHttps(sslCert, password, httpsConnectionAdapterOptions =>
                {
                    if (trustedAuthorities is null || trustedAuthorities.Length == 0)
                        return;
                    httpsConnectionAdapterOptions.ClientCertificateMode = ClientCertificateMode.RequireCertificate;
                    httpsConnectionAdapterOptions.ClientCertificateValidation = (cert, chain, err) =>
                    {
                        if (err != SslPolicyErrors.None)
                            return false;
                        X509Certificate2 authority = chain.ChainElements[chain.ChainElements.Count - 1].Certificate;
                        return trustedAuthorities.Contains(authority.Thumbprint);
                    };
                });
            }))
            .Configure(app =>
            {
                app.UseResponseCompression();
                app.Run(ProcessAsync);
            })
            .ConfigureServices(services =>
            {
                services.AddResponseCompression(options =>
                {
                    options.Providers.Add<GzipCompressionProvider>();
                    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[] { "application/json-rpc" });
                });

                services.Configure<GzipCompressionProviderOptions>(options =>
                {
                    options.Level = CompressionLevel.Fastest;
                });
            })
            .Build();

            host.Start();
        }

        private static JObject CreateErrorResponse(JObject id, int code, string message, JObject data = null)
        {
            JObject response = CreateResponse(id);
            response["error"] = new JObject();
            response["error"]["code"] = code;
            response["error"]["message"] = message;
            if (data != null)
                response["error"]["data"] = data;
            return response;
        }

        private static JObject CreateResponse(JObject id)
        {
            JObject response = new JObject();
            response["jsonrpc"] = "2.0";
            response["id"] = id;
            return response;
        }

        public void Dispose()
        {
            if (host != null)
            {
                host.Dispose();
                host = null;
            }
        }
 
        private JObject ProcessSend(JObject request)
        {
            //因为收发消息全异步，就用一个标识来明确某个回复对应哪个请求
            JArray _params = (JArray)request["params"];
            switch (request["method"].AsString())
            {
                case "getstorage":
                    {
                        UInt160 script_hash = UInt160.Parse(_params[0].AsString());
                        byte[] key = _params[1].AsString().HexToBytes();
                        StorageKey storageKey = new StorageKey
                        {
                            ScriptHash = script_hash,
                            Key = key
                        };
                        StorageItem item = store.GetStorages().TryGet(new StorageKey
                        {
                            ScriptHash = script_hash,
                            Key = key
                        }) ?? new StorageItem();
                        var response = CreateResponse(request["id"].AsString());
                        response["result"] = item.Value?.ToHexString();
                        return response;
                    }
                case "getblock":
                    {
                        Block block;
                        if (_params[0] is JNumber)
                        {
                            uint index = (uint)_params[0].AsNumber();
                            var hash = UpdateHeaderHashList.Ins.GetHeader((int)index);
                            block = store.GetBlock(hash);
                        }
                        else
                        {
                            UInt256 hash = UInt256.Parse(_params[0].AsString());
                            block = store.GetBlock(hash);
                        }
                        if (block == null)
                            return CreateErrorResponse(request["id"].AsString() ,- 100, "Unknown block");
                        bool verbose = _params.Count >= 2 && _params[1].AsBooleanOrDefault(false);
                        if (verbose)
                        {
                            JObject json = block.ToJson();
                            return json;
                        }
                        return block.ToArray().ToHexString();
                    }
                case "invokescript":
                    {
                        byte[] script = _params[0].AsString().HexToBytes();
                        try
                        {
                            SimpleApplicationEngine engine = SimpleApplicationEngine.Run(script, store, extraGAS: maxGasInvoke);
                            JObject json = new JObject();
                            json["script"] = script.ToHexString();
                            json["state"] = engine.State;
                            json["gas_consumed"] = engine.GasConsumed.ToString();
                            try
                            {
                                json["stack"] = new JArray(engine.ResultStack.Select(p => p.ToParameter().ToJson()));
                            }
                            catch (InvalidOperationException)
                            {
                                json["stack"] = "error: recursive reference";
                            }
                            return json;
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.Message);
                        }
                        break;
                    }
                default:
                    break;
            }
            return null;
        }
    }
}
