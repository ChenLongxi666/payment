﻿using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using Essensoft.AspNetCore.Payment.Alipay.Domain;
using Essensoft.AspNetCore.Payment.Alipay.Parser;
using Essensoft.AspNetCore.Payment.Alipay.Request;
using Essensoft.AspNetCore.Payment.Alipay.Utility;

namespace Essensoft.AspNetCore.Payment.Alipay
{
    /// <summary>
    /// Alipay 客户端。
    /// </summary>
    public class AlipayClient : IAlipayClient
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly AlipayPublicKeyManager _alipayPublicKeyManager;

        #region AlipayClient Constructors

        public AlipayClient(IHttpClientFactory httpClientFactory, AlipayPublicKeyManager certificateManager)
        {
            _httpClientFactory = httpClientFactory;
            _alipayPublicKeyManager = certificateManager;
        }

        #endregion

        #region IAlipayClient Members

        public async Task<T> PageExecuteAsync<T>(IAlipayRequest<T> request, AlipayOptions options) where T : AlipayResponse
        {
            return await PageExecuteAsync(request, options, null, "POST");
        }

        public async Task<T> PageExecuteAsync<T>(IAlipayRequest<T> request, AlipayOptions options, string accessToken, string reqMethod) where T : AlipayResponse
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (string.IsNullOrEmpty(options.AppId))
            {
                throw new ArgumentNullException(nameof(options.AppId));
            }

            if (string.IsNullOrEmpty(options.SignType))
            {
                throw new ArgumentNullException(nameof(options.SignType));
            }

            if (string.IsNullOrEmpty(options.AppPrivateKey))
            {
                throw new ArgumentNullException(nameof(options.AppPrivateKey));
            }

            if (string.IsNullOrEmpty(options.ServerUrl))
            {
                throw new ArgumentNullException(nameof(options.ServerUrl));
            }

            var apiVersion = string.IsNullOrEmpty(request.GetApiVersion()) ? options.Version : request.GetApiVersion();

            // 添加协议级请求参数
            var txtParams = new AlipayDictionary(request.GetParameters())
            {
                { AlipayConstants.METHOD, request.GetApiName() },
                { AlipayConstants.VERSION, apiVersion },
                { AlipayConstants.APP_ID, options.AppId },
                { AlipayConstants.FORMAT, options.Format },
                { AlipayConstants.TIMESTAMP, DateTime.Now },
                { AlipayConstants.ACCESS_TOKEN, accessToken },
                { AlipayConstants.SIGN_TYPE, options.SignType },
                { AlipayConstants.TERMINAL_TYPE, request.GetTerminalType() },
                { AlipayConstants.TERMINAL_INFO, request.GetTerminalInfo() },
                { AlipayConstants.PROD_CODE, request.GetProdCode() },
                { AlipayConstants.NOTIFY_URL, request.GetNotifyUrl() },
                { AlipayConstants.CHARSET, options.Charset },
                { AlipayConstants.RETURN_URL, request.GetReturnUrl() },
                { AlipayConstants.ALIPAY_ROOT_CERT_SN, options.RootCertSN },
                { AlipayConstants.APP_CERT_SN, options.AppCertSN }
            };

            // 序列化BizModel
            txtParams = SerializeBizModel(txtParams, request);

            // 添加签名参数
            var signContent = AlipaySignature.GetSignContent(txtParams);
            txtParams.Add(AlipayConstants.SIGN, AlipaySignature.RSASignContent(signContent, options.AppPrivateKey, options.Charset, options.SignType));

            string body;

            // 是否需要上传文件
            if (request is IAlipayUploadRequest<T> uploadRequest)
            {
                var fileParams = AlipayUtility.CleanupDictionary(uploadRequest.GetFileParameters());

                var client = _httpClientFactory.CreateClient(nameof(AlipayClient));
                body = await client.PostAsync(options.ServerUrl, txtParams, fileParams);
            }
            else
            {
                if (reqMethod.ToUpperInvariant() == "GET")
                {
                    var url = options.ServerUrl;
                    if (txtParams != null && txtParams.Count > 0)
                    {
                        if (url.Contains("?"))
                        {
                            url += "&" + AlipayUtility.BuildQuery(txtParams);
                        }
                        else
                        {
                            url += "?" + AlipayUtility.BuildQuery(txtParams);
                        }
                    }
                    body = url;
                }
                else
                {
                    body = BuildHtmlRequest(txtParams, options.ServerUrl, options.Charset, reqMethod);
                }
            }

            var parser = new AlipayJsonParser<T>();
            var rsp = parser.Parse(body);

            return rsp;
        }

        #endregion

        #region IAlipayClient Members

        public async Task<T> ExecuteAsync<T>(IAlipayRequest<T> request, AlipayOptions options) where T : AlipayResponse
        {
            return await ExecuteAsync(request, options, null);
        }

        public async Task<T> ExecuteAsync<T>(IAlipayRequest<T> request, AlipayOptions options, string accessToken) where T : AlipayResponse
        {
            return await ExecuteAsync(request, options, accessToken, null);
        }

        public async Task<T> ExecuteAsync<T>(IAlipayRequest<T> request, AlipayOptions options, string accessToken, string appAuthToken) where T : AlipayResponse
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (string.IsNullOrEmpty(options.AppId))
            {
                throw new ArgumentNullException(nameof(options.AppId));
            }

            if (string.IsNullOrEmpty(options.SignType))
            {
                throw new ArgumentNullException(nameof(options.SignType));
            }

            if (string.IsNullOrEmpty(options.AlipayPublicKey))
            {
                throw new ArgumentNullException(nameof(options.AlipayPublicKey));
            }

            if (string.IsNullOrEmpty(options.AppPrivateKey))
            {
                throw new ArgumentNullException(nameof(options.AppPrivateKey));
            }

            if (string.IsNullOrEmpty(options.ServerUrl))
            {
                throw new ArgumentNullException(nameof(options.ServerUrl));
            }

            var apiVersion = string.IsNullOrEmpty(request.GetApiVersion()) ? options.Version : request.GetApiVersion();

            // 添加协议级请求参数
            var txtParams = new AlipayDictionary(request.GetParameters())
            {
                { AlipayConstants.METHOD, request.GetApiName() },
                { AlipayConstants.VERSION, apiVersion },
                { AlipayConstants.APP_ID, options.AppId },
                { AlipayConstants.FORMAT, options.Format },
                { AlipayConstants.TIMESTAMP, DateTime.Now },
                { AlipayConstants.ACCESS_TOKEN, accessToken },
                { AlipayConstants.SIGN_TYPE, options.SignType },
                { AlipayConstants.TERMINAL_TYPE, request.GetTerminalType() },
                { AlipayConstants.TERMINAL_INFO, request.GetTerminalInfo() },
                { AlipayConstants.PROD_CODE, request.GetProdCode() },
                { AlipayConstants.CHARSET, options.Charset }
            };

            // 序列化BizModel
            txtParams = SerializeBizModel(txtParams, request);

            if (!string.IsNullOrEmpty(request.GetNotifyUrl()))
            {
                txtParams.Add(AlipayConstants.NOTIFY_URL, request.GetNotifyUrl());
            }

            if (!string.IsNullOrEmpty(appAuthToken))
            {
                txtParams.Add(AlipayConstants.APP_AUTH_TOKEN, appAuthToken);
            }

            if (request.GetNeedEncrypt())
            {
                if (string.IsNullOrEmpty(txtParams[AlipayConstants.BIZ_CONTENT]))
                {
                    throw new AlipayException("api request Fail ! The reason: encrypt request is not supported!");
                }

                if (string.IsNullOrEmpty(options.EncyptKey) || string.IsNullOrEmpty(options.EncyptType))
                {
                    throw new AlipayException("encryptType or encryptKey must not null!");
                }

                if (!"AES".Equals(options.EncyptType))
                {
                    throw new AlipayException("api only support Aes!");
                }

                var encryptContent = AlipaySignature.AESEncrypt(txtParams[AlipayConstants.BIZ_CONTENT], options.EncyptKey);
                txtParams.Remove(AlipayConstants.BIZ_CONTENT);
                txtParams.Add(AlipayConstants.BIZ_CONTENT, encryptContent);
                txtParams.Add(AlipayConstants.ENCRYPT_TYPE, options.EncyptType);
            }

            // 添加签名参数
            var signContent = AlipaySignature.GetSignContent(txtParams);
            txtParams.Add(AlipayConstants.SIGN, AlipaySignature.RSASignContent(signContent, options.AppPrivateKey, options.Charset, options.SignType));

            string body;
            var client = _httpClientFactory.CreateClient(nameof(AlipayClient));

            // 是否需要上传文件
            if (request is IAlipayUploadRequest<T> uRequest)
            {
                var fileParams = AlipayUtility.CleanupDictionary(uRequest.GetFileParameters());

                body = await client.PostAsync(options.ServerUrl, txtParams, fileParams);
            }
            else
            {
                body = await client.PostAsync(options.ServerUrl, txtParams);
            }

            var parser = new AlipayJsonParser<T>();
            var item = ParseRespItem(request, body, parser, options.EncyptKey, options.EncyptType);
            var rsp = parser.Parse(item.RealContent);

            CheckResponseSign(request, item.RespContent, rsp.IsError, parser, options);

            return rsp;
        }

        private void CheckResponseSign<T>(IAlipayRequest<T> request, string responseBody, bool isError, IAlipayParser<T> parser, AlipayOptions options) where T : AlipayResponse
        {
            var signItem = parser.GetSignItem(request, responseBody);
            if (signItem == null)
            {
                throw new AlipayException("sign check fail: Body is Empty!");
            }

            if (!isError || isError && !string.IsNullOrEmpty(signItem.Sign))
            {
                var rsaCheckContent = AlipaySignature.RSACheckContent(signItem.SignSourceDate, signItem.Sign, options.AlipayPublicKey, options.Charset, options.SignType);
                if (!rsaCheckContent)
                {
                    if (!string.IsNullOrEmpty(signItem.SignSourceDate) && signItem.SignSourceDate.Contains("\\/"))
                    {
                        var srouceData = signItem.SignSourceDate.Replace("\\/", "/");
                        var jsonCheck = AlipaySignature.RSACheckContent(srouceData, signItem.Sign, options.AlipayPublicKey, options.Charset, options.SignType);
                        if (!jsonCheck)
                        {
                            throw new AlipayException("sign check fail: check Sign and Data Fail JSON also");
                        }
                    }
                    else
                    {
                        throw new AlipayException("sign check fail: check Sign and Data Fail!");
                    }
                }
            }
        }

        #endregion

        #region IAlipayClient Members

        public async Task<T> CertificateExecuteAsync<T>(IAlipayRequest<T> request, AlipayOptions options) where T : AlipayResponse
        {
            return await CertificateExecuteAsync(request, options, null);
        }

        public async Task<T> CertificateExecuteAsync<T>(IAlipayRequest<T> request, AlipayOptions options, string accessToken) where T : AlipayResponse
        {
            return await CertificateExecuteAsync(request, options, accessToken, null);
        }

        public async Task<T> CertificateExecuteAsync<T>(IAlipayRequest<T> request, AlipayOptions options, string accessToken, string appAuthToken) where T : AlipayResponse
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (string.IsNullOrEmpty(options.AppId))
            {
                throw new ArgumentNullException(nameof(options.AppId));
            }

            if (string.IsNullOrEmpty(options.SignType))
            {
                throw new ArgumentNullException(nameof(options.SignType));
            }

            if (string.IsNullOrEmpty(options.AppPrivateKey))
            {
                throw new ArgumentNullException(nameof(options.AppPrivateKey));
            }

            if (string.IsNullOrEmpty(options.AppCert))
            {
                throw new ArgumentNullException(nameof(options.AppCert));
            }

            if (string.IsNullOrEmpty(options.AlipayPublicCert))
            {
                throw new ArgumentNullException(nameof(options.AlipayPublicCert));
            }

            if (string.IsNullOrEmpty(options.RootCert))
            {
                throw new ArgumentNullException(nameof(options.RootCert));
            }

            if (string.IsNullOrEmpty(options.ServerUrl))
            {
                throw new ArgumentNullException(nameof(options.ServerUrl));
            }

            var apiVersion = string.IsNullOrEmpty(request.GetApiVersion()) ? options.Version : request.GetApiVersion();

            // 添加协议级请求参数
            var txtParams = new AlipayDictionary(request.GetParameters())
            {
                { AlipayConstants.METHOD, request.GetApiName() },
                { AlipayConstants.VERSION, apiVersion },
                { AlipayConstants.APP_ID, options.AppId },
                { AlipayConstants.FORMAT, options.Format },
                { AlipayConstants.TIMESTAMP, DateTime.Now },
                { AlipayConstants.ACCESS_TOKEN, accessToken },
                { AlipayConstants.SIGN_TYPE, options.SignType },
                { AlipayConstants.TERMINAL_TYPE, request.GetTerminalType() },
                { AlipayConstants.TERMINAL_INFO, request.GetTerminalInfo() },
                { AlipayConstants.PROD_CODE, request.GetProdCode() },
                { AlipayConstants.CHARSET, options.Charset },
                { AlipayConstants.APP_CERT_SN, options.AppCertSN },
                { AlipayConstants.ALIPAY_ROOT_CERT_SN, options.RootCertSN }
            };

            // 序列化BizModel
            txtParams = SerializeBizModel(txtParams, request);

            if (!string.IsNullOrEmpty(request.GetNotifyUrl()))
            {
                txtParams.Add(AlipayConstants.NOTIFY_URL, request.GetNotifyUrl());
            }

            if (!string.IsNullOrEmpty(appAuthToken))
            {
                txtParams.Add(AlipayConstants.APP_AUTH_TOKEN, appAuthToken);
            }

            if (request.GetNeedEncrypt())
            {
                if (string.IsNullOrEmpty(txtParams[AlipayConstants.BIZ_CONTENT]))
                {
                    throw new AlipayException("api request Fail ! The reason: encrypt request is not supported!");
                }

                if (string.IsNullOrEmpty(options.EncyptKey) || string.IsNullOrEmpty(options.EncyptType))
                {
                    throw new AlipayException("encryptType or encryptKey must not null!");
                }

                if (!"AES".Equals(options.EncyptType))
                {
                    throw new AlipayException("api only support Aes!");
                }

                var encryptContent = AlipaySignature.AESEncrypt(txtParams[AlipayConstants.BIZ_CONTENT], options.EncyptKey);
                txtParams.Remove(AlipayConstants.BIZ_CONTENT);
                txtParams.Add(AlipayConstants.BIZ_CONTENT, encryptContent);
                txtParams.Add(AlipayConstants.ENCRYPT_TYPE, options.EncyptType);
            }

            // 添加签名参数
            var signContent = AlipaySignature.GetSignContent(txtParams);
            txtParams.Add(AlipayConstants.SIGN, AlipaySignature.RSASignContent(signContent, options.AppPrivateKey, options.Charset, options.SignType));

            string body;
            var client = _httpClientFactory.CreateClient(nameof(AlipayClient));

            // 是否需要上传文件
            if (request is IAlipayUploadRequest<T> uRequest)
            {
                var fileParams = AlipayUtility.CleanupDictionary(uRequest.GetFileParameters());

                body = await client.PostAsync(options.ServerUrl, txtParams, fileParams);
            }
            else
            {
                body = await client.PostAsync(options.ServerUrl, txtParams);
            }

            var parser = new AlipayJsonParser<T>();
            var item = ParseRespItem(request, body, parser, options.EncyptKey, options.EncyptType);
            var rsp = parser.Parse(item.RealContent);

            await CheckResponseCertSignAsync(request, item.RespContent, rsp.IsError, parser, options);

            return rsp;
        }

        private async Task CheckResponseCertSignAsync<T>(IAlipayRequest<T> request, string responseBody, bool isError, IAlipayParser<T> parser, AlipayOptions options) where T : AlipayResponse
        {
            var certItem = parser.GetCertItem(request, responseBody);
            if (certItem == null)
            {
                throw new AlipayException("cert check fail: Body is Empty!");
            }

            if (!string.IsNullOrEmpty(certItem.CertSN))
            {
                // 为空时添加本地支付宝公钥证书密钥
                if (_alipayPublicKeyManager.IsEmpty)
                {
                    _alipayPublicKeyManager.TryAdd(options.AlipayPublicCertSN, options.AlipayPublicKey);
                }

                // 如果返回的支付宝公钥证书序列号与本地支付宝公钥证书序列号不匹配，通过返回的支付宝公钥证书序列号去网关拉取新的支付宝公钥证书
                if (!_alipayPublicKeyManager.ContainsKey(certItem.CertSN))
                {
                    var model = new AlipayOpenAppAlipaycertDownloadModel
                    {
                        AlipayCertSn = certItem.CertSN
                    };

                    var req = new AlipayOpenAppAlipaycertDownloadRequest();
                    req.SetBizModel(model);

                    var response = await CertificateExecuteAsync(req, options);
                    if (response.IsError)
                    {
                        throw new AlipayException("支付宝公钥证书校验失败，请确认是否为支付宝签发的有效公钥证书");
                    }

                    if (!AntCertificationUtil.IsTrusted(response.AlipayCertContent, options.RootCert))
                    {
                        throw new AlipayException("支付宝公钥证书校验失败，请确认是否为支付宝签发的有效公钥证书");
                    }

                    var alipayCert = AntCertificationUtil.ParseCert(response.AlipayCertContent);
                    var alipayCertSN = AntCertificationUtil.GetCertSN(alipayCert);
                    var alipayCertPublicKey = AntCertificationUtil.ExtractPemPublicKeyFromCert(alipayCert);

                    _alipayPublicKeyManager.TryAdd(alipayCertSN, alipayCertPublicKey);
                }

                // 针对成功结果且有支付宝公钥的进行验签
                if (_alipayPublicKeyManager.TryGetValue(certItem.CertSN, out var alipayPublicKey))
                {
                    if (!isError || isError && !string.IsNullOrEmpty(certItem.Sign))
                    {
                        var rsaCheckContent = AlipaySignature.RSACheckContent(certItem.SignSourceDate, certItem.Sign, alipayPublicKey, options.Charset, options.SignType);
                        if (!rsaCheckContent)
                        {
                            // 针对JSON \/问题，替换/后再尝试做一次验证
                            if (!string.IsNullOrEmpty(certItem.SignSourceDate) && certItem.SignSourceDate.Contains("\\/"))
                            {
                                var srouceData = certItem.SignSourceDate.Replace("\\/", "/");
                                var jsonCheck = AlipaySignature.RSACheckContent(srouceData, certItem.Sign, alipayPublicKey, options.Charset, options.SignType);
                                if (!jsonCheck)
                                {
                                    throw new AlipayException("cert check fail: check Cert and Data Fail JSON also");
                                }
                            }
                            else
                            {
                                throw new AlipayException("cert check fail: check Cert and Data Fail!");
                            }
                        }
                    }
                }
                else
                {
                    throw new AlipayException("cert check fail: check Cert and Data Fail! CertSN non-existent");
                }
            }
        }

        #endregion

        #region Common Method

        private ResponseParseItem ParseRespItem<T>(IAlipayRequest<T> request, string respBody, IAlipayParser<T> parser, string encryptKey, string encryptType) where T : AlipayResponse
        {
            string realContent;
            if (request.GetNeedEncrypt())
            {
                realContent = parser.EncryptSourceData(request, respBody, encryptType, encryptKey);
            }
            else
            {
                realContent = respBody;
            }

            return new ResponseParseItem
            {
                RealContent = realContent,
                RespContent = respBody
            };
        }

        private string BuildHtmlRequest(IDictionary<string, string> dictionary, string serverUrl, string charset, string strMethod)
        {
            var sb = new StringBuilder();
            sb.Append($"<form id='submit' name='submit' action='{serverUrl}?charset={charset}' method='{strMethod}' style='display:none;'>");
            foreach (var iter in dictionary)
            {
                sb.Append("<input  name='" + iter.Key + "' value='" + iter.Value + "'/>");
            }
            sb.Append("<input type='submit' style='display:none;'></form>");
            sb.Append("<script>document.forms['submit'].submit();</script>");
            return sb.ToString();
        }

        private AlipayDictionary BuildRequestParams<T>(IAlipayRequest<T> request, string accessToken, string appAuthToken, AlipayOptions options) where T : AlipayResponse
        {
            var apiVersion = string.IsNullOrEmpty(request.GetApiVersion()) ? options.Version : request.GetApiVersion();

            // 添加协议级请求参数
            var result = new AlipayDictionary(request.GetParameters())
            {
                { AlipayConstants.METHOD, request.GetApiName() },
                { AlipayConstants.VERSION, apiVersion },
                { AlipayConstants.APP_ID, options.AppId },
                { AlipayConstants.FORMAT, options.Format },
                { AlipayConstants.TIMESTAMP, DateTime.Now },
                { AlipayConstants.ACCESS_TOKEN, accessToken },
                { AlipayConstants.SIGN_TYPE, options.SignType },
                { AlipayConstants.TERMINAL_TYPE, request.GetTerminalType() },
                { AlipayConstants.TERMINAL_INFO, request.GetTerminalInfo() },
                { AlipayConstants.PROD_CODE, request.GetProdCode() },
                { AlipayConstants.NOTIFY_URL, request.GetNotifyUrl() },
                { AlipayConstants.CHARSET, options.Charset },
                { AlipayConstants.RETURN_URL, request.GetReturnUrl() },
                { AlipayConstants.APP_AUTH_TOKEN, appAuthToken },
                { AlipayConstants.ALIPAY_ROOT_CERT_SN, options.RootCertSN },
                { AlipayConstants.APP_CERT_SN, options.AppCertSN }
            };

            // 序列化BizModel
            result = SerializeBizModel(result, request);

            if (request.GetNeedEncrypt())
            {
                if (string.IsNullOrEmpty(result[AlipayConstants.BIZ_CONTENT]))
                {
                    throw new AlipayException("api request Fail ! The reason: encrypt request is not supported!");
                }

                if (string.IsNullOrEmpty(options.EncyptKey) || string.IsNullOrEmpty(options.EncyptType))
                {
                    throw new AlipayException("encryptType or encryptKey must not null!");
                }

                if (!"AES".Equals(options.EncyptType))
                {
                    throw new AlipayException("api only support Aes!");
                }

                var encryptContent = AlipaySignature.AESEncrypt(result[AlipayConstants.BIZ_CONTENT], options.EncyptKey);
                result.Remove(AlipayConstants.BIZ_CONTENT);
                result.Add(AlipayConstants.BIZ_CONTENT, encryptContent);
                result.Add(AlipayConstants.ENCRYPT_TYPE, options.EncyptType);
            }

            return result;
        }

        #endregion

        #region SDK Execute

        public Task<T> SdkExecuteAsync<T>(IAlipayRequest<T> request, AlipayOptions options) where T : AlipayResponse
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (string.IsNullOrEmpty(options.AppId))
            {
                throw new ArgumentNullException(nameof(options.AppId));
            }

            if (string.IsNullOrEmpty(options.SignType))
            {
                throw new ArgumentNullException(nameof(options.SignType));
            }

            if (string.IsNullOrEmpty(options.AppPrivateKey))
            {
                throw new ArgumentNullException(nameof(options.AppPrivateKey));
            }

            if (string.IsNullOrEmpty(options.ServerUrl))
            {
                throw new ArgumentNullException(nameof(options.ServerUrl));
            }

            // 构造请求参数
            var requestParams = BuildRequestParams(request, null, null, options);

            // 字典排序
            var sortedParams = new SortedDictionary<string, string>(requestParams);
            var sortedDic = new AlipayDictionary(sortedParams);

            // 参数签名
            var signContent = AlipaySignature.GetSignContent(sortedDic);
            var signResult = AlipaySignature.RSASignContent(signContent, options.AppPrivateKey, options.Charset, options.SignType);

            // 添加签名结果参数
            sortedDic.Add(AlipayConstants.SIGN, signResult);

            // 参数拼接
            var signedResult = AlipayUtility.BuildQuery(sortedDic);

            // 构造结果
            var rsp = Activator.CreateInstance<T>();
            rsp.ResponseBody = signedResult;
            return Task.FromResult(rsp);
        }

        #endregion

        #region Model Serialize

        private static readonly JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions { IgnoreNullValues = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

        private AlipayDictionary SerializeBizModel<T>(AlipayDictionary requestParams, IAlipayRequest<T> request) where T : AlipayResponse
        {
            var result = requestParams;
            var isBizContentEmpty = !requestParams.ContainsKey(AlipayConstants.BIZ_CONTENT) || string.IsNullOrEmpty(requestParams[AlipayConstants.BIZ_CONTENT]);
            var bizModel = request.GetBizModel();
            if (isBizContentEmpty && bizModel != null)
            {
                var content = JsonSerializer.Serialize(bizModel, bizModel.GetType(), jsonSerializerOptions);
                result.Add(AlipayConstants.BIZ_CONTENT, content);
            }

            return result;
        }

        #endregion
    }
}