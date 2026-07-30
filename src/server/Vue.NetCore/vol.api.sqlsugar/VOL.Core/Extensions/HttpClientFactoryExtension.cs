using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace VOL.Core.Extensions
{
    public static class HttpClientFactoryExtension
    {
        public static async Task<(bool status, T data, string message)> PostAsync<T>(
           this IHttpClientFactory httpClientFactory,
           string url,
           object parameters = null,
           Dictionary<string, string> headers = null,
           string contentType = "application/x-www-form-urlencoded")
        {
            var request = InitPostData(url, parameters, headers, contentType);
            var client = httpClientFactory.CreateClient();
            using HttpResponseMessage httpResponseMessage = await client.SendAsync(request);
            string message = await httpResponseMessage.Content.ReadAsStringAsync();
            if (httpResponseMessage.StatusCode == HttpStatusCode.OK)
            {
                return (true, message.DeserializeObject<T>(), null);
            }
            return (false, default(T), httpResponseMessage.StatusCode + "," + message);
        }

        public static async Task<string> GetAsync(this IHttpClientFactory httpClientFactory, string url,
            Dictionary<string, string> parameters = null,
            Dictionary<string, string> headers = null,
            string contentType = "application/x-www-form-urlencoded")
        {
            var client = httpClientFactory.CreateClient();
            var request = InitGetData(url, parameters, headers, contentType);
            using (HttpResponseMessage httpResponseMessage = await client.SendAsync(request))
            {
                string result = await httpResponseMessage.Content.ReadAsStringAsync();
                return result;
            }
        }

        public static async Task<(bool status, T data, string message)> GetAsync<T>(this IHttpClientFactory httpClientFactory, string url,
             Dictionary<string, string> parameters = null,
             Dictionary<string, string> headers = null,
             string contentType = "application/x-www-form-urlencoded")
        {
            var client = httpClientFactory.CreateClient();
            var request = InitGetData(url, parameters, headers, contentType);
            using HttpResponseMessage httpResponseMessage = await client.SendAsync(request);
            string message = await httpResponseMessage.Content.ReadAsStringAsync();
            if (httpResponseMessage.StatusCode == HttpStatusCode.OK)
            {
                return (true, message.DeserializeObject<T>(), null);
            }
            return (false, default(T), httpResponseMessage.StatusCode + "," + message);
        }
        private static HttpRequestMessage InitPostData(
         string url,
         object parameters = null,
         Dictionary<string, string> headers = null,
         string contentType = "application/x-www-form-urlencoded")
        {
            HttpContent content = null;

            if (parameters != null)
            {
                // 根据Content-Type处理参数
                if (contentType.Equals("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
                {
                    // 处理表单格式：转换为键值对并URL编码
                    var formData = new FormUrlEncodedContent(ConvertToKeyValuePairs(parameters));
                    content = formData;
                }
                else if (contentType.Equals("application/json", StringComparison.OrdinalIgnoreCase))
                {
                    // 处理JSON格式
                    string json = Newtonsoft.Json.JsonConvert.SerializeObject(parameters);
                    content = new StringContent(json, Encoding.UTF8, "application/json");
                }
                else
                {
                    throw new NotSupportedException($"不支持的Content-Type: {contentType}");
                }
            }
            else
            {
                // 无参数时创建空内容
                content = new StringContent(string.Empty);
            }

            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = content
            };
            // 设置内容类型（覆盖默认值）
            content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

            // 添加自定义请求头
            if (headers != null)
            {
                foreach (var item in headers)
                {
                    // 注意：有些头信息（如Content-Type）不能通过这种方式添加，需要通过Content.Headers设置
                    if (!content.Headers.TryAddWithoutValidation(item.Key, item.Value) &&
                        !item.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                    {
                        // 如果头信息不能添加到内容头，则尝试添加到请求头
                        request.Headers.TryAddWithoutValidation(item.Key, item.Value);
                    }
                }
            }

            return request;
        }
        private static HttpRequestMessage InitGetData(
             string url,
                   Dictionary<string, string> parameters = null,
             Dictionary<string, string> headers = null,
             string contentType = "application/x-www-form-urlencoded")
        {
            var content = new StringContent("");
            if (parameters != null)
            {
                url += string.Join("&", parameters.Select(s => s.Key + "=" + HttpUtility.UrlEncode(s.Value)));
            }
            content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            var request = new HttpRequestMessage(HttpMethod.Get, url)
            {
                Content = content
            };
            if (headers != null)
            {
                foreach (var item in headers)
                {
                    request.Headers.Add(item.Key, item.Value);
                }
            }
            return request;
        }
        // 将对象转换为键值对（用于表单提交）
        private static IEnumerable<KeyValuePair<string, string>> ConvertToKeyValuePairs(object obj)
        {
            var dict = new Dictionary<string, string>();
            if (obj == null) return dict;

            var properties = obj.GetType().GetProperties();
            foreach (var prop in properties)
            {
                var value = prop.GetValue(obj)?.ToString() ?? string.Empty;
                dict.Add(prop.Name, value);
            }
            return dict;
        }
    }
}
